using System.Diagnostics;
using System.Windows;
using PRM.Core.Base;
using PRM.Core.Model;
using PRM.Core.Protocol.RDP;
using PRM.Core.UI.VM;
using PRM.RDP;
using PRM.View;
using Shawn.Ulits.PageHost;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        private ServerAbstract _server = null;
        public ServerAbstract Server
        {
            get => _server;
            private set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public readonly VmServerListPage Host;

        public VmServerEditorPage(ServerAbstract server, VmServerListPage host)
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


        //private RelayCommand _addServer;
        //public RelayCommand AddServer
        //{
        //    get
        //    {
        //        if (_addServer == null)
        //            _addServer = new RelayCommand((o) =>
        //            {
                        
        //                Debug.Assert(this.Server.Id == 0);
        //                Debug.Assert(this.Server.GetType() == typeof(NoneServer));

        //                // TODO 打开对话框，选择要新增的服务器类型
        //                if (add.IsSave)
        //                {
        //                    this.Server = (ServerAbstract)add.Server.Clone();
        //                    Host.OnVmServerEditorPageEditHandle(this, EServerAction.Add);
        //                }
        //            }, o => (Server?.Id ?? 0) == 0);
        //        return _addServer;
        //    }
        //}


        private RelayCommand _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                //if (_cmdSave == null)
                //    _cmdSave = new RelayCommand((o) =>
                //    {
                //        Host.Host.DispPage = null;
                //    }, o => (Server?.Id ?? 0) > 0);
                //return _cmdSave;


                if (_cmdSave == null)
                    _cmdSave = new RelayCommand((o) =>
                    {
                        if (Server?.Id > 0 && Global.GetInstance().ServerDict.ContainsKey(Server.Id))
                        {
                            Global.GetInstance().ServerDict[Server.Id].Update(Server);
                        }
                        Host.Host.DispPage = null;
                    });
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
