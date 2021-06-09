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
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.RDP.Host;
using Shawn.Utils;
using PRM.Model;
using PRM.ViewModel;
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

        protected TabWindowBase(string token)
        {
            Vm = new VmTabWindow(token);
            DataContext = Vm;
            _timer4CheckForegroundWindow = new Timer();

            _lastWindowState = this.WindowState;

            this.Loaded += (sender, args) =>
            {
                var wih = new WindowInteropHelper(this);
                _myWindowHandle = wih.Handle;
                _timer4CheckForegroundWindow.Interval = 100;
                _timer4CheckForegroundWindow.AutoReset = true;
                _timer4CheckForegroundWindow.Elapsed += Timer4CheckForegroundWindowOnElapsed;
                _timer4CheckForegroundWindow.Start();

                var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(new HwndSourceHook(WndProc));
            };


            this.Unloaded += (sender, args) =>
            {
                _timer4CheckForegroundWindow.Dispose();
            };
        }

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
            // bring Tab window to top, when the host content is Integrate
            if (Vm?.SelectedItem?.Content?.GetProtocolHostType() != ProtocolHostType.Integrate)
                return;

            var hWnd = this.Vm.SelectedItem.Content.GetHostHwnd();
            if (hWnd == IntPtr.Zero) return;

            var nowActivatedWindowHandle = GetForegroundWindow();
            if (nowActivatedWindowHandle == hWnd && nowActivatedWindowHandle != _lastActivatedWindowHandle)
            {
                SimpleLogHelper.Debug($"TabWindowBase: _lastActivatedWindowHandle = ({_lastActivatedWindowHandle})");
                SimpleLogHelper.Debug($"TabWindowBase: nowActivatedWindowHandle = ({nowActivatedWindowHandle}), hWnd = {hWnd}");
                SimpleLogHelper.Debug($"TabWindowBase: BringWindowToTop({_myWindowHandle})");
                BringWindowToTop(_myWindowHandle);
            }
            else if (nowActivatedWindowHandle == _myWindowHandle &&
                     System.Windows.Forms.Control.MouseButtons != MouseButtons.Left)
            {
                Vm?.SelectedItem?.Content?.MakeItFocus();
            }
            _lastActivatedWindowHandle = nowActivatedWindowHandle;
        }

        private void InitSizeChanged()
        {
            // save window size when size changed
            this.SizeChanged += (sizeChangeSender, _) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    SystemConfig.Instance.Locality.TabWindowHeight = this.Height;
                    SystemConfig.Instance.Locality.TabWindowWidth = this.Width;
                    SystemConfig.Instance.Locality.TabWindowState = this.WindowState;
                }
                SimpleLogHelper.Debug($"Tab size change to:W = {this.Width}, H = {this.Height}, Child {this.Vm?.SelectedItem?.Content?.Width}, {this.Vm?.SelectedItem?.Content?.Height}");
            };
        }

        private void InitStateChanged()
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
                    SystemConfig.Instance.Locality.TabWindowHeight = this.Height;
                    SystemConfig.Instance.Locality.TabWindowWidth = this.Width;
                    SystemConfig.Instance.Locality.TabWindowState = this.WindowState;
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
                    RemoteWindowPool.Instance.DelProtocolHostInSyncContext(pb?.ConnectionId);
                }
            };
        }

        private void InitClosed()
        {
            Closed += (sender, args) =>
            {
                DataContext = null;
                _timer4CheckForegroundWindow?.Dispose();
                Vm?.CmdCloseAll.Execute();
                Vm?.Dispose();
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
            this.Width = SystemConfig.Instance.Locality.TabWindowWidth;
            this.Height = SystemConfig.Instance.Locality.TabWindowHeight;
            this.MinWidth = this.MinHeight = 300;
            if (SystemConfig.Instance.Locality.TabWindowState != WindowState.Minimized)
                this.WindowState = SystemConfig.Instance.Locality.TabWindowState;

            InitSizeChanged();
            InitStateChanged();
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
            if (Vm.Items.Any(x => x.Content.ConnectionId == newItem.Content.ConnectionId))
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

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hWnd);




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