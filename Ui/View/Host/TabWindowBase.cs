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
using _1RM.Model;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using MessageBox = System.Windows.MessageBox;
using ProtocolHostType = _1RM.View.Host.ProtocolHosts.ProtocolHostType;
using Timer = System.Timers.Timer;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace _1RM.View.Host
{
    public abstract class TabWindowBase : WindowChromeBase
    {
        public const double TITLE_BAR_HEIGHT = 30;

        protected readonly TabWindowViewModel Vm;
        private TabablzControl? _tabablzControl = null!;
        public string Token => Vm.Token;

        private IntPtr _myHandle = IntPtr.Zero;
        private readonly Timer _timer4CheckForegroundWindow = new();
        private readonly LocalityService _localityService;

        protected TabWindowBase(string token, LocalityService localityService)
        {
            _localityService = localityService;
            Vm = new TabWindowViewModel(token, this);
            DataContext = Vm;

            this.MinWidth = this.MinHeight = 300;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.Loaded += (sender, args) =>
            {
                InitWindowSize();

                _timer4CheckForegroundWindow.Interval = 100;
                _timer4CheckForegroundWindow.AutoReset = false;
                _timer4CheckForegroundWindow.Elapsed += Timer4CheckForegroundWindowOnElapsed;
                _timer4CheckForegroundWindow.Start();
                _myHandle = new WindowInteropHelper(this).Handle;


                // save window size when size changed
                this.SizeChanged += (_, _) =>
                {
                    if (this.WindowState == WindowState.Normal)
                    {
                        _localityService.TabWindowHeight = this.Height;
                        _localityService.TabWindowWidth = this.Width;
                    }
                    SimpleLogHelper.Debug($"Tab size change to:W = {this.Width}, H = {this.Height}, Child {this.Vm?.SelectedItem?.Content?.Width}, {this.Vm?.SelectedItem?.Content?.Height}");
                };

                this.OnDragEnd += () =>
                {
                    _localityService.TabWindowTop = this.Top;
                    _localityService.TabWindowLeft = this.Left;
                };


                this.StateChanged += delegate
                {
                    if (this.WindowState != WindowState.Minimized)
                    {
                        if (Vm.SelectedItem?.Content.CanResizeNow() != true)
                        {
                            return;
                        }
                        Vm?.SelectedItem?.Content?.ToggleAutoResize(true);
                        _localityService.TabWindowState = this.WindowState;
                        _localityService.TabWindowStyle = this.WindowStyle;
                    }
                    SimpleLogHelper.Debug($"(Window state changed)Tab size change to:W = {this.Width}, H = {this.Height}, Child {this.Vm?.SelectedItem?.Content?.Width}, {this.Vm?.SelectedItem?.Content?.Height}");
                };


                Closed += (_, _) =>
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
                        var ids = Vm.Items.Select(x => x.Host.ConnectionId).ToArray();
                        if (ids.Length > 0)
                        {
                            IoC.Get<SessionControlService>().CloseProtocolHostAsync(ids);
                        }
                        Vm?.Dispose();
                    }
                    finally
                    {
                        DataContext = null;
                        System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
                    }
                };


                this.Closing += (sender, args) =>
                {
                    if (this.GetViewModel().Items.Count > 0
                        && App.ExitingFlag == false
                        && IoC.Get<ConfigurationService>().General.ConfirmBeforeClosingSession == true
                        && false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("Are you sure you want to close the connection?"), ownerViewModel: Vm))
                    {
                        args.Cancel = true;
                    }
                };


                this.Activated += (_, _) =>
                {
                    this.StopFlashingWindow();
                };
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
                    Vm?.SelectedItem?.Content?.FocusOnMe();
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

        private void InitWindowSize()
        {
            var ws = _localityService.TabWindowState;
            if (ws != System.Windows.WindowState.Minimized)
            {
                this.WindowState = ws;
                this.WindowStyle = _localityService.TabWindowStyle;
            }


            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            var leftTopOfCurrentScreen = new Point(screenEx.VirtualWorkingArea.X, screenEx.VirtualWorkingArea.Y);
            var rightBottomOfCurrentScreen = new Point(screenEx.VirtualWorkingArea.X + screenEx.VirtualWorkingArea.Width, screenEx.VirtualWorkingArea.Y + screenEx.VirtualWorkingArea.Height);
            if (WindowState == System.Windows.WindowState.Maximized)
            {

            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Width = _localityService.TabWindowWidth;
                this.Height = _localityService.TabWindowHeight;
                // check current screen size
                if (_localityService.TabWindowTop <= leftTopOfCurrentScreen.Y - TITLE_BAR_HEIGHT                                // check if the title bar outside the screen.
                    || _localityService.TabWindowTop > rightBottomOfCurrentScreen.Y                                             // check if the title bar outside the screen.
                    || _localityService.TabWindowLeft > rightBottomOfCurrentScreen.X                                            // check if the title bar outside the screen.
                    || _localityService.TabWindowLeft + _localityService.TabWindowWidth < leftTopOfCurrentScreen.X              // check if the title bar outside the screen.
                    || _localityService.TabWindowTop + _localityService.TabWindowHeight / 2 < leftTopOfCurrentScreen.Y          // check if the center of tab window local in current screen
                    || _localityService.TabWindowTop + _localityService.TabWindowHeight / 2 > rightBottomOfCurrentScreen.Y      // check if the center of tab window local in current screen
                    || _localityService.TabWindowLeft + _localityService.TabWindowWidth / 2 < leftTopOfCurrentScreen.X          // check if the center of tab window local in current screen
                    || _localityService.TabWindowLeft + _localityService.TabWindowWidth / 2 > rightBottomOfCurrentScreen.X      // check if the center of tab window local in current screen
                   )
                {
                    // default width & height
                    if (this.Width >= screenEx.VirtualWorkingArea.Width)
                        this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
                    if (this.Height >= screenEx.VirtualWorkingArea.Height)
                        this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);
                    // default top & left
                    this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
                    this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;
                }
                else
                {
                    this.Top = _localityService.TabWindowTop;
                    this.Left = _localityService.TabWindowLeft;
                }
            }
        }


        protected virtual void SetTabablzControl(TabablzControl tabablzControl)
        {
            _tabablzControl = tabablzControl;
        }

        protected virtual void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem?.Content != null)
            {
                this.Icon = IoC.Get<ConfigurationService>().General.ShowSessionIconInSessionWindow ?
                    Vm.SelectedItem.Content.ProtocolServer.IconImg : null;
            }
        }

        public TabWindowViewModel GetViewModel()
        {
            return Vm;
        }

        public Size GetTabContentSize(bool withoutBorderColor)
        {
            var size = new Size(800, 600);
            Execute.OnUIThreadSync(() =>
            {
                if (!this.IsLoaded || _tabablzControl == null) return;
                Debug.Assert(this.Resources["TabContentBorderWithColor"] != null);
                Debug.Assert(this.Resources["TabContentBorderWithOutColor"] != null);
                var tabContentBorderWithColor = (Thickness)this.Resources["TabContentBorderWithColor"];
                var tabContentBorderWithOutColor = (Thickness)this.Resources["TabContentBorderWithOutColor"];

                var screenEx = ScreenInfoEx.GetCurrentScreen(this);
                double actualWidth = _tabablzControl.ActualWidth;
                double actualHeight = this.WindowState == WindowState.Maximized ? screenEx.VirtualWorkingArea.Height : _tabablzControl.ActualHeight;
                double border1 = withoutBorderColor ? tabContentBorderWithOutColor.Left + tabContentBorderWithOutColor.Right : tabContentBorderWithColor.Left + tabContentBorderWithColor.Right;
                double border2 = withoutBorderColor ? tabContentBorderWithOutColor.Top + tabContentBorderWithOutColor.Bottom : tabContentBorderWithColor.Top + tabContentBorderWithColor.Bottom;
                size.Width = actualWidth - border1;
                size.Height = actualHeight - TITLE_BAR_HEIGHT - border2;
            });
            return size;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();



        public override void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (Vm.SelectedItem?.Content.CanResizeNow() == false)
                    return;
            }
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }
    }
}