using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Utils.DragablzTab;
using PRM.View;
using Shawn.Utils;
using MessageBox = System.Windows.MessageBox;

namespace PRM.Model
{
    public class RemoteWindowPool
    {
        #region singleton
        private static RemoteWindowPool _uniqueInstance;
        private static readonly object InstanceLock = new object();

        public static RemoteWindowPool GetInstance()
        {
            lock (InstanceLock)
            {
                if (_uniqueInstance == null)
                {
                    throw new NullReferenceException($"{nameof(RemoteWindowPool)} has not been inited!");
                }
            }
            return _uniqueInstance;
        }
        public static RemoteWindowPool Instance => GetInstance();
        #endregion

        public static void Init()
        {
            lock (InstanceLock)
            {
                if (_uniqueInstance == null)
                {
                    _uniqueInstance = new RemoteWindowPool();
                }
            }
        }

        private RemoteWindowPool()
        {
            GlobalEventHelper.OnServerConnect += ShowRemoteHost;
        }


        private string _lastTabToken = null;
        private readonly Dictionary<string, TabWindow> _tabWindows = new Dictionary<string, TabWindow>();
        private readonly Dictionary<string, ProtocolHostBase> _protocolHosts = new Dictionary<string, ProtocolHostBase>();
        private readonly Dictionary<string, FullScreenWindow> _host2FullScreenWindows = new Dictionary<string, FullScreenWindow>();

