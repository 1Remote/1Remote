using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Service;
using _1RM.Utils;
using _1RM.Utils.Tracing;
using Shawn.Utils;
using Shawn.Utils.Interface;

namespace _1RM.Model.Protocol.FileTransmit.Transmitters.TransmissionController
{
    public enum ETransmitTaskStatus
    {
        /// <summary>
        /// Waiting to start transmission.
        /// </summary>
        WaitTransmitStart,

        /// <summary>
        /// Scanning items before transmission.
        /// </summary>
        Scanning,

        /// <summary>
        /// Currently transmitting data.
        /// </summary>
        Transmitting,

        //Pause,

        /// <summary>
        /// Transmission completed.
        /// </summary>
        Transmitted,

        /// <summary>
        /// Transmission was canceled.
        /// </summary>
        Cancel,
    }

    public class TransmitTask : NotifyPropertyChangedBase
    {
        private readonly ITransmitter _transOrg;
        private ITransmitter? _trans;
        /// <summary>
        /// Gets the transmission direction of this task.
        /// </summary>
        public readonly ETransmissionType TransmissionType;
        private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private readonly string _destinationDirectoryPath;

        // for HostToServer transmission, _fis and _dis are source items; for ServerToHost transmission, _ris is source item.
        private readonly FileInfo[]? _fis = null;
        private readonly DirectoryInfo[]? _dis = null;
        private readonly RemoteItem[]? _ris = null;

        /// <summary>
        /// Gets the display names of items being transmitted.
        /// </summary>
        public string TransmitItemNames { get; private set; } = "";

        /// <summary>
        /// Gets the source directory path for display.
        /// </summary>
        public string TransmitItemSrcDirectoryPath { get; private set; } = "";

        /// <summary>
        /// Gets the destination directory path for display.
        /// </summary>
        public string TransmitItemDstDirectoryPath { get; private set; } = "";

        private readonly ILanguageService _languageService;
        /// <summary>
        /// Gets the connection identifier bound to this task.
        /// </summary>
        public readonly string ConnectionId;


        /// <summary>
        /// Initializes a new upload task from local files and directories.
        /// </summary>
        public TransmitTask(ILanguageService languageService, ITransmitter trans, string connectionId, string destinationDirectoryPath, FileInfo[]? fis, DirectoryInfo[]? dis = null)
        {
            Debug.Assert(fis != null || dis != null);
            TransmitTaskStatus = ETransmitTaskStatus.WaitTransmitStart;
            TransmissionType = ETransmissionType.HostToServer;
            this._transOrg = trans;

            _destinationDirectoryPath = destinationDirectoryPath.TrimEnd('/', '\\');

            _fis = fis;
            ConnectionId = connectionId;
            _languageService = languageService;
            _dis = dis;

            TransmitItemDstDirectoryPath = destinationDirectoryPath;
            if (_fis?.Length > 0)
            {
                TransmitItemSrcDirectoryPath = _fis.First().DirectoryName!;
                foreach (var fi in _fis)
                {
                    TransmitItemNames += fi.Name + ", ";
                }
            }
            else if (dis?.Length > 0)
            {
                TransmitItemSrcDirectoryPath = dis.First().FullName;
                foreach (var di in dis)
                {
                    TransmitItemNames += di.Name + ", ";
                }
            }
            TransmitItemNames = TransmitItemNames.Trim(',', ' ');
            RaisePropertyChanged(nameof(TransmitItemDstDirectoryPath));
            RaisePropertyChanged(nameof(TransmitItemSrcDirectoryPath));
            RaisePropertyChanged(nameof(TransmitItemNames));
        }

