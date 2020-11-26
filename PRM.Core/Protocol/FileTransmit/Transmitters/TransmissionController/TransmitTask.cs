using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmitter;
using PRM.Core.Protocol.FileTransmitter.TransmissionController;
using Shawn.Utils;

namespace PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController
{
    public enum ETransmitTaskStatus
    {
        NotStart,
        Scanning,
        Transmitting,
        //Pause,
        Transmitted,
        Cancel,
    }
    public class TransmitTask : NotifyPropertyChangedBase
    {
        private Thread _t = null;
        private readonly ITransmitter _transOrg = null;

        public TransmitTask(ITransmitter trans)
        {
            this._transOrg = trans;
        }

        ~TransmitTask()
        {
            TryCancel();
        }

        public void TryCancel()
        {
            if (TransmitTaskStatus == ETransmitTaskStatus.Transmitting)
                TransmitTaskStatus = ETransmitTaskStatus.Cancel;
        }

        public delegate void OnTaskEndDelegate(ETransmitTaskStatus status, Exception e = null);
        public OnTaskEndDelegate OnTaskEnd { get; set; } = null;

        private ETransmitTaskStatus _transmitTaskStatus = ETransmitTaskStatus.NotStart;
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

        public ETransmissionType TransmissionType => Items?.FirstOrDefault()?.TransmissionType ?? ETransmissionType.HostToServer;

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
            TransmittedByteLength >= TotalByteLength
                ? "100"
                : (100.0 * TransmittedByteLength / TotalByteLength).ToString("F");

        public List<TransmitItem> ItemsHaveBeenTransmitted { get; } = new List<TransmitItem>();
        public Queue<TransmitItem> ItemsWaitForTransmit { get; } = new Queue<TransmitItem>();
        public List<TransmitItem> Items { get; } = new List<TransmitItem>();

        /// <summary>
        /// remember transmittedDataLength in every timespan.
        /// </summary>
        private readonly ConcurrentQueue<Tuple<DateTime, ulong>> _transmittedDataLength = new ConcurrentQueue<Tuple<DateTime, ulong>>();

        public string TransmitSpeed
        {
            get
            {
                var now = DateTime.Now;
                const int second = 5;

                // throw data length before {second}
                while (_transmittedDataLength.TryPeek(out var p)
                    && (now - p.Item1).Seconds > second)
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
                double ss = totalBytes / (double)second;
                if (ss < 1024)
                    return ss + " bytes/s";
                else if (ss < 1024 * 1024)
                    return (ss / 1024.0).ToString("F2") + " kb/s";
                else if (ss < (long)1024 * 1024 * 1024)
                    return (ss / 1024.0 / 1024).ToString("F2") + " mb/s";
                else if (ss < (long)1024 * 1024 * 1024 * 1024)
                    return (ss / 1024.0 / 1024 / 1024).ToString("F2") + " gb/s";
                return "";
            }
        }

        public void AddItem(RemoteItem item, string dstPath, bool dirWithSubItems = true)
        {
            if (item.Name == ".." || item.Name == ".")
                return;
            if (TransmitTaskStatus == ETransmitTaskStatus.Transmitted
                || TransmitTaskStatus == ETransmitTaskStatus.Cancel)
            {
                throw new NotSupportedException();
            }
            if (!ItemsWaitForTransmit.Any(x =>
                x.TransmissionType == ETransmissionType.ServerToHost
                && string.Equals(x.SrcPath, item.FullName, StringComparison.CurrentCultureIgnoreCase)))
            {
                var ti = new TransmitItem(item, dstPath);
                ItemsWaitForTransmit.Enqueue(ti);
                Items.Add(ti);
                if (string.IsNullOrWhiteSpace(TransmittingItemName))
                    TransmittingItemName = ti.ItemName;

                if (item.IsDirectory)
                {
                    if (!dirWithSubItems) return;
                    var bk = TransmitTaskStatus;
                    TransmitTaskStatus = ETransmitTaskStatus.Scanning;
                    AddDirectoryServer(item, dstPath);
                    TransmitTaskStatus = bk;
                }
                else
                {
                    TotalByteLength += item.ByteSize;
                }
            }
        }

        public void AddItem(FileInfo fi, string dstPath)
        {
            if (!fi.Exists)
                return;
            if (TransmitTaskStatus == ETransmitTaskStatus.Transmitted
                || TransmitTaskStatus == ETransmitTaskStatus.Cancel)
            {
                throw new NotSupportedException();
            }
            if (!ItemsWaitForTransmit.Any(x =>
                x.TransmissionType == ETransmissionType.HostToServer
                && string.Equals(x.SrcPath, fi.FullName, StringComparison.CurrentCultureIgnoreCase)))
            {
                var ti = new TransmitItem(fi, dstPath);
                ItemsWaitForTransmit.Enqueue(ti);
                Items.Add(ti);
                if (string.IsNullOrWhiteSpace(TransmittingItemName))
                    TransmittingItemName = ti.ItemName;
                TotalByteLength += ti.ByteSize;
            }
        }