        public void ShowRemoteHost(uint serverId, string assignTabToken)
        {
            Debug.Assert(serverId > 0);
            Debug.Assert(GlobalData.Instance.VmItemList.Any(x => x.Server.Id == serverId));
            var vmProtocolServer = GlobalData.Instance.VmItemList.First(x => x.Server.Id == serverId);

            // update last conn time
            vmProtocolServer.Server.LastConnTime = DateTime.Now;
            Server.AddOrUpdate(vmProtocolServer.Server);

            // is connected now! activate it then return.
            if (vmProtocolServer.Server.OnlyOneInstance && _protocolHosts.ContainsKey(serverId.ToString()))
            {
                if (_protocolHosts[serverId.ToString()].ParentWindow is TabWindow t)
                {
                    var s = t.Vm?.Items?.First(x => x.Content?.ProtocolServer?.Id == serverId);
                    if (s != null)
                        t.Vm.SelectedItem = s;
                    t.Activate();
                }
                return;
            }

            // create new remote session
            ProtocolHostBase host = null;
            try
            {
                if (vmProtocolServer.Server.IsConnWithFullScreen())
                {
                    int factor = (int)(new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor * 100);
                    // check if screens are in different scale factors
                    // for those people using 2+ monitors in different scale factors, we will try "mstsc.exe" instead of "PRemoteM".
                    if (Screen.AllScreens.Length > 1
                        && vmProtocolServer.Server is ProtocolServerRDP rdp
                        && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens
                        && Screen.AllScreens.Select(screen => (int)(new ScreenInfoEx(screen).ScaleFactor * 100)).Any(factor2 => factor != factor2))
                    {
                        var tmp = Path.GetTempPath();
                        var rdpFileName = $"{rdp.DispName}_{rdp.Port}_{rdp.UserName}";
                        var invalid = new string(Path.GetInvalidFileNameChars()) +
                                  new string(Path.GetInvalidPathChars());
                        rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
                        var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");
                        try
                        {
                            File.WriteAllText(rdpFile, rdp.ToRdpConfig().ToString());
                            var p = new Process
                            {
                                StartInfo =
                                    {
                                        FileName = "cmd.exe",
                                        UseShellExecute = false,
                                        RedirectStandardInput = true,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        CreateNoWindow = true
                                    }
                            };
                            p.Start();
                            p.StandardInput.WriteLine("mstsc -admin \"" + rdpFile + "\"");
                            p.StandardInput.WriteLine("exit");
                            //FullTrustProcessLauncher.LaunchFullTrustProcessForAppAsync()
                            System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM");
                        }
                        finally
                        {
                            // delete tmp rdp file, ETA 10s
                            var t = new Task(() =>
                            {
                                try
                                {
                                    Thread.Sleep(1000 * 10);
                                    if (File.Exists(rdpFile))
                                        File.Delete(rdpFile);
                                }
                                catch (Exception e)
                                {
                                    SimpleLogHelper.Error(e, e.StackTrace);
                                }
                            });
                            t.Start();
                        }
                        return;
                    }
                    else
                    {
                        host = ProtocolHostFactory.Get(vmProtocolServer.Server);
                        Debug.Assert(!_protocolHosts.ContainsKey(host.ConnectionId));
                        _protocolHosts.Add(host.ConnectionId, host);
                        host.OnClosed += OnProtocolClose;
                        host.OnFullScreen2Window += OnFullScreen2Window;
                        var full = MoveProtocolHostToFullScreen(host.ConnectionId);
                        host.ParentWindow = full;
                        host.Conn();
                        SimpleLogHelper.Debug($@"Start Conn: {vmProtocolServer.Server.DispName}({vmProtocolServer.GetHashCode()}) by host({host.GetHashCode()}) with full");
                    }
                }
                else
                {
                    var server = vmProtocolServer.Server;
                    var tab = GetOrCreateTabWindow(server, assignTabToken);
                    var size = tab.GetTabContentSize();
                    host = ProtocolHostFactory.Get(vmProtocolServer.Server, size.Width, size.Height);
                    Debug.Assert(!_protocolHosts.ContainsKey(host.ConnectionId));
                    _protocolHosts.Add(host.ConnectionId, host);
                    host.OnClosed += OnProtocolClose;
                    host.OnFullScreen2Window += OnFullScreen2Window;
                    tab.Vm.Items.Add(new TabItemViewModel()
                    {
                        Content = host,
                        Header = vmProtocolServer.Server.DispName,
                    });
                    tab.Vm.SelectedItem = tab.Vm.Items.Last();
                    host.ParentWindow = tab;
                    host.Conn();
                    tab.Activate();
                    SimpleLogHelper.Debug($@"Start Conn: {vmProtocolServer.Server.DispName}({vmProtocolServer.GetHashCode()}) by host({host.GetHashCode()}) with Tab({tab.GetHashCode()})");
                    SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (host != null)
                    DelProtocolHost(host.ConnectionId);
                CloseEmptyTab();
                SimpleLogHelper.Error(e);
            }
        }

        private void OnFullScreen2Window(string connectionId)
        {
            MoveProtocolHostToTab(connectionId);
        }

        private void OnProtocolClose(string connectionId)
        {
            DelProtocolHost(connectionId);
        }


        public void AddTab(TabWindow tab)
        {
            var token = tab.Vm.Token;
            Debug.Assert(!_tabWindows.ContainsKey(token));
            Debug.Assert(!string.IsNullOrEmpty(token));
            _tabWindows.Add(token, tab);
            tab.Activated += (sender, args) =>
                _lastTabToken = tab.Vm.Token;
        }

        public Window MoveProtocolHostToFullScreen(string connectionId)
        {
            if (!_protocolHosts.ContainsKey(connectionId))
                throw new NullReferenceException($"_protocolHosts not contains {connectionId}");

            var host = _protocolHosts[connectionId];

            // remove from old parent
            TabWindow tab = null;
            {
                var tabs = _tabWindows.Values.Where(x => x.Vm.Items.Any(y => y.Content == host)).ToArray();
                if (tabs.Length > 0)
                {
                    tab = tabs.First();
                    foreach (var t in tabs)
                    {
                        var items = t.Vm.Items.ToArray().Where(x => x.Content == host);
                        foreach (var item in items.ToArray())
                        {
                            t.Vm.Items.Remove(item);
                            SimpleLogHelper.Debug($@"Remove host({host.GetHashCode()}) from tab({t.GetHashCode()})");
                        }
                        t.Vm.SelectedItem = t.Vm.Items.Count > 0 ? tab.Vm.Items.First() : null;
                    }
                }
            }



            FullScreenWindow full;
            if (_host2FullScreenWindows.ContainsKey(connectionId))
            {
                // restore from tab to full
                full = _host2FullScreenWindows[connectionId];
                full.LastTabToken = "";
                // full screen placement
                if (tab != null)
                {
                    var screenEx = ScreenInfoEx.GetCurrentScreen(tab);
                    full.Top = screenEx.VirtualWorkingAreaCenter.Y - full.Height / 2;
                    full.Left = screenEx.VirtualWorkingAreaCenter.X - full.Width / 2;
                    full.LastTabToken = _lastTabToken;
                }
                full.Show();
                full.SetProtocolHost(host);
                host.ParentWindow = full;
                host.GoFullScreen();
            }
            else
            {
                // first time to full
                full = new FullScreenWindow { LastTabToken = "" };

                // full screen placement
                ScreenInfoEx screenEx;
                if (tab != null)
                {
                    screenEx = ScreenInfoEx.GetCurrentScreen(tab);
                    full.LastTabToken = _lastTabToken;
                }
                else if (host.ProtocolServer is ProtocolServerRDP rdp
                    && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen
                    && rdp.AutoSetting.FullScreenLastSessionScreenIndex >= 0
                    && rdp.AutoSetting.FullScreenLastSessionScreenIndex < Screen.AllScreens.Length)
                    screenEx = ScreenInfoEx.GetCurrentScreen(rdp.AutoSetting.FullScreenLastSessionScreenIndex);
                else
                    screenEx = ScreenInfoEx.GetCurrentScreen(App.Window);

                full.WindowStartupLocation = WindowStartupLocation.Manual;
                full.Top = screenEx.VirtualWorkingAreaCenter.Y - full.Height / 2;
                full.Left = screenEx.VirtualWorkingAreaCenter.X - full.Width / 2;
                full.SetProtocolHost(host);
                full.Loaded += (sender, args) => { host.GoFullScreen(); };

                _host2FullScreenWindows.Add(full.ProtocolHostBase.ConnectionId, full);
                host.ParentWindow = full;
                full.Show();
            }

            CloseEmptyTab();

            SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
            SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");

            return full;
        }

        /// <summary>
        /// move ProtocolHost to Tab, if host has a FullScreenWindow Parent, then remove it from old parent first.
        /// if assignTabToken != null, then move to assign tab.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="assignTabToken"></param>
        /// <returns></returns>
        private TabWindow GetOrCreateTabWindow(ProtocolHostBase host, string assignTabToken = null)
        {
            var parentWindow = host.ParentWindow;
            // remove from old parent
            if (parentWindow is FullScreenWindow fullScreenWindow)
            {
                // !importance: do not close old FullScreenWindow, or RDP will lose conn bar when restore from tab to fullscreen.
                SimpleLogHelper.Debug($@"Hide full({fullScreenWindow.GetHashCode()})");
                fullScreenWindow.Hide();
                if (string.IsNullOrEmpty(assignTabToken))
                    assignTabToken = fullScreenWindow.LastTabToken;
            }

            var tab = GetOrCreateTabWindow(host.ProtocolServer, assignTabToken);
            return tab;
        }

        /// <summary>
        /// get a tab for server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="assignTabToken">if assignTabToken != null, try return _tabWindows[assignTabToken]</param>
        /// <returns></returns>
        private TabWindow GetOrCreateTabWindow(ProtocolServerBase server, string assignTabToken = null)
        {
            TabWindow tab = null;

            // use old TabWindow
            if (!string.IsNullOrEmpty(assignTabToken)
                && _tabWindows.ContainsKey(assignTabToken))
                tab = _tabWindows[assignTabToken];
            else
                switch (SystemConfig.Instance.General.TabMode)
                {
                    case EnumTabMode.NewItemGoesToGroup:
                        // work in tab by group mode
                        if (_tabWindows.Any(x => x.Value.Vm.Tag == server.GroupName))
                            tab = _tabWindows.First(x => x.Value.Vm.Tag == server.GroupName).Value;
                        break;
                    case EnumTabMode.NewItemGoesToProtocol:
                        // work in tab by protocol mode
                        if (_tabWindows.Any(x => x.Value.Vm.Tag == server.ProtocolDisplayName))
                            tab = _tabWindows.First(x => x.Value.Vm.Tag == server.ProtocolDisplayName).Value;
                        break;
                    default:
                        // work in tab by latest tab mode
                        if (!string.IsNullOrEmpty(_lastTabToken) && _tabWindows.ContainsKey(_lastTabToken))
                            tab = _tabWindows[_lastTabToken];
                        break;
                }

            // create new TabWindow
            if (tab == null)
            {
                var token = DateTime.Now.Ticks.ToString();
                AddTab(new TabWindow(token));
                tab = _tabWindows[token];

                if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToGroup)
                    tab.Vm.Tag = server.GroupName;
                else if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToProtocol)
                    tab.Vm.Tag = server.ProtocolDisplayName;

                var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());

