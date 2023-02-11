using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MSTSCLib;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using ProtocolHostStatus = _1RM.View.Host.ProtocolHosts.ProtocolHostStatus;
using Screen = System.Windows.Forms.Screen;
using _1RM.Service.DataSource;
using _1RM.Model.DAO.Dapper;
using _1RM.Service.DataSource.Model;

namespace _1RM.Service
{
    public class SessionControlService
    {
        private readonly DataSourceService _sourceService;
        private readonly ConfigurationService _configurationService;
        private readonly GlobalData _appData;

        public SessionControlService(DataSourceService sourceService, ConfigurationService configurationService, GlobalData appData)
        {
            _sourceService = sourceService;
            _configurationService = configurationService;
            _appData = appData;
            GlobalEventHelper.OnRequestServerConnect += this.ShowRemoteHostByObject;
            GlobalEventHelper.OnRequestQuickConnect += this.ShowRemoteHostByObject;
        }

        public void Release()
        {
            foreach (var tabWindow in _token2TabWindows.ToArray())
            {
                tabWindow.Value.Hide();
            }
            foreach (var kv in _connectionId2FullScreenWindows.ToArray())
            {
                kv.Value.Hide();
            }
            this.CloseProtocolHostAsync(_connectionId2Hosts.Keys.ToArray());
        }

        private string _lastTabToken = "";

        private readonly object _dictLock = new object();
        private readonly ConcurrentDictionary<string, TabWindowBase> _token2TabWindows = new ConcurrentDictionary<string, TabWindowBase>();
        private readonly ConcurrentDictionary<string, HostBase> _connectionId2Hosts = new ConcurrentDictionary<string, HostBase>();
        private readonly ConcurrentDictionary<string, FullScreenWindowView> _connectionId2FullScreenWindows = new ConcurrentDictionary<string, FullScreenWindowView>();
        private readonly ConcurrentQueue<HostBase> _hostToBeDispose = new ConcurrentQueue<HostBase>();
        private readonly ConcurrentQueue<Window> _windowToBeDispose = new ConcurrentQueue<Window>();

        public int TabWindowCount => _token2TabWindows.Count;

        public TabWindowBase? GetTabByConnectionId(string connectionId)
        {
            return _token2TabWindows.Values.FirstOrDefault(x => x.GetViewModel().Items.Any(y => y.Content.ConnectionId == connectionId));
        }

        public ConcurrentDictionary<string, HostBase> ConnectionId2Hosts => _connectionId2Hosts;

        private bool ActivateOrReConnIfServerSessionIsOpened(in ProtocolBase server)
        {
            var serverId = server.Id;
            // if is OnlyOneInstance Protocol and it is connected now, activate it and return.
            if (server.IsOnlyOneInstance() && _connectionId2Hosts.ContainsKey(serverId))
            {
                SimpleLogHelper.Debug($"_connectionId2Hosts ContainsKey {serverId}");
                if (_connectionId2Hosts[serverId].ParentWindow is TabWindowBase t)
                {
                    var s = t.GetViewModel().Items.FirstOrDefault(x => x.Content?.ProtocolServer?.Id == serverId);
                    if (s != null)
                        t.GetViewModel().SelectedItem = s;

                    if (t.IsClosed)
                    {
                        MarkProtocolHostToClose(new string[] { serverId.ToString() });
                        CleanupProtocolsAndWindows();
                        return false;
                    }

                    try
                    {
                        t.Show();
                        t.Activate();
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e);
                        MarkProtocolHostToClose(new string[] { serverId.ToString() });
                        CleanupProtocolsAndWindows();
                    }
                }
                if (_connectionId2Hosts[serverId].ParentWindow != null)
                {
                    if (_connectionId2Hosts[serverId].Status != ProtocolHostStatus.Connected)
                        _connectionId2Hosts[serverId].ReConn();
                }
                return true;
            }
            return false;
        }

        private void ConnectRdpByMstsc(in RDP rdp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{rdp.DisplayName}_{rdp.Port}_{MD5Helper.GetMd5Hash16BitString(rdp.UserName)}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
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
        }

