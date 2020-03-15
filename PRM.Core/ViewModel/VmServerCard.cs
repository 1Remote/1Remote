using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using PRM.Core.Base;
using PRM.Core.Protocol.RDP;
using PRM.Core.UI.VM;
using PRM.RDP;

namespace PRM.Core.ViewModel
{
    public class VmServerCard : NotifyPropertyChangedBase
    {
        private ServerAbstract _server = null;
        public ServerAbstract Server
        {
            get => _server;
            private set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }


        public VmServerCard()
        {
            Server = null;
        }
        public VmServerCard(ServerAbstract server)
        {
            Server = server;
        }



        private RelayCommand _connServer;
        public RelayCommand ConnServer
        {
            get
            {
                if (_connServer == null)
                    _connServer = new RelayCommand((o) =>
                    {
                        Debug.Assert(OnAction != null);
                        this.Server.Conn();
                    });
                return _connServer;
            }
        }


        private RelayCommand _addServer;
        public RelayCommand AddServer
        {
            get
            {
                if (_addServer == null)
                    _addServer = new RelayCommand((o) =>
                    {
                        Debug.Assert(OnAction != null);
                        Debug.Assert(this.Server.Id == 0);
                        Debug.Assert(this.Server.GetType() == typeof(NoneServer));

                        // TODO 打开对话框，选择要新增的服务器类型
                        var add = new AddRdp();
                        add.Server = new ServerRDP();
                        add.Server.GroupName = this.Server.GroupName;
                        add.ShowDialog();
                        if (add.IsSave)
                        {
                            this.Server = (ServerAbstract)add.Server.Clone();
                            OnAction.Invoke(this, EServerAction.Add);
                        }
                    }, o => (Server?.Id ?? 0) == 0);
                return _addServer;
            }
        }


        private RelayCommand _editServer;
        public RelayCommand EditServer
        {
            get
            {
                if (_editServer == null)
                    _editServer = new RelayCommand((o) =>
                    {
                        Debug.Assert(OnAction != null);
                        var add = new AddRdp();
                        add.Server = (ServerRDP)this.Server.Clone();
                        add.ShowDialog();
                        if (add.IsSave)
                        {
                            this.Server.Update(add.Server);
                            OnAction.Invoke(this, EServerAction.Edit);
                        }
                    }, o => (Server?.Id ?? 0) > 0);
                return _editServer;
            }
        }


        private RelayCommand _deleteServer;
        public RelayCommand DeleteServer
        {
            get
            {
                if (_deleteServer == null)
                    _deleteServer = new RelayCommand((o) =>
                    {
                        Debug.Assert(OnAction != null);
                        if (MessageBox.Show("TXT:确定要删除？", "TXT:提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                            MessageBoxResult.Yes)
                        {
                            OnAction.Invoke(this, EServerAction.Delete);
                        }
                    });
                return _deleteServer;
            }
        }




        public enum EServerAction
        {
            Add,
            Edit,
            Delete
        }

        public delegate void OnActionEventDelegate(VmServerCard sender, EServerAction action);

        public OnActionEventDelegate OnAction;
    }
}
