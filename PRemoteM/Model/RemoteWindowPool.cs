using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;
using PRM.View;
using PRM.View.TabWindow;
using PRM.ViewModel;

using Shawn.Utils;

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

        #endregion singleton

        public static void Init(PrmContext context)
        {
            lock (InstanceLock)
            {
                if (_uniqueInstance == null)
                {
                    _uniqueInstance = new RemoteWindowPool(context);
                }
            }
        }

        private readonly PrmContext _context;

        private RemoteWindowPool(PrmContext context)
        {
            _context = context;
            GlobalEventHelper.OnRequestServerConnect += ShowRemoteHost;
        }

        public void Release()
        {
            foreach (var tabWindow in _tabWindows.ToArray())
            {
                tabWindow.Value.Hide();
            }
            foreach (var kv in _host2FullScreenWindows.ToArray())
            {
                kv.Value.Hide();
            }
            foreach (var tabWindow in _tabWindows.ToArray())
            {
                DelTabWindow(tabWindow.Key);
            }

            foreach (var kv in _protocolHosts.ToArray())
            {
                DelProtocolHostInSyncContext(kv.Key);
            }
        }

        private string _lastTabToken = null;
        private readonly Dictionary<string, TabWindowBase> _tabWindows = new Dictionary<string, TabWindowBase>();
        private readonly Dictionary<string, ProtocolHostBase> _protocolHosts = new Dictionary<string, ProtocolHostBase>();
        private readonly Dictionary<string, FullScreenWindow> _host2FullScreenWindows = new Dictionary<string, FullScreenWindow>();

        private bool ActivateOrReConnIfServerSessionIsOpened(VmProtocolServer vmProtocolServer)
        {
            var serverId = vmProtocolServer.Server.Id;
            // if is OnlyOneInstance Protocol and it is connected now, activate it and return.
            if (vmProtocolServer.Server.IsOnlyOneInstance() && _protocolHosts.ContainsKey(serverId.ToString()))
            {
                if (_protocolHosts[serverId.ToString()].ParentWindow is TabWindowBase t)
                {
                    var s = t?.GetViewModel()?.Items?.FirstOrDefault(x => x.Content?.ProtocolServer?.Id == serverId);
                    if (t != null && s != null)
                        t.GetViewModel().SelectedItem = s;
                    t?.Activate();
                    if (s?.Content?.Status != ProtocolHostStatus.Connected)
                        s?.Content?.ReConn();
                }
                return true;
            }
            return false;
        }

        private void ConnectRdpWithFullScreenByMstsc(ProtocolServerRDP rdp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{rdp.DispName}_{rdp.Port}_{rdp.UserName}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
            File.WriteAllText(rdpFile, rdp.ToRdpConfig(_context).ToString());
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
            string admin = rdp.IsAdministrativePurposes ? " /admin " : "";
            p.StandardInput.WriteLine($"mstsc {admin} \"" + rdpFile + "\"");
            p.StandardInput.WriteLine("exit");

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
                    SimpleLogHelper.Error(e);
                }
            });
            t.Start();
        }

        private void ConnectRemoteApp(ProtocolServerRemoteApp remoteApp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{remoteApp.DispName}_{remoteApp.Port}_{remoteApp.UserName}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
            File.WriteAllText(rdpFile, remoteApp.ToRdpConfig(_context).ToString());
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
            p.StandardInput.WriteLine($"mstsc \"" + rdpFile + "\"");
            p.StandardInput.WriteLine("exit");

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
                    SimpleLogHelper.Error(e);
                }
            });
            t.Start();
        }

        private void ConnectWithFullScreen(VmProtocolServer vmProtocolServer)
        {
            // check if screens are in different scale factors
            int factor = (int)(new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor * 100);

            // for those people using 2+ monitors in different scale factors, we will try "mstsc.exe" instead of "PRemoteM".
            if (Screen.AllScreens.Length > 1
                && vmProtocolServer.Server is ProtocolServerRDP rdp
                && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens
                && Screen.AllScreens.Select(screen => (int)(new ScreenInfoEx(screen).ScaleFactor * 100))
                    .Any(factor2 => factor != factor2))
            {
                ConnectRdpWithFullScreenByMstsc(rdp);
                return;
            }

            // fullscreen normally
            var host = ProtocolHostFactory.Get(_context, vmProtocolServer.Server);
            Debug.Assert(!_protocolHosts.ContainsKey(host.ConnectionId));
            _protocolHosts.Add(host.ConnectionId, host);
            host.OnClosed += OnProtocolClose;
            host.OnFullScreen2Window += OnFullScreen2Window;
            var full = MoveProtocolHostToFullScreen(host.ConnectionId);
            host.ParentWindow = full;
            host.Conn();
            SimpleLogHelper.Debug($@"Start Conn: {vmProtocolServer.Server.DispName}({vmProtocolServer.GetHashCode()}) by host({host.GetHashCode()}) with full");
        }

        private void ConnectWithTab(VmProtocolServer vmProtocolServer, string assignTabToken)
        {
            var tab = GetOrCreateTabWindow(vmProtocolServer.Server, assignTabToken);
            var size = tab.GetTabContentSize();
            var host = ProtocolHostFactory.Get(_context, vmProtocolServer.Server, size.Width, size.Height);
            Debug.Assert(!_protocolHosts.ContainsKey(host.ConnectionId));
            host.OnClosed += OnProtocolClose;
            host.OnFullScreen2Window += OnFullScreen2Window;
            tab.AddItem(new TabItemViewModel()
            {
                Content = host,
                Header = vmProtocolServer.Server.DispName,
            });
            host.ParentWindow = tab;
            _protocolHosts.Add(host.ConnectionId, host);
            host.Conn();
            tab.Activate();
            SimpleLogHelper.Debug($@"Start Conn: {vmProtocolServer.Server.DispName}({vmProtocolServer.GetHashCode()}) by host({host.GetHashCode()}) with Tab({tab.GetHashCode()})");
        }

        public void ShowRemoteHost(long serverId, string assignTabToken)
        {
            if (serverId <= 0)
            {
                throw new Exception($"try to connect Server Id = {serverId}");
            }
            if (_context.AppData.VmItemList.All(x => x.Server.Id != serverId))
            {
                SimpleLogHelper.Warning($@"try to connect Server Id = {serverId} and {serverId} not in the list");
                _context.AppData.ServerListUpdate();
            }
            Debug.Assert(_context.AppData.VmItemList.Any(x => x.Server.Id == serverId));
            var vmProtocolServer = _context.AppData.VmItemList.First(x => x.Server.Id == serverId);

            // update the last conn time
            vmProtocolServer.Server.LastConnTime = DateTime.Now;
            _context.DbOperator.DbUpdateServer(vmProtocolServer.Server);

            // if is OnlyOneInstance Protocol and it is connected now, activate it and return.
            if (ActivateOrReConnIfServerSessionIsOpened(vmProtocolServer))
                return;

            // run script before connected
            vmProtocolServer.Server.RunScriptBeforConnect();

            if (vmProtocolServer.Server is ProtocolServerRemoteApp remoteApp)
            {
                ConnectRemoteApp(remoteApp);
                return;
            }

            // connect with host
            if (vmProtocolServer.Server.IsConnWithFullScreen())
                ConnectWithFullScreen(vmProtocolServer);
            else
                ConnectWithTab(vmProtocolServer, assignTabToken);

            SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
        }

        private void OnFullScreen2Window(string connectionId)
        {
            MoveProtocolHostToTab(connectionId);
        }

        private void OnProtocolClose(string connectionId)
        {
            DelProtocolHostInSyncContext(connectionId);
        }

        public void AddTab(TabWindowBase tab)
        {
            var token = tab.Token;
            Debug.Assert(!_tabWindows.ContainsKey(token));
            Debug.Assert(!string.IsNullOrEmpty(token));
            _tabWindows.Add(token, tab);
            tab.Activated += (sender, args) =>
                _lastTabToken = tab.Token;
        }

        private FullScreenWindow MoveToExistedFullScreenWindow(string connectionId, TabWindowBase fromTab)
        {
            Debug.Assert(_host2FullScreenWindows.ContainsKey(connectionId));
            Debug.Assert(_protocolHosts.ContainsKey(connectionId));
            var host = _protocolHosts[connectionId];

            // restore from tab to full
            var full = _host2FullScreenWindows[connectionId];
            full.LastTabToken = "";
            // full screen placement
            if (fromTab != null)
            {
                var screenEx = ScreenInfoEx.GetCurrentScreen(fromTab);
                full.Top = screenEx.VirtualWorkingAreaCenter.Y - full.Height / 2;
                full.Left = screenEx.VirtualWorkingAreaCenter.X - full.Width / 2;
                full.LastTabToken = _lastTabToken;
            }
            full.Show();
            full.SetProtocolHost(host);
            host.ParentWindow = full;
            host.GoFullScreen();
            return full;
        }

        private FullScreenWindow MoveToNewFullScreenWindow(string connectionId, TabWindowBase fromTab)
        {
            Debug.Assert(!_host2FullScreenWindows.ContainsKey(connectionId));
            Debug.Assert(_protocolHosts.ContainsKey(connectionId));
            var host = _protocolHosts[connectionId];

            // first time to full
            var full = new FullScreenWindow { LastTabToken = "" };

            // full screen placement
            ScreenInfoEx screenEx;
            if (fromTab != null)
            {
                screenEx = ScreenInfoEx.GetCurrentScreen(fromTab);
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
            return full;
        }

        /// <summary>
        /// if a session in in a tabwindow return it, or return null.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        private TabWindowBase GetTabParent(string connectionId)
        {
            var tabs = _tabWindows.Values
                .Where(x => x.GetViewModel().Items
                    .Any(y => y.Content.ConnectionId == connectionId)).ToArray();
            Debug.Assert(tabs.Length <= 1);

            if (tabs.Length > 0)
                return tabs.First();
            else
                return null;
        }

        public Window MoveProtocolHostToFullScreen(string connectionId)
        {
            if (!_protocolHosts.ContainsKey(connectionId))
                throw new NullReferenceException($"_protocolHosts not contains {connectionId}");

            var host = _protocolHosts[connectionId];

            // remove from old parent
            var tab = GetTabParent(connectionId);
            if (tab != null)
            {
                RemoveFromTabWindow(connectionId);
            }

            // move to full-screen-window
            FullScreenWindow full;
            if (_host2FullScreenWindows.ContainsKey(connectionId))
            {
                full = MoveToExistedFullScreenWindow(connectionId, tab);
            }
            else
            {
                full = MoveToNewFullScreenWindow(connectionId, tab);
            }
            CleanupTabs();

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
        private TabWindowBase GetOrCreateTabWindow(ProtocolHostBase host, string assignTabToken = null)
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

        private TabWindowBase GetExistedTabWindow(ProtocolServerBase server, string assignTabToken)
        {
            // get TabWindowBase by assignTabToken
            if (!string.IsNullOrEmpty(assignTabToken)
                && _tabWindows.ContainsKey(assignTabToken))
                return _tabWindows[assignTabToken];

            // get TabWindowBase by TabMode
            TabWindowBase tab = null;
            if (_tabWindows.Count <= 0) return null;
            switch (SystemConfig.Instance.General.TabMode)
            {
                case EnumTabMode.NewItemGoesToGroup:
                    // work in tab by group mode
                    if (_tabWindows.Any(x => x.Value.GetViewModel().Tag == server.GroupName))
                        tab = _tabWindows.First(x => x.Value.GetViewModel().Tag == server.GroupName).Value;
                    break;

                case EnumTabMode.NewItemGoesToProtocol:
                    // work in tab by protocol mode
                    if (_tabWindows.Any(x => x.Value.GetViewModel().Tag == server.ProtocolDisplayName))
                        tab = _tabWindows.First(x => x.Value.GetViewModel().Tag == server.ProtocolDisplayName).Value;
                    break;

                case EnumTabMode.NewItemGoesToLatestActivate:
                default:
                    // work in tab by latest tab mode
                    if (!string.IsNullOrEmpty(_lastTabToken) && _tabWindows.ContainsKey(_lastTabToken))
                        tab = _tabWindows[_lastTabToken];
                    break;
            }
            return tab;
        }

        private TabWindowBase CreateNewTabWindow(ProtocolServerBase server)
        {
            var token = DateTime.Now.Ticks.ToString();
            if (SystemConfig.Instance.Theme.TabUI == EnumTabUI.ChromeLike)
            {
                AddTab(new TabWindowChrome(token));
            }
            else
            {
                AddTab(new TabWindowClassical(token));
            }
            var tab = _tabWindows[token];

            // set tag
            if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToGroup)
                tab.GetViewModel().Tag = server.GroupName;
            else if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToProtocol)
                tab.GetViewModel().Tag = server.ProtocolDisplayName;

            // set location
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            tab.WindowStartupLocation = WindowStartupLocation.Manual;
            tab.Top = screenEx.VirtualWorkingAreaCenter.Y - tab.Height / 2;
            tab.Left = screenEx.VirtualWorkingAreaCenter.X - tab.Width / 2;
            tab.Show();
            _lastTabToken = tab.Token;

            return tab;
        }

        /// <summary>
        /// get a tab for server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="assignTabToken">if assignTabToken != null, try return _tabWindows[assignTabToken]</param>
        /// <returns></returns>
        private TabWindowBase GetOrCreateTabWindow(ProtocolServerBase server, string assignTabToken = null)
        {
            var tab = GetExistedTabWindow(server, assignTabToken);
            if (tab != null) return tab;

            // create new TabWindowBase
            tab = CreateNewTabWindow(server);

            return tab;
        }

        public Window MoveProtocolHostToTab(string connectionId)
        {
            Debug.Assert(_protocolHosts.ContainsKey(connectionId) == true);
            var host = _protocolHosts[connectionId];
            // get tab
            var tab = GetOrCreateTabWindow(host);
            // assign host to tab
            if (tab.GetViewModel().Items.All(x => x.Content != host))
            {
                // move
                tab.AddItem(new TabItemViewModel()
                {
                    Content = host,
                    Header = host.ProtocolServer.DispName,
                });
            }
            else
            {
                // just show
                tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.First(x => x.Content == host);
            }
            host.ParentWindow = tab;
            tab.Activate();
            SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
            SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            return tab;
        }

        private void CloseFullWindow(string connectionId)
        {
            if (!_host2FullScreenWindows.ContainsKey(connectionId))
                return;

            var full = _host2FullScreenWindows[connectionId];
            SimpleLogHelper.Debug($@"Close full({full.GetHashCode()})");
            full.Close();

            _host2FullScreenWindows.Remove(connectionId);
        }

        private void RemoveFromTabWindow(string connectionId)
        {
            var tab = GetTabParent(connectionId);
            if (tab == null)
                return;
            var item = tab.GetViewModel().Items.First(x => x.Content.ConnectionId == connectionId);
            tab?.GetViewModel().Items.Remove(item);
            tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.Count > 0 ? tab.GetViewModel().Items.First() : null;
            SimpleLogHelper.Debug($@"Remove connectionId = {connectionId} from tab({tab.GetHashCode()})");
            CleanupTabs();
        }

        private void DelProtocolHost(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)
                || !_protocolHosts.ContainsKey(connectionId))
                return;

            // close full
            if (_host2FullScreenWindows.ContainsKey(connectionId))
            {
                CloseFullWindow(connectionId);
            }

            // remove from tab
            RemoveFromTabWindow(connectionId);

            var host = _protocolHosts[connectionId];
            SimpleLogHelper.Debug($@"DelProtocolHost host({host.GetHashCode()})");
            if (host.OnClosed != null)
                host.OnClosed -= OnProtocolClose;
            _protocolHosts.Remove(connectionId);
            SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");

            // Dispose
            try
            {
                if (host.Status == ProtocolHostStatus.Connected)
                    host.Close();
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }

            if (host is IDisposable dp)
                dp.Dispose();

            host.ProtocolServer.RunScriptAfterDisconnected();

            CleanupTabs();
        }

        /// <summary>
        /// terminate remote connection
        /// </summary>
        public void DelProtocolHostInSyncContext(string connectionId)
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));
            SynchronizationContext.Current.Post(pl =>
            {
                DelProtocolHost(connectionId);
            }, null);
        }

        /// <summary>
        /// del window & terminate remote connection
        /// </summary>
        public void DelTabWindow(string token)
        {
            if (!_tabWindows.ContainsKey(token)) return;
            var tab = _tabWindows[token];
            var items = tab.GetViewModel().Items.ToArray();
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));
            SynchronizationContext.Current.Post(pl =>
            {
                // del protocol
                foreach (var tabItemViewModel in items)
                {
                    DelProtocolHostInSyncContext(tabItemViewModel.Content.ConnectionId);
                }
                SimpleLogHelper.Debug($@"DelTabWindow tab({tab.GetHashCode()})");
                SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                CleanupTabs();
            }, null);
        }

        private void CloseUnhandledProtocols()
        {
            var ps = _protocolHosts.Where(p =>
                _tabWindows.Values.All(x => x?.GetViewModel()?.Items != null
                                            && x.GetViewModel().Items.Count > 0
                                            && x.GetViewModel().Items.All(y => y.Content.ConnectionId != p.Key))
                && !_host2FullScreenWindows.ContainsKey(p.Key));
            if (ps.Any())
            {
                DelProtocolHostInSyncContext(ps.First().Key);
            }
        }

        private void CloseEmptyTabs()
        {
            var tabs = _tabWindows.Values.Where(x => x?.GetViewModel()?.Items == null
                                                     || x.GetViewModel().Items.Count == 0).ToArray();
            foreach (var tab in tabs)
            {
                SimpleLogHelper.Debug($@"Close tab({tab.GetHashCode()})");
                _tabWindows.Remove(tab.Token);
                tab.Close();
                SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }

        private void CleanupTabs()
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));
            SynchronizationContext.Current.Post(pl =>
            {
                CloseUnhandledProtocols();
                CloseEmptyTabs();
            }, null);
        }
    }
}