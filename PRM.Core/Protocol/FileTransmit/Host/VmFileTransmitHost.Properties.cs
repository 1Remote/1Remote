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

namespace PRM.Core.Protocol.FileTransmit.Host
{
    public partial class VmFileTransmitHost : NotifyPropertyChangedBase
    {
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
                SetAndNotifyIfChanged(nameof(GridLoadingVisibility), ref _gridLoadingVisibility, value);
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
                    SetAndNotifyIfChanged(nameof(IoMessage), ref _ioMessage, value);
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
            set => SetAndNotifyIfChanged(nameof(CurrentPathEdit), ref _currentPathEdit, value);
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
                SetAndNotifyIfChanged(nameof(CurrentPath), ref _currentPath, value);
                CurrentPathEdit = value;
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
        #endregion





        #region File list
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
        #endregion




        private ObservableCollection<TransmitTask> _transmitTasks = new ObservableCollection<TransmitTask>();
        public ObservableCollection<TransmitTask> TransmitTasks
        {
            get => _transmitTasks;
            set => SetAndNotifyIfChanged(nameof(TransmitTasks), ref _transmitTasks, value);
        }
    }
}
