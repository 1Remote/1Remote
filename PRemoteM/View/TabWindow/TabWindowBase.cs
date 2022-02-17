using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using Dragablz;
using PRM.Core.I;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.Core.Service;
using Shawn.Utils;
using PRM.Model;
using PRM.View.ProtocolHosts;
using PRM.ViewModel;
using ProtocolHostType = PRM.View.ProtocolHosts.ProtocolHostType;
using Timer = System.Timers.Timer;

namespace PRM.View.TabWindow
{
    public abstract class TabWindowBase : WindowChromeBase, ITab
    {
        protected VmTabWindow Vm;
        private TabablzControl _tabablzControl = null;
        public string Token => Vm?.Token;

        private IntPtr _lastActivatedWindowHandle = IntPtr.Zero;
        private IntPtr _myWindowHandle;
        private readonly Timer _timer4CheckForegroundWindow;
        private WindowState _lastWindowState;
        private readonly LocalityService _localityService;

        protected TabWindowBase(string token, LocalityService localityService)
        {
            _localityService = localityService;
            Vm = new VmTabWindow(token);
            DataContext = Vm;
            _timer4CheckForegroundWindow = new Timer();

            _lastWindowState = _localityService?.TabWindowState ?? WindowState.Normal;

            this.Loaded += (sender, args) =>
            {
                _timer4CheckForegroundWindow.Interval = 100;
                _timer4CheckForegroundWindow.AutoReset = false;
                _timer4CheckForegroundWindow.Elapsed += Timer4CheckForegroundWindowOnElapsed;
                _timer4CheckForegroundWindow.Start();

                _myWindowHandle = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(_myWindowHandle);
                source.AddHook(new HwndSourceHook(WndProc));
            };


            this.Unloaded += (sender, args) =>
            {
                _timer4CheckForegroundWindow.Dispose();
            };
        }

        /// <summary>
        /// Redirect USB Device, TODO move to main window.
        /// </summary>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DEVICECHANGE = 0x0219;
            if (msg == WM_DEVICECHANGE)
                if (Vm?.SelectedItem?.Content is AxMsRdpClient09Host rdp)
                {
                    SimpleLogHelper.Debug($"rdp.NotifyRedirectDeviceChange((uint){wParam}, (int){lParam})");
                    rdp.NotifyRedirectDeviceChange(msg, (uint)wParam, (int)lParam);
                }
            return IntPtr.Zero;
        }

        private void Timer4CheckForegroundWindowOnElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (Vm?.SelectedItem?.Content?.GetProtocolHostType() != ProtocolHostType.Integrate)
                    return;

                var hWnd = this.Vm.SelectedItem.Content.GetHostHwnd();
                if (hWnd == IntPtr.Zero) return;

                var nowActivatedWindowHandle = GetForegroundWindow();

                // bring Tab window to top, when the host content is Integrate.
                if (nowActivatedWindowHandle == hWnd && nowActivatedWindowHandle != _lastActivatedWindowHandle)
                {
                    SimpleLogHelper.Debug($"TabWindowBase: _lastActivatedWindowHandle = ({_lastActivatedWindowHandle})");
                    SimpleLogHelper.Debug($"TabWindowBase: nowActivatedWindowHandle = ({nowActivatedWindowHandle}), hWnd = {hWnd}");
                    SimpleLogHelper.Debug($"TabWindowBase: BringWindowToTop({_myWindowHandle})");
                    BringWindowToTop(_myWindowHandle);
                }

                // focus content when tab is focused and host is Integrate and left mouse is not pressed
                if (nowActivatedWindowHandle == _myWindowHandle && System.Windows.Forms.Control.MouseButtons != MouseButtons.Left)
                {
                    Vm?.SelectedItem?.Content?.MakeItFocus();
                }