        /// <summary>
        /// Initializes a new download task from remote items.
        /// </summary>
        public TransmitTask(ILanguageService languageService, ITransmitter trans, string connectionId, string destinationDirectoryPath, RemoteItem[] ris)
        {
            TransmitTaskStatus = ETransmitTaskStatus.WaitTransmitStart;
            TransmissionType = ETransmissionType.ServerToHost;
            this._transOrg = trans;
            _destinationDirectoryPath = destinationDirectoryPath.TrimEnd('/', '\\');

            _ris = ris;
            ConnectionId = connectionId;
            _languageService = languageService;

            TransmitItemDstDirectoryPath = destinationDirectoryPath;
            var ri = ris.First();
            if (ri.IsDirectory)
                TransmitItemSrcDirectoryPath = ris.First().FullName;
            else if (ris.First().FullName.IndexOf("/", StringComparison.Ordinal) >= 0)
                TransmitItemSrcDirectoryPath = ris.First().FullName.Substring(0, ris.First().FullName.LastIndexOf("/", StringComparison.Ordinal) + 1);
            foreach (var item in ris)
            {
                TransmitItemNames += item.Name + ", ";
            }
            Debug.Assert(TransmitItemNames != null);
            TransmitItemNames = TransmitItemNames!.Trim(',', ' ');
            RaisePropertyChanged(nameof(TransmitItemDstDirectoryPath));
            RaisePropertyChanged(nameof(TransmitItemSrcDirectoryPath));
            RaisePropertyChanged(nameof(TransmitItemNames));
        }

        /// <summary>
        /// Finalizes the task and attempts to cancel pending work.
        /// </summary>
        ~TransmitTask()
        {
            TryCancel();
        }

        /// <summary>
        /// Cancels the transmission task if it is not completed.
        /// </summary>
        public void TryCancel()
        {
            if (TransmitTaskStatus != ETransmitTaskStatus.Transmitted)
            {
                TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                _cancellationSource.Cancel(false);
                OnTaskEnd?.Invoke(TransmitTaskStatus);
            }
        }

        /// <summary>
        /// Defines the task completion callback signature.
        /// </summary>
        public delegate void OnTaskEndDelegate(ETransmitTaskStatus status, Exception? e = null);

        /// <summary>
        /// Gets or sets the callback invoked when the task ends.
        /// </summary>
        public OnTaskEndDelegate? OnTaskEnd { get; set; } = null;

        private ETransmitTaskStatus _transmitTaskStatus = ETransmitTaskStatus.WaitTransmitStart;

        /// <summary>
        /// Gets or sets the current transmission task status.
        /// </summary>
        public ETransmitTaskStatus TransmitTaskStatus
        {
            get => _transmitTaskStatus;
            set
            {
                SetAndNotifyIfChanged(ref _transmitTaskStatus, value);
                if (_transmitTaskStatus == ETransmitTaskStatus.Transmitted)
                    TransmittedByteLength = TotalByteLength;
                RaisePropertyChanged(nameof(TransmitSpeed));
                RaisePropertyChanged(nameof(TimeRemaining));
            }
        }

        /// <summary>
        /// Gets the parent destination directory path of transmission items.
        /// </summary>
        public string? TransmitDstDirectoryPath
        {
            get
            {
                if (TransmissionType == ETransmissionType.HostToServer)
                {
                    var dst = Items?.FirstOrDefault()?.DstPath;
                    if (dst != null
                        && !string.IsNullOrWhiteSpace(dst)
                        && dst.LastIndexOf("/", StringComparison.Ordinal) > 0)
                    {
                        return dst.Substring(0, dst.LastIndexOf("/", StringComparison.Ordinal));
                    }
                    return "/";
                }
                else
                {
                    return Items?.FirstOrDefault()?.DstPath ?? _destinationDirectoryPath;
                }
            }
        }

        private ulong _totalByteLength = 0;

        /// <summary>
        /// Gets or sets the total bytes to be transmitted.
        /// </summary>
        public ulong TotalByteLength
        {
            get => _totalByteLength;
            set => SetAndNotifyIfChanged(ref _totalByteLength, value);
        }

        private ulong _transmittedByteLength = 0;

        /// <summary>
        /// Gets or sets the number of bytes already transmitted.
        /// </summary>
        public ulong TransmittedByteLength
        {
            get => _transmittedByteLength;
            set
            {
                SetAndNotifyIfChanged(ref _transmittedByteLength, value);
                RaisePropertyChanged(nameof(TransmittedPercentage));
            }
        }

