using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using PRM.Core.Base;
using PRM.Core.DB;
using PRM.Core.UI.VM;
using PRM.RDP;

namespace PRM.Core.ViewModel
{
    public class VmMain : NotifyPropertyChangedBase
    {
        private ObservableCollection<VmServerCard> _dispServerlist = new ObservableCollection<VmServerCard>();
        private ConcurrentDictionary<string, VmServerCard> _groupsServerlist = new ConcurrentDictionary<string, VmServerCard>();
        private ObservableCollection<string> _serverGroups = new ObservableCollection<string>();
        private string _selectedGroup;

        /// <summary>
        /// ServerList data source for list view
        /// </summary>
        public ObservableCollection<VmServerCard> DispServerList
        {
            get => _dispServerlist;
            set => SetAndNotifyIfChanged(nameof(DispServerList), ref _dispServerlist, value);
        }



        public ObservableCollection<string> ServerGroups
        {
            get => _serverGroups;
            set => SetAndNotifyIfChanged(nameof(ServerGroups), ref _serverGroups, value);
        }

        public string SelectedGroup
        {
            get => _selectedGroup;
            set => SetAndNotifyIfChanged(nameof(SelectedGroup), ref _selectedGroup, value);
        }

        private RelayCommand _clearSelectedGroup;
        public RelayCommand ClearSelectedGroup
        {
            get
            {
                if (_clearSelectedGroup == null)
                    _clearSelectedGroup = new RelayCommand((o) =>
                    {
                        SelectedGroup = "";
                    });
                return _clearSelectedGroup;
            }
        }


        public VmMain()
        {
#if DEBUG
            // TODO 测试用
            if (File.Exists(PRM_DAO.DbPath))
                File.Delete(PRM_DAO.DbPath);
            if (PRM_DAO.GetInstance().ListAllServer().Count == 0)
            {
                var di = new DirectoryInfo(@"D:\rdpjson");
                if (di.Exists)
                {
                    // read from jsonfile 
                    var fis = di.GetFiles("*.rdpjson", SearchOption.AllDirectories);
                    var rdp = new ServerRDP();
                    foreach (var fi in fis)
                    {
                        var newRdp = rdp.CreateFromJsonString(File.ReadAllText(fi.FullName));
                        if (newRdp != null)
                        {
                            PRM_DAO.GetInstance().Insert(ServerOrm.ConvertFrom(newRdp));
                        }
                    }
                }
                else
                {
                    di.Create();
                }
            }
#endif
            



            // read all server configs from database into dict['all']
            var serverOrmList = PRM_DAO.GetInstance().ListAllServer();
            foreach (var serverOrm in serverOrmList)
            {
                var s = ServerFactory.GetInstance().CreateFromDb(serverOrm);
                if (s != null)
                {
                    DispServerList.Add(new VmServerCard(s));
                    DispServerList.Last().OnAction += OnAction;
                }
            }


            ServerGroups.Clear();
            foreach (var vmServerCard in DispServerList)
            {
                if (!string.IsNullOrEmpty(vmServerCard.Server.GroupName) &&
                    !ServerGroups.Contains(vmServerCard.Server.GroupName))
                {
                    ServerGroups.Add(vmServerCard.Server.GroupName);
                }
            }

            for (int i = 0; i < 20; i++)
            {
                ServerGroups.Add("tttttttt" + i);
            }
            SelectedGroup = ServerGroups[0];


            OrderServerList();
        }



        private void OnAction(VmServerCard sender, VmServerCard.EServerAction action)
        {
            switch (action)
            {
                case VmServerCard.EServerAction.Delete:
                    {
                        var id = ((VmServerCard)sender).Server.Id;
                        PRM_DAO.GetInstance().DeleteServer(id);
                        DispServerList.Remove(((VmServerCard)sender));
                        break;
                    }
                case VmServerCard.EServerAction.Add:
                    {
                        var serverOrm = ServerOrm.ConvertFrom(sender.Server);
                        if (PRM_DAO.GetInstance().Insert(serverOrm))
                        {
                            DispServerList.Add(new VmServerCard(ServerFactory.GetInstance().CreateFromDb(serverOrm)));
                            DispServerList.Last().OnAction += OnAction;
                        }
                        break;
                    }
                case VmServerCard.EServerAction.Edit:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
            OrderServerList();
        }

        private void OrderServerList()
        {
            // Delete NoneServer
            var noneServers = DispServerList.Where(s => s.GetType() == typeof(NoneServer)).ToArray();
            foreach (var s in noneServers)
            {
                DispServerList.Remove(s);
            }

            // TODO flag to order by LassConnTime
            DispServerList = new ObservableCollection<VmServerCard>(DispServerList.OrderByDescending(s => s.Server.LassConnTime));

            // add a 'NoneServer' so that 'add server' button will be shown
            var addServerCard = new VmServerCard(new NoneServer());
            addServerCard.OnAction += OnAction;
            DispServerList.Add(addServerCard);
        }
    }
}
