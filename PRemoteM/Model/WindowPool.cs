using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;
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
        private static readonly Dictionary<string, ProtocolHostBase> _protocolHosts = new Dictionary<string, ProtocolHostBase>();
        private static readonly Dictionary<string, FullScreenWindow> _host2FullScreenWindows = new Dictionary<string, FullScreenWindow>();



        public static void ShowRemoteHost(ProtocolServerBase server)
        {
            var id = server.Id;
            Debug.Assert(id > 0);
            if (server.OnlyOneInstance && _protocolHosts.ContainsKey(server.Id.ToString()))
            {
                _protocolHosts[server.Id.ToString()].ParentWindow?.Activate();
                return;
            }

            if (server.IsConnWithFullScreen())
            {
                var host = ProtocolHostFactory.Get(server);
                host.OnClosed += OnProtocolClose;
                host.OnFullScreen2Window += OnFullScreen2Window;
                WindowPool.AddProtocolHost(host);
                WindowPool.MoveProtocolHostToFullScreen(host.ConnectionId);
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
                    var token = DateTime.Now.Ticks.ToString();
                    AddTab(new TabWindow(token));
                    tab = _tabWindows[token];
                    tab.Show();
                    _lastTabToken = token;
                }
                tab.Activate();
                var size = tab.GetTabContentSize();
                var host = ProtocolHostFactory.Get(server, size.Width, size.Height);
                host.OnClosed += OnProtocolClose;
                host.OnFullScreen2Window += OnFullScreen2Window;
                host.ParentWindow = tab;
                tab.Vm.Items.Add(new TabItemViewModel()
                {
                    Content = host,
                    Header = server.DispName,
                });
                tab.Vm.SelectedItem = tab.Vm.Items.Last();
                host.Conn();
                _protocolHosts.Add(host.ConnectionId, host);
                SimpleLogHelper.Log($@"Start Conn: {server.DispName}({server.GetHashCode()}) by host({host.GetHashCode()}) with Tab({tab.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }

        private static void OnFullScreen2Window(string connectionId)
        {
            MoveProtocolHostToTab(connectionId);
        }

        private static void OnProtocolClose(string connectionId)
        {
            DelProtocolHost(connectionId);
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
            Debug.Assert(!_host2FullScreenWindows.ContainsKey(full.ProtocolHostBase.ConnectionId));
            _host2FullScreenWindows.Add(full.ProtocolHostBase.ConnectionId, full);
        }
        public static void AddProtocolHost(ProtocolHostBase protocol)
        {
            Debug.Assert(!_protocolHosts.ContainsKey(protocol.ConnectionId));
            _protocolHosts.Add(protocol.ConnectionId, protocol);
        }


        public static void MoveProtocolHostToFullScreen(string connectionId)
        {
            if (_protocolHosts.ContainsKey(connectionId))
            {
                var host = _protocolHosts[connectionId];

                // remove from old parent
                if (host.ParentWindow != null)
                {
                    var tab = (TabWindow) host.ParentWindow;
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
                if (_host2FullScreenWindows.ContainsKey(connectionId))
                {
                    full = _host2FullScreenWindows[connectionId];
                    full.Show();
                    full.SetProtocolHost(host);
                    host.ParentWindow = full;
                    host.GoFullScreen();
                }
                else
                {
                    // move to full
                    full = new FullScreenWindow();
                    full.Show();
                    full.SetProtocolHost(host);
                    host.ParentWindow = full;
                    full.Loaded += (sender, args) => { host.GoFullScreen(); };
                    AddFull(full);
                }

                SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }
        public static void MoveProtocolHostToTab(string connectionId)
        {
            if (_protocolHosts.ContainsKey(connectionId))
            {
                FullScreenWindow tmp = null;
                var host = _protocolHosts[connectionId];
                // remove from old parent
                if (host.ParentWindow != null)
                {
                    if (host.ParentWindow is TabWindow tab)
                    {
                        tab.Activate();
                        return;
                    }
                    else if (host.ParentWindow is FullScreenWindow full)
                    {
                        SimpleLogHelper.Log($@"Hide full({full.GetHashCode()})");
                        full.Hide();
                        tmp = full;
                    }
                    else
                        throw new ArgumentOutOfRangeException(host.ParentWindow + " type is wrong!");
                }


                {
                    TabWindow tab = null;
                    if (!string.IsNullOrEmpty(_lastTabToken) && _tabWindows.ContainsKey(_lastTabToken))
                        tab = _tabWindows[_lastTabToken];
                    else
                    {
                        var token = DateTime.Now.Ticks.ToString();
                        AddTab(new TabWindow(token));
                        tab = _tabWindows[token];
                        tab.Show();
                        _lastTabToken = token;
                        // move tab to screen witch display full window 
                        if (tmp != null)
                        {
                            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(tmp).Handle);
                            tab.Top = screen.Bounds.Y + screen.Bounds.Height / 2 - tab.Height/2;
                            tab.Left = screen.Bounds.X + screen.Bounds.Width / 2 - tab.Width / 2;
                        }
                    }
                    tab.Activate();
                    tab.Vm.Items.Add(new TabItemViewModel()
                    {
                        Content = host,
                        Header = host.ProtocolServer.DispName,
                    });
                    tab.Vm.SelectedItem = tab.Vm.Items.Last();
                    host.ParentWindow = tab;
                    SimpleLogHelper.Log($@"Move host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
                    SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }
            }
        }

        /// <summary>
        /// terminate remote connection
        /// </summary>
        public static void DelProtocolHost(string connectionId)
        {
            if (_protocolHosts.ContainsKey(connectionId))
            {
                var host = _protocolHosts[connectionId];
                SimpleLogHelper.Log($@"Del host({host.GetHashCode()})");
                _protocolHosts.Remove(connectionId);
                host.DisConn();
                SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");

                // close full
                if (_host2FullScreenWindows.ContainsKey(connectionId))
                {
                    var full = _host2FullScreenWindows[connectionId];
                    SimpleLogHelper.Log($@"Close full({full.GetHashCode()})");
                    full.Close();
                    _host2FullScreenWindows.Remove(connectionId);
                    SimpleLogHelper.Log($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }

                // close tab
                var tabs = _tabWindows.Values.Where(x => x.Vm.Items.Any(y => y.Content.ConnectionId == connectionId));
                foreach (var tab in tabs.ToArray())
                {
                    tab.Vm.Items.Remove(tab.Vm.Items.First(x => x.Content.ConnectionId == connectionId));
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
                foreach (var tabItemViewModel in tab.Vm.Items.ToArray())
                {
                    DelProtocolHost(tabItemViewModel.Content.ConnectionId);
                }
                CloseTabWindow(token);
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