        private static readonly object _progressLock = new object();

        /// <summary>
        /// Gets the current transmission progress percentage.
        /// </summary>
        public string TransmittedPercentage =>
            TransmitTaskStatus == ETransmitTaskStatus.Transmitted ? "100" :
                (TransmitTaskStatus != ETransmitTaskStatus.Transmitting ? "0" :
                    (TransmittedByteLength >= TotalByteLength ? "100" :
                        (Math.Floor(10000.0 * TransmittedByteLength / TotalByteLength) / 100.0).ToString("F")));

        /// <summary>
        /// Gets the transmitted items list.
        /// </summary>
        public List<TransmitItem> ItemsHaveBeenTransmitted { get; } = new List<TransmitItem>();

        /// <summary>
        /// Gets the queue of items waiting to transmit.
        /// </summary>
        protected Queue<TransmitItem> ItemsWaitForTransmit { get; } = new Queue<TransmitItem>();

        /// <summary>
        /// Gets all items scheduled for transmission.
        /// </summary>
        public List<TransmitItem> Items { get; } = new List<TransmitItem>();

        /// <summary>
        /// remember transmittedDataLength in timespan to calculate transmit speed.
        /// </summary>
        private readonly ConcurrentQueue<Tuple<DateTime, ulong>> _transmittedDataLength = new ConcurrentQueue<Tuple<DateTime, ulong>>();

        /// <summary>
        /// Gets the recent average transmitted bytes per second.
        /// </summary>
        private double TransmittedBytesPerSec
        {
            get
            {
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
                return totalBytes / (double)secondRange;
            }
        }

        /// <summary>
        /// Gets the current transmission speed text.
        /// </summary>
        public string TransmitSpeed
        {
            get
            {
                if (TransmitTaskStatus != ETransmitTaskStatus.Transmitting)
                    return "";

                var ss = TransmittedBytesPerSec;
                if (ss < 1024)
                    return ss.ToString("F2") + " Bytes/s";
                else if (ss < 1024 * 1024)
                    return (ss / 1024.0).ToString("F2") + " KB/s";
                else if (ss < (long)1024 * 1024 * 1024)
                    return (ss / 1024.0 / 1024).ToString("F2") + " MB/s";
                else if (ss < (long)1024 * 1024 * 1024 * 1024)
                    return (ss / 1024.0 / 1024 / 1024).ToString("F2") + " GB/s";
                return "";
            }
        }

        /// <summary>
        /// Gets the estimated remaining transmission time text.
        /// </summary>
        public string TimeRemaining
        {
            get
            {
                if (TransmitTaskStatus != ETransmitTaskStatus.Transmitting)
                    return "";

                int es = (int)Math.Ceiling((TotalByteLength - TransmittedByteLength) / TransmittedBytesPerSec);
                int seconds = es % 60;
                string toDisplay = seconds.ToString("D") + "s";
                es /= 60;
                if (es > 0)
                {
                    int minutes = es % 60;
                    toDisplay = minutes.ToString("D") + "m " + toDisplay;
                    es /= 60;
                    if (es > 0)
                    {
                        toDisplay = es.ToString("D") + "h " + toDisplay;
                    }
                }
                return toDisplay;
            }
        }