        public void AddItem(DirectoryInfo di, string dstPath, bool dirWithSubItems = true)
        {
            if (!di.Exists)
                return;
            dstPath = dstPath.Replace('\\', '/');
            if (TransmitTaskStatus == ETransmitTaskStatus.Transmitted
                || TransmitTaskStatus == ETransmitTaskStatus.Cancel)
            {
                throw new NotSupportedException();
            }
            if (!ItemsWaitForTransmit.Any(x =>
                x.TransmissionType == ETransmissionType.HostToServer
                && string.Equals(x.SrcPath, di.FullName, StringComparison.CurrentCultureIgnoreCase)))
            {
                var ti = new TransmitItem(di, dstPath);
                ItemsWaitForTransmit.Enqueue(ti);
                if (dirWithSubItems)
                {
                    var bk = TransmitTaskStatus;
                    TransmitTaskStatus = ETransmitTaskStatus.Scanning;
                    AddDirectoryHost(di.FullName, dstPath);
                    TransmitTaskStatus = bk;
                }
            }
        }

        private void AddDirectoryHost(string path, string dstPath)
        {
            if (!Directory.Exists(path))
                return;

            var dis = new Queue<DirectoryInfo>();
            dis.Enqueue(new DirectoryInfo(path));
            while (dis.Any())
            {
                var di = dis.Dequeue();
                foreach (var directoryInfo in di.GetDirectories())
                {
                    dis.Enqueue(directoryInfo);
                    AddItem(directoryInfo, ServerPathCombine(dstPath, directoryInfo.FullName.Replace(path, "").Replace('\\', '/')), false);
                }

                foreach (var fi in di.GetFiles())
                {
                    AddItem(fi, ServerPathCombine(dstPath, fi.FullName.Replace(path, "").Replace('\\', '/')));
                }
            }
        }

        private void AddDirectoryServer(RemoteItem topDir, string dstPath)
        {
            var dis = new Queue<RemoteItem>();
            dis.Enqueue(topDir);
            var topDirPath = topDir.FullName;
            if (!topDirPath.EndsWith("/"))
                topDirPath = topDirPath + "/";
            while (dis.Any())
            {
                var di = dis.Dequeue();
                var rms = _transOrg.ListDirectoryItems(di.FullName);
                foreach (var item in rms)
                {
                    if (item.IsDirectory && !item.IsSymlink && item.Name != "..")
                    {
                        dis.Enqueue(item);
                        AddItem(item, Path.Combine(dstPath, item.FullName.Replace(topDirPath, "").Replace('/', '\\')), false);
                    }
                }
                foreach (var item in rms)
                {
                    if (!item.IsDirectory && !item.IsSymlink)
                    {
                        AddItem(item, Path.Combine(dstPath, item.FullName.Replace(topDirPath, "").Replace('/', '\\')));
                    }
                }
            }
        }


        private readonly object _threadLocker = new object();
        public void TransmitAsync()
        {
            lock (_threadLocker)
            {
                if (_t == null
                    && TransmitTaskStatus != ETransmitTaskStatus.Transmitting
                    && TransmitTaskStatus != ETransmitTaskStatus.Cancel
                    && TransmitTaskStatus != ETransmitTaskStatus.Transmitted)
                {
                    _t = new Thread(MainLoop);
                    _t.Start();
                }
            }
        }

        public void TransmitSync()
        {
            lock (_threadLocker)
            {
                MainLoop();
            }
        }


