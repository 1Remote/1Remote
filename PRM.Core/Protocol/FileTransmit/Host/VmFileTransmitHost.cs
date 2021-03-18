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
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController;
using Shawn.Utils;

namespace PRM.Core.Protocol.FileTransmit.Host
{
    public partial class VmFileTransmitHost : NotifyPropertyChangedBase
    {
        private readonly PrmContext _context;

        public VmFileTransmitHost(PrmContext context, IProtocolFileTransmittable protocol)
        {
            _context = context;
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
                        Trans = _protocol.GeTransmitter(_context);
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
                var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_command_refresh") };
                menu.Click += (o, a) =>
                {
                    CmdGoToPathCurrent.Execute();
                };
                aMenu.Items.Add(menu);
            }
            {
                var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_command_create_folder") };
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
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_command_upload") };
                    menu.Click += (o, a) =>
                    {
                        if (CmdUploadClipboard.CanExecute())
                            CmdUploadClipboard.Execute();
                    };
                    menu.IsEnabled = CmdUploadClipboard.CanExecute();
                    aMenu.Items.Add(menu);
                }

                {
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_command_select_files_upload") };
                    menu.Click += (o, a) =>
                    {
                        if (CmdUpload.CanExecute())
                            CmdUpload.Execute();
                    };
                    aMenu.Items.Add(menu);
                }

                {
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_command_select_folder_upload") };
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
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_command_delete") };
                    menu.Click += (o, a) =>
                    {
                        CmdDelete.Execute();
                    };
                    aMenu.Items.Add(menu);
                }
                {
                    var menu = new System.Windows.Controls.MenuItem { Header = SystemConfig.Instance.Language.GetText("file_transmit_host_command_save_to") };
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