                _lastActivatedWindowHandle = nowActivatedWindowHandle;
            }
            finally
            {
                _timer4CheckForegroundWindow.Start();
            }
        }

        private void InitSizeChanged()
        {
            // save window size when size changed
            this.SizeChanged += (sizeChangeSender, _) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    _localityService.TabWindowHeight = this.Height;
                    _localityService.TabWindowWidth = this.Width;
                    //_localityService.TabWindowState = this.WindowState;
                }
                SimpleLogHelper.Debug($"Tab size change to:W = {this.Width}, H = {this.Height}, Child {this.Vm?.SelectedItem?.Content?.Width}, {this.Vm?.SelectedItem?.Content?.Height}");
            };
        }

        private void InitWindowStateChanged()
        {
            this.StateChanged += delegate (object sender, EventArgs args)
            {
                if (this.WindowState != WindowState.Minimized)
                    if (Vm.SelectedItem?.CanResizeNow != true)
                    {
                        this.WindowState = _lastWindowState;
                        return;
                    }
                    else
                    {
                        _lastWindowState = this.WindowState;
                    }

                if (this.WindowState != WindowState.Minimized)
                {
                    Vm?.SelectedItem?.Content?.ToggleAutoResize(true);
                    _localityService.TabWindowHeight = this.Height;
                    _localityService.TabWindowWidth = this.Width;
                    _localityService.TabWindowState = this.WindowState;
                }
                SimpleLogHelper.Debug($"Tab size change to:W = {this.Width}, H = {this.Height}, Child {this.Vm?.SelectedItem?.Content?.Width}, {this.Vm?.SelectedItem?.Content?.Height}");
            };
        }

        private void InitClosingItemCallback()
        {
            _tabablzControl.ClosingItemCallback += args =>
            {
                args.Cancel();
                if (args.DragablzItem.DataContext is TabItemViewModel viewModel)
                {
                    var pb = viewModel.Content;
                    RemoteWindowPool.Instance.DelProtocolHostInSyncContext(pb?.ConnectionId, true);
                }
            };
        }

        private void InitClosed()
        {
            Closed += (sender, args) =>
            {
                try
                {
                    _timer4CheckForegroundWindow?.Dispose();
                }
                finally
                {
                }
                try
                {
                    Vm?.CmdCloseAll.Execute();
                    RemoteWindowPool.Instance.DelTabWindow(Token);
                    Vm?.Dispose();
                }
                finally
                {
                    Vm = null;
                    DataContext = null;
                }
            };
        }

        protected void Init(TabablzControl tabablzControl)
        {
            _tabablzControl = tabablzControl;
            this.Activated += (sender, args) =>
            {
                this.StopFlashingWindow();
            };

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Width = _localityService.TabWindowWidth;
            this.Height = _localityService.TabWindowHeight;
            // check the current screen size
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            if (this.Width >= screenEx.VirtualWorkingArea.Width)
                this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
            if (this.Height >= screenEx.VirtualWorkingArea.Height)
                this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);

            this.MinWidth = this.MinHeight = 300;

            InitSizeChanged();
            InitWindowStateChanged();
            InitClosingItemCallback();
            InitClosed();
        }

        protected virtual void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem != null)
            {
                this.Icon = Vm.SelectedItem.Content.ProtocolServer.IconImg;
            }
        }

        public VmTabWindow GetViewModel()
        {
            return Vm;
        }

        public void AddItem(TabItemViewModel newItem)
        {
            Debug.Assert(newItem?.Content?.ConnectionId != null);
            if(Vm?.Items == null)
                return;
            if (Vm.Items.Any(x => x.Content?.ConnectionId == newItem.Content.ConnectionId))
            {
                Vm.SelectedItem = Vm.Items.First(x => x.Content.ConnectionId == newItem.Content.ConnectionId);
                return;
            }
            Vm.Items.Add(newItem);
            Vm.SelectedItem = Vm.Items.Last();
        }

        public Size GetTabContentSize()
        {
            Debug.Assert(this.Resources["TabContentBorder"] != null);
            Debug.Assert(this.Resources["TitleBarHeight"] != null);
            var tabContentBorder = (Thickness)this.Resources["TabContentBorder"];
            var trapezoidHeight = (double)this.Resources["TitleBarHeight"];
            return new Size()
            {
                Width = _tabablzControl.ActualWidth - tabContentBorder.Left - tabContentBorder.Right,
                Height = _tabablzControl.ActualHeight - trapezoidHeight - tabContentBorder.Bottom - tabContentBorder.Top - 1,
            };
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();




        protected override void WinTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (!Vm.SelectedItem.CanResizeNow)
                    return;
                if (Vm.IsLocked)
                    return;
            }
            base.WinTitleBar_MouseDown(sender, e);
        }
    }
}