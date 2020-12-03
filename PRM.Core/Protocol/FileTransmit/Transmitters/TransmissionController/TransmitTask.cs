using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmitter;
using Shawn.Utils;

namespace PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController
{
    public enum ETransmitTaskStatus
    {
        WaitTransmitStart,
        Scanning,
        Transmitting,
        //Pause,
        Transmitted,
        Cancel,
    }
    public class TransmitTask : NotifyPropertyChangedBase
    {
        private readonly ITransmitter _trans = null;
        public readonly ETransmissionType TransmissionType;
        private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private readonly string _destinationDirectoryPath;

        private readonly FileInfo[] _fis = null;
        private readonly DirectoryInfo[] _dis = null;
        private readonly RemoteItem[] _ris = null;

        public TransmitTask(ITransmitter trans, string destinationDirectoryPath, FileInfo[] fis, DirectoryInfo[] dis = null)
        {
            Debug.Assert(fis != null || dis != null);
            TransmitTaskStatus = ETransmitTaskStatus.WaitTransmitStart;
            TransmissionType = ETransmissionType.HostToServer;
            this._trans = trans;

            _destinationDirectoryPath = destinationDirectoryPath.TrimEnd(new[] { '/', '\\' }); ;

            _fis = fis;
            _dis = dis;

            TransmittingItemDstDirectoryPath = _transmittingItemDstDirectoryPath;
            if (_fis?.Length > 0)
                TransmittingItemSrcDirectoryPath = _fis.First().DirectoryName;
            else if (dis?.Length > 0)
                TransmittingItemSrcDirectoryPath = dis.First().FullName;
        }
        public TransmitTask(ITransmitter trans, string destinationDirectoryPath, RemoteItem[] ris)
        {
            Debug.Assert(ris != null);
            TransmitTaskStatus = ETransmitTaskStatus.WaitTransmitStart;
            TransmissionType = ETransmissionType.ServerToHost;
            this._trans = trans;
            _destinationDirectoryPath = destinationDirectoryPath.TrimEnd(new[] { '/', '\\' }); ;

            _ris = ris;

            TransmittingItemDstDirectoryPath = _transmittingItemDstDirectoryPath;
            var ri = ris.First();
            if(ri.IsDirectory)
                TransmittingItemSrcDirectoryPath = ris.First().FullName;
            else
                TransmittingItemSrcDirectoryPath = ris.First().FullName.Substring(0, ris.First().FullName.LastIndexOf("/", StringComparison.Ordinal) + 1);
        }

        ~TransmitTask()
        {
            TryCancel();
        }



        public void TryCancel()
        {
            if (TransmitTaskStatus == ETransmitTaskStatus.Transmitting)
                TransmitTaskStatus = ETransmitTaskStatus.Cancel;
            _cancellationSource.Cancel(false);
            OnTaskEnd?.Invoke(TransmitTaskStatus);
        }

        public delegate void OnTaskEndDelegate(ETransmitTaskStatus status, Exception e = null);
        public OnTaskEndDelegate OnTaskEnd { get; set; } = null;

        private ETransmitTaskStatus _transmitTaskStatus = ETransmitTaskStatus.WaitTransmitStart;
        public ETransmitTaskStatus TransmitTaskStatus
        {
            get => _transmitTaskStatus;
            set
            {
                SetAndNotifyIfChanged(nameof(TransmitTaskStatus), ref _transmitTaskStatus, value);
                if (_transmitTaskStatus == ETransmitTaskStatus.Transmitted)
                    TransmittedByteLength = TotalByteLength;
            }
        }


        /// <summary>
        /// return the parent directory full path of Transmission
        /// </summary>
        public string TransmitDstDirectoryPath
        {
            get
            {
                var dst = Items?.FirstOrDefault()?.DstPath;
                if (TransmissionType == ETransmissionType.HostToServer)
                {
                    if (!string.IsNullOrWhiteSpace(dst)
                        && dst.LastIndexOf("/", StringComparison.Ordinal) > 0)
                    {
                        return dst.Substring(0, dst.LastIndexOf("/", StringComparison.Ordinal));
                    }
                    return "/";
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(dst))
                    {
                        var di = new DirectoryInfo(dst);
                        return di?.Parent?.FullName;
                    }
                    return null;
                }
            }
        }

