using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.Core.Ulits.DragablzTab;
using PRM.View;
using Shawn.Ulits;
using MessageBox = System.Windows.MessageBox;

namespace PRM.Model
{
    public class RemoteWindowPool
    {
        #region singleton
        private static RemoteWindowPool uniqueInstance;
        private static readonly object InstanceLock = new object();

        public static RemoteWindowPool GetInstance()
        {
            lock (InstanceLock)
            {
                if (uniqueInstance == null)
                {
                    throw new NullReferenceException($"{nameof(RemoteWindowPool)} has not been inited!");
                }
            }
            return uniqueInstance;
        }
        public static RemoteWindowPool Instance => GetInstance();
        #endregion

        public static void Init()
        {
            lock (InstanceLock)
            {
                if (uniqueInstance == null)
                {
                    uniqueInstance = new RemoteWindowPool();
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

        public void ShowRemoteHost(uint serverId)
        {
            Debug.Assert(serverId > 0);
            Debug.Assert(GlobalData.Instance.ServerList.Any(x => x.Id == serverId));
            var server = GlobalData.Instance.ServerList.First(x => x.Id == serverId);

            // update last conn time
            server.LastConnTime = DateTime.Now;
            Server.AddOrUpdate(server);

            // is connected now! activate it then return.
            if (server.OnlyOneInstance && _protocolHosts.ContainsKey(serverId.ToString()))
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
            TabWindow tab = null;
            try
            {
                if (server.IsConnWithFullScreen())
                {
                    // for those people using 2+ monitor which are in different scale factors, we will try "mstsc.exe" instead of "PRemoteM".
                    if (Screen.AllScreens.Length > 1
                        && server is ProtocolServerRDP rdp
                        && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
                    {
                        int factor = (int)(new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor * 100);
                        // check if screens are in different scale factors
                        bool differentScaleFactorFlag = Screen.AllScreens.Select(screen => (int)(new ScreenInfoEx(screen).ScaleFactor * 100)).Any(factor2 => factor != factor2);
                        if (differentScaleFactorFlag)
                        {
                            var tmp = Path.GetTempPath();
                            var dp = rdp.DispName;
                            var invalid = new string(Path.GetInvalidFileNameChars()) +
                                      new string(Path.GetInvalidPathChars());
                            dp = invalid.Aggregate(dp, (current, c) => current.Replace(c.ToString(), ""));
                            var rdpFile = Path.Combine(tmp, dp + ".rdp");
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
                                p.StandardInput.WriteLine("mstsc -admin " + rdpFile);
                                p.StandardInput.WriteLine("exit");
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            finally
                            {
                                var t = new Task(() =>
                            {
                                Thread.Sleep(1000 * 10);
                                if (File.Exists(rdpFile))
                                    File.Delete(rdpFile);
                            });
                                t.Start();
                            }
                            return;
                        }
                    }


                    var host = ProtocolHostFactory.Get(server);
                    host.OnClosed += OnProtocolClose;
                    host.OnFullScreen2Window += OnFullScreen2Window;
                    AddProtocolHost(host);
                    MoveProtocolHostToFullScreen(host.ConnectionId);
                    host.Conn();
                    SimpleLogHelper.Debug($@"Start Conn: {server.DispName}({server.GetHashCode()}) by host({host.GetHashCode()}) with full");
                }
                else
                {
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

                    if (tab == null)
                    {
                        var token = DateTime.Now.Ticks.ToString();
                        AddTab(new TabWindow(token));
                        tab = _tabWindows[token];
                        tab.Show();
                        _lastTabToken = token;

                        if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToGroup)
                            tab.Vm.Tag = server.GroupName;
                        else if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToProtocol)
                            tab.Vm.Tag = server.ProtocolDisplayName;
                    }
                    tab.Activate();
                    var size = tab.GetTabContentSize();
                    var host = ProtocolHostFactory.Get(server, size.Width, size.Height);
                    host.OnClosed += OnProtocolClose;
                    host.OnFullScreen2Window += OnFullScreen2Window;
                    host.ParentWindow = tab;
                    tab.Vm.Items.Add(new TabItemViewModel()
                    {
                        Content = host,
                        Header = server.DispName,
                    });
                    tab.Vm.SelectedItem = tab.Vm.Items.Last();
                    host.Conn();
                    _protocolHosts.Add(host.ConnectionId, host);
                    SimpleLogHelper.Debug($@"Start Conn: {server.DispName}({server.GetHashCode()}) by host({host.GetHashCode()}) with Tab({tab.GetHashCode()})");
                    SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }
            }
            catch (Exception e)
            {
                CloseEmpytTab();
                SimpleLogHelper.Error(e);
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public void AddFull(FullScreenWindow full)
        {
            Debug.Assert(!_host2FullScreenWindows.ContainsKey(full.ProtocolHostBase.ConnectionId));
            _host2FullScreenWindows.Add(full.ProtocolHostBase.ConnectionId, full);
        }
        public void AddProtocolHost(ProtocolHostBase protocol)
        {
            Debug.Assert(!_protocolHosts.ContainsKey(protocol.ConnectionId));
            _protocolHosts.Add(protocol.ConnectionId, protocol);
        }


        public void MoveProtocolHostToFullScreen(string connectionId)
        {
            if (_protocolHosts.ContainsKey(connectionId))
            {
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
                                if (t.Vm.Items.Count > 0)
                                    t.Vm.SelectedItem = tab.Vm.Items.First();
                                SimpleLogHelper.Debug($@"Remove host({host.GetHashCode()}) from tab({t.GetHashCode()})");
                            }
                        }
                    }
                }



                FullScreenWindow full;
                if (_host2FullScreenWindows.ContainsKey(connectionId))
                {
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
                    // move to full
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
                    host.ParentWindow = full;
                    full.Loaded += (sender, args) => { host.GoFullScreen(); };
                    full.Show();
                    AddFull(full);
                }
                CloseEmpytTab();

                SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to full({full.GetHashCode()})");
                SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
        }
        public void MoveProtocolHostToTab(string connectionId)
        {
            if (_protocolHosts.ContainsKey(connectionId))
            {
                FullScreenWindow full = null;
                var host = _protocolHosts[connectionId];
                // remove from old parent
                if (host.ParentWindow != null)
                {
                    if (host.ParentWindow is TabWindow tab)
                    {
                        tab.Activate();
                        return;
                    }
                    else if (host.ParentWindow is FullScreenWindow fullScreenWindow)
                    {
                        SimpleLogHelper.Debug($@"Hide full({fullScreenWindow.GetHashCode()})");
                        fullScreenWindow.Hide();
                        full = fullScreenWindow;
                    }
                    else
                        throw new ArgumentOutOfRangeException(host.ParentWindow + " type is wrong!");
                }

                if (full != null)
                {
                    TabWindow tab = null;

                    var server = full.ProtocolHostBase.ProtocolServer;

                    if (!string.IsNullOrEmpty(full.LastTabToken) && _tabWindows.ContainsKey(full.LastTabToken))
                        tab = _tabWindows[full.LastTabToken];
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

                    if (tab == null)
                    {
                        var token = DateTime.Now.Ticks.ToString();
                        AddTab(new TabWindow(token));
                        tab = _tabWindows[token];

                        if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToGroup)
                            tab.Vm.Tag = server.GroupName;
                        else if (SystemConfig.Instance.General.TabMode == EnumTabMode.NewItemGoesToProtocol)
                            tab.Vm.Tag = server.ProtocolDisplayName;

                        var screenEx = ScreenInfoEx.GetCurrentScreen(full);
                        tab.Top = screenEx.VirtualWorkingAreaCenter.Y - tab.Height / 2;
                        tab.Left = screenEx.VirtualWorkingAreaCenter.X - tab.Width / 2;
                        tab.WindowStartupLocation = WindowStartupLocation.Manual;
                        tab.Show();
                        _lastTabToken = token;
                        // move tab to screen which display full window 
                    }
                    tab.Activate();
                    tab.Vm.Items.Add(new TabItemViewModel()
                    {
                        Content = host,
                        Header = host.ProtocolServer.DispName,
                    });
                    tab.Vm.SelectedItem = tab.Vm.Items.Last();
                    host.ParentWindow = tab;
                    SimpleLogHelper.Debug($@"Move host({host.GetHashCode()}) to tab({tab.GetHashCode()})");
                    SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }
            }
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
                SimpleLogHelper.Debug($@"Del host({host.GetHashCode()})");
                _protocolHosts.Remove(connectionId);
                host.DisConn();
                SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");

                // close full
                if (_host2FullScreenWindows.ContainsKey(connectionId))
                {
                    var full = _host2FullScreenWindows[connectionId];
                    SimpleLogHelper.Debug($@"Close full({full.GetHashCode()})");
                    full.Close();
                    _host2FullScreenWindows.Remove(connectionId);
                    SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
                }

                // remove from tab
                var tabs = _tabWindows.Values.Where(x => x.Vm.Items.Any(y => y.Content.ConnectionId == connectionId));
                foreach (var tab in tabs.ToArray())
                {
                    tab.Vm.Items.Remove(tab.Vm.Items.First(x => x.Content.ConnectionId == connectionId));
                }
            }
            CloseEmpytTab();
        }

