using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.DB;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.Core.Ulits.DragablzTab;
using PRM.View;
using Shawn.Ulits;
using Shawn.Ulits.RDP;
using static System.Diagnostics.Debug;

namespace PRM.Core.Model
{
    public class Global
    {
        #region singleton
        private static Global uniqueInstance;
        private static readonly object InstanceLock = new object();

        public static Global GetInstance()
        {
            lock (InstanceLock)
            {
                if (uniqueInstance == null)
                {
                    uniqueInstance = new Global();
                }
            }
            return uniqueInstance;
        }
        #endregion

        private Global()
        {
            ReadServerDataFromDb();
            SystemConfig.GetInstance().General.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfig.General.DbPath))
                    ReadServerDataFromDb();
            };
        }

        #region Server Data

        public ObservableCollection<ProtocolServerBase> ServerList = new ObservableCollection<ProtocolServerBase>();

        private void ReadServerDataFromDb()
        {
            //#if DEBUG
            //            // TODO 测试用删除数据库
            //            if (File.Exists(SystemConfig.GetInstance().General.DbPath))
            //                File.Delete(SystemConfig.GetInstance().General.DbPath);
            //            if (PRM_DAO.GetInstance().ListAllServer().Count == 0)
            //            {
            //                var di = new DirectoryInfo(@"D:\rdpjson");
            //                if (di.Exists)
            //                {
            //                    // read from jsonfile 
            //                    var fis = di.GetFiles("*.prmj", SearchOption.AllDirectories);
            //                    var rdp = new ProtocolServerRDP();
            //                    foreach (var fi in fis)
            //                    {
            //                        var newRdp = rdp.CreateFromJsonString(File.ReadAllText(fi.FullName));
            //                        if (newRdp != null)
            //                        {
            //                            PRM_DAO.GetInstance().Insert(ServerOrm.ConvertFrom(newRdp));
            //                        }
            //                    }
            //                }
            //                else
            //                {
            //                    di.Create();
            //                }
            //            }
            //#endif

            ServerList.Clear();
            var serverOrmList = PRM_DAO.GetInstance().ListAllServer();
            foreach (var serverOrm in serverOrmList)
            {
                var serverAbstract = ServerFactory.GetInstance().CreateFromDbObjectServerOrm(serverOrm);
                if (serverAbstract.OnCmdConn == null)
                    serverAbstract.OnCmdConn += OnCmdConn;
                ServerList.Add(serverAbstract);
            }
        }

        public void ServerListUpdate(ProtocolServerBase server)
        {
            // edit
            if (server.Id > 0 && ServerList.First(x => x.Id == server.Id) != null)
            {
                ServerList.First(x => x.Id == server.Id).Update(server);
                var serverOrm = ServerOrm.ConvertFrom(server);
                if (ServerList.First(x => x.Id == server.Id) == null)
                    ServerList.First(x => x.Id == server.Id).OnCmdConn += OnCmdConn;
                PRM_DAO.GetInstance().Update(serverOrm);
            }
            // add
            else
            {
                var serverOrm = ServerOrm.ConvertFrom(server);
                if (PRM_DAO.GetInstance().Insert(serverOrm))
                {
                    var newServer = ServerFactory.GetInstance().CreateFromDbObjectServerOrm(serverOrm);
                    newServer.OnCmdConn += OnCmdConn;
                    Global.GetInstance().ServerList.Add(newServer);
                }
            }
        }


        public void ServerListRemove(ProtocolServerBase server)
        {
            Debug.Assert(server.Id > 0);
            PRM_DAO.GetInstance().DeleteServer(server.Id);
            Global.GetInstance().ServerList.Remove(server);
        }

        #endregion

        public string lastTabToken = null;
        public Dictionary<string, TabWindow> TabWindows = new Dictionary<string, TabWindow>();
        public Dictionary<uint, ProtocolHostBase> ProtocolHosts = new Dictionary<uint, ProtocolHostBase>();
        
        private void OnCmdConn(uint id)
        {
            Debug.Assert(id > 0);
            Debug.Assert(ServerList.Any(x => x.Id == id));
            if (ProtocolHosts.ContainsKey(id))
            {
                ProtocolHosts[id].Parent?.Activate();
                return;
            }

            var server = ServerList.First(x => x.Id == id);

            // TODO 删掉测试代码
            (server as ProtocolServerRDP).AutoSetting = new ProtocolServerRDP.LocalSetting()
            {
                FullScreen_LastSessionIsFullScreen = false,
            };

            if (server.IsConnWithFullScreen())
            {
                var host = ProtocolHostFactory.Get(server);
                var nw = new FullScreenWindow(host);
                host.GoFullScreen();
                nw.Show();
                host.Conn();
            }
            else
            {
                // TODO 通过委托，创建 RDP HOST，并指派到 TAB
                TabWindow tab = null;
                if (!string.IsNullOrEmpty(lastTabToken) && TabWindows.ContainsKey(lastTabToken))
                    tab = TabWindows[lastTabToken];
                else
                {
                    string token = DateTime.Now.Ticks.ToString();
                    TabWindows.Add(token, new TabWindow(token));
                    tab = TabWindows[token];
                    //tab.Loaded += (sender, args) => host.Conn();
                    tab.Show();
                }
                tab.Activate();
                var size = tab.GetTabContentSize();
                var host = ProtocolHostFactory.Get(server, size.Width, size.Height);
                tab.Vm.Items.Add(new TabItemViewModel()
                {
                    Content = host,
                    Header = server.DispName,
                });
                tab.Vm.SelectedItem = tab.Vm.Items.Last();
                host.Conn();
            }
        }
        public void MoveFullScreenToTab(uint id)
        {

        }
    }
}
