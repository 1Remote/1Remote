using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ColorPickerWPF.Code;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController;
using PRM.Core.Protocol.FileTransmitter;
using PRM.Core.Protocol.FileTransmitter.TransmissionController;
using Shawn.Utils;

namespace PRM.Core.Protocol.FileTransmit.Host
{
    public class VmFileTransmitHost : NotifyPropertyChangedBase
    {
        private ITransmitter _trans = null;
        private readonly IProtocolFileTransmittable _protocol = null;
        public VmFileTransmitHost(IProtocolFileTransmittable protocol)
        {
            _protocol = protocol;
        }

        ~VmFileTransmitHost()
        {
        }

        public void Release()
        {
            foreach (var t in TransmitTasks)
            {
                t.TryCancel();
            }
        }


        private readonly Stack<string> _pathHistoryPrevious = new Stack<string>();
        private readonly Stack<string> _pathHistoryFollowing = new Stack<string>();

        public double GridLoadingBgOpacity { get; set; } = 1;
        private Visibility _gridLoadingVisibility = Visibility.Collapsed;
        public Visibility GridLoadingVisibility
        {
            get => _gridLoadingVisibility;
            set
            {
                SetAndNotifyIfChanged(nameof(GridLoadingVisibility), ref _gridLoadingVisibility, value);
                if (GridLoadingBgOpacity > 0.99 && value != Visibility.Visible)
                {
                    GridLoadingBgOpacity = 0.1;
                    RaisePropertyChanged(nameof(GridLoadingBgOpacity));
                }
            }
        }


        private string _currentPathEdit = "";
        public string CurrentPathEdit
        {
            get => _currentPathEdit;
            set => SetAndNotifyIfChanged(nameof(CurrentPathEdit), ref _currentPathEdit, value);
        }

