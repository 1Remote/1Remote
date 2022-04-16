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
using PRM.Model;
using PRM.Service;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using ProtocolHostType = PRM.View.Host.ProtocolHosts.ProtocolHostType;
using Timer = System.Timers.Timer;

namespace PRM.View.Host
{
    public abstract class TabWindowBase : WindowChromeBase
    {
        protected TabWindowViewModel Vm;
        private TabablzControl _tabablzControl = null!;
        public string Token => Vm.Token;

        private IntPtr _myHandle = IntPtr.Zero;
        private readonly Timer _timer4CheckForegroundWindow = new();
        private WindowState _lastWindowState;
        private readonly LocalityService _localityService;

        protected TabWindowBase(string token, LocalityService localityService)
        {
            _localityService = localityService;
            Vm = new TabWindowViewModel(token);
            DataContext = Vm;

            _lastWindowState = _localityService.TabWindowState;

            this.Loaded += (sender, args) =>
            {
                _timer4CheckForegroundWindow.Interval = 100;
                _timer4CheckForegroundWindow.AutoReset = false;
                _timer4CheckForegroundWindow.Elapsed += Timer4CheckForegroundWindowOnElapsed;
                _timer4CheckForegroundWindow.Start();
                _myHandle = new WindowInteropHelper(this).Handle;
            };


            this.Unloaded += (sender, args) =>
            {
                _timer4CheckForegroundWindow.Dispose();
            };
        }

        private IntPtr _lastActivatedWindowHandle = IntPtr.Zero;
        private void Timer4CheckForegroundWindowOnElapsed(object? sender, ElapsedEventArgs e)
        {
            _timer4CheckForegroundWindow.Stop();
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
                    SimpleLogHelper.Debug($@"TabWindowBase: _lastActivatedWindowHandle = ({_lastActivatedWindowHandle})
TabWindowBase: nowActivatedWindowHandle = ({nowActivatedWindowHandle}), hWnd = {hWnd}
TabWindowBase: BringWindowToTop({_myHandle})");
                    BringWindowToTop(_myHandle);
                }
                // focus content when tab is focused and host is Integrate and left mouse is not pressed
                else if (nowActivatedWindowHandle == _myHandle && System.Windows.Forms.Control.MouseButtons != MouseButtons.Left)
                {
                    Vm?.SelectedItem?.Content?.MakeItFocus();
                }
                _lastActivatedWindowHandle = nowActivatedWindowHandle;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning(ex);
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
            this.StateChanged += delegate
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
                if (args.DragablzItem.DataContext is TabItemViewModel viewModel
                    && viewModel.Content != null)
                {
                    IoC.Get<RemoteWindowPool>().DelProtocolHostInSyncContext(viewModel.Content.ConnectionId, true);
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
                    IoC.Get<RemoteWindowPool>().DelTabWindow(Token);
                    Vm?.Dispose();
                }
                finally
                {
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
            if (Vm?.SelectedItem?.Content != null)
            {
                this.Icon = Vm.SelectedItem.Content.ProtocolServer.IconImg;
            }
        }

        public TabWindowViewModel GetViewModel()
        {
            return Vm;
        }

        public void AddItem(TabItemViewModel newItem)
        {
            Debug.Assert(newItem?.Content?.ConnectionId != null);
            if (Vm?.SelectedItem?.Content == null) return;
            if (Vm?.Items == null) return;
            if (Vm.Items.Any(x => x.Content?.ConnectionId == newItem.Content.ConnectionId))
            {
                Vm.SelectedItem = Vm.Items.First(x => x.Content!.ConnectionId == newItem.Content.ConnectionId);
                return;
            }
            Vm.Items.Add(newItem);
            Vm.SelectedItem = Vm.Items.Last();
        }

        public Size GetTabContentSize(bool colorIsTransparent)
        {
            Debug.Assert(this.Resources["TitleBarHeight"] != null);
            Debug.Assert(this.Resources["TabContentBorderWithColor"] != null);
            Debug.Assert(this.Resources["TabContentBorderWithOutColor"] != null);
            var tabContentBorderWithColor = (Thickness)this.Resources["TabContentBorderWithColor"];
            var tabContentBorderWithOutColor = (Thickness)this.Resources["TabContentBorderWithOutColor"];
            var trapezoidHeight = (double)this.Resources["TitleBarHeight"];
            if (colorIsTransparent)
                return new Size()
                {
                    Width = _tabablzControl.ActualWidth - tabContentBorderWithOutColor.Left - tabContentBorderWithOutColor.Right,
                    Height = _tabablzControl.ActualHeight - trapezoidHeight - tabContentBorderWithOutColor.Top - tabContentBorderWithOutColor.Bottom,
                };
            else
                return new Size()
                {
                    Width = _tabablzControl.ActualWidth - tabContentBorderWithColor.Left - tabContentBorderWithColor.Right,
                    Height = _tabablzControl.ActualHeight - trapezoidHeight - tabContentBorderWithColor.Top - tabContentBorderWithColor.Bottom,
                };
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();



        public override void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (Vm.SelectedItem?.CanResizeNow == false)
                    return;
                if (Vm.IsLocked)
                    return;
            }
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }
    }
}