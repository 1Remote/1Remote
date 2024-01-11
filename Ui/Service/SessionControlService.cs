using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Stylet;
using ProtocolHostStatus = _1RM.View.Host.ProtocolHosts.ProtocolHostStatus;
using _1RM.Service.DataSource;
using _1RM.Service.Locality;
using _1RM.Service.DataSource.DAO.Dapper;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        private readonly DataSourceService _sourceService;
        private readonly ConfigurationService _configurationService;
        private readonly GlobalData _appData;

        public SessionControlService(DataSourceService sourceService, ConfigurationService configurationService, GlobalData appData)
        {
            _sourceService = sourceService;
            _configurationService = configurationService;
            _appData = appData;
            GlobalEventHelper.OnRequestServerConnect += this.OnRequestOpenConnection;
            GlobalEventHelper.OnRequestQuickConnect += this.OnRequestOpenConnection;
            GlobalEventHelper.OnRequestServersConnect += this.OnRequestOpenConnection;
        }

        public void Release()
        {
            lock (_dictLock)
            {
                foreach (var tabWindow in _token2TabWindows.ToArray())
                {
                    tabWindow.Value.Hide();
                }
                foreach (var kv in _connectionId2FullScreenWindows.ToArray())
                {
                    kv.Value.Hide();
                }
            }
            this.CloseProtocolHostAsync(_connectionId2Hosts.Keys.ToArray());
        }

        private string _lastTabToken = "";

        private readonly object _dictLock = new object();
        private readonly ConcurrentDictionary<string, TabWindowView> _token2TabWindows = new ConcurrentDictionary<string, TabWindowView>();
        private readonly ConcurrentDictionary<string, HostBase> _connectionId2Hosts = new ConcurrentDictionary<string, HostBase>();
        private readonly ConcurrentDictionary<string, FullScreenWindowView> _connectionId2FullScreenWindows = new ConcurrentDictionary<string, FullScreenWindowView>();
        private readonly ConcurrentQueue<HostBase> _hostToBeDispose = new ConcurrentQueue<HostBase>();
        private readonly ConcurrentQueue<Window> _windowToBeDispose = new ConcurrentQueue<Window>();

        public int TabWindowCount
        {
            get
            {
                lock (_dictLock)
                {
                    return _token2TabWindows.Count;
                }
            }
        }

        public ConcurrentDictionary<string, HostBase> ConnectionId2Hosts => _connectionId2Hosts;


        private void OnRequestOpenConnection(in ProtocolBase? serverOrg, in string fromView, in string assignTabToken = "", in string assignRunnerName = "", in string assignCredentialName = "")
        {
            CleanupProtocolsAndWindows();
            #region START MULTIPLE SESSION
            // if server == null, then start multiple sessions
            if (serverOrg == null)
            {
                var list = _appData.VmItemList
                    .Where(x => x.IsSelected)
                    .Select(x => x.Server)
                    .ToArray();
                OnRequestOpenConnection(list, in fromView, in assignTabToken, in assignRunnerName, in assignCredentialName);
                MsAppCenterHelper.TraceSessionOpen($"multiple sessions ({((list.Length >= 5) ? ">=5" : list.Length.ToString())})", fromView);
                return;
            }
            #endregion

            var org = serverOrg;
            var view = fromView;
            var tabToken = assignTabToken;
            var runnerName = assignRunnerName;
            var credentialName = assignCredentialName;
            Task.Factory.StartNew(async () =>
            {
                await Connect(org, view, tabToken, runnerName, credentialName);
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    SimpleLogHelper.Fatal(t.Exception);
                }
            });
        }

        private void OnRequestOpenConnection(IEnumerable<ProtocolBase> protocolBases, in string fromView, in string assignTabToken = "", in string assignRunnerName = "", in string assignCredentialName = "")
        {
            CleanupProtocolsAndWindows();
            var view = fromView;
            var tabToken = assignTabToken;
            var runnerName = assignRunnerName;
            var credentialName = assignCredentialName;
            Task.Factory.StartNew(async () =>
            {
                foreach (var org in protocolBases)
                {
                    tabToken = await Connect(org, view, tabToken, runnerName, credentialName);
                }
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    SimpleLogHelper.Fatal(t.Exception);
                }
            });
        }


        private void OnRequestCloseConnection(string connectionId)
        {
            this.CloseProtocolHostAsync(connectionId);
        }


        private bool ActivateOrReConnIfServerSessionIsOpened(in ProtocolBase server, Credential? assignCredential)
        {
            if (!server.IsOnlyOneInstance()) return false;
            var connectionId = server.BuildConnectionId();
            // if is OnlyOneInstance Protocol and it is connected now, activate it and return.
            if (!_connectionId2Hosts.ContainsKey(connectionId))
                return false;

            // FOR RDP with alternative account
            if (assignCredential != null)
            {
                if (_connectionId2Hosts[connectionId].ProtocolServer is ProtocolBaseWithAddressPort p
                    && assignCredential.Address != p.Address)
                    return false;
                if (_connectionId2Hosts[connectionId].ProtocolServer is ProtocolBaseWithAddressPortUserPwd p2
                    && assignCredential.UserName != p2.UserName)
                    return false;
            }

            SimpleLogHelper.Debug($"_connectionId2Hosts ContainsKey {connectionId}");
            // Activate
            if (_connectionId2Hosts[connectionId].ParentWindow is { } win)
            {
                if (win is TabWindowView tab)
                {
                    var serverId = server.Id;
                    var s = tab.GetViewModel().Items.FirstOrDefault(x => x.Content?.ProtocolServer?.Id == serverId);
                    if (s != null)
                        tab.GetViewModel().SelectedItem = s;
                }

                if (win.IsClosed)
                {
                    MarkProtocolHostToClose(new string[] { connectionId });
                    CleanupProtocolsAndWindows();
                    return false;
                }

                try
                {
                    Execute.OnUIThreadSync(() =>
                    {
                        if (win.IsClosing != false) return;
                        win.WindowState = win.WindowState == WindowState.Minimized ? WindowState.Normal : win.WindowState;
                        win.Show();
                        win.Activate();
                    });

                    var vmServer = _appData.GetItemById(server.DataSource?.DataSourceName ?? "", server.Id);
                    vmServer?.UpdateConnectTime();
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    MarkProtocolHostToClose(new string[] { connectionId });
                    CleanupProtocolsAndWindows();
                }
            }

            // Reconnect
            if (_connectionId2Hosts[connectionId].ParentWindow != null)
            {
                if (_connectionId2Hosts[connectionId].Status != ProtocolHostStatus.Connected)
                    _connectionId2Hosts[connectionId].ReConn();
            }
            return true;
        }




        #region CloseProtocol

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

                    host.OnClosed -= OnRequestCloseConnection;
                    host.OnFullScreen2Window -= this.MoveSessionToTabWindow;
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
                        if (tab.GetViewModel().TryRemoveItem(connectionId))
                        {
                            var items = tab.GetViewModel().Items.ToList();
                            if (items.Count == 0)
                            {
                                tab.Hide();
                                // move tab from dict to queue
                                _token2TabWindows.TryRemove(key, out _);
                                _windowToBeDispose.Enqueue(tab);
                            }
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
                            full.ShowOrHide(null);
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
                        if (items.Any(x => x.Content.ConnectionId == id))
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
                        host.OnClosed -= OnRequestCloseConnection;
                        host.OnFullScreen2Window -= this.MoveSessionToTabWindow;
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
                host.OnClosed -= OnRequestCloseConnection;
                host.OnFullScreen2Window -= this.MoveSessionToTabWindow;
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
                SimpleLogHelper.DebugWarning($@"CloseEmptyWindows: {closeCount} Empty Host closed");

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

        public void CleanupProtocolsAndWindows()
        {
            lock (_dictLock)
            {
                this.CloseEmptyWindows();
            }
            this.CloseMarkedProtocolHost();
        }
        #endregion

        private void PrintCacheCount([CallerMemberName] string callMember = "")
        {
            SimpleLogHelper.Debug($@"{callMember}: Current: Host = {_connectionId2Hosts.Count}, Full = {_connectionId2FullScreenWindows.Count}, Tab = {_token2TabWindows.Count}, HostToBeDispose = {_hostToBeDispose.Count}, WindowToBeDispose = {_windowToBeDispose.Count}");
        }
    }
}