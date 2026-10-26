using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils.Wpf.Controls;
using Stylet;
using System.Diagnostics;
using System.Linq;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using _1RM.View.Host.ProtocolHosts;

namespace _1RM.View.Host
{
    public partial class TabWindowView
    {
        public const double TITLE_BAR_HEIGHT = 30;

        /// <summary>
        /// True when this window was created by Dragablz to host a dragged-out tab.
        /// Such windows get their position/size from the drag operation and must not
        /// restore the remembered window placement or state in Loaded.
        /// </summary>
        internal bool IsCreatedByDragDetach { get; set; }

        protected readonly TabWindowViewModel Vm;
        public string Token => Vm.Token;

        private IntPtr _tabWindowHandle = IntPtr.Zero;
        private System.Windows.Interop.HwndSource? _hwndSource;
        private System.Windows.Interop.HwndSourceHook? _wndProcHook;



        public TabWindowView()
        {
            InitializeComponent();
            Vm = new TabWindowViewModel(this);
            DataContext = Vm;

            this.MinWidth = this.MinHeight = 300;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.SingleBorderWindow;

            Focusable = true;
            this.Loaded += (_, _) =>
            {
                if (!IsCreatedByDragDetach)
                {
                    InitWindowSizeOnLoaded();
                }
                TimerInitOnLoaded();
                _tabWindowHandle = new WindowInteropHelper(this).Handle;
                ForegroundWinEventInit();
                Keyboard.Focus(this);

                _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(_tabWindowHandle);
                _wndProcHook = new System.Windows.Interop.HwndSourceHook(AdditionalWndProc);
                _hwndSource?.AddHook(_wndProcHook);

                // remember window size when size changed
                SizeChanged += (_, _) =>
                {
                    if (this.WindowState == WindowState.Normal)
                    {
                        IoC.Get<LocalityService>().TabWindowWidth = this.ActualWidth;
                        IoC.Get<LocalityService>().TabWindowHeight = this.ActualHeight;
                    }
                    SimpleLogHelper.DebugInfo($"(Window size changed) Tab size change to:W = {this.ActualWidth}, H = {this.ActualHeight}");
                };

                // remember window pos when size changed
                OnDragEnd += () =>
                {
                    IoC.Get<LocalityService>().TabWindowTop = this.Top;
                    IoC.Get<LocalityService>().TabWindowLeft = this.Left;
                    SaveCurrentScreenInfo();
                };


                StateChanged += delegate
                {
                    if (this.WindowState == WindowState.Minimized)
                    {
                        Vm?.SelectedItem?.Content?.ToggleAutoResize(false);
                        return;
                    }

                    if (Vm.SelectedItem?.Content.CanResizeNow() != true)
                    {
                        return;
                    }
                    Vm?.SelectedItem?.Content?.ToggleAutoResize(true);
                    IoC.Get<LocalityService>().TabWindowState = this.WindowState;
                    SimpleLogHelper.DebugInfo($"(Window state changed) Tab size change to:W = {this.ActualWidth}, H = {this.ActualHeight}");
                };


                Closing += (_, args) =>
                {
                    if (this.GetViewModel().Items.Count > 0
                        && App.ExitingFlag == false
                        && IoC.Get<ConfigurationService>().General.ConfirmBeforeClosingSession == true
                        && false == MessageBoxHelper.Confirm(IoC.Translate("Are you sure you want to close the connection?"), ownerViewModel: Vm))
                    {
                        args.Cancel = true;
                    }
                };


                Closed += (_, _) =>
                {
                    _hwndSource?.RemoveHook(_wndProcHook);
                    TimerDispose();
                    ForegroundWinEventDispose();
                    try
                    {
                        var ids = Vm.Items.Select(x => x.Content.ConnectionId).ToArray();
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


                this.Activated += (_, _) =>
                {
                    this.StopFlashingWindow();
                    // Ensure taskbar icon is visible when window is activated (fixes Win11 issue where taskbar icon disappears when window loses focus)
                    this.ShowInTaskbar = true;
                    SaveCurrentScreenInfo();
                };

                if (!IsCreatedByDragDetach
                    && IoC.Get<LocalityService>().TabWindowState != System.Windows.WindowState.Minimized)
                {
                    this.WindowState = IoC.Get<LocalityService>().TabWindowState;
                }
            };
        }

        private IntPtr AdditionalWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETICON = 0x007F;
            const int ICON_BIG = 1;
            const int ICON_SMALL = 0;
            // const int ICON_SMALL2 = 2;

            if (msg == WM_GETICON)
            {
                // Abort the GETICON message under certain conditions to
                // prevent the Windows 10 Volume Mixer from picking up the
                // session icons. Not an elegant way, but it works.

                // Experiments confirmed that the following items display
                // session icon as before (as expected).
                //   This tab, Taskbar, Alt+Tab, Win+Tab, Task Manager

                int size = wParam.ToInt32();
                int dpi = lParam.ToInt32();
                if (dpi == 0 && (size == ICON_SMALL || size == ICON_BIG))
                {
                    handled = true;  // Abort
                }
            }
            return IntPtr.Zero;
        }

        private int? _startupScreenIndexOverride;

        private static bool IsScreenAvailable(int screenIndex)
        {
            if (screenIndex < 0 || screenIndex >= System.Windows.Forms.Screen.AllScreens.Length)
            {
                return false;
            }

            return IsScreenUsable(System.Windows.Forms.Screen.AllScreens[screenIndex]);
        }

        private static bool IsScreenUsable(System.Windows.Forms.Screen screen)
        {
            if (screen == null)
            {
                return false;
            }

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

        public void PrepareStartupDisplay(bool preferLastOpenedDisplay)
        {
            _startupScreenIndexOverride = null;
            if (!preferLastOpenedDisplay)
            {
                return;
            }

            var locality = IoC.Get<LocalityService>();
            if (string.IsNullOrWhiteSpace(locality.TabWindowScreenLayoutSignature))
            {
                return;
            }

            var currentSignature = BuildScreenLayoutSignature();
            if (!string.Equals(locality.TabWindowScreenLayoutSignature, currentSignature, StringComparison.Ordinal))
            {
                ClearSavedDisplaySelection(locality);
                return;
            }

            var index = locality.TabWindowLastScreenIndex;
            if (!IsScreenAvailable(index))
            {
                ClearSavedDisplaySelection(locality);
                return;
            }

            _startupScreenIndexOverride = index;
        }

        private static void ClearSavedDisplaySelection(LocalityService locality)
        {
            locality.TabWindowScreenLayoutSignature = "";
            locality.TabWindowLastScreenIndex = -1;
        }

        private ScreenInfoEx GetStartupScreen()
        {
            if (_startupScreenIndexOverride.HasValue)
            {
                var targetScreen = ScreenInfoEx.GetCurrentScreen(_startupScreenIndexOverride.Value);
                if (targetScreen != null && IsScreenUsable(targetScreen.Screen))
                {
                    return targetScreen;
                }
                _startupScreenIndexOverride = null;
            }

            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (IsScreenUsable(primaryScreen))
            {
                return new ScreenInfoEx(primaryScreen);
            }

            if (IoC.TryGet<MainWindowView>() is MainWindowView mainWindow && mainWindow.IsLoaded)
            {
                var mainScreen = ScreenInfoEx.GetCurrentScreen(mainWindow);
                if (IsScreenUsable(mainScreen.Screen))
                {
                    return mainScreen;
                }
            }

            var fallbackScreen = System.Windows.Forms.Screen.AllScreens
                .Where(screen => IsScreenUsable(screen))
                .OrderBy(screen => screen.Primary ? 0 : 1)
                .ThenBy(screen => screen.Bounds.Left)
                .FirstOrDefault();
            if (fallbackScreen != null)
            {
                return new ScreenInfoEx(fallbackScreen);
            }

            return new ScreenInfoEx(System.Windows.Forms.Screen.PrimaryScreen);
        }

        private void SaveCurrentScreenInfo()
        {
            if (!this.IsLoaded)
            {
                return;
            }

            var locality = IoC.Get<LocalityService>();
            var currentScreen = ScreenInfoEx.GetCurrentScreen(this);
            if (!IsScreenUsable(currentScreen.Screen))
            {
                ClearSavedDisplaySelection(locality);
                return;
            }

            locality.TabWindowScreenLayoutSignature = BuildScreenLayoutSignature();
            locality.TabWindowLastScreenIndex = currentScreen.Index;
        }

        private static string BuildScreenLayoutSignature()
        {
            var screens = System.Windows.Forms.Screen.AllScreens
                .Where(screen => IsScreenUsable(screen))
                .Select((screen, index) =>
                    $"{index}:{screen.DeviceName}:{screen.Bounds.X},{screen.Bounds.Y},{screen.Bounds.Width},{screen.Bounds.Height}:{screen.WorkingArea.X},{screen.WorkingArea.Y},{screen.WorkingArea.Width},{screen.WorkingArea.Height}:{screen.Primary}")
                .ToArray();
            return string.Join("|", screens);
        }

        private void InitWindowSizeOnLoaded()
        {
            var screenEx = GetStartupScreen();
            var leftTopOfCurrentScreen = new Point(screenEx.VirtualWorkingArea.X, screenEx.VirtualWorkingArea.Y);
            var rightBottomOfCurrentScreen = new Point(screenEx.VirtualWorkingArea.X + screenEx.VirtualWorkingArea.Width, screenEx.VirtualWorkingArea.Y + screenEx.VirtualWorkingArea.Height);
            if (WindowState == System.Windows.WindowState.Maximized)
            {

            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Width = IoC.Get<LocalityService>().TabWindowWidth;
                this.Height = IoC.Get<LocalityService>().TabWindowHeight;
                // check current screen size
                if (IoC.Get<LocalityService>().TabWindowTop <= leftTopOfCurrentScreen.Y - TITLE_BAR_HEIGHT                                // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowTop > rightBottomOfCurrentScreen.Y                                             // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowLeft > rightBottomOfCurrentScreen.X                                            // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowLeft + IoC.Get<LocalityService>().TabWindowWidth < leftTopOfCurrentScreen.X              // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowTop + IoC.Get<LocalityService>().TabWindowHeight / 2 < leftTopOfCurrentScreen.Y          // check if the center of tab window local in current screen
                    || IoC.Get<LocalityService>().TabWindowTop + IoC.Get<LocalityService>().TabWindowHeight / 2 > rightBottomOfCurrentScreen.Y      // check if the center of tab window local in current screen
                    || IoC.Get<LocalityService>().TabWindowLeft + IoC.Get<LocalityService>().TabWindowWidth / 2 < leftTopOfCurrentScreen.X          // check if the center of tab window local in current screen
                    || IoC.Get<LocalityService>().TabWindowLeft + IoC.Get<LocalityService>().TabWindowWidth / 2 > rightBottomOfCurrentScreen.X      // check if the center of tab window local in current screen
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
                    this.Top = IoC.Get<LocalityService>().TabWindowTop;
                    this.Left = IoC.Get<LocalityService>().TabWindowLeft;
                }
            }
            SaveCurrentScreenInfo();
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
                if (!this.IsLoaded || TabablzControl == null) return;
                Debug.Assert(this.Resources["TabContentBorderWithColor"] != null);
                Debug.Assert(this.Resources["TabContentBorderWithOutColor"] != null);
                var tabContentBorderWithColor = (Thickness)this.Resources["TabContentBorderWithColor"];
                var tabContentBorderWithOutColor = (Thickness)this.Resources["TabContentBorderWithOutColor"];

                var screenEx = ScreenInfoEx.GetCurrentScreen(this);
                double actualWidth = TabablzControl.ActualWidth;
                double actualHeight = this.WindowState == WindowState.Maximized ? screenEx.VirtualWorkingArea.Height : TabablzControl.ActualHeight;
                double border1 = withoutBorderColor ? tabContentBorderWithOutColor.Left + tabContentBorderWithOutColor.Right : tabContentBorderWithColor.Left + tabContentBorderWithColor.Right;
                double border2 = withoutBorderColor ? tabContentBorderWithOutColor.Top + tabContentBorderWithOutColor.Bottom : tabContentBorderWithColor.Top + tabContentBorderWithColor.Bottom;
                size.Width = actualWidth - border1;
                size.Height = actualHeight - TITLE_BAR_HEIGHT - border2;
            });
            return size;
        }



        /// <summary>
        /// double click title bar to Maximized
        /// </summary>
        public override void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (Vm.SelectedItem?.Content.CanResizeNow() == false)
                    return;
            }
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }



        private void TabablzControl_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // focus to be on the session content (e.g. the integrated exe) after clicking on the tab label.
            // The tunneling PreviewMouseLeftButtonUp arrives before the button Click (raised later in the
            // same input message), and focusing an integrated exe runs SetForegroundWindow which
            // deactivates this window - doing that any earlier would swallow the pending click (tab
            // switching, the close/reconnect buttons on the label, ...). Hence defer to Background
            // priority so it runs strictly after the click has been dispatched.
            Dispatcher.InvokeAsync(() =>
            {
                if (IsLoaded)
                {
                    FocusOnTabContent();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        public override void WinTitleBar_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var isDragging = _isDragging;
            base.WinTitleBar_OnPreviewMouseMove(sender, e);
            // When the window drag ends, focus the session content (e.g. the integrated exe)
            if (isDragging && !_isDragging)
            {
                FocusOnTabContent();
            }
        }
    }
}
