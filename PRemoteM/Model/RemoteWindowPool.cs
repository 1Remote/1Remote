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
using PRM.Utils;
using Shawn.Utils;
using PRM.View;
using PRM.View.Host;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils.Wpf;
using MessageBox = System.Windows.MessageBox;
using ProtocolHostStatus = PRM.View.Host.ProtocolHosts.ProtocolHostStatus;


namespace PRM.Model
{
    public class RemoteWindowPool
    {
        #region singleton

        private static RemoteWindowPool _uniqueInstance = null;
        private static readonly object InstanceLock = new object();

        public static RemoteWindowPool Instance => _uniqueInstance;

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

        private bool _isReleased = false;
        public void Release()
        {
            if (_isReleased)
                return;
            _isReleased = true;

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
        private readonly Dictionary<string, HostBase> _protocolHosts = new Dictionary<string, HostBase>();
        private readonly Dictionary<string, FullScreenWindow> _host2FullScreenWindows = new Dictionary<string, FullScreenWindow>();

        public int TabWindowCount => _tabWindows.Count;

        private bool ActivateOrReConnIfServerSessionIsOpened(ProtocolBaseViewModel protocolServerViewModel)
        {
            var serverId = protocolServerViewModel.Server.Id;
            // if is OnlyOneInstance Protocol and it is connected now, activate it and return.
            if (protocolServerViewModel.Server.IsOnlyOneInstance() && _protocolHosts.ContainsKey(serverId.ToString()))
            {
                if (_protocolHosts[serverId.ToString()].ParentWindow is TabWindowBase t)
                {
                    var s = t?.GetViewModel()?.Items?.FirstOrDefault(x => x.Content?.ProtocolServer?.Id == serverId);
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

        private void ConnectWithFullScreen(ProtocolBaseViewModel protocolServerViewModel, Runner runner)
        {
            // fullscreen normally
            var host = ProtocolRunnerHostHelper.GetHostForInternalRunner(_context, protocolServerViewModel.Server, runner);
            if (host == null)
                return;
            Debug.Assert(!_protocolHosts.ContainsKey(host.ConnectionId));
            _protocolHosts.Add(host.ConnectionId, host);
            host.OnClosed += OnProtocolClose;
            host.OnFullScreen2Window += OnFullScreen2Window;
            var full = MoveProtocolHostToFullScreen(host.ConnectionId);
            host.ParentWindow = full;
            host.Conn();
            SimpleLogHelper.Debug($@"Start Conn: {protocolServerViewModel.Server.DisplayName}({protocolServerViewModel.GetHashCode()}) by host({host.GetHashCode()}) with full");
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
            TabWindowBase tab = null;
            if (protocol is RDP)
            {
                tab = GetOrCreateTabWindow(assignTabToken);
                size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(protocol.ColorHex));
            }

            protocol.ConnectPreprocess(_context);
            HostBase host;
            if (runner is ExternalRunner)
            {
                host = ProtocolRunnerHostHelper.GetHostOrRunDirectlyForExternalRunner(_context, protocol, runner);
            }
            else
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
            tab.AddItem(new TabItemViewModel()
            {
                Content = host,
                Header = protocol.DisplayName,
            });
            host.ParentWindow = tab;
            _protocolHosts.Add(host.ConnectionId, host);
            host.Conn();
            tab.Activate();
        }

        public void ShowRemoteHost(long serverId, string assignTabToken, string assignRunnerName)
        {
            #region START MULTIPLE SESSION
            // if serverId <= 0, then start multiple sessions
            if (serverId <= 0)
            {
                var list = _context.AppData.VmItemList.Where(x => x.IsSelected).ToArray();
                foreach (var server in list)
                {
                    ShowRemoteHost(server.Id, assignTabToken, assignRunnerName);
                }
                return;
            }
            #endregion

            Debug.Assert(_context.AppData.VmItemList.Any(x => x.Server.Id == serverId));

            // clear selected state
            _context.AppData.UnselectAllServers();

            var vmProtocolServer = _context.AppData.VmItemList.FirstOrDefault(x => x.Server.Id == serverId);
            if (vmProtocolServer == null)
            {
                SimpleLogHelper.Error($@"try to connect Server Id = {serverId} while {serverId} is not in the db");
                return;
            }

            // update the last conn time
            vmProtocolServer.Server.LastConnTime = DateTime.Now;
            _context.DataService.Database_UpdateServer(vmProtocolServer.Server);

            // if is OnlyOneInstance protocol and it is connected now, activate it and return.
            if (ActivateOrReConnIfServerSessionIsOpened(vmProtocolServer))
                return;

            // run script before connected
            vmProtocolServer.Server.RunScriptBeforeConnect();

            if (vmProtocolServer.Server is RdpApp remoteApp)
            {
                ConnectRemoteApp(remoteApp);
                return;
            }




            var runner = ProtocolRunnerHostHelper.GetRunner(_context, vmProtocolServer.Server.Protocol, assignRunnerName);


            if (vmProtocolServer.Server is RDP rdp)
            {
                // check if screens are in different scale factors
                int factor = (int)(new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor * 100);
                // for those people using 2+ monitors in different scale factors, we will try "mstsc.exe" instead of "PRemoteM".
                if (rdp.MstscModeEnabled == true
                    || (vmProtocolServer.Server.ThisTimeConnWithFullScreen()
                        && Screen.AllScreens.Length > 1
                        && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens
                        && Screen.AllScreens.Select(screen => (int)(new ScreenInfoEx(screen).ScaleFactor * 100)).Any(factor2 => factor != factor2))
                )
                {
                    ConnectRdpByMstsc(rdp);
                    return;
                }

                // rdp full screen
                if (vmProtocolServer.Server.ThisTimeConnWithFullScreen())
                {
                    ConnectWithFullScreen(vmProtocolServer, runner);
                    return;
                }
            }

            ConnectWithTab(vmProtocolServer.Server, runner, assignTabToken);

            CloseEmptyTabs();
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
            var full = new FullScreenWindow
            {
                LastTabToken = fromTab?.Token ?? "",
                WindowStartupLocation = WindowStartupLocation.Manual,
            };

            // full screen placement
            ScreenInfoEx screenEx;
            if (fromTab != null)
                screenEx = ScreenInfoEx.GetCurrentScreen(fromTab);
            else if (host.ProtocolServer is RDP rdp
                     && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen
                     && rdp.AutoSetting.FullScreenLastSessionScreenIndex >= 0
                     && rdp.AutoSetting.FullScreenLastSessionScreenIndex < Screen.AllScreens.Length)
                screenEx = ScreenInfoEx.GetCurrentScreen(rdp.AutoSetting.FullScreenLastSessionScreenIndex);
            else
                screenEx = ScreenInfoEx.GetCurrentScreen(App.MainUi);

            full.Top = screenEx.VirtualWorkingAreaCenter.Y - full.Height / 2;
            full.Left = screenEx.VirtualWorkingAreaCenter.X - full.Width / 2;
            full.SetProtocolHost(host);
            full.Loaded += (sender, args) => { host.GoFullScreen(); };

            _host2FullScreenWindows.Add(full.HostBase.ConnectionId, full);
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
            {
                RemoveFromTabWindow(connectionId);
            }

            // move to full-screen-window
            var full = _host2FullScreenWindows.ContainsKey(connectionId) ?
                MoveToExistedFullScreenWindow(connectionId, tab)
                : MoveToNewFullScreenWindow(connectionId, tab);

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
        private TabWindowBase GetOrCreateTabWindow(HostBase host, string assignTabToken = null)
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

            var tab = GetOrCreateTabWindow(assignTabToken);
            return tab;
        }

        private TabWindowBase FindTabWindow(string assignTabToken)
        {
            // get TabWindowBase by assignTabToken
            if (!string.IsNullOrEmpty(assignTabToken)
                && _tabWindows.ContainsKey(assignTabToken))
                return _tabWindows[assignTabToken];
            return null;
        }

        private TabWindowBase FindLastTabWindow()
        {
            if (_tabWindows.Count <= 0) return null;
            if (!string.IsNullOrEmpty(_lastTabToken) && _tabWindows.ContainsKey(_lastTabToken))
                return _tabWindows[_lastTabToken];
            return _tabWindows.LastOrDefault().Value;
        }

        private TabWindowBase CreateNewTabWindow(string assignTabToken = null)
        {
            var token = DateTime.Now.Ticks.ToString();
            if (string.IsNullOrEmpty(assignTabToken) == false)
                token = assignTabToken;
            AddTab(new TabWindowChrome(token, _context.LocalityService));
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
        private TabWindowBase GetOrCreateTabWindow(string assignTabToken = null)
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
                tab.AddItem(new TabItemViewModel()
                {
                    Content = host,
                    Header = host.ProtocolServer.DisplayName,
                });
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

            SimpleLogHelper.Debug($@"DelProtocolHost: enter to delete host(id = {connectionId})");

            // close full
            CloseFullWindow(connectionId);

            // remove from tab
            RemoveFromTabWindow(connectionId);

            HostBase host = null;

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

            CleanupTabs();
        }

        /// <summary>
        /// terminate remote connection
        /// </summary>
        public void DelProtocolHostInSyncContext(string connectionId, bool needConfirm = false)
        {
            if (_protocolHosts.ContainsKey(connectionId) == false)
            {
                return;
            }

            if (_context.ConfigurationService.General.ConfirmBeforeClosingSession == true
                && needConfirm == true
                && MessageBox.Show(_context.LanguageService.Translate("Are you sure you want to close the connection?"), _context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }


            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));
            SynchronizationContext.Current.Post(pl => { DelProtocolHost(connectionId); }, null);
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
                var items = tab.GetViewModel()?.Items?.ToArray() ?? new TabItemViewModel[0];
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));
                SynchronizationContext.Current.Post(pl =>
                {
                    // del protocol
                    foreach (var tabItemViewModel in items)
                    {
                        DelProtocolHostInSyncContext(tabItemViewModel.Content.ConnectionId);
                    }

                    SimpleLogHelper.Debug($@"DelTabWindow: deleted tab(token = {token}, hash = {tab.GetHashCode()})", $@"Now ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                    CleanupTabs();
                }, null);
            }
        }

        private void CloseUnhandledProtocols()
        {
            lock (this)
            {
                var ps = _protocolHosts.Where(p => _tabWindows.Values.All(x => x?.GetViewModel()?.Items != null
                                                                               && x.GetViewModel().Items.Count > 0
                                                                               && x.GetViewModel().Items.All(y => y.Content.ConnectionId != p.Key))
                                                   && !_host2FullScreenWindows.ContainsKey(p.Key));
                var enumerable = ps as KeyValuePair<string, HostBase>[] ?? ps.ToArray();
                if (enumerable.Any())
                {
                    DelProtocolHostInSyncContext(enumerable.First().Key);
                }
            }
        }

        private void CloseEmptyTabs()
        {
            lock (this)
            {
                var tabs = _tabWindows.Values.Where(x => x?.GetViewModel()?.Items == null
                                                         || x.GetViewModel().Items.Count == 0
                                                         || x.GetViewModel().Items.All(x => x.Content == null)).ToArray();
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