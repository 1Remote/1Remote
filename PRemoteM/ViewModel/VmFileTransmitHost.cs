using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController;
using Shawn.Utils;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MessageBox = System.Windows.MessageBox;

namespace PRM.ViewModel
{
    public partial class VmFileTransmitHost : NotifyPropertyChangedBase
    {
        public readonly PrmContext Context;

        public VmFileTransmitHost(PrmContext context, IProtocolFileTransmittable protocol)
        {
            Context = context;
            _protocol = protocol;
        }

        ~VmFileTransmitHost()
        {
            _consumingTransmitTaskCancellationTokenSource.Cancel(false);
        }

        public void Release()
        {
            foreach (var t in TransmitTasks)
            {
                t.TryCancel();
            }
            Trans?.Release();
            Trans = null;
        }

        public void Conn()
        {
            if (Trans?.IsConnected() != true)
            {
                GridLoadingVisibility = Visibility.Visible;
                var task = new Task(() =>
                {
                    try
                    {
                        Trans = _protocol.GeTransmitter(Context);
                        if (!string.IsNullOrWhiteSpace(_protocol.GetStartupPath()))
                            ShowFolder(_protocol.GetStartupPath());
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Fatal(e);
                        IoMessageLevel = 2;
                        IoMessage = e.Message;
                    }
                    finally
                    {
                        GridLoadingVisibility = Visibility.Collapsed;
                    }
                }, _consumingTransmitTaskCancellationTokenSource.Token);
                task.Start();
            }
            else
                GridLoadingVisibility = Visibility.Collapsed;
        }

        private void AddTransmitTask(TransmitTask t)
        {
            TransmitTasks.Add(t);
            void func(ETransmitTaskStatus status, Exception e)
            {
                if (t.OnTaskEnd != null)
                    t.OnTaskEnd -= func;

                if (e != null)
                {
                    IoMessageLevel = 2;
                    IoMessage = e.Message;
                }

                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        if (Application.Current == null)
                            return;
                        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));
                        SynchronizationContext.Current.Post(pl =>
                        {
                            try
                            {
                                // refresh after transmitted
                                if (t.ItemsHaveBeenTransmitted.Any(x =>
                                                                            x.TransmissionType == ETransmissionType.HostToServer
                                                                        && x.DstPath.IndexOf(CurrentPath, StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    CmdGoToPathCurrent.Execute();
                                }
                                if (t.TransmitTaskStatus == ETransmitTaskStatus.Cancel
                                && TransmitTasks.Contains(t))
                                    TransmitTasks.Remove(t);
                            }
                            catch (Exception e1)
                            {
                                SimpleLogHelper.Error(e1);
                            }
                        }, null);
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
                    SetAndNotifyIfChanged(ref _remoteItemsOrderBy, value);
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
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderBy<RemoteItem, int>(RemoteItems, a => a.IsDirectory ? 1 : 2).ThenBy(x => x.Name));
                        break;

                    case 0:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderBy<RemoteItem, string>(RemoteItems, x => x.Name));
                        break;

                    case 1:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderByDescending<RemoteItem, string>(RemoteItems, x => x.Name));
                        break;