        /// <summary>
        /// del window terminate remote connection
        /// </summary>
        public void DelTabWindow(string token)
        {
            var tag = "";
            // del protocol
            if (_tabWindows.ContainsKey(token))
            {
                var tab = _tabWindows[token];
                SimpleLogHelper.Debug($@"Del tab({tab.GetHashCode()})");
                foreach (var tabItemViewModel in tab.Vm.Items.ToArray())
                {
                    DelProtocolHost(tabItemViewModel.Content.ConnectionId);
                }
                SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");
            }
            CloseEmpytTab();
        }

        private void CloseEmpytTab()
        {
            // close tab
            {
                var tabs = _tabWindows.Values.Where(x => x.Vm.Items.Count == 0).ToArray();
                foreach (var tab in tabs)
                {
                    SimpleLogHelper.Debug($@"Close tab({tab.GetHashCode()})");
                    _tabWindows.Remove(tab.Vm.Token);
                    tab.Close();
                    SimpleLogHelper.Debug($@"ProtocolHosts.Count = {_protocolHosts.Count}, FullWin.Count = {_host2FullScreenWindows.Count}, _tabWindows.Count = {_tabWindows.Count}");

                    /*
                    // if close a tab with tag, then find another tag can be this tag.
                    var tag = tab.Vm.Tag;
                    if (!string.IsNullOrEmpty(tag))
                        switch (SystemConfig.Instance.General.TabMode)
                        {
                            case EnumTabMode.NewItemGoesToGroup:
                            {
                                if (_tabWindows.Values.Any(x =>
                                    x.Vm.Items.All(y => y.Content.ProtocolServer.GroupName == tag)))
                                {
                                    var t = _tabWindows.Values.First(x =>
                                        x.Vm.Items.All(y => y.Content.ProtocolServer.GroupName == tag));
                                    t.Vm.Tag = tag;
                                }
                            }
                                break;
                            case EnumTabMode.NewItemGoesToProtocol:
                            {
                                if (_tabWindows.Values.Any(x =>
                                    x.Vm.Items.All(y => y.Content.ProtocolServer.ProtocolDisplayName == tag)))
                                {
                                    var t = _tabWindows.Values.First(x =>
                                        x.Vm.Items.All(y => y.Content.ProtocolServer.ProtocolDisplayName == tag));
                                    t.Vm.Tag = tag;
                                }
                            }
                                break;
                        }
                    */
                }
            }
        }
    }
}
