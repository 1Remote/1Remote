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
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.FileTransmit;
using PRM.Model.ProtocolRunner;
using PRM.Model.ProtocolRunner.Default;
using PRM.Utils;
using Shawn.Utils;
using PRM.View;
using PRM.View.Host;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using MessageBox = System.Windows.MessageBox;
using ProtocolHostStatus = PRM.View.Host.ProtocolHosts.ProtocolHostStatus;


namespace PRM.Model
{
    public class RemoteWindowPool
    {
        private readonly PrmContext _context;
        public RemoteWindowPool(PrmContext context)
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
                DelProtocolHost(kv.Key);
            }
        }

        private string _lastTabToken = "";
        private readonly Dictionary<string, TabWindowBase> _tabWindows = new Dictionary<string, TabWindowBase>();
        private readonly Dictionary<string, HostBase> _protocolHosts = new Dictionary<string, HostBase>();
        private readonly Dictionary<string, FullScreenWindowView> _host2FullScreenWindows = new Dictionary<string, FullScreenWindowView>();

        public int TabWindowCount => _tabWindows.Count;
        public Dictionary<string, HostBase> ProtocolHosts => _protocolHosts;

        private bool ActivateOrReConnIfServerSessionIsOpened(ProtocolBase server)
        {
            var serverId = server.Id;
            // if is OnlyOneInstance Protocol and it is connected now, activate it and return.
            if (server.IsOnlyOneInstance() && _protocolHosts.ContainsKey(serverId.ToString()))
            {
                if (_protocolHosts[serverId.ToString()].ParentWindow is TabWindowBase t)
                {
                    var s = t.GetViewModel().Items.FirstOrDefault(x => x.Content?.ProtocolServer?.Id == serverId);
                    if (s != null)
                        t.GetViewModel().SelectedItem = s;
                    t?.Activate();
                    if (s?.Content?.Status != ProtocolHostStatus.Connected)
                        s?.Content?.ReConn();
                }
                return true;
            }
            return false;
        }

        private void ConnectRdpByMstsc(RDP rdp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{rdp.DisplayName}_{rdp.Port}_{MD5Helper.GetMd5Hash16BitString(rdp.UserName)}";
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
            string admin = rdp.IsAdministrativePurposes == true ? " /admin " : "";
            p.StandardInput.WriteLine($"mstsc {admin} \"" + rdpFile + "\"");
            p.StandardInput.WriteLine("exit");

            // delete tmp rdp file, ETA 10s
            Task.Factory.StartNew(() =>
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
        }

        private void ConnectRemoteApp(RdpApp remoteApp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{remoteApp.DisplayName}_{remoteApp.Port}_{remoteApp.UserName}";
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

        private void ConnectWithFullScreen(ProtocolBase server, Runner runner)
        {
            // fullscreen normally
            var host = ProtocolRunnerHostHelper.GetHostForInternalRunner(_context, server, runner);
            if (host == null)
                return;
            Debug.Assert(!_protocolHosts.ContainsKey(host.ConnectionId));
            _protocolHosts.Add(host.ConnectionId, host);
            host.OnClosed += OnProtocolClose;
            host.OnFullScreen2Window += OnFullScreen2Window;
            var full = MoveProtocolHostToFullScreen(host.ConnectionId);
            host.ParentWindow = full;
            host.Conn();
            SimpleLogHelper.Debug($@"Start Conn: {server.DisplayName}({server.GetHashCode()}) by host({host.GetHashCode()}) with full");
        }

        private void ConnectWithTab(ProtocolBase protocol, Runner runner, string assignTabToken)
        {
            // open SFTP when SSH is connected.
            if (protocol is SSH { OpenSftpOnConnected: true } ssh)
            {
                var tmpRunner = ProtocolRunnerHostHelper.GetRunner(_context, SFTP.ProtocolName);
                var sftp = new SFTP
                {
                    ColorHex = ssh.ColorHex,
                    IconBase64 = ssh.IconBase64,
                    DisplayName = ssh.DisplayName + " (SFTP)",
                    Address = ssh.Address,
                    Port = ssh.Port,
                    UserName = ssh.UserName,
                    Password = ssh.Password,
                    PrivateKey = ssh.PrivateKey
                };
                ConnectWithTab(sftp, tmpRunner, assignTabToken);
            }

            var size = new Size(0, 0);
            TabWindowBase? tab = null;
            if (protocol is RDP)
            {
                tab = GetOrCreateTabWindow(assignTabToken);
                size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(protocol.ColorHex));
            }

            protocol.ConnectPreprocess(_context);
            HostBase? host = null;
            if (runner is ExternalRunner)
            {
                host = ProtocolRunnerHostHelper.GetHostOrRunDirectlyForExternalRunner(_context, protocol, runner);
            }
            else if (runner is InternalDefaultRunner)
            {
                host = ProtocolRunnerHostHelper.GetHostForInternalRunner(_context, protocol, runner, size.Width, size.Height);
            }
            if (host == null)
                return;

            tab ??= GetOrCreateTabWindow(assignTabToken);

            // get display area size for host
            Debug.Assert(!_protocolHosts.ContainsKey(host.ConnectionId));
            host.OnClosed += OnProtocolClose;
            host.OnFullScreen2Window += OnFullScreen2Window;
            tab.AddItem(new TabItemViewModel((HostBase)host, protocol.DisplayName));
            host.ParentWindow = tab;
            _protocolHosts.Add(host.ConnectionId, host);
            host.Conn();
            tab.Activate();
        }

        public void ShowRemoteHost(long serverId, string? assignTabToken, string? assignRunnerName)
        {
            #region START MULTIPLE SESSION
            // if serverId <= 0, then start multiple sessions
            if (serverId <= 0)
            {
                var list = _context.AppData.VmItemList.Where(x => x.IsSelected).ToArray();
                foreach (var item in list)
                {
                    ShowRemoteHost(item.Id, assignTabToken, assignRunnerName);
                }
                return;
            }
            #endregion

            CloseUnhandledProtocols();
            CloseEmptyTabs();

            Debug.Assert(_context.AppData.VmItemList.Any(x => x.Server.Id == serverId));
            _context.ConfigurationService.Engagement.ConnectCount++;
            _context.ConfigurationService.Save();
            // clear selected state
            _context.AppData.UnselectAllServers();

            var server = _context.AppData.VmItemList.FirstOrDefault(x => x.Server.Id == serverId)?.Server;
            if (server == null)
            {
                SimpleLogHelper.Error($@"try to connect Server Id = {serverId} while {serverId} is not in the db");
                return;
            }

            // update the last conn time
            server.LastConnTime = DateTime.Now;
            Debug.Assert(_context.DataService != null);
            _context.DataService.Database_UpdateServer(server);

            // if is OnlyOneInstance protocol and it is connected now, activate it and return.
            if (ActivateOrReConnIfServerSessionIsOpened(server))
                return;

            // run script before connected
            server.RunScriptBeforeConnect();

            if (server is RdpApp remoteApp)
            {
                ConnectRemoteApp(remoteApp);
                return;
            }


            var runner = ProtocolRunnerHostHelper.GetRunner(_context, server.Protocol, assignRunnerName)!;
            if (server is RDP rdp)
            {
                // check if screens are in different scale factors
                int factor = (int)(new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor * 100);
                // for those people using 2+ monitors in different scale factors, we will try "mstsc.exe" instead of "PRemoteM".
                if (rdp.MstscModeEnabled == true
                    || (server.ThisTimeConnWithFullScreen()
                        && Screen.AllScreens.Length > 1
                        && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens
                        && Screen.AllScreens.Select(screen => (int)(new ScreenInfoEx(screen).ScaleFactor * 100)).Any(factor2 => factor != factor2))
                )
                {
                    ConnectRdpByMstsc(rdp);
                    return;
                }

                // rdp full screen
                if (server.ThisTimeConnWithFullScreen())
                {
                    ConnectWithFullScreen(server, runner);
                    return;
                }
            }
            ConnectWithTab(server, runner, assignTabToken ?? "");
            SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
        }

        private void OnFullScreen2Window(string connectionId)
        {
            MoveProtocolHostToTab(connectionId);
        }

        private void OnProtocolClose(string connectionId)
        {
            DelProtocolHost(connectionId);
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

        private FullScreenWindowView MoveToExistedFullScreenWindow(string connectionId, TabWindowBase? fromTab)
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

        private FullScreenWindowView MoveToNewFullScreenWindow(string connectionId, TabWindowBase? fromTab)
        {
            Debug.Assert(!_host2FullScreenWindows.ContainsKey(connectionId));
            Debug.Assert(_protocolHosts.ContainsKey(connectionId));
            var host = _protocolHosts[connectionId];

            // first time to full
            var full = new FullScreenWindowView
            {
                LastTabToken = fromTab?.Token ?? "",
                WindowStartupLocation = WindowStartupLocation.Manual,
            };

            // full screen placement
            ScreenInfoEx? screenEx;
            if (fromTab != null)
                screenEx = ScreenInfoEx.GetCurrentScreen(fromTab);
            else if (host.ProtocolServer is RDP rdp
                     && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen
                     && rdp.AutoSetting.FullScreenLastSessionScreenIndex >= 0
                     && rdp.AutoSetting.FullScreenLastSessionScreenIndex < Screen.AllScreens.Length)
                screenEx = ScreenInfoEx.GetCurrentScreen(rdp.AutoSetting.FullScreenLastSessionScreenIndex);
            else
                screenEx = ScreenInfoEx.GetCurrentScreen(IoC.Get<MainWindowView>());

            if (screenEx != null)
            {
                full.Top = screenEx.VirtualWorkingAreaCenter.Y - full.Height / 2;
                full.Left = screenEx.VirtualWorkingAreaCenter.X - full.Width / 2;
            }

            full.SetProtocolHost(host);
            //full.Loaded += (sender, args) => { host.GoFullScreen(); };
            _host2FullScreenWindows.Add(host.ConnectionId, full);
            host.ParentWindow = full;
            full.Show();
            return full;
        }

        /// <summary>
        /// if a session in in a tabwindow return it, or return null.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        private TabWindowBase? GetTabParent(string connectionId)
        {
            var tabs = _tabWindows.Values.Where(x => x.GetViewModel()?.Items != null && x.GetViewModel().Items.Any(y => y.Content.ConnectionId == connectionId)).ToArray();
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
                RemoveFromTabWindow(connectionId);

            // move to full-screen-window
            var full = _host2FullScreenWindows.ContainsKey(connectionId) ? MoveToExistedFullScreenWindow(connectionId, tab) : MoveToNewFullScreenWindow(connectionId, tab);

            CleanupProtocolsAndTabs();

            SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
            SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");

            return full;
        }

        /// <summary>
        /// move ProtocolHost to Tab, if host has a FullScreenWindowView Parent, then remove it from old parent first.
        /// if assignTabToken != null, then move to assign tab.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="assignTabToken"></param>
        /// <returns></returns>
        private TabWindowBase GetOrCreateTabWindow(HostBase host, string assignTabToken = "")
        {
            var parentWindow = host.ParentWindow;
            // remove from old parent
            if (parentWindow is FullScreenWindowView fullScreenWindow)
            {
                // !importance: do not close old FullScreenWindowView, or RDP will lose conn bar when restore from tab to fullscreen.
                SimpleLogHelper.Debug($@"Hide full({fullScreenWindow.GetHashCode()})");
                fullScreenWindow.Hide();
                if (string.IsNullOrEmpty(assignTabToken))
                    assignTabToken = fullScreenWindow.LastTabToken;
            }

            var tab = GetOrCreateTabWindow(assignTabToken);
            return tab;
        }

        private TabWindowBase? FindTabWindow(string assignTabToken)
        {
            // get TabWindowBase by assignTabToken
            if (!string.IsNullOrEmpty(assignTabToken)
                && _tabWindows.ContainsKey(assignTabToken))
                return _tabWindows[assignTabToken];
            return null;
        }

        private TabWindowBase? FindLastTabWindow()
        {
            if (_tabWindows.Count <= 0) return null;
            if (!string.IsNullOrEmpty(_lastTabToken) && _tabWindows.ContainsKey(_lastTabToken))
                return _tabWindows[_lastTabToken];
            return _tabWindows.LastOrDefault().Value;
        }

        private TabWindowBase CreateNewTabWindow(string assignTabToken = "")
        {
            var token = DateTime.Now.Ticks.ToString();
            if (string.IsNullOrEmpty(assignTabToken) == false)
                token = assignTabToken;
            AddTab(new TabWindowView(token, _context.LocalityService));
            var tab = _tabWindows[token];

            // set location
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            tab.WindowStartupLocation = WindowStartupLocation.Manual;
            if (tab.Width > screenEx.VirtualWorkingArea.Width
                || tab.Height > screenEx.VirtualWorkingArea.Height)
            {
                tab.Width = screenEx.VirtualWorkingArea.Width;
                tab.Height = screenEx.VirtualWorkingArea.Height;
            }
            tab.Top = screenEx.VirtualWorkingAreaCenter.Y - tab.Height / 2;
            tab.Left = screenEx.VirtualWorkingAreaCenter.X - tab.Width / 2;
            tab.Show();
            _lastTabToken = tab.Token;

            return tab;
        }

        /// <summary>
        /// get a tab for server,
        /// if assignTabToken == null, create a new tab
        /// if assignTabToken != null, find _tabWindows[assignTabToken], if _tabWindows[assignTabToken] is null, then create a new tab
        /// </summary>
        /// <param name="assignTabToken"></param>
        /// <returns></returns>
        private TabWindowBase GetOrCreateTabWindow(string assignTabToken = "")
        {
            var tab = FindTabWindow(assignTabToken);
            if (tab != null) return tab;

            if (string.IsNullOrEmpty(assignTabToken))
            {
                tab = FindLastTabWindow();
                if (tab != null) return tab;
            }

            // create new TabWindowBase
            tab = CreateNewTabWindow(assignTabToken);

            return tab;
        }

        public void MoveProtocolHostToTab(string connectionId)
        {
            Debug.Assert(_protocolHosts.ContainsKey(connectionId) == true);
            var host = _protocolHosts[connectionId];
            if (host == null)
                return;
            SimpleLogHelper.Debug($@"MoveProtocolHostToTab: Moving host({host.GetHashCode()}) to any tab");
            // get tab
            var tab = GetOrCreateTabWindow(host);
            // assign host to tab
            if (tab.GetViewModel().Items.All(x => x.Content != host))
            {
                // move
                tab.AddItem(new TabItemViewModel((HostBase)host, host.ProtocolServer.DisplayName));
            }
            else
            {
                // just show
                tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.First(x => x.Content == host);
            }
            host.ParentWindow = tab;
            tab.Activate();
            SimpleLogHelper.Debug($@"MoveProtocolHostToTab: Moved host({host.GetHashCode()}) to tab({tab.GetHashCode()})", $@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
        }

        private void CloseFullWindow(string connectionId)
        {
            if (!_host2FullScreenWindows.ContainsKey(connectionId))
                return;

            SimpleLogHelper.Debug($@"CloseFullWindow: closing(id = {connectionId})");
            var full = _host2FullScreenWindows[connectionId];
            full.Close();
            _host2FullScreenWindows.Remove(connectionId);
            SimpleLogHelper.Debug($@"CloseFullWindow: closed(id = {connectionId}, hash = {full.GetHashCode()})");
        }

        private void RemoveFromTabWindow(string connectionId)
        {
            var tab = GetTabParent(connectionId);
            if (tab == null)
                return;
            var item = tab.GetViewModel().Items.First(x => x.Content.ConnectionId == connectionId);
            tab.GetViewModel().Items.Remove(item);
            tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.Count > 0 ? tab.GetViewModel().Items.First() : null;
            SimpleLogHelper.Debug($@"Remove connectionId = {connectionId} from tab({tab.GetHashCode()})");
            CleanupProtocolsAndTabs();
        }

        public void DelProtocolHost(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)
                || !_protocolHosts.ContainsKey(connectionId))
                return;

            SimpleLogHelper.Debug($@"DelProtocolHost: enter to delete host(id = {connectionId})");

            // close full
            CloseFullWindow(connectionId);

            // remove from tab
            RemoveFromTabWindow(connectionId);

            HostBase? host = null;

            if (_protocolHosts.ContainsKey(connectionId))
                lock (this)
                {
                    if (_protocolHosts.ContainsKey(connectionId))
                        try
                        {
                            host = _protocolHosts[connectionId];
                            SimpleLogHelper.Debug($@"DelProtocolHost: get host({host.GetHashCode()})");
                            if (host.OnClosed != null)
                                host.OnClosed -= OnProtocolClose;
                            _protocolHosts.Remove(connectionId);
                            SimpleLogHelper.Debug($@"DelProtocolHost: removed and now, ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                        }
                        catch (Exception e)
                        {
                            host = null;
                            SimpleLogHelper.Error("DelProtocolHost: error when get host by connectionId and remove it from dictionary `ProtocolHosts`", e);
                        }
                }

            // Dispose
            try
            {
                if (host is IDisposable d)
                {
                    d.Dispose();
                }
                else
                {
                    host?.Close();
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }

            host?.ProtocolServer?.RunScriptAfterDisconnected();

            CleanupProtocolsAndTabs();
        }


        /// <summary>
        /// del window & terminate remote connection
        /// </summary>
        public void DelTabWindow(string token)
        {
            SimpleLogHelper.Debug($@"DelTabWindow: try to delete token = {token}");
            lock (this)
            {
                if (!_tabWindows.ContainsKey(token)) return;
                var tab = _tabWindows[token];
                var items = tab.GetViewModel().Items.ToArray();
                // del protocol
                foreach (var tabItemViewModel in items)
                {
                    DelProtocolHost(tabItemViewModel.Content.ConnectionId);
                }
                SimpleLogHelper.Debug($@"DelTabWindow: deleted tab(token = {token}, hash = {tab.GetHashCode()})", $@"Now ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                CleanupProtocolsAndTabs();
            }
        }

        private void CloseUnhandledProtocols()
        {
            lock (this)
            {
                var ps = _protocolHosts.Where(p => _tabWindows.Values.All(x => x.GetViewModel().Items.All(y => y.Content.ConnectionId != p.Key))
                                                   && !_host2FullScreenWindows.ContainsKey(p.Key));
                var enumerable = ps as KeyValuePair<string, HostBase>[] ?? ps.ToArray();
                if (enumerable.Any())
                {
                    DelProtocolHost(enumerable.First().Key);
                }
            }
        }

        private void CloseEmptyTabs()
        {
            lock (this)
            {
                var tabs = _tabWindows.Values.Where(x => x.GetViewModel().Items.Count == 0).ToArray();
                foreach (var tab in tabs)
                {
                    SimpleLogHelper.Debug($@"CloseEmptyTabs: Closing tab({tab.GetHashCode()})");
                    if (string.IsNullOrEmpty(tab.Token) == false)
                        _tabWindows.Remove(tab.Token);
                    tab.Close();
                    SimpleLogHelper.Debug($@"CloseEmptyTabs: Closed tab({tab.GetHashCode()})ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }
            }
        }

        private void CleanupProtocolsAndTabs()
        {
            CloseUnhandledProtocols();
            CloseEmptyTabs();
        }
    }
}