        private string _transmittingItemName;
        public string TransmittingItemName
        {
            get => _transmittingItemName;
            protected set => SetAndNotifyIfChanged(nameof(TransmittingItemName), ref _transmittingItemName, value);
        }


        private string _transmittingItemSrcDirectoryPath;
        public string TransmittingItemSrcDirectoryPath
        {
            get => _transmittingItemSrcDirectoryPath;
            protected set => SetAndNotifyIfChanged(nameof(TransmittingItemSrcDirectoryPath), ref _transmittingItemSrcDirectoryPath, value);
        }



        private string _transmittingItemDstDirectoryPath;
        public string TransmittingItemDstDirectoryPath
        {
            get => _transmittingItemDstDirectoryPath;
            protected set => SetAndNotifyIfChanged(nameof(TransmittingItemDstDirectoryPath), ref _transmittingItemDstDirectoryPath, value);
        }


        private ulong _totalByteLength = 0;
        /// <summary>
        /// byte length to transmit
        /// </summary>
        public ulong TotalByteLength
        {
            get => _totalByteLength;
            set => SetAndNotifyIfChanged(nameof(TotalByteLength), ref _totalByteLength, value);
        }


        private ulong _transmittedByteLength = 0;
        /// <summary>
        /// byte length has been transmitted
        /// </summary>
        public ulong TransmittedByteLength
        {
            get => _transmittedByteLength;
            set
            {
                SetAndNotifyIfChanged(nameof(TransmittedByteLength), ref _transmittedByteLength, value);
                RaisePropertyChanged(nameof(TransmittedPercentage));
            }
        }


        public string TransmittedPercentage =>
            TransmitTaskStatus == ETransmitTaskStatus.Transmitted ? "100" :
                (TransmitTaskStatus != ETransmitTaskStatus.Transmitting ? "0" :
                   (TransmittedByteLength >= TotalByteLength ? "100" : (100.0 * TransmittedByteLength / TotalByteLength).ToString("F")));

        public List<TransmitItem> ItemsHaveBeenTransmitted { get; } = new List<TransmitItem>();
        public Queue<TransmitItem> ItemsWaitForTransmit { get; } = new Queue<TransmitItem>();
        /// <summary>
        /// all items need to be transmitted
        /// </summary>
        public List<TransmitItem> Items { get; } = new List<TransmitItem>();

        /// <summary>
        /// remember transmittedDataLength in timespan to calculate transmit speed.
        /// </summary>
        private readonly ConcurrentQueue<Tuple<DateTime, ulong>> _transmittedDataLength = new ConcurrentQueue<Tuple<DateTime, ulong>>();

        public string TransmitSpeed
        {
            get
            {
                if (TransmitTaskStatus != ETransmitTaskStatus.Transmitting) 
                    return "";

                var now = DateTime.Now;
                const int secondRange = 5;

                // throw data length before {second}
                while (_transmittedDataLength.TryPeek(out var p)
                       && (now - p.Item1).Seconds > secondRange)
                {
                    _transmittedDataLength.TryDequeue(out _);
                }

                // counting total bytes transmitted in last {second}
                ulong totalBytes = 0;
                foreach (var t in _transmittedDataLength)
                {
                    totalBytes += t.Item2;
                }

                // get speed
                double ss = totalBytes / (double) secondRange;
                if (ss < 1024)
                    return ss + " Bytes/s";
                else if (ss < 1024 * 1024)
                    return (ss / 1024.0).ToString("F2") + " KB/s";
                else if (ss < (long) 1024 * 1024 * 1024)
                    return (ss / 1024.0 / 1024).ToString("F2") + " MB/s";
                else if (ss < (long) 1024 * 1024 * 1024 * 1024)
                    return (ss / 1024.0 / 1024 / 1024).ToString("F2") + " GB/s";
                return "";
            }
        }


