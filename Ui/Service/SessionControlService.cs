using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
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
using ProtocolHostStatus = PRM.View.Host.ProtocolHosts.ProtocolHostStatus;


namespace PRM.Service
{
    public class SessionControlService
    {
        private readonly PrmContext _context;
        public SessionControlService(PrmContext context)
        {
            _context = context;
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
            foreach (var kv in _connectionId2Hosts.ToArray())
            {
                DelProtocolHost(kv.Key);
            }
            this.CleanupProtocolsAndWindows();
        }

        private string _lastTabToken = "";
        private readonly Dictionary<string, TabWindowBase> _token2TabWindows = new Dictionary<string, TabWindowBase>();
        private readonly Dictionary<string, HostBase> _connectionId2Hosts = new Dictionary<string, HostBase>();
        private readonly Dictionary<string, FullScreenWindowView> _connectionId2FullScreenWindows = new Dictionary<string, FullScreenWindowView>();

        public int TabWindowCount => _token2TabWindows.Count;
        public Dictionary<string, HostBase> ConnectionId2Hosts => _connectionId2Hosts;

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
            Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
            _connectionId2Hosts.Add(host.ConnectionId, host);
            host.OnClosed += this.OnProtocolClose;
            host.OnFullScreen2Window += this.MoveProtocolHostToTab;
            this.MoveProtocolHostToFullScreen(host.ConnectionId);
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
                this.ConnectWithTab(sftp, tmpRunner, assignTabToken);
            }