        private void MainLoop()
        {
            TransmittedByteLength = 0;
            TransmitTaskStatus = ETransmitTaskStatus.Transmitting;

            //var transmitter = _transOrg;
            var transmitter = _transOrg.Clone();

            // check if existed
            int existedFiles = 0;
            foreach (var item in ItemsWaitForTransmit)
            {
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
                                        //Application.Current.Dispatcher.Invoke(() =>
                                        //{
                                        //    MessageBox.Show(item.DstPath + ": \r\n" + e.Message,
                                        //        SystemConfig.Instance.Language.GetText("messagebox_title_error"),
                                        //        MessageBoxButton.OK, MessageBoxImage.Stop);
                                        //});
                                        return;
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
                                    if (transmitter.Exists(item.DstPath))
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
                                return;
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
                TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                return;
            }

            while (ItemsWaitForTransmit.Count > 0
                   && TransmitTaskStatus != ETransmitTaskStatus.Cancel)
            {
                var item = ItemsWaitForTransmit.Peek();
                var tokenSource = new CancellationTokenSource();
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
                                    using (var fileStream = File.OpenWrite(item.DstPath))
                                    {
                                        Exception e = null;
                                        var t = Task.Factory.StartNew(() =>
                                        {
                                            try
                                            {
                                                ulong lastReadLength = 0;
                                                transmitter.DownloadFile(item.SrcPath, fileStream, readLength =>
                                                {
                                                    var add = readLength - lastReadLength;
                                                    lastReadLength = readLength;
                                                    TransmittedByteLength += add;
                                                    _transmittedDataLength.Enqueue(
                                                        new Tuple<DateTime, ulong>(DateTime.Now, add));
                                                    RaisePropertyChanged(nameof(TransmitSpeed));
                                                    SimpleLogHelper.Debug($"{DateTime.Now}: {TransmittedByteLength}done, {TransmittedPercentage}%");
                                                });
                                            }
                                            catch (Exception e1)
                                            {
                                                e = e1;
                                            }
                                        }, tokenSource.Token);
                                        while (!t.IsCompleted)
                                        {
                                            Thread.Sleep(100);
                                            if (TransmitTaskStatus == ETransmitTaskStatus.Cancel)
                                            {
                                                tokenSource.Cancel(false);
                                                break;
                                            }
                                        }

                                        if (e != null)
                                        {
                                            throw e;
                                        }
                                    }
                                }
                            }
                            break;
                        case ETransmissionType.HostToServer:
                            {
                                if (item.IsDirectory)
                                {
                                    transmitter.CreateDirectory(item.DstPath);
                                }
                                else if (File.Exists(item.SrcPath))
                                {
                                    Exception e = null;
                                    var t = Task.Factory.StartNew(() =>
                                    {
                                        try
                                        {
                                            if (transmitter.Exists(item.DstPath))
                                                transmitter.Delete(item.DstPath);
                                            using (var fileStream = File.OpenRead(item.SrcPath))
                                            {
                                                ulong lastReadLength = 0;
                                                transmitter.UploadFile(fileStream, item.DstPath, readLength =>
                                                {
                                                    Console.WriteLine(DateTime.Now.ToString("O"));
                                                    var add = readLength - lastReadLength;
                                                    lastReadLength = readLength;
                                                    _transmittedDataLength.Enqueue(new Tuple<DateTime, ulong>(DateTime.Now, add));
                                                    RaisePropertyChanged(nameof(TransmitSpeed));
                                                    TransmittedByteLength += add;
                                                });
                                            }
                                        }
                                        catch (Exception e1)
                                        {
                                            e = e1;
                                        }
                                    }, tokenSource.Token);
                                    while (!t.IsCompleted)
                                    {
                                        Thread.Sleep(100);
                                        if (TransmitTaskStatus == ETransmitTaskStatus.Cancel)
                                        {
                                            tokenSource.Cancel(false);
                                            break;
                                        }
                                    }

                                    if (e != null)
                                    {
                                        throw e;
                                    }
                                }
                            }
                            break;
                    }
                    if (ItemsWaitForTransmit.Peek() == item)
                        ItemsWaitForTransmit.Dequeue();
                    ItemsHaveBeenTransmitted.Add(item);
                    OnTaskEnd?.Invoke(TransmitTaskStatus);
                }
                catch (Exception e)
                {
                    TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                    SimpleLogHelper.Debug(e, e.StackTrace);
                    Exception e2;

                    if (TransmissionType == ETransmissionType.HostToServer)
                        e2 = new Exception($"Upload to {item.DstPath}: " + e.Message, e);
                    else
                        e2 = new Exception($"Download to {item.DstPath}: " + e.Message, e);
                    OnTaskEnd?.Invoke(TransmitTaskStatus, e2);
                    //Application.Current.Dispatcher.Invoke(() =>
                    //{
                    //    MessageBox.Show(item.DstPath + ": \r\n" + e.Message,
                    //        SystemConfig.Instance.Language.GetText("messagebox_title_error"),
                    //        MessageBoxButton.OK, MessageBoxImage.Stop);
                    //});
                    return;
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

        public static bool IsWindowsFileInUsed(string path)
        {
            if (!File.Exists(path))
                return false;
            bool inUse = true;

            FileStream fs = null;
            try
            {
                fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                inUse = false;
            }
            catch
            {

            }
            finally
            {
                if (fs != null)

                    fs.Close();
            }
            return inUse;//true表示正在使用,false没有使用  
        }
    }
}
