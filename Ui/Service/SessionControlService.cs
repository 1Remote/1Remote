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
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Model.ProtocolRunner;
using PRM.Model.ProtocolRunner.Default;
using PRM.Utils;
using PRM.View;
using PRM.View.Host;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using ProtocolHostStatus = PRM.View.Host.ProtocolHosts.ProtocolHostStatus;
using Screen = System.Windows.Forms.Screen;


namespace PRM.Service
{
    public class SessionControlService
    {
        private readonly PrmContext _context;
        private readonly ConfigurationService _configurationService;
        private readonly GlobalData _appData;

        public SessionControlService(PrmContext context, ConfigurationService configurationService, GlobalData appData)
        {
            _context = context;
            _configurationService = configurationService;
            _appData = appData;
            GlobalEventHelper.OnRequestServerConnect += this.ShowRemoteHost;
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
        private readonly ConcurrentDictionary<string, TabWindowBase> _token2TabWindows = new();
        private readonly ConcurrentDictionary<string, HostBase> _connectionId2Hosts = new();
        private readonly ConcurrentDictionary<string, FullScreenWindowView> _connectionId2FullScreenWindows = new();
        private readonly ConcurrentQueue<HostBase> _hostToBeDispose = new();
        private readonly ConcurrentQueue<Window> _windowToBeDispose = new();

        public int TabWindowCount => _token2TabWindows.Count;
        public ConcurrentDictionary<string, HostBase> ConnectionId2Hosts => _connectionId2Hosts;

        private bool ActivateOrReConnIfServerSessionIsOpened(ProtocolBase server)
        {
            var serverId = server.Id;
            // if is OnlyOneInstance Protocol and it is connected now, activate it and return.
            if (server.IsOnlyOneInstance() && _connectionId2Hosts.ContainsKey(serverId.ToString()))
            {
                if (_connectionId2Hosts[serverId.ToString()].ParentWindow is TabWindowBase t)
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
            if (_context.DataService != null)
            {
                File.WriteAllText(rdpFile, rdp.ToRdpConfig(_context.DataService).ToString());
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

        private void ConnectRemoteApp(RdpApp remoteApp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{remoteApp.DisplayName}_{remoteApp.Port}_{remoteApp.UserName}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
            if (_context.DataService != null)
            {
                File.WriteAllText(rdpFile, remoteApp.ToRdpConfig(_context.DataService).ToString());
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

        private void ConnectWithFullScreen(ProtocolBase server, Runner runner)
        {
            CleanupProtocolsAndWindows();
            // fullscreen normally
            var host = ProtocolRunnerHostHelper.GetHostForInternalRunner(_context, server, runner);
            if (host == null)
                return;
            Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
            _connectionId2Hosts.TryAdd(host.ConnectionId, host);
            host.OnClosed += this.OnProtocolClose;
            host.OnFullScreen2Window += this.MoveProtocolHostToTab;
            this.MoveProtocolHostToFullScreen(host.ConnectionId);
            host.Conn();
            SimpleLogHelper.Debug($@"Start Conn: {server.DisplayName}({server.GetHashCode()}) by host({host.GetHashCode()}) with full");
        }

        private void ConnectWithTab(ProtocolBase server, Runner runner, string assignTabToken)
        {
            CleanupProtocolsAndWindows();
            // open SFTP when SSH is connected.
            if (server is SSH { OpenSftpOnConnected: true } ssh)
            {
                var tmpRunner = ProtocolRunnerHostHelper.GetRunner(_context, server, SFTP.ProtocolName);
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
                        server.ConnectPreprocess(_context);
                        if (server is RDP)
                        {
                            tab = this.GetOrCreateTabWindow(assignTabToken);
                            var size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(server.ColorHex) == true);
                            host = ProtocolRunnerHostHelper.GetRdpInternalHost(_context, server, runner, size.Width, size.Height);
                        }
                        else
                        {
                            host = ProtocolRunnerHostHelper.GetHostForInternalRunner(_context, server, runner);
                        }

                        break;
                    }
                case ExternalRunner:
                    {
                        host = ProtocolRunnerHostHelper.GetHostOrRunDirectlyForExternalRunner(_context, server, runner);
                        // if host is null, could be run without integrate
                        break;
                    }
                default:
                    throw new NotImplementedException($"unknown runner: {runner.GetType()}");
            }

            if (host != null)
            {
                if (tab == null)
                    tab = this.GetOrCreateTabWindow(assignTabToken);
                // get display area size for host
                Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
                host.OnClosed += OnProtocolClose;
                host.OnFullScreen2Window += this.MoveProtocolHostToTab;
                tab.AddItem(new TabItemViewModel(host, server.DisplayName));
                _connectionId2Hosts.TryAdd(host.ConnectionId, host);
                host.Conn();
                tab.Activate();
            }
        }

        private void ShowRemoteHost(long serverId, string? assignTabToken, string? assignRunnerName)
        {
            #region START MULTIPLE SESSION
            // if serverId <= 0, then start multiple sessions
            if (serverId <= 0)
            {
                var list = _appData.VmItemList.Where(x => x.IsSelected).ToArray();
                foreach (var item in list)
                {
                    this.ShowRemoteHost(item.Id, assignTabToken, assignRunnerName);
                }
                return;
            }
            #endregion

            Debug.Assert(_appData.VmItemList.Any(x => x.Server.Id == serverId));
            _configurationService.Engagement.ConnectCount++;
            _configurationService.Save();
            // clear selected state
            _appData.UnselectAllServers();

            var server = _appData.VmItemList.FirstOrDefault(x => x.Server.Id == serverId)?.Server;
            if (server == null)
            {
                SimpleLogHelper.Error($@"try to connect Server Id = {serverId} while {serverId} is not in the db");
                return;
            }

            // update the last conn time
            // TODO remember connection time in the localstorage
            server.LastConnTime = DateTime.Now;
            Debug.Assert(_context.DataService != null);
            _context.DataService.Database_UpdateServer(server);

            // if is OnlyOneInstance server and it is connected now, activate it and return.
            if (this.ActivateOrReConnIfServerSessionIsOpened(server))
                return;

            // run script before connected
            server.RunScriptBeforeConnect();

            var runner = ProtocolRunnerHostHelper.GetRunner(_context, server, server.Protocol, assignRunnerName)!;
            switch (server)
            {
                case RdpApp remoteApp:
                    this.ConnectRemoteApp(remoteApp);
                    return;
                case RDP rdp:
                    {
                        // check if screens are in different scale factors
                        int factor = (int)(new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor * 100);
                        // for those people using 2+ monitors in different scale factors, we will try "mstsc.exe" instead of "PRemoteM".
                        if (rdp.MstscModeEnabled == true
                            || (server.ThisTimeConnWithFullScreen()
                                && Screen.AllScreens.Length > 1
                                && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens
                                && Screen.AllScreens.Select(screen => (int)(new ScreenInfoEx(screen).ScaleFactor * 100)).Any(factor2 => factor != factor2)))
                        {
                            this.ConnectRdpByMstsc(rdp);
                            return;
                        }
                        // rdp full screen
                        if (server.ThisTimeConnWithFullScreen())
                        {
                            this.ConnectWithFullScreen(server, runner);
                            return;
                        }
                        break;
                    }
            }

            this.ConnectWithTab(server, runner, assignTabToken ?? "");
            SimpleLogHelper.Debug($@"Hosts.Count = {_connectionId2Hosts.Count}, FullWin.Count = {_connectionId2FullScreenWindows.Count}, _token2tabWindows.Count = {_token2TabWindows.Count}");
        }

        public void AddTab(TabWindowBase tab)
        {
            var token = tab.Token;
            Debug.Assert(!_token2TabWindows.ContainsKey(token));
            Debug.Assert(!string.IsNullOrEmpty(token));
            _token2TabWindows.TryAdd(token, tab);
            tab.Activated += (sender, args) =>
                _lastTabToken = tab.Token;
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
                     && IoC.Get<LocalityService>().RdpLocalityGet(rdp.Id.ToString()) is { } setting
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

        public Window MoveProtocolHostToFullScreen(string connectionId)
        {
            if (!_connectionId2Hosts.ContainsKey(connectionId))
                throw new NullReferenceException($"can not find host by connectionId = `{connectionId}`");

            var host = _connectionId2Hosts[connectionId];

            // remove from old parent
            var tab = _token2TabWindows.Values.FirstOrDefault(x => x.GetViewModel().Items.Any(y => y.Content.ConnectionId == connectionId));
            if (tab != null)
            {
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
            SimpleLogHelper.Debug($@"Hosts.Count = {_connectionId2Hosts.Count}, FullWin.Count = {_connectionId2FullScreenWindows.Count}, _token2tabWindows.Count = {_token2TabWindows.Count}");

            return full;
        }

        /// <summary>
        /// get a tab for server,
        /// if assignTabToken == null, create a new tab
        /// if assignTabToken != null, find _token2tabWindows[assignTabToken], if _token2tabWindows[assignTabToken] is null, then create a new tab
        /// </summary>
        /// <param name="assignTabToken"></param>
        /// <returns></returns>
        private TabWindowBase GetOrCreateTabWindow(string assignTabToken = "")
        {
            if (string.IsNullOrEmpty(assignTabToken) == false && _token2TabWindows.ContainsKey(assignTabToken))
                return _token2TabWindows[assignTabToken];
            // return the latest tab window.
            else if (_token2TabWindows.ContainsKey(_lastTabToken))
                return _token2TabWindows[_lastTabToken];
            else if (_token2TabWindows.IsEmpty == false)
                return _token2TabWindows.Last().Value;
            // create new TabWindowBase
            return CreateNewTabWindow();
        }

        private TabWindowBase CreateNewTabWindow()
        {
            var token = DateTime.Now.Ticks.ToString();
            AddTab(new TabWindowView(token, IoC.Get<LocalityService>()));
            var tab = _token2TabWindows[token];

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

        private void MoveProtocolHostToTab(string connectionId)
        {
            Debug.Assert(_connectionId2Hosts.ContainsKey(connectionId) == true);
            var host = _connectionId2Hosts[connectionId];
            SimpleLogHelper.Debug($@"MoveProtocolHostToTab: Moving host({host.GetHashCode()}) to any tab");
            // get tab
            TabWindowBase tab;
            {
                // remove from old parent
                if (host.ParentWindow is FullScreenWindowView fullScreenWindow)
                {
                    // !importance: do not close old FullScreenWindowView, or RDP will lose conn bar when restore from tab to fullscreen.
                    SimpleLogHelper.Debug($@"Hide full({fullScreenWindow.GetHashCode()})");
                    fullScreenWindow.SetProtocolHost(null);
                    fullScreenWindow.Hide();
                    tab = this.GetOrCreateTabWindow(fullScreenWindow.LastTabToken ?? "");
                }
                else
                    tab = this.GetOrCreateTabWindow();
            }
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
            SimpleLogHelper.Debug($@"MoveProtocolHostToTab: Moved host({host.GetHashCode()}) to tab({tab.GetHashCode()})", $@"Hosts.Count = {_connectionId2Hosts.Count}, FullWin.Count = {_connectionId2FullScreenWindows.Count}, _token2tabWindows.Count = {_token2TabWindows.Count}");
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
            foreach (var connectionId in connectionIds)
            {
                if (_connectionId2Hosts.TryRemove(connectionId, out var host))
                {
                    SimpleLogHelper.Debug($@"MarkProtocolHostToClose: marking to close: {host.GetType().Name}(id = {connectionId}, hash = {host.GetHashCode()})");

                    if (host.OnClosed != null)
                        host.OnClosed -= OnProtocolClose;
                    if (host.OnFullScreen2Window != null)
                        host.OnFullScreen2Window -= this.MoveProtocolHostToTab;
                    _hostToBeDispose.Enqueue(host);
                    host.ProtocolServer.RunScriptAfterDisconnected();
                    SimpleLogHelper.Debug($@"Current: Host = {_connectionId2Hosts.Count}, Full = {_connectionId2FullScreenWindows.Count}, Tab = {_token2TabWindows.Count}, HostToBeDispose = {_hostToBeDispose.Count}");

                    // remove from tab
                    foreach (var kv in _token2TabWindows.ToArray())
                    {
                        var tab = kv.Value;
                        var items = tab.GetViewModel().Items.Where(x => x.Content.ConnectionId == connectionId).ToArray();
                        if (items.Length > 0)
                            Execute.OnUIThread(() =>
                            {
                                foreach (var item in items)
                                {
                                    tab.GetViewModel().Items.Remove(item);
                                }
                                if (tab.GetViewModel().Items.Count == 0)
                                    tab.Hide();
                            });
                        if (tab.GetViewModel().Items.Count == 0)
                        {
                            _token2TabWindows.TryRemove(kv.Key, out _);
                            _windowToBeDispose.Enqueue(tab);
                        }
                    }

                    // hide full
                    foreach (var kv in _connectionId2FullScreenWindows.Where(x => x.Key == connectionId).ToArray())
                    {
                        var full = kv.Value;
                        if (full.Host == null || _connectionId2Hosts.ContainsKey(full.Host.ConnectionId) == false)
                        {
                            _connectionId2FullScreenWindows.TryRemove(kv.Key, out _);
                            _windowToBeDispose.Enqueue(full);
                            Execute.OnUIThread(() =>
                            {
                                full.SetProtocolHost(null);
                                full.Hide();
                            });
                        }
                    }
                }
            }

            MarkUnhandledProtocolToClose();
        }


        private void MarkUnhandledProtocolToClose()
        {
            foreach (var id2Host in _connectionId2Hosts.ToArray())
            {
                var id = id2Host.Key;
                bool unhandledFlag = true;
                // if host in the tab
                foreach (var kv in _token2TabWindows)
                {
                    var tab = kv.Value;
                    var vm = tab.GetViewModel();
                    if (vm.Items.Any(x => x.Host.ConnectionId == id))
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
                    if (host.OnClosed != null)
                        host.OnClosed -= OnProtocolClose;
                    if (host.OnFullScreen2Window != null)
                        host.OnFullScreen2Window -= this.MoveProtocolHostToTab;
                    _hostToBeDispose.Enqueue(host);
                    host.ProtocolServer.RunScriptAfterDisconnected();
                    PrintCacheCount();
                }
            }
        }

        #endregion

        #region Clean up CloseProtocol
        private void CloseMarkedProtocolHost()
        {
            while (_hostToBeDispose.TryDequeue(out var host))
            {
                SimpleLogHelper.Info($@"CloseMarkedProtocolHost: Current: Host = {_connectionId2Hosts.Count}, Full = {_connectionId2FullScreenWindows.Count}, Tab = {_token2TabWindows.Count}, HostToBeDispose = {_hostToBeDispose.Count}");
                if (host.OnClosed != null)
                    host.OnClosed -= OnProtocolClose;
                if (host.OnFullScreen2Window != null)
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
                var token = kv.Key;
                var tab = kv.Value;
                if (tab.GetViewModel().Items.Count == 0 || tab.GetViewModel().Items.All(x => _connectionId2Hosts.ContainsKey(x.Content.ConnectionId) == false))
                {
                    SimpleLogHelper.Debug($@"CloseEmptyWindows: closing tab({tab.GetHashCode()})");
                    ++closeCount;
                    _token2TabWindows.TryRemove(kv.Key, out _);
                    _windowToBeDispose.Enqueue(tab);
                }
            }

            foreach (var kv in _connectionId2FullScreenWindows.ToArray())
            {
                var connectionId = kv.Key;
                var full = kv.Value;
                if (full.Host == null || _connectionId2Hosts.ContainsKey(full.Host.ConnectionId) == false)
                {
                    SimpleLogHelper.Debug($@"CloseEmptyWindows: closing full(hash = {full.GetHashCode()})");
                    ++closeCount;
                    _connectionId2FullScreenWindows.TryRemove(connectionId, out _);
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
        private readonly object _cleanupLock = new();
        public void CleanupProtocolsAndWindows()
        {
            if (_isCleaning == false)
            {
                lock (_cleanupLock)
                {
                    if (_isCleaning == false)
                    {
                        _isCleaning = true;
                        try
                        {
                            this.CloseEmptyWindows();
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
            SimpleLogHelper.Info($@"{callMember}: Current: Host = {_connectionId2Hosts.Count}, Full = {_connectionId2FullScreenWindows.Count}, Tab = {_token2TabWindows.Count}, HostToBeDispose = {_hostToBeDispose.Count}, WindowToBeDispose = {_windowToBeDispose.Count}");
        }
    }
}