                tab.Top = screenEx.VirtualWorkingAreaCenter.Y - tab.Height / 2;
                tab.Left = screenEx.VirtualWorkingAreaCenter.X - tab.Width / 2;
                tab.WindowStartupLocation = WindowStartupLocation.Manual;
                tab.Show();
                _lastTabToken = token;
            }
            return tab;
        }

        public Window MoveProtocolHostToTab(string connectionId)
        {
            if (!_protocolHosts.ContainsKey(connectionId))
                throw new NullReferenceException($"_protocolHosts not contains {connectionId}");
            var host = _protocolHosts[connectionId];
            // get tab
            var tab = GetOrCreateTabWindow(host);
            // add host to tab
            if (tab.Vm.Items.All(x => x.Content != host))
            {
                tab.Vm.Items.Add(new TabItemViewModel()
                {
                    Content = host,
                    Header = host.ProtocolServer.DispName,
                });
                tab.Vm.SelectedItem = tab.Vm.Items.Last();
            }
            else
            {
                tab.Vm.SelectedItem = tab.Vm.Items.First(x => x.Content != host);
            }
            host.ParentWindow = tab;
            tab.Activate();
            SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
            SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            return tab;
        }

        /// <summary>
        /// terminate remote connection
        /// </summary>
        public void DelProtocolHost(string connectionId)
        {
            if (!string.IsNullOrEmpty(connectionId)
                && _protocolHosts.ContainsKey(connectionId))
            {
                var host = _protocolHosts[connectionId];
                SimpleLogHelper.Debug($@"DelProtocolHost host({host.GetHashCode()})");
                if (host.OnClosed != null)
                    host.OnClosed -= OnProtocolClose;
                if (host.IsConnected())
                    host.DisConn();

                // close full
                if (_host2FullScreenWindows.ContainsKey(connectionId))
                {
                    var full = _host2FullScreenWindows[connectionId];
                    SimpleLogHelper.Debug($@"Close full({full.GetHashCode()})");
                    full.Close();
                    _host2FullScreenWindows.Remove(connectionId);
                }

                // remove from tab
                var tabs = _tabWindows.Values.Where(x => x.Vm.Items.Any(y => y.Content.ConnectionId == connectionId));
                foreach (var tab in tabs.ToArray())
                {
                    tab.Vm.Items.Remove(tab.Vm.Items.First(x => x.Content.ConnectionId == connectionId));
                }
                _protocolHosts.Remove(connectionId);
                SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
            CloseEmptyTab();
        }

        /// <summary>
        /// del window & terminate remote connection
        /// </summary>
        public void DelTabWindow(string token)
        {
            // del protocol
            if (_tabWindows.ContainsKey(token))
            {
                var tab = _tabWindows[token];
                SimpleLogHelper.Debug($@"DelFromPuttyRegistryTable tab({tab.GetHashCode()})");
                foreach (var tabItemViewModel in tab.Vm.Items.ToArray())
                {
                    DelProtocolHost(tabItemViewModel.Content.ConnectionId);
                }
                SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
            CloseEmptyTab();
        }

        private void CloseEmptyTab()
        {
            // close un-handel protocol
            {
                var ps = _protocolHosts.Where(p =>
                    _tabWindows.Values.All(x => x?.Vm?.Items != null
                                                && x.Vm.Items.Count > 0
                                                && x.Vm.Items.All(y => y.Content.ConnectionId != p.Key))
                    && !_host2FullScreenWindows.ContainsKey(p.Key));
                if (ps.Any())
                {
                    DelProtocolHost(ps.First().Key);
                }
            }

            // close tab
            {
                var tabs = _tabWindows.Values.Where(x => x?.Vm?.Items == null
                                                         || x.Vm.Items.Count == 0).ToArray();
                foreach (var tab in tabs)
                {
                    SimpleLogHelper.Debug($@"Close tab({tab.GetHashCode()})");
                    _tabWindows.Remove(tab.Vm.Token);
                    tab.Close();
                    SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }
            }
        }
    }
}