        private void ConnectRemoteApp(in RdpApp remoteApp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{remoteApp.DisplayName}_{remoteApp.Port}_{remoteApp.UserName}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
            {
                File.WriteAllText(rdpFile, remoteApp.ToRdpConfig().ToString());
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
        }

        private void ConnectWithFullScreen(in ProtocolBase server, in Runner runner)
        {
            CleanupProtocolsAndWindows();
            // fullscreen normally
            var host = ProtocolRunnerHostHelper.GetHostForInternalRunner(server, runner);
            if (host == null)
                return;
            Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
            _connectionId2Hosts.TryAdd(host.ConnectionId, host);
            host.OnClosed += OnProtocolClose;
            host.OnFullScreen2Window += this.MoveProtocolHostToTab;
            this.MoveProtocolHostToFullScreen(host.ConnectionId);
            host.Conn();
            SimpleLogHelper.Debug($@"Start Conn: {server.DisplayName}({server.GetHashCode()}) by host({host.GetHashCode()}) with full");
        }

        private void ConnectWithTab(in ProtocolBase serverClone, in Runner runner, string assignTabToken)
        {
            lock (_dictLock)
            {
                CleanupProtocolsAndWindows();
                // open SFTP when SSH is connected.
                if (serverClone is SSH { OpenSftpOnConnected: true } ssh)
                {
                    var tmpRunner = ProtocolRunnerHostHelper.GetRunner(IoC.Get<ProtocolConfigurationService>(), serverClone, SFTP.ProtocolName);
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
                    Debug.Assert(tmpRunner != null);
                    this.ConnectWithTab(sftp, tmpRunner, assignTabToken);
                }

                TabWindowBase? tab = null;
                HostBase? host = null;
                switch (runner)
                {
                    case InternalDefaultRunner:
                        {
                            if (serverClone is RDP)
                            {
                                tab = this.GetOrCreateTabWindow(assignTabToken);
                                if (tab == null)
                                    return;
                                var size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(serverClone.ColorHex) == true);
                                host = ProtocolRunnerHostHelper.GetRdpInternalHost(serverClone, runner, size.Width, size.Height);
                            }
                            else
                            {
                                host = ProtocolRunnerHostHelper.GetHostForInternalRunner(serverClone, runner);
                            }

                            break;
                        }
                    case ExternalRunner:
                        {
                            host = ProtocolRunnerHostHelper.GetHostOrRunDirectlyForExternalRunner(_sourceService, serverClone, runner);
                            // if host is null, could be run without integrate
                            break;
                        }
                    default:
                        throw new NotImplementedException($"unknown runner: {runner.GetType()}");
                }

                if (host != null)
                {
                    string displayName = serverClone.DisplayName;
                    Execute.OnUIThreadSync(() =>
                    {
                        tab ??= this.GetOrCreateTabWindow(assignTabToken);
                        if (tab == null)
                            return;
                        tab.Show();

                        // get display area size for host
                        Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
                        host.OnClosed += OnProtocolClose;
                        host.OnFullScreen2Window += this.MoveProtocolHostToTab;
                        tab.AddItem(new TabItemViewModel(host, displayName));
                        _connectionId2Hosts.TryAdd(host.ConnectionId, host);
                        host.Conn();
                        if (tab.WindowState == WindowState.Minimized)
                        {
                            tab.WindowState = WindowState.Normal;
                        }
                        tab.Activate();
                    });
                }
            }
        }

