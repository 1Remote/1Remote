using System.Windows;
using System.Windows.Controls;
using PRM.Core.Model;
using Shawn.Utils;

namespace PRM.Core.Protocol
{
    public class VmProtocolServer : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;

        public int Id => Server?.Id ?? 0;

        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public VmProtocolServer(ProtocolServerBase psb)
        {
            Server = psb;
            SubTitleControl = OrgSubTitleControl;
        }

        public object OrgDispNameControl =>
            new TextBlock()
            {
                Text = Server.DispName,
            };

        public object OrgSubTitleControl =>
            new TextBlock()
            {
                Text = Server?.SubTitle,
            };

        private object _dispNameControl = null;

        public object DispNameControl
        {
            get => _dispNameControl;
            set => SetAndNotifyIfChanged(nameof(DispNameControl), ref _dispNameControl, value);
        }

        private object _subTitleControl = null;

        public object SubTitleControl
        {
            get => _subTitleControl;
            set => SetAndNotifyIfChanged(nameof(SubTitleControl), ref _subTitleControl, value);
        }

        private Visibility _objectVisibility = Visibility.Visible;

        public Visibility ObjectVisibility
        {
            get => _objectVisibility;
            set => SetAndNotifyIfChanged(nameof(ObjectVisibility), ref _objectVisibility, value);
        }

        private Visibility _objectVisibilityInList = Visibility.Visible;

        public Visibility ObjectVisibilityInList
        {
            get => _objectVisibilityInList;
            set => SetAndNotifyIfChanged(nameof(ObjectVisibilityInList), ref _objectVisibilityInList, value);
        }

        private bool _isSelected;

        /// <summary>
        /// is selected in list of MainWindow?
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotifyIfChanged(nameof(IsSelected), ref _isSelected, value);
        }

        #region CMD

        private RelayCommand _cmdConnServer;

        public RelayCommand CmdConnServer
        {
            get
            {
                if (_cmdConnServer == null)
                    _cmdConnServer = new RelayCommand((o) =>
                    {
                        GlobalEventHelper.OnRequestServerConnect?.Invoke(Server.Id);
                    });
                return _cmdConnServer;
            }
        }

        private RelayCommand _cmdEditServer;

        public RelayCommand CmdEditServer
        {
            get
            {
                return _cmdEditServer ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(Server.Id, false, true);
                });
            }
        }

        private RelayCommand _cmdDuplicateServer;

        public RelayCommand CmdDuplicateServer
        {
            get
            {
                return _cmdDuplicateServer ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(Server.Id, true, true);
                });
            }
        }

        private RelayCommand _cmdDeleteServer;

        public RelayCommand CmdDeleteServer
        {
            get
            {
                return _cmdDeleteServer ??= new RelayCommand((o) =>
                {
                    if (MessageBoxResult.Yes == MessageBox.Show(
                            SystemConfig.Instance.Language.GetText("confirm_to_delete"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"),
                            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None)
                        )
                    {
                        GlobalEventHelper.OnRequestDeleteServer?.Invoke(Server.Id);
                    }
                });
            }
        }

        private RelayCommand _cmdSave;

        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave != null) return _cmdSave;

                _cmdSave = new RelayCommand((o) =>
                {
                    if (string.IsNullOrWhiteSpace(this.Server.DispName)) return;

                    GlobalEventHelper.OnRequestUpdateServer?.Invoke(Server);
                }, o => (this.Server.DispName.Trim() != ""));
                return _cmdSave;
            }
        }

        #endregion CMD
    }
}