                    case 2:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderBy<RemoteItem, ulong>(RemoteItems, x => x.ByteSize));
                        break;

                    case 3:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderByDescending<RemoteItem, ulong>(RemoteItems, x => x.ByteSize));
                        break;

                    case 4:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderBy<RemoteItem, DateTime>(RemoteItems, x => x.LastUpdate));
                        break;

                    case 5:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderByDescending<RemoteItem, DateTime>(RemoteItems, x => x.LastUpdate));
                        break;

                    case 6:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderBy<RemoteItem, string>(RemoteItems, x => x.FileType).ThenBy(x => x.Name));
                        break;

                    case 7:
                        remoteItemInfos = new ObservableCollection<RemoteItem>(Enumerable.OrderByDescending<RemoteItem, string>(RemoteItems, x => x.FileType).ThenBy(x => x.Name));
                        break;
                }
                RemoteItems = remoteItemInfos;
            }
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
                            var items = Trans.ListDirectoryItems(path);
                            if (Enumerable.Any<RemoteItem>(items))
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

        public void TvFileList_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ListView view = null;
            ScrollContentPresenter p = null;
            if (sender is ListView lv)
            {
                view = lv;
                var ip = MyVisualTreeHelper.FindVisualChild<ItemsPresenter>(view);
                p = MyVisualTreeHelper.FindVisualChild<ScrollContentPresenter>((DependencyObject)ip);
            }
            if (view == null || p == null)
                return;
            var curSelectedItem = MyVisualTreeHelper.GetItemOnPosition(p, e.GetPosition(p));
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
                var ip = MyVisualTreeHelper.FindVisualChild<ItemsPresenter>(view);
                p = MyVisualTreeHelper.FindAncestor<ScrollContentPresenter>((DependencyObject)ip);
            }
            if (view == null || p == null)
                return;

            var aMenu = new System.Windows.Controls.ContextMenu();
            {
                var menu = new System.Windows.Controls.MenuItem { Header = Context.LanguageService.Translate("file_transmit_host_command_refresh") };
                menu.Click += (o, a) =>
                {
                    CmdGoToPathCurrent.Execute();
                };
                aMenu.Items.Add(menu);
            }
            {
                var menu = new System.Windows.Controls.MenuItem { Header = Context.LanguageService.Translate("file_transmit_host_command_create_folder") };
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

            var curSelectedItem = MyVisualTreeHelper.GetItemOnPosition(p, e.GetPosition(p));
            if (curSelectedItem == null)
            {
                ((ListView)sender).SelectedItem = null;

                {
                    var menu = new System.Windows.Controls.MenuItem { Header = Context.LanguageService.Translate("file_transmit_host_command_upload") };
                    menu.Click += (o, a) =>
                    {
                        if (CmdUploadClipboard.CanExecute())
                            CmdUploadClipboard.Execute();
                    };
                    menu.IsEnabled = CmdUploadClipboard.CanExecute();
                    aMenu.Items.Add(menu);
                }

                {
                    var menu = new System.Windows.Controls.MenuItem { Header = Context.LanguageService.Translate("file_transmit_host_command_select_files_upload") };
                    menu.Click += (o, a) =>
                    {
                        if (CmdUpload.CanExecute())
                            CmdUpload.Execute();
                    };
                    aMenu.Items.Add(menu);
                }

                {
                    var menu = new System.Windows.Controls.MenuItem { Header = Context.LanguageService.Translate("file_transmit_host_command_select_folder_upload") };
                    menu.Click += (o, a) =>
                    {
                        if (CmdUpload.CanExecute())
                            CmdUpload.Execute(1);
                    };
                    aMenu.Items.Add(menu);
                }
            }
            else if (MyVisualTreeHelper.VisualUpwardSearch<ListViewItem>(e.OriginalSource as DependencyObject) is ListViewItem item)
            {
                {
                    var menu = new System.Windows.Controls.MenuItem { Header = Context.LanguageService.Translate("file_transmit_host_command_delete") };
                    menu.Click += (o, a) =>
                    {
                        CmdDelete.Execute();
                    };
                    aMenu.Items.Add(menu);
                }
                {
                    var menu = new System.Windows.Controls.MenuItem { Header = Context.LanguageService.Translate("file_transmit_host_command_save_to") };
                    menu.Click += (o, a) =>
                    {
                        CmdDownload.Execute();
                    };
                    aMenu.Items.Add(menu);
                }
            }
            ((ListView)sender).ContextMenu = aMenu;
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
                            if (Trans?.IsConnected() != true)
                                return;
                            if (MessageBox.Show(
                                Context.LanguageService.Translate("confirm_to_delete"),
                                Context.LanguageService.Translate("messagebox_title_warning"),
                                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None) == MessageBoxResult.Yes)
                            {
                                foreach (var itemInfo in RemoteItems)
                                {
                                    if (itemInfo.IsSelected)
                                    {
                                        var selected = itemInfo.FullName;
                                        try
                                        {
                                            Trans.Delete(selected);
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
                        ShowFolder(CurrentPath, showIoMessage: false);
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
                        if (Trans?.IsConnected() != true)
                            return;
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
                        if (Trans?.IsConnected() != true)
                            return;
                        if (RemoteItems.Any(x => x.IsRenaming == true))
                        {
                            foreach (var item in RemoteItems.Where(x => x.IsRenaming == true))
                            {
                                var newPath = CurrentPath + "/" + item.Name;
                                if (string.IsNullOrEmpty(item.FullName) || !Trans.Exists(item.FullName))
                                {
                                    // add
                                    if (item.IsDirectory)
                                    {
                                        try
                                        {
                                            Trans.CreateDirectory(newPath);
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
                                        Trans.RenameFile(item.FullName, newPath);
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
                            ShowFolder(CurrentPath, showIoMessage: false);
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
                        if (Trans?.IsConnected() != true)
                            return;
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
                        if (Trans?.IsConnected() != true)
                            return;
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
                            var msg = Context.LanguageService.Translate("file_transmit_host_message_preview_over_size");
                            msg = msg.Replace("1 MB", $"{limit} MB");
                            if (SelectedRemoteItem.ByteSize > 1024 * 1024 * limit
                            && MessageBox.Show(
                                msg,
                                Context.LanguageService.Translate("messagebox_title_warning"),
                                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None) != MessageBoxResult.Yes)
                            {
                                return;
                            }
                            try
                            {
                                var tmpPath = Path.Combine(Path.GetTempPath(), SelectedRemoteItem.Name);
                                if (File.Exists(tmpPath))
                                {
                                    File.SetAttributes(tmpPath, FileAttributes.Temporary);
                                    File.Delete(tmpPath);
                                }

                                var fi = new FileInfo(tmpPath);
                                var ris = RemoteItems.Where(x => x.IsSelected == true).ToArray();
                                var t = new TransmitTask(Context.LanguageService, Trans, fi.Directory.FullName, ris);
                                AddTransmitTask(t);
                                t.StartTransmitAsync();
                                t.OnTaskEnd += (status, exception) =>
                                {
                                    var item = t.Items.FirstOrDefault();
                                    if (item != null)
                                    {
                                        // set read only
                                        File.SetAttributes(item.DstPath, FileAttributes.ReadOnly);
                                        System.Diagnostics.Process.Start(item.DstPath);
                                    }
                                };
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
                        if (Trans?.IsConnected() != true)
                            return;
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
                        if (Trans?.IsConnected() != true)
                            return;
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
                        if (Trans?.IsConnected() != true)
                            return;
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
                        if (Trans?.IsConnected() != true)
                            return;
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
                        if (Trans?.IsConnected() != true)
                            return;

                        if (RemoteItems.All(x => x.IsSelected != true))
                        {
                            return;
                        }

                        var path = SelectFileHelper.SaveFile(title: Context.LanguageService.Translate("file_transmit_host_message_files_download_to"), selectedFileName: Context.LanguageService.Translate("file_transmit_host_message_files_download_to_dir"));
                        if (path == null) return;
                        {
                            var destinationDirectoryPath = new FileInfo(path).DirectoryName;

                            if (!IOPermissionHelper.HasWritePermissionOnFile(path)
                            || !IOPermissionHelper.HasWritePermissionOnDir(destinationDirectoryPath))
                            {
                                IoMessage = Context.LanguageService.Translate("string_permission_denied") + $": {path}";
                                IoMessageLevel = 2;
                                return;
                            }

                            var ris = RemoteItems.Where(x => x.IsSelected == true).ToArray();
                            var t = new TransmitTask(Context.LanguageService, Trans, destinationDirectoryPath, ris);
                            AddTransmitTask(t);
                            t.StartTransmitAsync();
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
                        if (o == null)
                        {
                            if (Trans?.IsConnected() != true)
                                return;

                            var paths = SelectFileHelper.OpenFiles(title: Context.LanguageService.Translate("file_transmit_host_message_select_files_to_upload"));
                            if (paths == null) return;

                            if (paths?.Length > 0)
                            {
                                DoUpload(paths.ToList());
                            }
                        }
                        else if (o is int n)
                        {
                            // upload select folder
                            if (Trans?.IsConnected() != true)
                                return;

                            var fbd = new FolderBrowserDialog();
                            fbd.Description = Context.LanguageService.Translate("file_transmit_host_message_select_files_to_upload");
                            fbd.ShowNewFolderButton = false;
                            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                DoUpload(new List<string>() { fbd.SelectedPath });
                            }
                        }
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
                        if (Trans?.IsConnected() != true)
                            return;
                        var fl = Clipboard.GetFileDropList().Cast<string>().ToList();
                        if (fl.Count == 0)
                        {
                            return;
                        }

                        if (fl.Any(f => !File.Exists(f) && !Directory.Exists(f)))
                        {
                            return;
                        }
                        DoUpload(fl);
                    }, o => Trans?.IsConnected() == true
                            && Clipboard.GetFileDropList().Count > 0
                            && Clipboard.GetFileDropList().Cast<string>().All(f => File.Exists(f) || Directory.Exists(f)));
                }
                return _cmdUploadClipboard;
            }
        }

        private void DoUpload(List<string> filePathList)
        {
            var fis = new List<FileInfo>();
            var dis = new List<DirectoryInfo>();
            foreach (var f in filePathList)
            {
                var fi = new FileInfo(f);
                if (fi.Exists)
                {
                    fis.Add(fi);
                }
                else
                {
                    var di = new DirectoryInfo(f);
                    if (di.Exists)
                    {
                        dis.Add(di);
                    }
                }
            }

            if (fis.Count > 0 || dis.Count > 0)
            {
                var t = new TransmitTask(Context.LanguageService, Trans, CurrentPath, fis.ToArray(), dis.ToArray());
                AddTransmitTask(t);
                t.StartTransmitAsync();
            }
        }

        private RelayCommand _cmdShowTransmitDstPath;

        public RelayCommand CmdShowTransmitDstPath
        {
            get
            {
                if (_cmdShowTransmitDstPath == null)
                {
                    _cmdShowTransmitDstPath = new RelayCommand((o) =>
                    {
                        if (Trans?.IsConnected() != true)
                            return;
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
                        if (Trans?.IsConnected() != true)
                            return;
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

        #endregion CMD


        #region Properties

        public ITransmitter Trans = null;
        private readonly IProtocolFileTransmittable _protocol = null;

        private readonly CancellationTokenSource _consumingTransmitTaskCancellationTokenSource = new CancellationTokenSource();


        public double GridLoadingBgOpacity { get; set; } = 1;
        private Visibility _gridLoadingVisibility = Visibility.Collapsed;
        public Visibility GridLoadingVisibility
        {
            get => _gridLoadingVisibility;
            set
            {
                SetAndNotifyIfChanged(ref _gridLoadingVisibility, value);
                if (GridLoadingBgOpacity > 0.99 && value != Visibility.Visible)
                {
                    GridLoadingBgOpacity = 0.1;
                    RaisePropertyChanged(nameof(GridLoadingBgOpacity));
                }
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
                    SetAndNotifyIfChanged(ref _ioMessage, value);
                    RaisePropertyChanged(nameof(IoMessageLevel));
                }
            }
        }




        #region Path conrol
        private readonly Stack<string> _pathHistoryPrevious = new Stack<string>();
        private readonly Stack<string> _pathHistoryFollowing = new Stack<string>();


        private string _currentPathEdit = "";
        /// <summary>
        /// for ui display and edit
        /// </summary>
        public string CurrentPathEdit
        {
            get => _currentPathEdit;
            set => SetAndNotifyIfChanged(ref _currentPathEdit, value);
        }

        private string _currentPath = "";
        /// <summary>
        /// for logic control to remember current path
        /// </summary>
        private string CurrentPath
        {
            get => _currentPath;
            set
            {
                SetAndNotifyIfChanged(ref _currentPath, value);
                CurrentPathEdit = value;
            }
        }


        private bool _cmdGoToPathPreviousEnable = false;
        public bool CmdGoToPathPreviousEnable
        {
            get => _cmdGoToPathPreviousEnable;
            set => SetAndNotifyIfChanged(ref _cmdGoToPathPreviousEnable, value);
        }



        private bool _cmdGoToPathFollowingEnable = false;
        public bool CmdGoToPathFollowingEnable
        {
            get => _cmdGoToPathFollowingEnable;
            set => SetAndNotifyIfChanged(ref _cmdGoToPathFollowingEnable, value);
        }




        private bool _cmdGoToPathParentEnable = false;
        public bool CmdGoToPathParentEnable
        {
            get => _cmdGoToPathParentEnable;
            set => SetAndNotifyIfChanged(ref _cmdGoToPathParentEnable, value);
        }
        #endregion





        #region File list
        private RemoteItem _selectedRemoteItem;
        public RemoteItem SelectedRemoteItem
        {
            get => _selectedRemoteItem;
            set => SetAndNotifyIfChanged(ref _selectedRemoteItem, value);
        }

        private ObservableCollection<RemoteItem> _remoteItems = new ObservableCollection<RemoteItem>();
        public ObservableCollection<RemoteItem> RemoteItems
        {
            get => _remoteItems;
            set => SetAndNotifyIfChanged(ref _remoteItems, value);
        }
        #endregion




        private ObservableCollection<TransmitTask> _transmitTasks = new ObservableCollection<TransmitTask>();
        public ObservableCollection<TransmitTask> TransmitTasks
        {
            get => _transmitTasks;
            set => SetAndNotifyIfChanged(ref _transmitTasks, value);
        }
        #endregion
    }
}