        private void AddTransmitItem(TransmitItem item)
        {
            if (TransmissionType != item.TransmissionType)
            {
                throw new MethodAccessException($"{TransmissionType} transmit task can't add a {item.TransmissionType} item!");
            }

            if (TransmitTaskStatus == ETransmitTaskStatus.Transmitted
                || TransmitTaskStatus == ETransmitTaskStatus.Cancel)
            {
                throw new NotSupportedException();
            }

            if (!ItemsWaitForTransmit.Any(x =>
                string.Equals(x.SrcPath, item.SrcPath, StringComparison.CurrentCultureIgnoreCase)
                && string.Equals(x.DstPath, item.DstPath, StringComparison.CurrentCultureIgnoreCase)))
            {
                ItemsWaitForTransmit.Enqueue(item);
                Items.Add(item);
                if (string.IsNullOrWhiteSpace(TransmittingItemName))
                    TransmittingItemName = item.ItemName;
                TotalByteLength += item.ByteSize;
            }
        }


        private void AddLocalDirectory(DirectoryInfo topDirectory)
        {
            Debug.Assert(TransmissionType == ETransmissionType.HostToServer);
            Debug.Assert(!_destinationDirectoryPath.EndsWith("\\"));
            Debug.Assert(!_destinationDirectoryPath.EndsWith("/"));

            try
            {
                if (!topDirectory.Exists)
                    return;

                var srcTopDirPath = topDirectory.FullName.TrimEnd(new[] { '/', '\\' });

                var dis = new Queue<DirectoryInfo>();
                var allItems = new Queue<TransmitItem>();
                dis.Enqueue(topDirectory);
                allItems.Enqueue(new TransmitItem(topDirectory, _destinationDirectoryPath));

                while (dis.Any())
                {
                    var di = dis.Dequeue();
                    var subDis = di.GetDirectories();
                    foreach (var subDi in subDis)
                    {
                        dis.Enqueue(subDi);
                        var dst = ServerPathCombine(_destinationDirectoryPath, subDi.FullName.Replace(srcTopDirPath, "").Replace('\\', '/').Trim(new char[] { '/', '\\' }));
                        allItems.Enqueue(new TransmitItem(subDi, dst));
                    }
                    var subFis = di.GetFiles();
                    foreach (var fi in subFis)
                    {
                        var dst = ServerPathCombine(_destinationDirectoryPath, fi.FullName.Replace(srcTopDirPath, "").Replace('\\', '/').Trim(new char[] { '/', '\\' }));
                        allItems.Enqueue(new TransmitItem(fi, dst));
                    }
                }

                foreach (var item in allItems)
                {
                    AddTransmitItem(item);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }


        private void AddServerDirectory(RemoteItem topItem)
        {
            Debug.Assert(TransmissionType == ETransmissionType.ServerToHost);
            Debug.Assert(!_destinationDirectoryPath.EndsWith("\\"));
            Debug.Assert(!_destinationDirectoryPath.EndsWith("/"));

            try
            {
                if (!_trans.Exists(topItem.FullName))
                    return;

                var srcTopDirPath = topItem.FullName.TrimEnd(new[] { '/', '\\' });

                var dirPaths = new Queue<string>();
                var allItems = new Queue<TransmitItem>();
                dirPaths.Enqueue(topItem.FullName);
                allItems.Enqueue(new TransmitItem(topItem, _destinationDirectoryPath));
                while (dirPaths.Any())
                {
                    var path = dirPaths.Dequeue();
                    var rms = _trans.ListDirectoryItems(path);
                    foreach (var item in rms)
                    {
                        if (item.IsDirectory && !item.IsSymlink && item.Name != "..")
                        {
                            dirPaths.Enqueue(item.FullName);
                        }
                        var dst = Path.Combine(_destinationDirectoryPath, item.FullName.Replace(srcTopDirPath, "").Replace('/', '\\').Trim(new char[] { '/', '\\' }));
                        allItems.Enqueue(new TransmitItem(item, dst));
                    }
                }

                foreach (var item in allItems)
                {
                    AddTransmitItem(item);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }


        public async Task StartTransmitAsync()
        {
            Debug.Assert(_trans != null);

            if (TransmitTaskStatus != ETransmitTaskStatus.Scanning
                && TransmitTaskStatus != ETransmitTaskStatus.Transmitting
                && TransmitTaskStatus != ETransmitTaskStatus.Cancel
                && TransmitTaskStatus != ETransmitTaskStatus.Transmitted)
            {
                TransmitTaskStatus = ETransmitTaskStatus.Scanning;
                await Task.Factory.StartNew(() =>
                {
                    try
                    {
                        ScanTransmitItems();

                        if (!CheckExistedFiles())
                            return;

                        RunTransmit();
                        SimpleLogHelper.Debug($"{nameof(TransmitTask)}: OnTaskEnd?.Invoke({TransmitTaskStatus}, null); ");
                        OnTaskEnd?.Invoke(TransmitTaskStatus, null);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Debug($"{nameof(TransmitTask)}: OnTaskEnd?.Invoke({TransmitTaskStatus}, {e}); ");
                        OnTaskEnd?.Invoke(TransmitTaskStatus, e);
                    }
                }, _cancellationSource.Token);
            }
        }


        当前问题：不应该用堆栈实现waitingtask应该用FIFO，当前结构无法及时提示文件重复，等待传输队列的文件名不显示

        /// <summary>
        /// scan all files to be transmitted init by class constructor
        /// </summary>
        private void ScanTransmitItems()
        {
            TransmitTaskStatus = ETransmitTaskStatus.Scanning;
            if (TransmissionType == ETransmissionType.HostToServer)
            {
                Debug.Assert(_fis != null || _dis != null);
                if (_fis != null)
                    foreach (var fi in _fis)
                    {
                        var dstPath = ServerPathCombine(_destinationDirectoryPath, fi.Name);
                        AddTransmitItem(new TransmitItem(fi, dstPath));
                    }

                if (_dis != null)
                    foreach (var di in _dis)
                    {
                        AddLocalDirectory(di);
                    }
            }
            else
            {
                Debug.Assert(_ris != null);
                if (_ris != null)
                    foreach (var item in _ris)
                    {
                        if (item.Name == ".." || item.Name == ".")
                            return;
                        if (item.IsDirectory)
                        {
                            var bk = TransmitTaskStatus;
                            AddServerDirectory(item);
                            TransmitTaskStatus = bk;
                        }
                        else
                        {
                            var dstPath = ServerPathCombine(_destinationDirectoryPath, item.Name);
                            AddTransmitItem(new TransmitItem(item, dstPath));
                        }
                    }
            }
        }


        /// <summary>
        /// check if any same name file exited, return if can continue transmit.
        /// </summary>
        /// <returns></returns>
        private bool CheckExistedFiles()
        {
            // check if existed
            int existedFiles = 0;
            foreach (var item in ItemsWaitForTransmit)
            {
                TransmittingItemSrcDirectoryPath = item.SrcDirectoryPath;
                TransmittingItemDstDirectoryPath = item.DstDirectoryPath;
                switch (item.TransmissionType)
                {
                    case ETransmissionType.ServerToHost:
                        {
                            if (!item.IsDirectory)
                                if (File.Exists(item.DstPath))
                                {
                                    ++existedFiles;

                                    // check if file in used
                                    FileStream fs = null;
                                    try
                                    {
                                        fs = new FileStream(item.DstPath, FileMode.Open, FileAccess.Read, FileShare.None);
                                    }
                                    catch (Exception e)
                                    {
                                        TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                                        Exception e2;
                                        if (TransmissionType == ETransmissionType.HostToServer)
                                            e2 = new Exception($"Upload to {item.DstPath}: " + e.Message, e);
                                        else
                                            e2 = new Exception($"Download to {item.DstPath}: " + e.Message, e);
                                        OnTaskEnd?.Invoke(TransmitTaskStatus, e2);
                                        return false;
                                    }
                                    finally
                                    {
                                        fs?.Close();
                                    }
                                }
                            break;
                        }
                    case ETransmissionType.HostToServer:
                        {
                            try
                            {
                                if (!item.IsDirectory)
                                    if (_trans.Exists(item.DstPath))
                                        ++existedFiles;
                            }
                            catch (Exception e)
                            {
                                TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(item.DstPath + ": \r\n" + e.Message,
                                        SystemConfig.Instance.Language.GetText("messagebox_title_error"),
                                        MessageBoxButton.OK, MessageBoxImage.Stop);
                                });
                                return false;
                            }
                            break;
                        }
                }
            }

            if (existedFiles > 0
            && MessageBox.Show(SystemConfig.Instance.Language.GetText("file_transmit_host_warning_same_names").Replace("{0}", existedFiles.ToString()),
                                    SystemConfig.Instance.Language.GetText("file_transmit_host_warning_same_names_title"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                TryCancel();
                return false;
            }
            return true;
        }


        private void RunTransmit()
        {
            TransmittedByteLength = 0;
            TransmitTaskStatus = ETransmitTaskStatus.Transmitting;

            while (ItemsWaitForTransmit.Count > 0
                   && TransmitTaskStatus != ETransmitTaskStatus.Cancel
                   && _cancellationSource.IsCancellationRequested == false)
            {
                var item = ItemsWaitForTransmit.Peek();
                TransmittingItemName = item.ItemName;
                TransmittingItemSrcDirectoryPath = item.SrcDirectoryPath;
                TransmittingItemDstDirectoryPath = item.DstDirectoryPath;
                try
                {
                    switch (item.TransmissionType)
                    {
                        case ETransmissionType.ServerToHost:
                            {
                                if (item.IsDirectory)
                                {
                                    if (!Directory.Exists(item.DstPath))
                                        Directory.CreateDirectory(item.DstPath);
                                }
                                else
                                {
                                    var fi = new FileInfo(item.DstPath);
                                    if (!fi.Directory.Exists)
                                        fi.Directory.Create();
                                    if (fi.Exists)
                                        fi.Delete();

                                    ulong lastReadLength = 0;
                                    _trans.DownloadFile(item.SrcPath, item.DstPath, readLength =>
                                    {
                                        var add = readLength - lastReadLength;
                                        lastReadLength = readLength;
                                        TransmittedByteLength += add;
                                        _transmittedDataLength.Enqueue(new Tuple<DateTime, ulong>(DateTime.Now, add));
                                        RaisePropertyChanged(nameof(TransmitSpeed));
                                        SimpleLogHelper.Debug($"{DateTime.Now}: {TransmittedByteLength}done, {TransmittedPercentage}%");
                                    }, _cancellationSource.Token);
                                }
                            }
                            break;
                        case ETransmissionType.HostToServer:
                            {
                                if (item.IsDirectory)
                                {
                                    _trans.CreateDirectory(item.DstPath);
                                }
                                else if (File.Exists(item.SrcPath))
                                {
                                    if (_trans.Exists(item.DstPath))
                                        _trans.Delete(item.DstPath);

                                    ulong lastReadLength = 0;
                                    _trans.UploadFile(item.SrcPath, item.DstPath, readLength =>
                                    {
                                        Console.WriteLine(DateTime.Now.ToString("O"));
                                        var add = readLength - lastReadLength;
                                        lastReadLength = readLength;
                                        TransmittedByteLength += add;
                                        _transmittedDataLength.Enqueue(new Tuple<DateTime, ulong>(DateTime.Now, add));
                                        RaisePropertyChanged(nameof(TransmitSpeed));
                                        SimpleLogHelper.Debug($"{DateTime.Now}: {TransmittedByteLength}done, {TransmittedPercentage}%");
                                    }, _cancellationSource.Token);
                                }
                            }
                            break;
                    }
                    SimpleLogHelper.Debug($"ItemsWaitForTransmit.Dequeue()");

                    // move transmitted into ItemsHaveBeenTransmitted
                    if (ItemsWaitForTransmit.Peek() == item)
                        ItemsWaitForTransmit.Dequeue();
                    ItemsHaveBeenTransmitted.Add(item);
                }
                catch (Exception e)
                {
                    TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                    SimpleLogHelper.Warning(e, e.StackTrace);
                    var e2 = TransmissionType == ETransmissionType.HostToServer ? 
                        new Exception($"Upload to {item.DstPath}: " + e.Message, e) : 
                        new Exception($"Download to {item.DstPath}: " + e.Message, e);
                    throw e2;
                }
            }

            if (TransmitTaskStatus != ETransmitTaskStatus.Cancel)
            {
                TransmitTaskStatus = ETransmitTaskStatus.Transmitted;
            }
        }


        public static string ServerPathCombine(string path1, params string[] paths)
        {
            var ret = path1.Replace('\\', '/').TrimEnd('/');
            foreach (var path in paths)
            {
                ret = ret.TrimEnd('/') + "/" + path.Replace('\\', '/').TrimStart('/');
            }
            return ret;
        }
    }
}