            var tab = this.GetOrCreateTabWindow(assignTabToken);
            var size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(protocol.ColorHex) == true);
            protocol.ConnectPreprocess(_context);
            var host = runner switch
            {
                ExternalRunner => ProtocolRunnerHostHelper.GetHostOrRunDirectlyForExternalRunner(_context, protocol, runner),
                InternalDefaultRunner => ProtocolRunnerHostHelper.GetHostForInternalRunner(_context, protocol, runner, size.Width, size.Height),
                _ => throw new NotImplementedException($"unknown runner: {runner.GetType()}")
            };
            Debug.Assert(host != null);

            // get display area size for host
            Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
            host.OnClosed += OnProtocolClose;
            host.OnFullScreen2Window += this.MoveProtocolHostToTab;
            tab.AddItem(new TabItemViewModel(host, protocol.DisplayName));
            _connectionId2Hosts.Add(host.ConnectionId, host);
            host.Conn();
            tab.Activate();
        }

        private void ShowRemoteHost(long serverId, string? assignTabToken, string? assignRunnerName)
        {
            #region START MULTIPLE SESSION
            // if serverId <= 0, then start multiple sessions
            if (serverId <= 0)
            {
                var list = _context.AppData.VmItemList.Where(x => x.IsSelected).ToArray();
                foreach (var item in list)
                {
                    this.ShowRemoteHost(item.Id, assignTabToken, assignRunnerName);
                }
                return;
            }
            #endregion

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
            // TODO remember connection time in the localstorage
            server.LastConnTime = DateTime.Now;
            Debug.Assert(_context.DataService != null);
            _context.DataService.Database_UpdateServer(server);

            // if is OnlyOneInstance protocol and it is connected now, activate it and return.
            if (this.ActivateOrReConnIfServerSessionIsOpened(server))
                return;

            // run script before connected
            server.RunScriptBeforeConnect();

            var runner = ProtocolRunnerHostHelper.GetRunner(_context, server.Protocol, assignRunnerName)!;
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

        private void OnProtocolClose(string connectionId)
        {
            this.DelProtocolHost(connectionId);
        }

        public void AddTab(TabWindowBase tab)
        {
            var token = tab.Token;
            Debug.Assert(!_token2TabWindows.ContainsKey(token));
            Debug.Assert(!string.IsNullOrEmpty(token));
            _token2TabWindows.Add(token, tab);
            tab.Activated += (sender, args) =>
                _lastTabToken = tab.Token;
        }

        private FullScreenWindowView MoveToExistedFullScreenWindow(string connectionId, TabWindowBase? fromTab)
        {
            Debug.Assert(_connectionId2FullScreenWindows.ContainsKey(connectionId));
            Debug.Assert(_connectionId2Hosts.ContainsKey(connectionId));
            var host = _connectionId2Hosts[connectionId];

            // restore from tab to full
            var full = _connectionId2FullScreenWindows[connectionId];
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

        private FullScreenWindowView MoveToNewFullScreenWindow(string connectionId, TabWindowBase? fromTab)
        {
            Debug.Assert(!_connectionId2FullScreenWindows.ContainsKey(connectionId));
            Debug.Assert(_connectionId2Hosts.ContainsKey(connectionId));
            var host = _connectionId2Hosts[connectionId];

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

            _connectionId2FullScreenWindows.Add(connectionId, full);
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
                tab.GetViewModel().Items.Remove(item);
                tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.Count > 0 ? tab.GetViewModel().Items.First() : null;
                SimpleLogHelper.Debug($@"MoveProtocolHostToFullScreen: remove connectionId = {connectionId} from tab({tab.GetHashCode()}) ");
            }

            // move to full-screen-window
            var full = _connectionId2FullScreenWindows.ContainsKey(connectionId) ? this.MoveToExistedFullScreenWindow(connectionId, tab) : this.MoveToNewFullScreenWindow(connectionId, tab);

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
            else if (_token2TabWindows.Count > 0)
                return _token2TabWindows.Last().Value;
            // create new TabWindowBase
            return CreateNewTabWindow();
        }


        private TabWindowBase CreateNewTabWindow()
        {
            var token = DateTime.Now.Ticks.ToString();
            AddTab(new TabWindowView(token, _context.LocalityService));
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

        public void DelProtocolHost(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)
                || !_connectionId2Hosts.ContainsKey(connectionId))
            {
                SimpleLogHelper.Warning($"DelProtocolHost: Attempted to delete a nonexistent host(id = {connectionId})");
                return;
            }




            HostBase? host = null;
            lock (this)
            {
                if (_connectionId2Hosts.ContainsKey(connectionId))
                {
                    host = _connectionId2Hosts[connectionId];
                    SimpleLogHelper.Debug($@"DelProtocolHost: try to delete host(id = {connectionId}, hash = {host.GetHashCode()})");
                    try
                    {
                        if (host.OnClosed != null)
                            host.OnClosed -= OnProtocolClose;
                        if (host.OnFullScreen2Window != null)
                            host.OnFullScreen2Window -= this.MoveProtocolHostToTab;
                        _connectionId2Hosts.Remove(connectionId);
                        SimpleLogHelper.Debug($@"DelProtocolHost: removed and now, Hosts.Count = {_connectionId2Hosts.Count}, FullWin.Count = {_connectionId2FullScreenWindows.Count}, _token2tabWindows.Count = {_token2TabWindows.Count}");
                    }
                    catch (Exception e)
                    {
                        host = null;
                        SimpleLogHelper.Error("DelProtocolHost: error when get host by connectionId and remove it from dictionary `Hosts`", e);
                    }
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

            this.CleanupProtocolsAndWindows();
        }


        /// <summary>
        /// del window & terminate remote connection
        /// </summary>
        public void DelTabWindow(string token)
        {
            SimpleLogHelper.Debug($@"DelTabWindow: try to delete token = {token}");
            lock (this)
            {
                if (!_token2TabWindows.ContainsKey(token)) return;
                var tab = _token2TabWindows[token];
                var items = tab.GetViewModel().Items.ToArray();
                // del protocol
                foreach (var tabItemViewModel in items)
                {
                    DelProtocolHost(tabItemViewModel.Content.ConnectionId);
                }
                SimpleLogHelper.Debug($@"DelTabWindow: deleted tab(token = {token}, hash = {tab.GetHashCode()})", $@"Now Hosts.Count = {_connectionId2Hosts.Count}, FullWin.Count = {_connectionId2FullScreenWindows.Count}, _token2tabWindows.Count = {_token2TabWindows.Count}");
                this.CleanupProtocolsAndWindows();
            }
        }

        #region Clean up
        public void CloseUnhandledProtocols()
        {
            lock (this)
            {
                foreach (var (id, host) in _connectionId2Hosts.ToArray())
                {
                    bool unhandledFlag = true;
                    foreach (var (token, tab) in _token2TabWindows)
                    {
                        var vm = tab.GetViewModel();
                        if (vm.Items.Any(x => x.Host.ConnectionId == id))
                        {
                            unhandledFlag = false;
                            break;
                        }
                    }

                    if (unhandledFlag && _connectionId2FullScreenWindows.ContainsKey(id))
                    {
                        unhandledFlag = false;
                    }

                    if (unhandledFlag)
                    {
                        DelProtocolHost(id);
                    }
                }
            }
        }

        public void CloseEmptyWindows()
        {
            bool flag = false;
            lock (this)
            {
                foreach (var (token, tab) in _token2TabWindows.ToArray())
                {
                    var items = tab.GetViewModel().Items.Where(x => _connectionId2Hosts.ContainsKey(x.Content.ConnectionId) == false).ToArray();
                    foreach (var item in items)
                    {
                        tab.GetViewModel().Items.Remove(item);
                    }
                }

                foreach (var (token, tab) in _token2TabWindows.ToArray())
                {
                    if (tab.GetViewModel().Items.Count == 0 || tab.GetViewModel().Items.All(x => _connectionId2Hosts.ContainsKey(x.Content.ConnectionId) == false))
                    {
                        SimpleLogHelper.Debug($@"CloseEmptyWindows: Close tab({tab.GetHashCode()})");
                        flag = true;
                        _token2TabWindows.Remove(token);
                        tab.Close();
                    }
                }

                foreach (var (connectionId, fullScreenWindow) in _connectionId2FullScreenWindows.ToArray())
                {
                    if (fullScreenWindow.Host == null || _connectionId2Hosts.ContainsKey(fullScreenWindow.Host.ConnectionId) == false)
                    {
                        SimpleLogHelper.Debug($@"CloseFullWindow: close(hash = {fullScreenWindow.GetHashCode()})");
                        flag = true;
                        _connectionId2FullScreenWindows.Remove(connectionId);
                        fullScreenWindow.Close();
                    }
                }
            }
            if (flag)
                SimpleLogHelper.Debug($@"Hosts.Count = {_connectionId2Hosts.Count}, FullWin.Count = {_connectionId2FullScreenWindows.Count}, _token2tabWindows.Count = {_token2TabWindows.Count}");
        }

        public void CleanupProtocolsAndWindows()
        {
            this.CloseUnhandledProtocols();
            this.CloseEmptyWindows();
        }
        #endregion
    }
}