        private void ShowRemoteHostByObject(in ProtocolBase? serverOrg, in string fromView, in string assignTabToken = "", in string assignRunnerName = "", in string assignCredentialName = "")
        {
            #region START MULTIPLE SESSION
            // if server == null, then start multiple sessions
            if (serverOrg == null)
            {
                var list = _appData.VmItemList.Where(x => x.IsSelected).ToArray();
                foreach (var item in list)
                {
                    this.ShowRemoteHostByObject(item.Server, assignTabToken, assignRunnerName, fromView);
                }
                MsAppCenterHelper.TraceSessionOpen("multiple sessions", fromView);
                return;
            }
            #endregion



            // if is OnlyOneInstance server and it is connected now, activate it and return.
            if (this.ActivateOrReConnIfServerSessionIsOpened(serverOrg))
                return;


            if (string.IsNullOrEmpty(fromView) == false)
                MsAppCenterHelper.TraceSessionOpen(serverOrg.Protocol, fromView);

            // recode connect count
            _configurationService.Engagement.ConnectCount++;
            _configurationService.Save();


            {
                var vmServer = _appData.GetItemById(serverOrg.DataSourceName, serverOrg.Id);
                if (vmServer != null)
                {
                    // update the last conn time
                    ConnectTimeRecorder.UpdateAndSave(vmServer.Server);
                    vmServer.LastConnectTime = ConnectTimeRecorder.Get(vmServer.Server);
                }
            }


            var serverClone = serverOrg.Clone();
            serverClone.ConnectPreprocess();

            // use assign credential
            if(string.IsNullOrEmpty(assignCredentialName) == false)
            {
                var assignCredentialNameTmp = assignCredentialName;
                if (serverClone is ProtocolBaseWithAddressPortUserPwd protocol
                    && protocol.Credentials?.Count > 0
                    && protocol.Credentials.Any(x => x.Name == assignCredentialNameTmp))
                {
                    var c = protocol.Credentials.First(x => x.Name == assignCredentialNameTmp);
                    if (!string.IsNullOrEmpty(c.Address))
                        protocol.Address = c.Address;
                    if (!string.IsNullOrEmpty(c.Port))
                        protocol.Port = c.Port;
                    if (!string.IsNullOrEmpty(c.UserName))
                        protocol.UserName = c.UserName;
                    if (!string.IsNullOrEmpty(c.Password))
                        protocol.Password = c.Password;
                }
            }

            // run script before connected
            serverClone.RunScriptBeforeConnect();

            var runner = ProtocolRunnerHostHelper.GetRunner(IoC.Get<ProtocolConfigurationService>(), serverClone, serverClone.Protocol, assignRunnerName)!;
            switch (serverClone)
            {
                case RdpApp remoteApp:
                    this.ConnectRemoteApp(remoteApp);
                    return;
                case RDP rdp:
                    {
                        // check if screens are in different scale factors
                        int factor = (int)(new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor * 100);
                        // for those people using 2+ monitors in different scale factors, we will try "mstsc.exe" instead of internal runner.
                        if (rdp.MstscModeEnabled == true
                            || (serverClone.ThisTimeConnWithFullScreen()
                                && Screen.AllScreens.Length > 1
                                && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens
                                && Screen.AllScreens.Select(screen => (int)(new ScreenInfoEx(screen).ScaleFactor * 100)).Any(factor2 => factor != factor2)))
                        {
                            this.ConnectRdpByMstsc(rdp);
                            return;
                        }
                        // rdp full screen
                        if (serverClone.ThisTimeConnWithFullScreen())
                        {
                            this.ConnectWithFullScreen(serverClone, runner);
                            return;
                        }
                        break;
                    }
            }

            this.ConnectWithTab(serverClone, runner, assignTabToken ?? "");
            PrintCacheCount();
        }

        public void AddTab(TabWindowBase tab)
        {
            lock (_dictLock)
            {
                var token = tab.Token;
                Debug.Assert(!_token2TabWindows.ContainsKey(token));
                Debug.Assert(!string.IsNullOrEmpty(token));
                _token2TabWindows.TryAdd(token, tab);
                tab.Activated += (sender, args) =>
                    _lastTabToken = tab.Token;
            }
        }

        private FullScreenWindowView MoveToExistedFullScreenWindow(HostBase host, TabWindowBase? fromTab)
        {
            // restore from tab to full
            var full = _connectionId2FullScreenWindows[host.ConnectionId];
            full.LastTabToken = "";
            // full screen placement
            if (fromTab != null)
            {
                var screenEx = ScreenInfoEx.GetCurrentScreen(fromTab);
                full.Top = screenEx.VirtualWorkingAreaCenter.Y - full.Height / 2;
                full.Left = screenEx.VirtualWorkingAreaCenter.X - full.Width / 2;
                full.LastTabToken = _lastTabToken;
            }
            full.SetProtocolHost(host);
            full.Show();
            return full;
        }

        private FullScreenWindowView MoveToNewFullScreenWindow(HostBase host, TabWindowBase? fromTab)
        {
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
                     && IoC.Get<LocalityService>().RdpLocalityGet(rdp.Id) is { } setting
                     && setting.FullScreenLastSessionScreenIndex >= 0
                     && setting.FullScreenLastSessionScreenIndex < Screen.AllScreens.Length)
                screenEx = ScreenInfoEx.GetCurrentScreen(setting.FullScreenLastSessionScreenIndex);
            else
                screenEx = ScreenInfoEx.GetCurrentScreen(IoC.Get<MainWindowView>());