        /// <summary>
        /// Adds a transmission item to the queue and updates totals.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
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
                TotalByteLength += item.ByteSize;
            }
        }

        /// <summary>
        /// Scans a local directory and enqueues all child items for upload.
        /// </summary>
        /// <param name="topDirectory">The root local directory to scan.</param>
        private void AddLocalDirectory(DirectoryInfo topDirectory)
        {
            Debug.Assert(_trans != null);
            Debug.Assert(TransmissionType == ETransmissionType.HostToServer);
            Debug.Assert(!_destinationDirectoryPath.EndsWith("\\"));
            Debug.Assert(!_destinationDirectoryPath.EndsWith("/"));

            try
            {
                if (!topDirectory.Exists)
                    return;

                var srcParentDirPath = topDirectory.Parent!.FullName.TrimEnd('/', '\\');

                var dis = new Queue<DirectoryInfo>();
                var allItems = new Queue<TransmitItem>();
                dis.Enqueue(topDirectory);
                allItems.Enqueue(new TransmitItem(topDirectory, ServerPathCombine(_destinationDirectoryPath, topDirectory.Name)));

                while (dis.Any())
                {
                    var di = dis.Dequeue();
                    var subDis = di.GetDirectories();
                    foreach (var subDi in subDis)
                    {
                        dis.Enqueue(subDi);
                        var dst = ServerPathCombine(_destinationDirectoryPath, subDi.FullName.RemoveFirst(srcParentDirPath).Replace('\\', '/').Trim(new char[] { '/', '\\' }));
                        allItems.Enqueue(new TransmitItem(subDi, dst));
                    }
                    var subFis = di.GetFiles();
                    foreach (var fi in subFis)
                    {
                        var dst = ServerPathCombine(_destinationDirectoryPath, fi.FullName.RemoveFirst(srcParentDirPath).Replace('\\', '/').Trim(new char[] { '/', '\\' }));
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

        /// <summary>
        /// Scans a remote directory and enqueues all child items for download.
        /// </summary>
        /// <param name="topItem">The root remote directory item to scan.</param>
        private async Task AddServerDirectory(RemoteItem topItem)
        {
            Debug.Assert(_trans != null);
            Debug.Assert(TransmissionType == ETransmissionType.ServerToHost);
            Debug.Assert(!_destinationDirectoryPath.EndsWith("\\"));
            Debug.Assert(!_destinationDirectoryPath.EndsWith("/"));

            try
            {
                if (_trans == null)
                    return;
                if (await _trans.Exists(topItem.FullName) != true)
                    return;

                var srcParentDirPath = topItem.FullName.TrimEnd('/', '\\');
                if (srcParentDirPath.LastIndexOf("/", StringComparison.Ordinal) >= 0)
                    srcParentDirPath = srcParentDirPath.Substring(0, srcParentDirPath.LastIndexOf("/", StringComparison.Ordinal));
                if (string.IsNullOrEmpty(srcParentDirPath))
                    srcParentDirPath = "/";

                var dirPaths = new Queue<string>();
                var allItems = new Queue<TransmitItem>();
                dirPaths.Enqueue(topItem.FullName);
                allItems.Enqueue(new TransmitItem(topItem, Path.Combine(_destinationDirectoryPath, topItem.Name)));
                while (dirPaths.Any())
                {
                    var path = dirPaths.Dequeue();
                    var rms = await _trans.ListDirectoryItems(path);
                    foreach (var item in rms)
                    {
                        if (item.IsDirectory && !item.IsSymlink && item.Name != "..")
                        {
                            dirPaths.Enqueue(item.FullName);
                        }
                        var dst = Path.Combine(_destinationDirectoryPath, item.FullName.RemoveFirst(srcParentDirPath).Replace('/', '\\').Trim(new char[] { '/', '\\' }));
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

        /// <summary>
        /// Starts scanning, conflict check, and transmission asynchronously.
        /// </summary>
        /// <param name="remoteItems">Current remote items used for conflict checks.</param>
        public async void StartTransmitAsync(IEnumerable<RemoteItem> remoteItems)
        {
            _trans = _transOrg.Clone();
            Debug.Assert(_trans != null);

            if (TransmitTaskStatus != ETransmitTaskStatus.Scanning
                && TransmitTaskStatus != ETransmitTaskStatus.Transmitting
                && TransmitTaskStatus != ETransmitTaskStatus.Cancel
                && TransmitTaskStatus != ETransmitTaskStatus.Transmitted)
            {
                TransmitTaskStatus = ETransmitTaskStatus.Scanning;
                await Task.Run(async () =>
                {
                    try
                    {
                        await ScanTransmitItems();

                        if (await CheckExistedFiles(remoteItems))
                        {
                            await RunTransmit();
                            SimpleLogHelper.Debug($"{nameof(TransmitTask)}: OnTaskEnd?.Invoke({TransmitTaskStatus}, null); ");
                        }
                        else
                        {
                            TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                        }
                        OnTaskEnd?.Invoke(TransmitTaskStatus, null);
                    }
                    catch (Exception e)
                    {
                        TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                        SimpleLogHelper.Debug($"{nameof(TransmitTask)}: OnTaskEnd?.Invoke({TransmitTaskStatus}, {e}); ");
                        OnTaskEnd?.Invoke(TransmitTaskStatus, e);
                    }
                }, _cancellationSource.Token);
            }
        }

        /// <summary>
        /// Scans all source items and builds the transmission queue.
        /// </summary>
        private async Task ScanTransmitItems()
        {
            Debug.Assert(_trans != null);
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
                            await AddServerDirectory(item);
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
        /// Checks whether a destination file already exists for download.
        /// </summary>
        /// <param name="item">The transmit item to verify.</param>
        /// <returns>True when an existing destination file is found.</returns>
        private bool CheckExistedFileServerToHost(TransmitItem item)
        {
            if (!item.IsDirectory)
                if (File.Exists(item.DstPath))
                {
                    // check if file in used
                    FileStream? fs = null;
                    try
                    {
                        fs = new FileStream(item.DstPath, FileMode.Open, FileAccess.Read, FileShare.None);
                        return true;
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

            return false;
        }

        /// <summary>
        /// Checks whether a destination file already exists for upload.
        /// </summary>
        /// <param name="item">The transmit item to verify.</param>
        /// <param name="remoteItem">Remote items in the target directory.</param>
        /// <returns>True when an existing destination file is found.</returns>
        private async Task<bool> CheckExistedFileHostToServer(TransmitItem item, IEnumerable<RemoteItem> remoteItem)
        {
            if (!item.IsDirectory)
            {
                if (remoteItem.Any(x => x.IsDirectory == false && x.Name == item.ItemName))
                    return true;
                if (_trans != null)
                    if (await _trans.Exists(item.DstPath) == true)
                        return true;
            }
            return false;
        }

        /// <summary>
        /// Checks name conflicts and asks whether to continue transmission.
        /// </summary>
        /// <param name="remoteItems">Current remote items used for upload conflict checks.</param>
        /// <returns>True when transmission can continue.</returns>
        private async Task<bool> CheckExistedFiles(IEnumerable<RemoteItem> remoteItems)
        {
            // check if existed
            int existedFiles = 0;
            foreach (var item in ItemsWaitForTransmit)
            {
                switch (item.TransmissionType)
                {
                    case ETransmissionType.ServerToHost:
                        {
                            if (CheckExistedFileServerToHost(item))
                                ++existedFiles;
                            break;
                        }
                    case ETransmissionType.HostToServer:
                        {
                            if (await CheckExistedFileHostToServer(item, remoteItems))
                                ++existedFiles;
                            break;
                        }
                }
            }

            var vm = IoC.Get<SessionControlService>().GetTabByConnectionId(ConnectionId)?.GetViewModel();
            if (existedFiles > 0
                && false == MessageBoxHelper.Confirm(_languageService.Translate("file_transmit_host_warning_same_names", existedFiles.ToString()),
                _languageService.Translate("file_transmit_host_warning_same_names_title"), ownerViewModel: vm == null ? this : vm))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Updates byte progress for a transmitting item.
        /// </summary>
        /// <param name="item">The transmit item being updated.</param>
        /// <param name="readLength">Current transferred length reported by callback.</param>
        private void DataTransmitting(ref TransmitItem item, ulong readLength)
        {
            lock (_progressLock)
            {
                try
                {
                    if (readLength > item.TransmittedSize)
                    {
                        var add = readLength - item.TransmittedSize;
                        item.TransmittedSize += add;
                        TransmittedByteLength += add;
                        _transmittedDataLength.Enqueue(new Tuple<DateTime, ulong>(DateTime.Now, add));
                        RaisePropertyChanged(nameof(TransmitSpeed));
                        RaisePropertyChanged(nameof(TimeRemaining));
                        SimpleLogHelper.Debug($"{DateTime.Now}: {TransmittedByteLength}done, {TransmittedPercentage}%");
                    }
                }
                catch (Exception e)
                {
                    UnifyTracing.Error(e, new Dictionary<string, string>()
                    {
                        {"readLength", readLength.ToString()},
                        {"item.TransmittedSize", item.TransmittedSize.ToString()},
                        {"item.ByteSize", item.ByteSize.ToString()},
                    });
                    SimpleLogHelper.Fatal(e, $"readLength = {readLength}, item.TransmittedSize = {item.TransmittedSize}, item.ByteSize = {item.ByteSize}");
                }
            }
        }

        /// <summary>
        /// Executes transmission for a single download item.
        /// </summary>
        /// <param name="item">The download item to process.</param>
        private async Task RunTransmitServerToHost(TransmitItem item)
        {
            try
            {
                if (item.IsDirectory)
                {
                    if (!Directory.Exists(item.DstPath))
                        Directory.CreateDirectory(item.DstPath);
                }
                else
                {
                    var fi = new FileInfo(item.DstPath);
                    if (fi?.Directory?.Exists == false)
                        fi.Directory.Create();
                    if (fi!.Exists)
                        fi.Delete();

                    if (_trans != null)
                    {
                        item.TransmittedSize = 0;
                        await _trans.DownloadFile(item.SrcPath, item.DstPath,
                                                  readLength => { DataTransmitting(ref item, readLength); },
                                                  _cancellationSource.Token);
                    }
                }
            }
            catch (Exception e)
            {
                TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                SimpleLogHelper.Error(e);
            }
        }

        /// <summary>
        /// Executes transmission for a single upload item.
        /// </summary>
        /// <param name="item">The upload item to process.</param>
        private async Task RunTransmitHostToServer(TransmitItem item)
        {
            if (_trans != null)
            {
                if (item.IsDirectory)
                {
                    await _trans.CreateDirectory(item.DstPath);
                }
                else if (File.Exists(item.SrcPath))
                {
                    if (await _trans.Exists(item.DstPath) == true)
                        await _trans.Delete(item.DstPath);

                    item.TransmittedSize = 0;
                    await _trans.UploadFile(item.SrcPath, item.DstPath,
                                            readLength => { DataTransmitting(ref item, readLength); },
                                            _cancellationSource.Token);
                }
            }
        }

        /// <summary>
        /// Runs queued transmission items sequentially.
        /// </summary>
        private async Task RunTransmit()
        {
            Debug.Assert(_trans != null);
            TransmittedByteLength = 0;
            TransmitTaskStatus = ETransmitTaskStatus.Transmitting;

            while (ItemsWaitForTransmit.Count > 0
                   && TransmitTaskStatus != ETransmitTaskStatus.Cancel
                   && _cancellationSource.IsCancellationRequested == false)
            {
                var item = ItemsWaitForTransmit.Peek();
                try
                {
                    SimpleLogHelper.Debug($"Transmit start");
                    switch (item.TransmissionType)
                    {
                        case ETransmissionType.ServerToHost:
                            await RunTransmitServerToHost(item);
                            break;

                        case ETransmissionType.HostToServer:
                            await RunTransmitHostToServer(item);
                            break;
                    }
                    SimpleLogHelper.Debug($"Transmit end, ItemsWaitForTransmit.Dequeue()");

                    // move transmitted into ItemsHaveBeenTransmitted
                    if (ItemsWaitForTransmit.Peek() == item)
                        ItemsWaitForTransmit.Dequeue();
                    ItemsHaveBeenTransmitted.Add(item);
                }
                catch (Exception e)
                {
                    TransmitTaskStatus = ETransmitTaskStatus.Cancel;
                    SimpleLogHelper.Warning(e);
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

        /// <summary>
        /// Combines path segments using server-style separators.
        /// </summary>
        /// <param name="path1">The base path.</param>
        /// <param name="paths">Additional path segments.</param>
        /// <returns>A normalized server path.</returns>
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