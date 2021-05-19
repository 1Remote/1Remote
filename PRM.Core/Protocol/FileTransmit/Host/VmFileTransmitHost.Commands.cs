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
using System.Windows.Media;
using System.Windows.Threading;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController;
using Shawn.Utils;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using MessageBoxOptions = System.Windows.MessageBoxOptions;

namespace PRM.Core.Protocol.FileTransmit.Host
{
    public partial class VmFileTransmitHost : NotifyPropertyChangedBase
    {
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
                                SystemConfig.Instance.Language.GetText("confirm_to_delete"),
                                SystemConfig.Instance.Language.GetText("messagebox_title_warning"),
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
                            var msg = SystemConfig.Instance.Language.GetText("file_transmit_host_message_preview_over_size");
                            msg = msg.Replace("1 MB", $"{limit} MB");
                            if (SelectedRemoteItem.ByteSize > 1024 * 1024 * limit
                            && MessageBox.Show(
                                msg,
                                SystemConfig.Instance.Language.GetText("messagebox_title_warning"),
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
                                var t = new TransmitTask(Trans, fi.Directory.FullName, ris);
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

                        var dlg = new System.Windows.Forms.SaveFileDialog
                        {
                            Title = SystemConfig.Instance.Language.GetText("file_transmit_host_message_files_download_to"),
                            CheckFileExists = false,
                            ValidateNames = false,
                            FileName = SystemConfig.Instance.Language.GetText("file_transmit_host_message_files_download_to_dir"),
                        };
                        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            var destinationDirectoryPath = new FileInfo(dlg.FileName).DirectoryName;

                            if (!IOPermissionHelper.HasWritePermissionOnFile(dlg.FileName)
                            || !IOPermissionHelper.HasWritePermissionOnDir(destinationDirectoryPath))
                            {
                                IoMessage = SystemConfig.Instance.Language.GetText("string_permission_denied") + $": {dlg.FileName}";
                                IoMessageLevel = 2;
                                return;
                            }

                            var ris = RemoteItems.Where(x => x.IsSelected == true).ToArray();
                            var t = new TransmitTask(Trans, destinationDirectoryPath, ris);
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
                            List<string> fl = new List<string>();
                            var dlg = new System.Windows.Forms.OpenFileDialog();
                            dlg.Title = SystemConfig.Instance.Language.GetText(
                                "file_transmit_host_message_select_files_to_upload");
                            dlg.CheckFileExists = true;
                            dlg.Multiselect = true;
                            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                fl = dlg.FileNames?.ToList();
                                if (fl?.Count > 0)
                                {
                                    DoUpload(fl);
                                }
                            }
                        }
                        else if (o is int n)
                        {
                            // upload select folder
                            if (Trans?.IsConnected() != true)
                                return;
                            List<string> fl = new List<string>();
                            var fbd = new FolderBrowserDialog();
                            fbd.Description = SystemConfig.Instance.Language.GetText("file_transmit_host_message_select_files_to_upload");
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
                var t = new TransmitTask(Trans, CurrentPath, fis.ToArray(), dis.ToArray());
                AddTransmitTask(t);
                t.StartTransmitAsync();
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
        //            }, o => _trans?.IsConnected() == true);
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
        //            }, o => _trans?.IsConnected() == true);
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
    }
}