            if (screenEx != null)
            {
                full.Top = screenEx.VirtualWorkingAreaCenter.Y - full.Height / 2;
                full.Left = screenEx.VirtualWorkingAreaCenter.X - full.Width / 2;
            }

            _connectionId2FullScreenWindows.TryAdd(host.ConnectionId, full);
            full.SetProtocolHost(host);
            full.Show();
            return full;
        }

        public void MoveProtocolHostToFullScreen(string connectionId)
        {
            if (!_connectionId2Hosts.ContainsKey(connectionId))
                throw new NullReferenceException($"can not find host by connectionId = `{connectionId}`");

            var host = _connectionId2Hosts[connectionId];

            // remove from old parent
            var tab = GetTabByConnectionId(connectionId);
            if (tab != null)
            {
                // if tab is not loaded, do not allow move to full-screen, 防止 loaded 事件中的逻辑覆盖
                if (tab.IsLoaded == false)
                    return;

                var item = tab.GetViewModel().Items.First(x => x.Content.ConnectionId == connectionId);
                Execute.OnUIThread(() => { tab.GetViewModel().Items.Remove(item); });
                tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.Count > 0 ? tab.GetViewModel().Items.First() : null;
                SimpleLogHelper.Debug($@"MoveProtocolHostToFullScreen: remove connectionId = {connectionId} from tab({tab.GetHashCode()}) ");
            }

            // move to full-screen-window
            var full = _connectionId2FullScreenWindows.ContainsKey(connectionId) ?
                this.MoveToExistedFullScreenWindow(host, tab) :
                this.MoveToNewFullScreenWindow(host, tab);

            this.CleanupProtocolsAndWindows();

            SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
            PrintCacheCount();
        }

        /// <summary>
        /// get a tab for server,
        /// if assignTabToken == null, create a new tab
        /// if assignTabToken != null, find _token2tabWindows[assignTabToken], if _token2tabWindows[assignTabToken] is null, then create a new tab
        /// </summary>
        /// <param name="assignTabToken"></param>
        /// <returns></returns>
        private TabWindowBase? GetOrCreateTabWindow(string assignTabToken = "")
        {
            TabWindowBase? ret = null;
            lock (_dictLock)
            {
                if (_token2TabWindows.ContainsKey(assignTabToken))
                {
                    ret = _token2TabWindows[assignTabToken];
                }
                else if (string.IsNullOrEmpty(assignTabToken) == false)
                {
                    ret = CreateNewTabWindow();
                }
                // return the latest tab window.
                else if (_token2TabWindows.ContainsKey(_lastTabToken))
                {
                    ret = _token2TabWindows[_lastTabToken];
                }
                else if (_token2TabWindows.IsEmpty == false)
                {
                    ret = _token2TabWindows.Last().Value;
                }

                ret ??= CreateNewTabWindow();
                return ret;
            }
        }

        private TabWindowBase? CreateNewTabWindow()
        {
            lock (_dictLock)
            {
                var token = DateTime.Now.Ticks.ToString();
                var tab = new TabWindowView(token, IoC.Get<LocalityService>());
                Debug.Assert(!_token2TabWindows.ContainsKey(token));
                Debug.Assert(!string.IsNullOrEmpty(token));
                _token2TabWindows.TryAdd(token, tab);
                tab.Activated += (sender, args) => _lastTabToken = tab.Token;
                tab.Show();
                _lastTabToken = tab.Token;

                int loopCount = 0;
                while (tab.IsLoaded == false)
                {
                    ++loopCount;
                    Thread.Sleep(100);
                    if (loopCount > 50)
                        break;
                }

                if (loopCount > 50)
                {
                    MessageBoxHelper.ErrorAlert("Can not open a new TebWindow for the session! Check you permissions and antivirus plz.");
                    return null;
                }
                return tab;
            }
        }

        private void MoveProtocolHostToTab(string connectionId)
        {
            Debug.Assert(_connectionId2Hosts.ContainsKey(connectionId) == true);
            var host = _connectionId2Hosts[connectionId];
            SimpleLogHelper.Debug($@"MoveProtocolHostToTab: Moving host({host.GetHashCode()}) to any tab");
            // get tab
            TabWindowBase? tab;
            {
                // remove from old parent
                if (host.ParentWindow is FullScreenWindowView fullScreenWindow)
                {
                    if (fullScreenWindow.IsLoaded == false)
                    {
                        // if FullScreenWindowView is not loaded, do not allow move to tab, 防止 loaded 事件中的逻辑覆盖
                        return;
                    }

                    tab = this.GetOrCreateTabWindow(fullScreenWindow.LastTabToken ?? "");
                    // !importance: do not close old FullScreenWindowView, or RDP will lose conn bar when restore from tab to fullscreen.
                    if (tab is { IsClosed: false })
                    {
                    }
                    else
                    {
                        tab = this.GetOrCreateTabWindow();
                    }

                    SimpleLogHelper.Debug($@"Hide full({fullScreenWindow.GetHashCode()})");
                    fullScreenWindow.SetProtocolHost(null);
                    fullScreenWindow.Hide();
                }
                else
                    tab = this.GetOrCreateTabWindow();
            }

            if (tab == null)
                return;

            // assign host to tab
            if (tab.GetViewModel().Items.All(x => x.Content != host))
            {
                // move
                tab.AddItem(new TabItemViewModel(host, host.ProtocolServer.DisplayName));
            }
            else
            {
                // just show
                tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.First(x => x.Content == host);
            }
            tab.Activate();
            SimpleLogHelper.Debug($@"MoveProtocolHostToTab: Moved host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
            PrintCacheCount();
        }

        private void OnProtocolClose(string connectionId)
        {
            this.CloseProtocolHostAsync(connectionId);
        }

        #region Mark CloseProtocol

        public void CloseProtocolHostAsync(string connectionId)
        {
            CloseProtocolHostAsync(new[] { connectionId });
        }
        public void CloseProtocolHostAsync(string[] connectionIds)
        {
            Task.Factory.StartNew(() =>
            {
                MarkProtocolHostToClose(connectionIds);
                CleanupProtocolsAndWindows();
            });
        }

        private void MarkProtocolHostToClose(string[] connectionIds)
        {
            lock (_dictLock)
            {
                foreach (var connectionId in connectionIds)
                {
                    if (!_connectionId2Hosts.TryRemove(connectionId, out var host)) continue;

                    SimpleLogHelper.Debug($@"MarkProtocolHostToClose: marking to close: {host.GetType().Name}(id = {connectionId}, hash = {host.GetHashCode()})");

                    host.OnClosed -= OnProtocolClose;
                    host.OnFullScreen2Window -= this.MoveProtocolHostToTab;
                    _hostToBeDispose.Enqueue(host);
                    host.ProtocolServer.RunScriptAfterDisconnected();
                    PrintCacheCount();

#if NETFRAMEWORK
                    foreach (var kv in _token2TabWindows.ToArray())
                    {
                        var key = kv.Key;
                        var tab = kv.Value;
#else
                    foreach (var (key, tab) in _token2TabWindows.ToArray())
                    {
#endif
                        var tabItemVm = tab.GetViewModel().Items.FirstOrDefault(x => x.Content.ConnectionId == connectionId);
                        // remove items from tab
                        if (tabItemVm != null)
                        {
                            Execute.OnUIThread(() =>
                            {
                                tab.GetViewModel().Items.Remove(tabItemVm);
                                var items = tab.GetViewModel().Items.ToList();
                                if (items.Count == 0)
                                {
                                    tab.Hide();
                                    // move tab from dict to queue
                                    _token2TabWindows.TryRemove(key, out _);
                                    _windowToBeDispose.Enqueue(tab);
                                }
                            });
                        }
                    }

                    // hide full
#if NETFRAMEWORK
                    foreach (var kv in _connectionId2FullScreenWindows.Where(x => x.Key == connectionId).ToArray())
                    {
                        var key = kv.Key;
                        var full = kv.Value;
#else
                    foreach (var (key, full) in _connectionId2FullScreenWindows.Where(x => x.Key == connectionId).ToArray())
                    {
#endif
                        if (full.Host == null || _connectionId2Hosts.ContainsKey(full.Host.ConnectionId) == false)
                        {
                            _connectionId2FullScreenWindows.TryRemove(key, out _);
                            _windowToBeDispose.Enqueue(full);
                            Execute.OnUIThread(() =>
                            {
                                full.SetProtocolHost(null);
                                full.Hide();
                            });
                        }
                    }
                }

                // Mark Unhandled Protocol To Close
                foreach (var id2Host in _connectionId2Hosts.ToArray())
                {
                    var id = id2Host.Key;
                    bool unhandledFlag = true;
                    // if host in the tab
                    foreach (var kv in _token2TabWindows)
                    {
                        var tab = kv.Value;
                        var items = tab.GetViewModel().Items.ToList();
                        if (items.Any(x => x.Host.ConnectionId == id))
                        {
                            unhandledFlag = false;
                            break;
                        }
                    }

                    // if host in the full-screen
                    if (unhandledFlag && _connectionId2FullScreenWindows.ContainsKey(id))
                    {
                        unhandledFlag = false;
                    }

                    // host not in either tab or full-screen
                    if (unhandledFlag && _connectionId2Hosts.TryRemove(id, out var host))
                    {
                        SimpleLogHelper.Warning($@"MarkUnhandledProtocolToClose: marking to close: {host.GetType().Name}(id = {id}, hash = {host.GetHashCode()})");
                        host.OnClosed -= OnProtocolClose;
                        host.OnFullScreen2Window -= this.MoveProtocolHostToTab;
                        _hostToBeDispose.Enqueue(host);
                        host.ProtocolServer.RunScriptAfterDisconnected();
                        PrintCacheCount();
                    }
                }
            }
        }

        #endregion

        #region Clean up CloseProtocol
        private void CloseMarkedProtocolHost()
        {
            while (_hostToBeDispose.TryDequeue(out var host))
            {
                PrintCacheCount();
                host.OnClosed -= OnProtocolClose;
                host.OnFullScreen2Window -= this.MoveProtocolHostToTab;
                // Dispose
                try
                {
                    if (host is IDisposable d)
                    {
                        d.Dispose();
                    }
                    else
                    {
                        host.Close();
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
        }

        private void CloseEmptyWindows()
        {
            int closeCount = 0;
            foreach (var kv in _token2TabWindows.ToArray())
            {
                var key = kv.Key;
                var tab = kv.Value;
                var items = tab.GetViewModel().Items.ToList();
                items = items.Where(x => x != null).ToList();
                if (items.Count == 0 || items.All(x => _connectionId2Hosts.ContainsKey(x?.Content?.ConnectionId ?? "****") == false))
                {
                    SimpleLogHelper.Debug($@"CloseEmptyWindows: closing tab({tab.GetHashCode()})");
                    ++closeCount;
                    _token2TabWindows.TryRemove(key, out _);
                    _windowToBeDispose.Enqueue(tab);
                }
            }

            foreach (var kv in _connectionId2FullScreenWindows.ToArray())
            {
                var key = kv.Key;
                var full = kv.Value;
                if (full.Host == null || _connectionId2Hosts.ContainsKey(full.Host.ConnectionId) == false)
                {
                    SimpleLogHelper.Debug($@"CloseEmptyWindows: closing full(hash = {full.GetHashCode()})");
                    ++closeCount;
                    _connectionId2FullScreenWindows.TryRemove(key, out _);
                    _windowToBeDispose.Enqueue(full);
                }
            }

            PrintCacheCount();
            // 在正常的逻辑中，在关闭session时就应该把空窗体移除，不应该有空窗体的存在
            if (closeCount > 0)
                SimpleLogHelper.Warning($@"CloseEmptyWindows: {closeCount} Empty Host closed");

            if (_windowToBeDispose.IsEmpty == false)
            {
                SimpleLogHelper.Debug($@"Closing: {_windowToBeDispose.Count} Empty Host.");
                Execute.OnUIThread(() =>
                {
                    while (_windowToBeDispose.TryDequeue(out var window))
                    {
                        window.Close();
                    }
                });
            }
        }

        private bool _isCleaning = false;
        public void CleanupProtocolsAndWindows()
        {
            if (_isCleaning == false)
            {
                lock (this)
                {
                    if (_isCleaning == false)
                    {
                        _isCleaning = true;
                        try
                        {
                            lock (_dictLock)
                            {
                                this.CloseEmptyWindows();
                            }
                            this.CloseMarkedProtocolHost();
                        }
                        finally
                        {
                            _isCleaning = false;
                        }
                    }
                }
            }
        }
        #endregion

        private void PrintCacheCount([CallerMemberName] string callMember = "")
        {
            SimpleLogHelper.Debug($@"{callMember}: Current: Host = {_connectionId2Hosts.Count}, Full = {_connectionId2FullScreenWindows.Count}, Tab = {_token2TabWindows.Count}, HostToBeDispose = {_hostToBeDispose.Count}, WindowToBeDispose = {_windowToBeDispose.Count}");
        }
    }
}