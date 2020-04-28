using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.Core.Ulits.DragablzTab;
using PRM.View;
using Shawn.Ulits;

namespace PRM.Model
{

    public static class WindowPool
    {
        private static string _lastTabToken = null;
        private static readonly Dictionary<string, TabWindow> _tabWindows = new Dictionary<string, TabWindow>();
        private static readonly Dictionary<uint, ProtocolHostBase> _protocolHosts = new Dictionary<uint, ProtocolHostBase>();
        private static readonly Dictionary<uint, FullScreenWindow> _host2FullScreenWindows = new Dictionary<uint, FullScreenWindow>();



        public static void ShowRemoteHost(ProtocolServerBase server)
        {
            var id = server.Id;
            Debug.Assert(id > 0);
            if (_protocolHosts.ContainsKey(id))
            {
                _protocolHosts[id].Parent?.Activate();
                return;
            }


            // TODO 删掉测试代码
            ((ProtocolServerRDP)server).AutoSetting = new ProtocolServerRDP.LocalSetting()
            {
                FullScreen_LastSessionIsFullScreen = false,
            };

            if (server.IsConnWithFullScreen())
            {
                var host = ProtocolHostFactory.Get(server);
                host.OnClose += OnProtocolClose;
                host.OnFullScreen2Window += OnFullScreen2Window;
                WindowPool.AddProtocolHost(host);
                WindowPool.MoveProtocolToFullScreen(server.Id);
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
                host.OnClose += OnProtocolClose;
                host.OnFullScreen2Window += OnFullScreen2Window;
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

        private static void OnFullScreen2Window(uint id)
        {
            MoveProtocolToTab(id);
        }

        private static void OnProtocolClose(uint id)
        {
            DelProtocolHost(id);
        }


        public static void AddTab(TabWindow tab)
        {
            var token = tab.Vm.Token;
            Debug.Assert(!_tabWindows.ContainsKey(token));
            Debug.Assert(!string.IsNullOrEmpty(token));
            _tabWindows.Add(token, tab);
            tab.Activated += (sender, args) =>
                _lastTabToken = tab.Vm.Token;
        }
        public static void AddFull(FullScreenWindow full)
        {
            Debug.Assert(!_host2FullScreenWindows.ContainsKey(full.ProtocolHostBase.Id));
            _host2FullScreenWindows.Add(full.ProtocolHostBase.Id, full);
        }
        public static void AddProtocolHost(ProtocolHostBase protocol)
        {
            Debug.Assert(!_protocolHosts.ContainsKey(protocol.Id));
            _protocolHosts.Add(protocol.Id, protocol);
        }


        public static void MoveProtocolToFullScreen(uint id)
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


                FullScreenWindow full;
                if (_host2FullScreenWindows.ContainsKey(id))
                {
                    full = _host2FullScreenWindows[id];
                    full.SetProtocolHost(host);
                    host.Parent = full;
                    host.GoFullScreen();
                }
                else
                {
                    // move to full
                    full = new FullScreenWindow(host);
                    AddFull(full);
                    host.Parent = full;
                    full.Loaded += (sender, args) => { host.GoFullScreen(); };
                }
                full.Show();

                SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }
        public static void MoveProtocolToTab(uint id)
        {
            if (_protocolHosts.ContainsKey(id))
            {
                var host = _protocolHosts[id];
                // remove from old parent
                if (host.Parent != null)
                {
                    if (host.Parent is TabWindow tab)
                    {
                        tab.Activate();
                        return;
                    }
                    else if (host.Parent is FullScreenWindow full)
                    {
                        SimpleLogHelper.Log($@"Hide full({full.GetHashCode()})");
                        full.Hide();
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
                }
            }
        }
        /// <summary>
        /// terminate remote connection
        /// </summary>
        public static void DelProtocolHost(uint id)
        {
            if (_protocolHosts.ContainsKey(id))
            {
                var host = _protocolHosts[id];
                SimpleLogHelper.Log($@"Del host({host.GetHashCode()})");
                _protocolHosts.Remove(id);
                host.DisConn();
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");

                // close full
                CloseFullScreenWindow(id);

                // close tab
                var tabs = _tabWindows.Values.Where(x => x.Vm.Items.Any(y => y.Content.ProtocolServer.Id == id));
                foreach (var tab in tabs.ToArray())
                {
                    tab.Vm.Items.Remove(tab.Vm.Items.First(x => x.Content.ProtocolServer.Id == id));
                    if (tab.Vm.Items.Count == 0)
                        CloseTabWindow(tab.Vm.Token);
                }
            }
        }

        /// <summary>
        /// del window terminate remote connection
        /// </summary>
        public static void DelTabWindow(string token)
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

        public static void CloseFullScreenWindow(uint id)
        {
            if (_host2FullScreenWindows.ContainsKey(id))
            {
                var parent = _host2FullScreenWindows[id];
                SimpleLogHelper.Log($@"Close full({parent.GetHashCode()})");
                parent.Close();
                _host2FullScreenWindows.Remove(id);
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }
        public static void CloseTabWindow(string token)
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
}
