using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        private string _lastTabToken = null;
        public Dictionary<string, TabWindow> TabWindows = new Dictionary<string, TabWindow>();
        private Dictionary<uint, ProtocolHostBase> ProtocolHosts = new Dictionary<uint, ProtocolHostBase>();
        private readonly Dictionary<uint, FullScreenWindow> _host2FullScreenWindows = new Dictionary<uint, FullScreenWindow>();

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
                var parent = new FullScreenWindow(host);
                host.GoFullScreen();
                parent.Show();
                host.Conn();
                ProtocolHosts.Add(server.Id, host);
                _host2FullScreenWindows.Add(id, parent);
                host.Parent = parent;
                SimpleLogHelper.Log($@"Start Conn: {server.DispName}({server.GetHashCode()}) by host({host.GetHashCode()}) with FullWin({parent.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
            }
            else
            {
                TabWindow parent = null;
                if (!string.IsNullOrEmpty(_lastTabToken) && TabWindows.ContainsKey(_lastTabToken))
                    parent = TabWindows[_lastTabToken];
                else
                {
                    string token = DateTime.Now.Ticks.ToString();
                    TabWindows.Add(token, new TabWindow(token));
                    parent = TabWindows[token];
                    //tab.Loaded += (sender, args) => host.Conn();
                    parent.Show();
                    _lastTabToken = token;
                }
                parent.Activate();
                var size = parent.GetTabContentSize();
                var host = ProtocolHostFactory.Get(server, size.Width, size.Height);
                host.Parent = parent;
                parent.Vm.Items.Add(new TabItemViewModel()
                {
                    Content = host,
                    Header = server.DispName,
                });
                parent.Vm.SelectedItem = parent.Vm.Items.Last();
                host.Conn();
                ProtocolHosts.Add(server.Id, host);
                SimpleLogHelper.Log($@"Start Conn: {server.DispName}({server.GetHashCode()}) by host({host.GetHashCode()}) with Tab({parent.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
            }
        }

        public FullScreenWindow MoveProtocolToFullScreen(uint id)
        {
            if (ProtocolHosts.ContainsKey(id))
            {
                var host = ProtocolHosts[id];
                var tab = (TabWindow) host.Parent;
                var parent = new FullScreenWindow(tab.Vm.SelectedItem.Content);
                parent.Loaded += (sender, args) =>
                {
                    tab.Vm.SelectedItem.Content.GoFullScreen();
                };
                parent.Show();
                SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) from tab({tab.GetHashCode()}) to full({parent.GetHashCode()})");
                _host2FullScreenWindows.Add(id, parent);
                tab.Vm.Items.Remove(tab.Vm.SelectedItem);
                if (tab.Vm.Items.Count > 0)
                    tab.Vm.SelectedItem = tab.Vm.Items.First();
                else
                {
                    SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) Close");
                    CloseTabWindow(tab.Vm.Token);
                }
                host.Parent = parent;
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
                return parent;
            }
            return null;
        }
        public TabWindow MoveProtocolToTab(uint id)
        {
            CloseFullScreenWindow(id);
            if (ProtocolHosts.ContainsKey(id))
            {
                var host = ProtocolHosts[id];
                TabWindow parent = null;
                if (!string.IsNullOrEmpty(_lastTabToken) && TabWindows.ContainsKey(_lastTabToken))
                    parent = TabWindows[_lastTabToken];
                else
                {
                    string token = DateTime.Now.Ticks.ToString();
                    TabWindows.Add(token, new TabWindow(token));
                    parent = TabWindows[token];
                    //tab.Loaded += (sender, args) => host.Conn();
                    parent.Show();
                    _lastTabToken = token;
                }
                parent.Activate();
                var size = parent.GetTabContentSize();
                parent.Vm.Items.Add(new TabItemViewModel()
                {
                    Content = host,
                    Header = host.ProtocolServer.DispName,
                });
                parent.Vm.SelectedItem = parent.Vm.Items.Last();
                host.Parent = parent;
                SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) to tab({parent.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
                return parent;
            }
            return null;
        }

        public void DelProtocolHost(uint id)
        {
            if (ProtocolHosts.ContainsKey(id))
            {
                var host = ProtocolHosts[id];
                SimpleLogHelper.Log($@"Del host({host.GetHashCode()})");
                ProtocolHosts.Remove(id);
                host.DisConn();
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
            }
        }
        public void DelFullScreenWindow(uint id)
        {
            CloseFullScreenWindow(id);
            DelProtocolHost(id);
        }
        public void DelTabWindow(string token)
        {
            if (TabWindows.ContainsKey(token))
            {
                var tab = TabWindows[token];
                SimpleLogHelper.Log($@"Del tab({tab.GetHashCode()})");
                foreach (var tabItemViewModel in tab.Vm.Items)
                {
                    DelProtocolHost(tabItemViewModel.Content.ProtocolServer.Id);
                }
                CloseTabWindow(token);
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
            }
        }
        public void CloseFullScreenWindow(uint id)
        {
            if (_host2FullScreenWindows.ContainsKey(id))
            {
                var parent = _host2FullScreenWindows[id];
                SimpleLogHelper.Log($@"Close full win({parent.GetHashCode()})");
                try
                {
                    _host2FullScreenWindows[id].ProtocolHostBase.OnFullScreen2Window -= _host2FullScreenWindows[id].OnFullScreen2Window;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                parent.ProtocolHostBase = null;
                parent.Close();
                _host2FullScreenWindows.Remove(id);
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
            }
        }
        public void CloseTabWindow(string token)
        {
            if (TabWindows.ContainsKey(token))
            {
                var tab = TabWindows[token];
                SimpleLogHelper.Log($@"Close tab({tab.GetHashCode()})");
                TabWindows.Remove(token);
                tab.Close();
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {ProtocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, TabWindows.Count = {TabWindows.Count}");
            }
        }
    }
}
