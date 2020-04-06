using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.Core.UI.VM;
using PRM.View;
using Shawn.Ulits.PageHost;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public readonly VmServerListPage Host;

        public VmServerEditorPage(ProtocolServerBase server, VmServerListPage host)
        {
            Server = server;
            Host = host;
        }



        private RelayCommand _connServer;
        public RelayCommand ConnServer
        {
            get
            {
                if (_connServer == null)
                    _connServer = new RelayCommand((o) =>
                    {

                        this.Server.Conn();
                    });
                return _connServer;
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
                        Global.GetInstance().ServerListUpdate(Server);
                        Host.Host.DispPage = null;
                    }, o => (this.Server.DispName != ""));
                return _cmdSave;
            }
        }




        private RelayCommand _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                if (_cmdCancel == null)
                    _cmdCancel = new RelayCommand((o) =>
                    {
                        Host.Host.DispPage = null;
                    });
                return _cmdCancel;
            }
        }
    }
}
