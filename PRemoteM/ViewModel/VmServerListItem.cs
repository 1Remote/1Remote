using System.Windows;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Protocol;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerListItem : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            private set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public VmServerListItem(ProtocolServerBase server)
        {
            Server = server;
        }


        private Visibility _visible = Visibility.Collapsed;
        public Visibility Visible
        {
            get => _visible;
            set => SetAndNotifyIfChanged(nameof(Visible), ref _visible, value);
        }



        private bool _isSelected;
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
                        GlobalEventHelper.OnRequireServerConnect?.Invoke(Server.Id);
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
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(Server.Id, false, true);
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
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(Server.Id, true, true);
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
                            SystemConfig.Instance.Language.GetText("string_delete_confirm"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"),
                            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None)
                        )
                    {
                        GlobalData.Instance.ServerListRemove(Server);
                    }
                });
            }
        }


        private RelayCommand _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave == null)
                    _cmdSave = new RelayCommand((o) =>
                    {
                        if (!string.IsNullOrWhiteSpace(this.Server.DispName))
                        {
                            GlobalData.Instance.ServerListUpdate(Server);
                        }
                    }, o => (this.Server.DispName.Trim() != ""));
                return _cmdSave;
            }
        }
        #endregion
    }
}
