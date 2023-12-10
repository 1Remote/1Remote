using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
using _1RM.Service.DataSource;
using System.Collections.Generic;
using _1RM.Service.Locality;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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

                    tab = this.GetOrCreateTabWindow(full.LastTabToken ?? "");
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
        private TabWindowView GetOrCreateTabWindow(string assignTabToken = "")
        {
            TabWindowView? ret = null;
            lock (_dictLock)
            {
                // find existed
                if (_token2TabWindows.ContainsKey(assignTabToken))
                {
                    ret = _token2TabWindows[assignTabToken];
                }
                else if (string.IsNullOrEmpty(assignTabToken))
                {
                    if (_token2TabWindows.ContainsKey(_lastTabToken))
                    {
                        ret = _token2TabWindows[_lastTabToken];
                    }
                    else if (_token2TabWindows.IsEmpty == false)
                    {
                        ret = _token2TabWindows.Last().Value;
                    }
                }

                // create new
                if (ret == null)
                {
                    Execute.OnUIThreadSync(() =>
                    {
                        ret = new TabWindowView();
                        AddTab(ret);
                        ret.Show();
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