using System.Diagnostics;
using System.Windows;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.View;
using Shawn.Ulits;
using Shawn.Ulits.PageHost;

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
                        this.Server.Conn();
                    });
                return _cmdConnServer;
            }
        }

        private RelayCommand _cmdEditServer;
        public RelayCommand CmdEditServer
        {
            get
            {
                if (_cmdEditServer == null)
                {
                    _cmdEditServer = new RelayCommand((o) =>
                    {
                        Host.Vm.DispPage = new AnimationPage()
                        {
                            InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                            OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                            Page = new ServerEditorPage(new VmServerEditorPage((ProtocolServerBase)this.Server.Clone(), this.Host)),
                        };
                    });
                }
                return _cmdEditServer;
            }
        }


        private RelayCommand _cmdDuplicateServer;
        public RelayCommand CmdDuplicateServer
        {
            get
            {
                if (_cmdDuplicateServer == null)
                {
                    _cmdDuplicateServer = new RelayCommand((o) =>
                    {
                        var s = (ProtocolServerBase) this.Server.Clone();
                        s.Id = 0;
                        Host.Vm.DispPage = new AnimationPage()
                        {
                            InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                            OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                            Page = new ServerEditorPage(new VmServerEditorPage(s, this.Host)),
                        };
                    });
                }
                return _cmdDuplicateServer;
            }
        }

        private RelayCommand _cmdDeleteServer;
        public RelayCommand CmdDeleteServer
        {
            get
            {
                if (_cmdDeleteServer == null)
                    _cmdDeleteServer = new RelayCommand((o) =>
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
                return _cmdDeleteServer;
            }
        }
        #endregion
    }
}
