using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        public void AddTab(TabWindowView tab)
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

        private FullScreenWindowView MoveToExistedFullScreenWindow(HostBase host, TabWindowView? fromTab)
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
            full.ShowOrHide(host);
            return full;
        }

        private FullScreenWindowView MoveToNewFullScreenWindow(HostBase host, TabWindowView? fromTab)
        {
            // first time to full
            var full = FullScreenWindowView.Create(fromTab?.Token ?? "", host, fromTab);
            full.ShowOrHide(host);
            _connectionId2FullScreenWindows.TryAdd(host.ConnectionId, full);
            return full;
        }


        public void MoveSessionToFullScreen(string connectionId)
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

                tab.GetViewModel().TryRemoveItem(connectionId);
                SimpleLogHelper.Debug($@"MoveSessionToFullScreen: remove connectionId = {connectionId} from tab({tab.GetHashCode()}) ");
            }

            // move to full-screen-window
            // #1119 review hardening: never reuse a cached FullScreenWindowView that has been closed
            // (e.g. the user closed a ghost copy shown by a reconnect race) — WindowBase silently
            // swallows Show() on a closed window, which would leave the live session with no window.
            // Drop the stale entry so MoveToNewFullScreenWindow can register the fresh one.
            if (_connectionId2FullScreenWindows.TryGetValue(connectionId, out var cachedFull) && cachedFull.IsClosed)
            {
                _connectionId2FullScreenWindows.TryRemove(connectionId, out _);
            }
            var full = _connectionId2FullScreenWindows.ContainsKey(connectionId) ?
                this.MoveToExistedFullScreenWindow(host, tab) :
                this.MoveToNewFullScreenWindow(host, tab);

            this.CleanupProtocolsAndWindows();

            SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
            PrintCacheCount();
        }

        public void MoveSessionToTabWindow(string connectionId)
        {
            Debug.Assert(_connectionId2Hosts.ContainsKey(connectionId) == true);
            var host = _connectionId2Hosts[connectionId];
            SimpleLogHelper.Debug($@"MoveSessionToTabWindow: Moving host({host.GetHashCode()}) to any tab");
            // get tab
            TabWindowView? tab;

            lock (_dictLock)
            {
                // remove from old parent
                if (host.ParentWindow is FullScreenWindowView full)
                {
                    if (full.IsLoaded == false)
                    {
                        // if FullScreenWindowView is not loaded, do not allow move to tab, 防止 loaded 事件中的逻辑覆盖
                        return;
                    }

                    // The recorded tab may have been closed by CloseEmptyWindows after its last
                    // session went full-screen (its token is then gone from _token2TabWindows).
                    // GetOrCreateTabWindow falls back to the most recently used tab only for an
                    // empty token, so passing a stale token would spawn a new tab per restoring
                    // session: sessions from the same dead tab would land in one tab each.
                    var lastTabToken = full.LastTabToken ?? "";
                    tab = this.GetOrCreateTabWindow(_token2TabWindows.ContainsKey(lastTabToken) ? lastTabToken : "");
                    if (tab.IsClosed)
                    {
                        tab = this.GetOrCreateTabWindow();
                    }

                    SimpleLogHelper.Debug($@"Hide full({full.GetHashCode()})");
                    // !importance: do not close old FullScreenWindowView, or RDP will lose conn bar when restore from tab to fullscreen.
                    full.ShowOrHide(null);
                }
                else
                    tab = this.GetOrCreateTabWindow();
            }


            // assign host to tab
            if (tab.GetViewModel().Items.All(x => x.Content != host))
            {
                // move
                tab.GetViewModel().AddItem(new TabItemViewModel(host, host.ProtocolServer.DisplayName));
            }
            else
            {
                // just show
                tab.GetViewModel().SelectedItem = tab.GetViewModel().Items.First(x => x.Content == host);
            }
            tab.Activate();
            SimpleLogHelper.Debug($@"MoveSessionToTabWindow: Moved host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
            PrintCacheCount();
        }


        /// <summary>
        /// get a tab for server,
        /// if assignTabToken == null, create a new tab
        /// if assignTabToken != null, find _token2tabWindows[assignTabToken], if _token2tabWindows[assignTabToken] is null, then create a new tab
        /// </summary>
        /// <param name="assignTabToken"></param>
        /// <returns></returns>
        private static bool IsUsableScreen(System.Windows.Forms.Screen screen)
        {
            if (screen == null)
                return false;

            try
            {
                return screen.Bounds.Width > 0
                    && screen.Bounds.Height > 0
                    && screen.WorkingArea.Width > 0
                    && screen.WorkingArea.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsUsableTabWindow(TabWindowView tab)
        {
            if (tab == null || tab.IsClosed)
                return false;
            if (!tab.IsLoaded)
                return true;

            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(tab).Handle;
                if (hwnd == IntPtr.Zero)
                    return false;
                var screen = ScreenInfoEx.GetCurrentScreen(hwnd);
                return screen != null && IsUsableScreen(screen.Screen);
            }
            catch
            {
                return false;
            }
        }

        private void PruneStaleTabWindows()
        {
            foreach (var kv in _token2TabWindows.ToArray())
            {
                var key = kv.Key;
                var tab = kv.Value;
                if (tab.IsClosed)
                {
                    _token2TabWindows.TryRemove(key, out _);
                    continue;
                }

                // Keep alive windows registered so ownership / session tracking remains intact,
                // even if the window is no longer usable for reuse (e.g. hidden or disconnected screen).
                // Only retire truly empty, stale tabs.
                if (!IsUsableTabWindow(tab) && tab.GetViewModel().Items.Count == 0)
                {
                    _token2TabWindows.TryRemove(key, out _);
                }
            }
        }

        private TabWindowView GetOrCreateTabWindow(string assignTabToken = "", bool preferLastOpenedDisplayWhenNew = false)
        {
            TabWindowView? ret = null;
            lock (_dictLock)
            {
                PruneStaleTabWindows();

                if (_token2TabWindows.ContainsKey(assignTabToken))
                {
                    ret = _token2TabWindows[assignTabToken];
                    if (ret == null || ret.IsClosed)
                    {
                        _token2TabWindows.TryRemove(assignTabToken, out _);
                        ret = null;
                    }
                    else if (!IsUsableTabWindow(ret) && ret.GetViewModel().Items.Count == 0)
                    {
                        _token2TabWindows.TryRemove(assignTabToken, out _);
                        ret = null;
                    }
                    else if (!IsUsableTabWindow(ret))
                    {
                        ret = null;
                    }
                }
                else if (string.IsNullOrEmpty(assignTabToken))
                {
                    if (_token2TabWindows.ContainsKey(_lastTabToken))
                    {
                        var last = _token2TabWindows[_lastTabToken];
                        if (last.IsClosed)
                        {
                            _token2TabWindows.TryRemove(_lastTabToken, out _);
                        }
                        else if (!IsUsableTabWindow(last) && last.GetViewModel().Items.Count == 0)
                        {
                            _token2TabWindows.TryRemove(_lastTabToken, out _);
                        }
                        else if (!IsUsableTabWindow(last))
                        {
                            // Keep live but unusable windows registered so owner tracking is preserved.
                        }
                        else
                        {
                            ret = last;
                        }
                    }

                    if (ret == null && _token2TabWindows.IsEmpty == false)
                    {
                        ret = _token2TabWindows.Values.FirstOrDefault(IsUsableTabWindow);
                    }
                }

                // create new
                if (ret == null)
                {
                    Execute.OnUIThreadSync(() =>
                    {
                        ret = new TabWindowView();
                        ret.PrepareStartupDisplay(preferLastOpenedDisplayWhenNew);
                        AddTab(ret);
                        ret.Show();
                        ret.ShowInTaskbar = true;
                        _lastTabToken = ret.Token;

                        int loopCount = 0;
                        while (ret.IsLoaded == false)
                        {
                            ++loopCount;
                            Thread.Sleep(100);
                            if (loopCount > 50)
                                break;
                        }
                    });
                }
                Debug.Assert(ret != null);
                return ret!;
            }
        }

        public TabWindowView? GetTabByConnectionId(string connectionId)
        {
            lock (_dictLock)
                return _token2TabWindows.Values.FirstOrDefault(x => x.GetViewModel().Items.Any(y => y.Content.ConnectionId == connectionId));
        }
    }
}