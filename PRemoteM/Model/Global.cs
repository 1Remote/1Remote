using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        private readonly Dictionary<string, TabWindow> _tabWindows = new Dictionary<string, TabWindow>();
        private readonly Dictionary<uint, ProtocolHostBase> _protocolHosts = new Dictionary<uint, ProtocolHostBase>();
        private readonly Dictionary<uint, FullScreenWindow> _host2FullScreenWindows = new Dictionary<uint, FullScreenWindow>();

        public void AddTab(TabWindow tab)
        {
            var token = tab.Vm.Token;
            Debug.Assert(!string.IsNullOrEmpty(token));
            _tabWindows.Add(token, tab);
            tab.Activated += (sender, args) =>
                _lastTabToken = tab.Vm.Token;
        }

        private void OnCmdConn(uint id)
        {
            Debug.Assert(id > 0);
            Debug.Assert(ServerList.Any(x => x.Id == id));
            if (_protocolHosts.ContainsKey(id))
            {
                _protocolHosts[id].Parent?.Activate();
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
                _protocolHosts.Add(server.Id, host);
                MoveProtocolToFullScreen(server.Id);
                host.Conn();
                SimpleLogHelper.Log($@"Start Conn: {server.DispName}({server.GetHashCode()}) by host({host.GetHashCode()}) with full");
            }
            else
            {
                TabWindow tab = null;
                if (!string.IsNullOrEmpty(_lastTabToken) && _tabWindows.ContainsKey(_lastTabToken))
                    tab = _tabWindows[_lastTabToken];
                else
                {
                    string token = DateTime.Now.Ticks.ToString();
                    AddTab(new TabWindow(token));
                    tab = _tabWindows[token];
                    tab.Show();
                    _lastTabToken = token;
                }
                tab.Activate();
                var size = tab.GetTabContentSize();
                var host = ProtocolHostFactory.Get(server, size.Width, size.Height);
                host.Parent = tab;
                tab.Vm.Items.Add(new TabItemViewModel()
                {
                    Content = host,
                    Header = server.DispName,
                });
                tab.Vm.SelectedItem = tab.Vm.Items.Last();
                host.Conn();
                _protocolHosts.Add(server.Id, host);
                SimpleLogHelper.Log($@"Start Conn: {server.DispName}({server.GetHashCode()}) by host({host.GetHashCode()}) with Tab({tab.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }

        public FullScreenWindow MoveProtocolToFullScreen(uint id)
        {
            if (_protocolHosts.ContainsKey(id))
            {
                var host = _protocolHosts[id];

                // remove from old parent
                if (host.Parent != null)
                {
                    var tab = (TabWindow) host.Parent;
                    SimpleLogHelper.Log($@"Remove host({host.GetHashCode()}) from tab({tab.GetHashCode()})");
                    tab.Vm.Items.Remove(tab.Vm.SelectedItem);
                    if (tab.Vm.Items.Count > 0)
                        tab.Vm.SelectedItem = tab.Vm.Items.First();
                    else
                    {
                        SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) Close");
                        CloseTabWindow(tab.Vm.Token);
                    }
                }

                // move to full
                var full = new FullScreenWindow(host);
                _host2FullScreenWindows.Add(id, full);
                full.Loaded += (sender, args) =>
                {
                    host.GoFullScreen();
                };
                full.Show();
                host.Parent = full;
                SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                return full;
            }
            return null;
        }
        public TabWindow MoveProtocolToTab(uint id)
        {
            if (_protocolHosts.ContainsKey(id))
            {
                var host = _protocolHosts[id];

                // remove from old parent
                if (host.Parent != null)
                {
                    if (host.Parent is TabWindow tab)
                    {
                        SimpleLogHelper.Log($@"Remove host({host.GetHashCode()}) from tab({tab.GetHashCode()})");
                        tab.Vm.Items.Remove(tab.Vm.SelectedItem);
                        if (tab.Vm.Items.Count > 0)
                            tab.Vm.SelectedItem = tab.Vm.Items.First();
                        else
                        {
                            SimpleLogHelper.Log($@"Move tab({tab.GetHashCode()}) Close");
                            CloseTabWindow(tab.Vm.Token);
                        }
                    }
                    else if (host.Parent is FullScreenWindow full)
                    {
                        SimpleLogHelper.Log($@"Move full({full.GetHashCode()}) Close");
                        CloseFullScreenWindow(id);
                    }
                    else
                        throw new ArgumentOutOfRangeException(host.Parent + " type is wrong!");
                }


                {
                    TabWindow tab = null;
                    if (!string.IsNullOrEmpty(_lastTabToken) && _tabWindows.ContainsKey(_lastTabToken))
                        tab = _tabWindows[_lastTabToken];
                    else
                    {
                        string token = DateTime.Now.Ticks.ToString();
                        AddTab(new TabWindow(token));
                        tab = _tabWindows[token];
                        tab.Show();
                        _lastTabToken = token;
                    }
                    tab.Activate();
                    var size = tab.GetTabContentSize();
                    tab.Vm.Items.Add(new TabItemViewModel()
                    {
                        Content = host,
                        Header = host.ProtocolServer.DispName,
                    });
                    tab.Vm.SelectedItem = tab.Vm.Items.Last();
                    host.Parent = tab;
                    SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
                    SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                    return tab;
                }
            }
            return null;
        }

        public void DelProtocolHost(uint id)
        {
            if (_protocolHosts.ContainsKey(id))
            {
                var host = _protocolHosts[id];
                SimpleLogHelper.Log($@"Del host({host.GetHashCode()})");
                _protocolHosts.Remove(id);
                //var tab = _tabWindows.Values.Where(x => x.Vm.Items.Any(y => y.Content.ProtocolServer.Id == id));
                //if (tab.Any())
                //{
                //    Debug.Assert(tab.Count() == 1);
                //    tab.First().Vm.Items.
                //}
                host.DisConn();
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }
        public void DelFullScreenWindow(uint id)
        {
            CloseFullScreenWindow(id);
            DelProtocolHost(id);
        }
        public void DelTabWindow(string token)
        {
            if (_tabWindows.ContainsKey(token))
            {
                var tab = _tabWindows[token];
                SimpleLogHelper.Log($@"Del tab({tab.GetHashCode()})");
                foreach (var tabItemViewModel in tab.Vm.Items)
                {
                    DelProtocolHost(tabItemViewModel.Content.ProtocolServer.Id);
                }
                CloseTabWindow(token);
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
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
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }
        public void CloseTabWindow(string token)
        {
            if (_tabWindows.ContainsKey(token))
            {
                var tab = _tabWindows[token];
                SimpleLogHelper.Log($@"Close tab({tab.GetHashCode()})");
                _tabWindows.Remove(token);
                tab.Close();
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }
    }
    public static class WindowPool
    { }

}
