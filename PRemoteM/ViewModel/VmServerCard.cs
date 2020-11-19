using System.Diagnostics;
using System.Windows;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.View;
using Shawn.Utils;
using Shawn.Utils.PageHost;

namespace PRM.ViewModel
{
    public class VmServerCard : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            private set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public readonly VmServerListPage Host;

        public VmServerCard(ProtocolServerBase server, VmServerListPage host)
        {
            Server = server;
            Host = host;
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
                        GlobalEventHelper.OnServerConnect?.Invoke(Server.Id);
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
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(Server.Id, false);
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
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(Server.Id, true);
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
                    if (MessageBox.Show(
                            SystemConfig.Instance.Language.GetText("server_card_operate_confirm_delete"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"),
                            MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                        MessageBoxResult.Yes)
                    {
                        GlobalData.Instance.ServerListRemove(Server);
                    }
                });
            }
        }
        #endregion
    }
}