        private string _currentPath = "";
        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                SetAndNotifyIfChanged(nameof(CurrentPath), ref _currentPath, value);
                CurrentPathEdit = value;
            }
        }



        /// <summary>
        /// level: 0 normal; 1 warning(yellow); 2 error(red);
        /// </summary>
        public int IoMessageLevel { get; set; } = 0;
        private string _ioMessage = "";
        private bool stopUpdateIoMessage = false;
        public string IoMessage
        {
            get => _ioMessage;
            set
            {
                if (!stopUpdateIoMessage)
                {
                    SetAndNotifyIfChanged(nameof(IoMessage), ref _ioMessage, value);
                    RaisePropertyChanged(nameof(IoMessageLevel));
                }
            }
        }


        private bool _cmdGoToPathPreviousEnable = false;
        public bool CmdGoToPathPreviousEnable
        {
            get => _cmdGoToPathPreviousEnable;
            set => SetAndNotifyIfChanged(nameof(CmdGoToPathPreviousEnable), ref _cmdGoToPathPreviousEnable, value);
        }



        private bool _cmdGoToPathFollowingEnable = false;
        public bool CmdGoToPathFollowingEnable
        {
            get => _cmdGoToPathFollowingEnable;
            set => SetAndNotifyIfChanged(nameof(CmdGoToPathFollowingEnable), ref _cmdGoToPathFollowingEnable, value);
        }




        private bool _cmdGoToPathParentEnable = false;
        public bool CmdGoToPathParentEnable
        {
            get => _cmdGoToPathParentEnable;
            set => SetAndNotifyIfChanged(nameof(CmdGoToPathParentEnable), ref _cmdGoToPathParentEnable, value);
        }







        private RemoteItem _selectedRemoteItem;
        public RemoteItem SelectedRemoteItem
        {
            get => _selectedRemoteItem;
            set => SetAndNotifyIfChanged(nameof(SelectedRemoteItem), ref _selectedRemoteItem, value);
        }

        private ObservableCollection<RemoteItem> _remoteItems = new ObservableCollection<RemoteItem>();
        public ObservableCollection<RemoteItem> RemoteItems
        {
            get => _remoteItems;
            set => SetAndNotifyIfChanged(nameof(RemoteItems), ref _remoteItems, value);
        }



        public void Conn()
        {
            if (_trans?.IsConnected() != true)
            {
                GridLoadingVisibility = Visibility.Visible;
                var task = new Task(() =>
                {
                    //Thread.Sleep(5000);
                    _trans = _protocol.GeTransmitter();
                    if (!string.IsNullOrWhiteSpace(_protocol.GetStartupPath()))
                        ShowFolder(_protocol.GetStartupPath(), -1);
                    GridLoadingVisibility = Visibility.Collapsed;
                });
                task.Start();
            }
            else
                GridLoadingVisibility = Visibility.Collapsed;
        }

        public bool IsConnected()
        {
            return _trans?.IsConnected() == true;
        }







        private ObservableCollection<TransmitTask> _transmitTasks = new ObservableCollection<TransmitTask>();
        public ObservableCollection<TransmitTask> TransmitTasks
        {
            get => _transmitTasks;
            set => SetAndNotifyIfChanged(nameof(TransmitTasks), ref _transmitTasks, value);
        }

        private void AddTransmitTask(TransmitTask t)
        {
            if (!t.ItemsWaitForTransmit.Any())
                return;
            TransmitTasks.Add(t);

            void func(ETransmitTaskStatus status, Exception e)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    if (Application.Current == null)
                        return;
                    try
                    {
                        if (t.OnTaskEnd != null)
                            t.OnTaskEnd -= func;
                        if (t.TransmitTaskStatus == ETransmitTaskStatus.Cancel)
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                TransmitTasks.Remove(t);
                            });
                        if (e != null)
                        {
                            IoMessageLevel = 2;
                            IoMessage = e.Message;
                        }
                    }
                    catch (Exception e2)
                    {
                        SimpleLogHelper.Error(e2);
                    }
                });
            }

            t.OnTaskEnd += func;
        }



        private int _remoteItemsOrderBy = -1;
        /// <summary>
        /// -1: by name asc(default)
        /// 0: by name asc
        /// 1: by name desc
        /// 2: by size asc
        /// 3: by size desc
        /// 4: by LastUpdate asc
        /// 5: by LastUpdate desc
        /// 6: by FileType asc
        /// 7: by FileType desc
        /// </summary>
        public int RemoteItemsOrderBy
        {
            get => _remoteItemsOrderBy;
            set
            {
                if (_remoteItemsOrderBy != value)
                {
                    SetAndNotifyIfChanged(nameof(RemoteItemsOrderBy), ref _remoteItemsOrderBy, value);
                    MakeRemoteItemsOrderBy();
                }
            }
        }

        private void MakeRemoteItemsOrderBy()
        {
            if (RemoteItems?.Count > 0)
            {
                ObservableCollection<RemoteItem> remoteItemInfos;
                switch (RemoteItemsOrderBy)
                {
                    case -1:
                    default:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderBy(a => a.IsDirectory ? 1 : 2).ThenBy(x => x.Name));
                        break;
                    case 0:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderBy(x => x.Name));
                        break;
                    case 1:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderByDescending(x => x.Name));
                        break;
                    case 2:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderBy(x => x.ByteSize));
                        break;
                    case 3:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderByDescending(x => x.ByteSize));
                        break;
                    case 4:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderBy(x => x.LastUpdate));
                        break;
                    case 5:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderByDescending(x => x.LastUpdate));
                        break;
                    case 6:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderBy(x => x.FileType).ThenBy(x => x.Name));
                        break;
                    case 7:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(RemoteItems.OrderByDescending(x => x.FileType).ThenBy(x => x.Name));
                        break;
                }
                RemoteItems = remoteItemInfos;
            }
        }


        private void ShowFolder(string path, bool showIoMessage)
        {
            ShowFolder(path, 0, showIoMessage);
        }
        /// <summary>
        /// mode = 1 go preview, mode = 2 go following
        /// will not remember pathHistory when mode != 0
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="showIoMessage"></param>
        private void ShowFolder(string path, int mode = 0, bool showIoMessage = true)
        {
            var t = new Task(() =>
            {
                lock (this)
                {
                    GridLoadingVisibility = Visibility.Visible;
                    try
                    {
                        SimpleLogHelper.Debug($"ShowFolder({path}, {mode}) START");
                        if (string.IsNullOrWhiteSpace(path))
                            path = "/";
                        if (path.EndsWith("/.."))
                        {
                            SimpleLogHelper.Debug($"ShowFolder after path.EndsWith(/..)");
                            path = path.Substring(0, path.Length - 3);
                            if (path.LastIndexOf("/") > 0)
                            {
                                var i = path.LastIndexOf("/");
                                path = path.Substring(0, i);
                            }
                        }

                        try
                        {
                            var remoteItemInfos = new ObservableCollection<RemoteItem>();
                            SimpleLogHelper.Debug($"ShowFolder before ListDirectoryItems");
                            var items = _trans.ListDirectoryItems(path);
                            if (items.Any())
                            {
                                remoteItemInfos = new ObservableCollection<RemoteItem>(items);
                            }

                            RemoteItems = remoteItemInfos;
                            SimpleLogHelper.Debug($"ShowFolder before MakeRemoteItemsOrderBy");
                            MakeRemoteItemsOrderBy();

                            if (path != CurrentPath)
                            {
                                if (mode == 0
                                    && (_pathHistoryPrevious.Count == 0 || _pathHistoryPrevious.Peek() != CurrentPath))
                                {
                                    _pathHistoryPrevious.Push(CurrentPath);
                                    if (_pathHistoryFollowing.Count > 0 &&
                                        _pathHistoryFollowing.Peek() != path)
                                        _pathHistoryFollowing.Clear();
                                }

                                CurrentPath = path;
                            }

                            if (CurrentPath != "/" && CurrentPath != "")
                                CmdGoToPathParentEnable = true;
                            else
                                CmdGoToPathParentEnable = false;

                            CmdGoToPathPreviousEnable = _pathHistoryPrevious.Count > 0;
                            CmdGoToPathFollowingEnable = _pathHistoryFollowing.Count > 0;

                            if (showIoMessage)
                            {
                                IoMessageLevel = 0;
                                IoMessage = $"ls {CurrentPath}";
                            }
                        }
                        catch (Exception e)
                        {
                            IoMessageLevel = 2;
                            IoMessage = $"ls {CurrentPath}: " + e.Message;
                            if (CurrentPath != path)
                                ShowFolder(CurrentPath);
                            return;
                        }

                    }
                    finally
                    {
                        GridLoadingVisibility = Visibility.Collapsed;
                    }
                    SimpleLogHelper.Debug($"ShowFolder({path}, {mode}) END"); 
                }
            });
            t.Start();
        }


        #region CMD


        private RelayCommand _cmdDelete;
        public RelayCommand CmdDelete
        {
            get
            {
                if (_cmdDelete == null)
                {
                    _cmdDelete = new RelayCommand((o) =>
                    {
                        if (SelectedRemoteItem != null)
                        {
                            if (MessageBox.Show(
                                SystemConfig.Instance.Language.GetText("string_delete_confirm"),
                                SystemConfig.Instance.Language.GetText("string_delete_confirm_title"),
                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                foreach (var itemInfo in RemoteItems)
                                {
                                    if (itemInfo.IsSelected)
                                    {
                                        var selected = itemInfo.FullName;
                                        try
                                        {
                                            _trans.Delete(selected);
                                            IoMessageLevel = 0;
                                            IoMessage = $"Delete {selected}";
                                        }
                                        catch (Exception e)
                                        {
                                            IoMessageLevel = 2;
                                            IoMessage = $"Delete {selected}: " + e.Message;
                                        }
                                    }
                                }
                            }
                        }
                        ShowFolder(CurrentPath, false);
                    });
                }
                return _cmdDelete;
            }
        }


        private RelayCommand _cmdBeginRenaming;

        public RelayCommand CmdBeginRenaming
        {
            get
            {
                if (_cmdBeginRenaming == null)
                {
                    _cmdBeginRenaming = new RelayCommand((o) =>
                    {
                        if (SelectedRemoteItem != null
                            && SelectedRemoteItem.Name != "."
                            && SelectedRemoteItem.Name != ".."
                            && SelectedRemoteItem.Name != "/")
                        {
                            SelectedRemoteItem.IsRenaming = true;
                        }
                    });
                }
                return _cmdBeginRenaming;
            }
        }


        private RelayCommand _cmdEndRenaming;

        public RelayCommand CmdEndRenaming
        {
            get
            {
                if (_cmdEndRenaming == null)
                {
                    _cmdEndRenaming = new RelayCommand((o) =>
                    {
                        if (RemoteItems.Any(x => x.IsRenaming == true))
                        {
                            foreach (var item in RemoteItems.Where(x => x.IsRenaming == true))
                            {
                                var newPath = CurrentPath + "/" + item.Name;
                                if (string.IsNullOrEmpty(item.FullName) || !_trans.Exists(item.FullName))
                                {
                                    // add
                                    if (item.IsDirectory)
                                    {
                                        try
                                        {
                                            _trans.CreateDirectory(newPath);
                                            IoMessageLevel = 0;
                                            IoMessage = $"Create folder {newPath}";
                                        }
                                        catch (Exception e)
                                        {
                                            IoMessageLevel = 2;
                                            IoMessage = $"Create folder {newPath}: " + e.Message;
                                        }
                                    }
                                }
                                else
                                {
                                    // edit
                                    try
                                    {
                                        _trans.RenameFile(item.FullName, newPath);
                                        IoMessageLevel = 0;
                                        IoMessage = $"Move {item.FullName} => {newPath}";
                                    }
                                    catch (Exception e)
                                    {
                                        IoMessageLevel = 2;
                                        IoMessage = $"Move {item.FullName} => {newPath}: " + e.Message;
                                    }
                                }
                            }
                            ShowFolder(CurrentPath, false);
                        }
                    });
                }
                return _cmdEndRenaming;
            }
        }


        private RelayCommand _cmdCancelRenaming;
        public RelayCommand CmdCancelRenaming
        {
            get
            {
                if (_cmdCancelRenaming == null)
                {
                    _cmdCancelRenaming = new RelayCommand((o) =>
                    {
                        if (RemoteItems.Any(x => x.IsRenaming == true))
                        {
                            var selected = "";
                            if (SelectedRemoteItem != null
                                && SelectedRemoteItem.Name != "."
                                && SelectedRemoteItem.Name != ".."
                                && SelectedRemoteItem.Name != "/")
                            {
                                selected = SelectedRemoteItem.FullName;
                            }
                            CmdGoToPathCurrent.Execute();
                            var item = RemoteItems.Where(x => x.FullName == selected);
                            if (item.Any())
                            {
                                SelectedRemoteItem = item.First();
                            }
                        }
                    });
                }
                return _cmdCancelRenaming;
            }
        }


        private RelayCommand _cmdListViewDoubleClick;
        /// <summary>
        /// double click to enter folder or open file
        /// </summary>
        public RelayCommand CmdListViewDoubleClick
        {
            get
            {
                if (_cmdListViewDoubleClick == null)
                {
                    _cmdListViewDoubleClick = new RelayCommand((o) =>
                    {
                        if (RemoteItems.Any(x => x.IsRenaming == true))
                        {
                            CmdEndRenaming.Execute();
                        }
                        else if (SelectedRemoteItem?.IsDirectory == true)
                        {
                            ShowFolder(SelectedRemoteItem.FullName);
                        }
                        else if (SelectedRemoteItem?.IsSymlink == false)
                        {
                            const int limit = 1;
                            var msg = SystemConfig.Instance.Language.GetText("file_transmit_host_message_preview_over_size");
                            msg = msg.Replace("1 MB", $"{limit} MB");
                            if (SelectedRemoteItem.ByteSize > 1024 * 1024 * limit
                            && MessageBox.Show(
                                msg,
                                SystemConfig.Instance.Language.GetText("messagebox_title_warning"),
                                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                            {
                                return;
                            }
                            try
                            {
                                var tmpPath = Path.Combine(Path.GetTempPath(), "ReadOnly_" + SelectedRemoteItem.Name);
                                if (File.Exists(tmpPath))
                                {
                                    File.SetAttributes(tmpPath, FileAttributes.Temporary);
                                    File.Delete(tmpPath);
                                }
                                using (var stream = File.OpenWrite(tmpPath))
                                {
                                    _trans.DownloadFile(SelectedRemoteItem.FullName, stream);
                                }
                                // set read only
                                File.SetAttributes(tmpPath, FileAttributes.ReadOnly);
                                System.Diagnostics.Process.Start(tmpPath);
                            }
                            catch (Exception e)
                            {
                                IoMessageLevel = 2;
                                IoMessage = e.Message;
                            }
                        }
                    });
                }
                return _cmdListViewDoubleClick;
            }
        }


        private RelayCommand _cmdGoToPathCurrent;
        public RelayCommand CmdGoToPathCurrent
        {
            get
            {
                if (_cmdGoToPathCurrent == null)
                {
                    _cmdGoToPathCurrent = new RelayCommand((o) =>
                    {
                        var selected = "";
                        if (SelectedRemoteItem != null)
                        {
                            selected = SelectedRemoteItem.FullName;
                        }
                        ShowFolder(CurrentPathEdit);
                        var item = RemoteItems.Where(x => x.FullName == selected);
                        if (!string.IsNullOrEmpty(selected) && item.Any())
                        {
                            SelectedRemoteItem = item.First();
                        }
                    });
                }
                return _cmdGoToPathCurrent;
            }
        }

        private RelayCommand _cmdGoToParent;
        public RelayCommand CmdGoToParent
        {
            get
            {
                if (_cmdGoToParent == null)
                {
                    _cmdGoToParent = new RelayCommand((o) =>
                    {
                        SimpleLogHelper.Debug($"call CmdGoToParent");
                        if (CurrentPath == "/")
                            return;
                        if (CurrentPath?.LastIndexOf("/") >= 0)
                        {
                            ShowFolder(CurrentPath.Substring(0, CurrentPath.LastIndexOf("/")));
                        }
                    });
                }
                return _cmdGoToParent;
            }
        }

        private RelayCommand _cmdGoToPathPrevious;
        public RelayCommand CmdGoToPathPrevious
        {
            get
            {
                if (_cmdGoToPathPrevious == null)
                {
                    _cmdGoToPathPrevious = new RelayCommand((o) =>
                    {
                        if (_pathHistoryPrevious.Count > 0)
                        {
                            SimpleLogHelper.Debug($"call CmdGoToPathPrevious");
                            var p = _pathHistoryPrevious.Pop();
                            _pathHistoryFollowing.Push(CurrentPath);
                            ShowFolder(p, 1);
                        }
                    });
                }
                return _cmdGoToPathPrevious;
            }
        }

        private RelayCommand _cmdGoToPathFollowing;
        public RelayCommand CmdGoToPathFollowing
        {
            get
            {
                if (_cmdGoToPathFollowing == null)
                {
                    _cmdGoToPathFollowing = new RelayCommand((o) =>
                    {
                        if (_pathHistoryFollowing.Count > 0)
                        {
                            SimpleLogHelper.Debug($"call CmdGoToPathFollowing");
                            var p = _pathHistoryFollowing.Pop();
                            _pathHistoryPrevious.Push(CurrentPath);
                            ShowFolder(p, 2);
                        }
                    });
                }
                return _cmdGoToPathFollowing;
            }
        }


        private RelayCommand _cmdDownload;
        public RelayCommand CmdDownload
        {
            get
            {
                if (_cmdDownload == null)
                {
                    _cmdDownload = new RelayCommand((o) =>
                    {
                        if (RemoteItems.All(x => x.IsSelected != true))
                        {
                            return;
                        }

                        var dlg = new System.Windows.Forms.SaveFileDialog
                        {
                            Title = SystemConfig.Instance.Language.GetText("file_transmit_host_message_files_download_to"),
                            CheckFileExists = false,
                            ValidateNames = false,
                            FileName = SystemConfig.Instance.Language.GetText("file_transmit_host_message_files_download_to_dir"),
                        };
                        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            var path = new FileInfo(dlg.FileName).DirectoryName;

                            if (!IOPermissionHelper.HasWritePermissionOnFile(dlg.FileName)
                            || !IOPermissionHelper.HasWritePermissionOnDir(path))
                            {
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("string_permission_denied") + $": {dlg.FileName}");
                                return;
                            }

                            var t = new TransmitTask(_trans);
                            foreach (var remoteItem in RemoteItems)
                            {
                                if (remoteItem.IsSelected)
                                {
                                    t.AddItem(remoteItem, Path.Combine(path, remoteItem.Name));
                                }
                            }
                            AddTransmitTask(t);
                            t.TransmitAsync();
                        }
                    });
                }
                return _cmdDownload;
            }
        }




        private RelayCommand _cmdUpload;
        public RelayCommand CmdUpload
        {
            get
            {
                if (_cmdUpload == null)
                {
                    _cmdUpload = new RelayCommand((o) =>
                    {
                        List<string> fl = new List<string>();
                        var dlg = new System.Windows.Forms.OpenFileDialog();
                        dlg.Title = SystemConfig.Instance.Language.GetText("file_transmit_host_message_select_files_to_upload");
                        dlg.CheckFileExists = true;
                        dlg.Multiselect = true;
                        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            fl = dlg.FileNames.ToList();
                        }
                        if (fl.Count == 0)
                        {
                            return;
                        }
                        DoUpload(fl);
                    });
                }
                return _cmdUpload;
            }
        }

        private RelayCommand _cmdUploadClipboard;

        public RelayCommand CmdUploadClipboard
        {
            get
            {
                if (_cmdUploadClipboard == null)
                {
                    _cmdUploadClipboard = new RelayCommand((o) =>
                    {
                        var fl = Clipboard.GetFileDropList().Cast<string>().ToList();
                        if (fl.Count == 0)
                        {
                            return;
                        }
                        DoUpload(fl);
                    });
                }
                return _cmdUploadClipboard;
            }
        }


        private void DoUpload(List<string> filePathList)
        {
            var t = new TransmitTask(_trans);
            foreach (var f in filePathList)
            {
                var fi = new FileInfo(f);
                if (fi.Exists)
                {
                    t.AddItem(fi, TransmitTask.ServerPathCombine(CurrentPath, fi.Name));
                }
                else
                {
                    var di = new DirectoryInfo(f);
                    if (di.Exists)
                    {
                        t.AddItem(di, TransmitTask.ServerPathCombine(CurrentPath, di.Name));
                    }
                }
            }

            if (t.ItemsWaitForTransmit.Count > 0)
            {
                AddTransmitTask(t);
                t.TransmitAsync();
            }
        }

        //private RelayCommand _cmdContinueTransmitTask;
        //public RelayCommand CmdContinueTransmitTask
        //{
        //    get
        //    {
        //        if (_cmdContinueTransmitTask == null)
        //        {
        //            _cmdContinueTransmitTask = new RelayCommand((o) =>
        //            {
        //                if (o is TransmitTask t)
        //                {
        //                    SimpleLogHelper.Debug($"Try to continue Task:{t.GetHashCode()}");
        //                    t.TryContinue();
        //                }
        //            });
        //        }
        //        return _cmdContinueTransmitTask;
        //    }
        //}
        //private RelayCommand _cmdPauseTransmitTask;
        //public RelayCommand CmdPauseTransmitTask
        //{
        //    get
        //    {
        //        if (_cmdPauseTransmitTask == null)
        //        {
        //            _cmdPauseTransmitTask = new RelayCommand((o) =>
        //            {
        //                if (o is TransmitTask t)
        //                {
        //                    SimpleLogHelper.Debug($"Try to pause Task:{t.GetHashCode()}");
        //                    t.TryPause();
        //                }
        //            });
        //        }
        //        return _cmdPauseTransmitTask;
        //    }
        //}


        private RelayCommand _cmdShowTransmitDstPath;
        public RelayCommand CmdShowTransmitDstPath
        {
            get
            {
                if (_cmdShowTransmitDstPath == null)
                {
                    _cmdShowTransmitDstPath = new RelayCommand((o) =>
                    {
                        if (o is TransmitTask t)
                        {
                            var dst = t.TransmitDstDirectoryPath;
                            if (!string.IsNullOrEmpty(dst))
                            {
                                if (t.TransmissionType == ETransmissionType.HostToServer)
                                {
                                    ShowFolder(dst);
                                }
                                else
                                {
                                    //var path = o.ToString();
                                    //if (File.Exists(path))
                                    //{
                                    //    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe");
                                    //    //psi.Arguments = "/e,/select," + path;
                                    //    System.Diagnostics.Process.Start(psi);
                                    //}
                                    if (Directory.Exists(dst))
                                    {
                                        System.Diagnostics.Process.Start("explorer.exe", dst);
                                    }
                                }
                            }
                        }
                    });
                }
                return _cmdShowTransmitDstPath;
            }
        }




        private RelayCommand _cmdDeleteTransmitTask;
        public RelayCommand CmdDeleteTransmitTask
        {
            get
            {
                if (_cmdDeleteTransmitTask == null)
                {
                    _cmdDeleteTransmitTask = new RelayCommand((o) =>
                    {
                        if (o is TransmitTask t)
                        {
                            SimpleLogHelper.Debug($"Try to cancel and delete Task:{t.GetHashCode()}");
                            t.TryCancel();
                            TransmitTasks.Remove(t);
                        }
                    });
                }
                return _cmdDeleteTransmitTask;
            }
        }




        #endregion


        #region Static Func
        private static T FindAncestor<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T o)
                {
                    return o;
                }

                obj = VisualTreeHelper.GetParent(obj);
            }
            return default(T);
        }

        /// <summary>
        /// 取得指定位置处的 ListViewItem
        /// </summary>
        /// <param name="lvSender"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        private static ListViewItem GetItemOnPosition(ScrollContentPresenter lvSender, Point position)
        {
            HitTestResult r = VisualTreeHelper.HitTest(lvSender, position);
            if (r == null)
            {
                return null;
            }
            var obj = r.VisualHit;
            while (!(obj is ListView) && (obj != null))
            {
                obj = VisualTreeHelper.GetParent(obj);
                if (obj is ListViewItem item)
                {
                    return item;
                }
            }
            return null;
        }

        private static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            try
            {
                while (source != null && !(source is T))
                    source = System.Windows.Media.VisualTreeHelper.GetParent(source);
                return source;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        //http://stackoverflow.com/questions/665719/wpf-animate-listbox-scrollviewer-horizontaloffset
        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            // Search immediate children first (breadth-first)
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T o)
                {
                    return o;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }
        #endregion


        public void TvFileList_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ListView view = null;
            ScrollContentPresenter p = null;
            if (sender is ListView lv)
            {
                view = lv;
                var ip = FindVisualChild<ItemsPresenter>(view);
                p = FindAncestor<ScrollContentPresenter>((DependencyObject)ip);
            }
            if (view == null || p == null)
                return;
            var curSelectedItem = GetItemOnPosition(p, e.GetPosition(p));
            if (curSelectedItem == null)
            {
                ((ListView)sender).SelectedItem = null;
            }
            e.Handled = false;
        }

        public void FileList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ListView view = null;
            ScrollContentPresenter p = null;
            if (sender is ListView lv)
            {
                view = lv;
                var ip = FindVisualChild<ItemsPresenter>(view);
                p = FindAncestor<ScrollContentPresenter>((DependencyObject)ip);
            }
            if (view == null || p == null)
                return;

            var aMenu = new System.Windows.Controls.ContextMenu();
            {
                var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_button_refresh") };
                menu.Click += (o, a) =>
                {
                    CmdGoToPathCurrent.Execute();
                };
                aMenu.Items.Add(menu);
            }
            {
                var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_button_create_folder") };
                menu.Click += (o, a) =>
                {
                    CmdEndRenaming.Execute();
                    var newFolder = new RemoteItem()
                    {
                        IsRenaming = true,
                        Name = "New Folder",
                        FullName = "",
                        IsDirectory = true,
                        Icon = TransmitItemIconCache.GetDictIcon(),
                    };
                    RemoteItems.Add(newFolder);
                };
                aMenu.Items.Add(menu);
            }

            var curSelectedItem = GetItemOnPosition(p, e.GetPosition(p));
            if (curSelectedItem == null)
            {
                ((ListView)sender).SelectedItem = null;

                {
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_button_upload") };
                    menu.Click += (o, a) =>
                    {
                        if (CmdUploadClipboard.CanExecute())
                            CmdUploadClipboard.Execute();
                    };
                    menu.IsEnabled = CmdUploadClipboard.CanExecute();
                    aMenu.Items.Add(menu);
                }

                {
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_button_select_upload") };
                    menu.Click += (o, a) =>
                    {
                        if (CmdUpload.CanExecute())
                            CmdUpload.Execute();
                    };
                    aMenu.Items.Add(menu);
                }
            }
            else if (VisualUpwardSearch<ListViewItem>(e.OriginalSource as DependencyObject) is ListViewItem item)
            {
                {
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_button_delete") };
                    menu.Click += (o, a) =>
                    {
                        CmdDelete.Execute();
                    };
                    aMenu.Items.Add(menu);
                }
                {
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_button_save_to") };
                    menu.Click += (o, a) =>
                    {
                        CmdDownload.Execute();
                    };
                    aMenu.Items.Add(menu);
                }
            }
            ((ListView)sender).ContextMenu = aMenu;
        }
    }
}
