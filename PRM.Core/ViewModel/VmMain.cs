using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Controls;
using PRM.Core.Base;
using PRM.Core.DB;
using PRM.Core.UI.VM;
using PRM.RDP;

namespace PRM.Core.ViewModel
{
    public class VmMain : NotifyPropertyChangedBase
    {
        private ObservableCollection<VmServerCard> _serverlist = new ObservableCollection<VmServerCard>();
        /// <summary>
        /// 设备列表
        /// </summary>
        public ObservableCollection<VmServerCard> ServerList
        {
            get => _serverlist;
            set => SetAndNotifyIfChanged(nameof(ServerList), ref _serverlist, value);
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


            // read from database
            var serverOrmList = PRM_DAO.GetInstance().ListAllServer();
            foreach (var serverOrm in serverOrmList)
            {
                var s = ServerFactory.GetInstance().CreateFromDb(serverOrm);
                if (s != null)
                {
                    ServerList.Add(new VmServerCard(s));
                    ServerList.Last().OnAction += OnAction;
                }
            }

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
                        ServerList.Remove(((VmServerCard)sender));
                        break;
                    }
                case VmServerCard.EServerAction.Add:
                    {
                        var serverOrm = ServerOrm.ConvertFrom(sender.Server);
                        if (PRM_DAO.GetInstance().Insert(serverOrm))
                        {
                            ServerList.Add(new VmServerCard(ServerFactory.GetInstance().CreateFromDb(serverOrm)));
                            ServerList.Last().OnAction += OnAction;
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
            // Delete none id card
            var noneServers = ServerList.Where(s => s.Server.Id <= 0).ToArray();
            foreach (var s in noneServers)
            {
                ServerList.Remove(s);
            }

            // TODO flag to order by LassConnTime
            ServerList = new ObservableCollection<VmServerCard>(ServerList.OrderByDescending(s => s.Server.LassConnTime));

            // add new none id card so that a add server button will be shown
            var addServerCard = new VmServerCard(new NoneServer());
            addServerCard.OnAction += OnAction;
            ServerList.Add(addServerCard);
        }
    }
}
