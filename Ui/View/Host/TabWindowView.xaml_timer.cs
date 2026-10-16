using System.Runtime.InteropServices;
using System.Timers;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Stylet;
using ProtocolHostType = _1RM.View.Host.ProtocolHosts.ProtocolHostType;
using Timer = System.Timers.Timer;

namespace _1RM.View.Host
{
    public partial class TabWindowView
    {
        private readonly Timer _timer4CheckForegroundWindow = new Timer();

        private void TimerInitOnLoaded()
        {
            _timer4CheckForegroundWindow.Interval = 100;
            _timer4CheckForegroundWindow.AutoReset = false;
            _timer4CheckForegroundWindow.Elapsed += Timer4CheckForegroundWindowOnElapsed;
            _timer4CheckForegroundWindow.Start();
        }

        private void TimerDispose()
        {
            try
            {
                _timer4CheckForegroundWindow?.Dispose();
            }
            finally
            {
            }
        }

        // The timer is kept for RunForRdpV2 only (mouse-position driven RDP focus).
        // The integrate/foreground logic formerly polled here is now event driven, see OnForegroundWinEvent.
        private void Timer4CheckForegroundWindowOnElapsed(object? sender, ElapsedEventArgs e)
        {
            _timer4CheckForegroundWindow.Stop();
            try
            {
                RunForRdpV2();
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


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Focus the session content (e.g. the integrated exe) when the tab label is clicked
        /// or a window drag ends, so that the focus is not lost after clicking on the tab label.
        /// </summary>
        private void FocusOnTabContent()
        {
            // runs in deferred dispatcher callbacks (FocusOnTabContentWhenMouseReleased /
            // TabablzControl_OnPreviewMouseLeftButtonUp) which carry no exception handling:
            // FocusOnMe may throw on a disposed host (e.g. a closed RDP ActiveX), and an
            // unhandled dispatcher exception would pop the ErrorReport window.
            try
            {
                var content = Vm?.SelectedItem?.Content;
                if (content == null)
                    return;
                // For an integrated exe: only focus it while its Win32 root ancestor is this window.
                // While a tab is being detached the reparenting is not finalized yet and the exe still
                // roots at the old window - SetForegroundWindow on it would then activate the OLD
                // window (e.g. the source tab window takes the foreground instead of the detached one).
                if (content.GetProtocolHostType() == ProtocolHostType.Integrate)
                {
                    var exeHandle = GetHostHwndSafe(content);
                    var root = GetAncestor(exeHandle, GaRoot);
                    if (root != _tabWindowHandle)
                    {
                        SimpleLogHelper.Debug($"TabWindowView.FocusOnTabContent: skip focus, exe={exeHandle} still roots at {root} instead of tab={_tabWindowHandle}");
                        return;
                    }
                }
                content.FocusOnMe();
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning(ex);
            }
        }

        /// <summary>
        /// Focus the session content once the current window activation has settled and the mouse
        /// button is released (the user may still be holding the taskbar button or the tab label).
        /// Moving the focus to an integrated exe runs SetForegroundWindow on its child window:
        /// doing that from inside the foreground-change notification races with the activation
        /// sequence still in progress and may leave the tab window behind other windows (the
        /// taskbar highlights the tab, but the window stays obscured and the exe gets no input).
        /// </summary>
        private void FocusOnTabContentWhenMouseReleased()
        {
            Dispatcher.InvokeAsync(async () =>
            {
                // wait until the mouse button is released - without a timeout on purpose: while it
                // is held (dragging a tab out, holding a taskbar button, ...) no focus move may
                // intervene. The wait ends with the release or when the window is closed.
                while (IsLoaded && System.Windows.Forms.Control.MouseButtons != MouseButtons.None)
                {
                    await System.Threading.Tasks.Task.Delay(50);
                }
                // do not steal the focus back if the user has already switched away meanwhile
                if (IsLoaded && GetForegroundWindow() == _tabWindowHandle)
                {
                    FocusOnTabContent();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        #region Foreground window change notification (replaces the former polling in RunForIntegrate)

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint OBJID_WINDOW = 0x0000;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        // MUST be kept in a field: if the delegate is garbage collected while the system still
        // holds a pointer to it, the process crashes when the next event is dispatched.
        private WinEventDelegate? _foregroundWinEventProc;
        private IntPtr _hForegroundWinEventHook;

        private void ForegroundWinEventInit()
        {
            if (_hForegroundWinEventHook != IntPtr.Zero)
                return;
            _foregroundWinEventProc = OnForegroundWinEvent;
            // WINEVENT_OUTOFCONTEXT: the callback is dispatched on the thread that installs the
            // hook while it pumps messages, i.e. the WPF UI thread here.
            _hForegroundWinEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _foregroundWinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

            // WinEvents are only reported from now on: mimic the first tick of the former timer,
            // which focused the content when the tab window was already in the foreground.
            HandleForegroundWindowChanged(GetForegroundWindow());
        }

        private void ForegroundWinEventDispose()
        {
            if (_hForegroundWinEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_hForegroundWinEventHook);
                _hForegroundWinEventHook = IntPtr.Zero;
            }
            _foregroundWinEventProc = null;
        }

        /// <summary>
        /// 1. If the integrated exe became the foreground window, bring the tab window to the top
        ///    of the z-order once (BringWindowToTop, no activation), so a tab that was obscured by
        ///    other windows shows up on top while the focus stays on the exe.
        /// 2. If the tab window itself became the foreground window, move the focus into the session
        ///    content (all protocols, like the old timer did), deferred until the activation settles
        ///    and the mouse button is released (see FocusOnTabContentWhenMouseReleased).
        /// </summary>
        private void OnForegroundWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != OBJID_WINDOW || hwnd == IntPtr.Zero)
                return;
            HandleForegroundWindowChanged(hwnd);
        }

        private void HandleForegroundWindowChanged(IntPtr foregroundHwnd)
        {
            if (_tabWindowHandle == IntPtr.Zero || foregroundHwnd == IntPtr.Zero)
                return;
            try
            {
                var content = Vm?.SelectedItem?.Content;
                if (content == null)
                    return;
                var isIntegrate = content.GetProtocolHostType() == ProtocolHostType.Integrate;
                var exeHandle = GetHostHwndSafe(content);

                if (isIntegrate && foregroundHwnd == exeHandle)
                {
                    // the integrated exe got the foreground: raise the tab window in the z-order
                    // without activating it, so the focus remains on the exe.
                    // Done unconditionally: BringWindowToTop is idempotent for a window already on
                    // top, while skipping it after a GetWindow check raced with the z-order still
                    // being updated and could leave the tab window behind other windows.
                    SimpleLogHelper.Debug($@"TabWindowView.OnForegroundWinEvent: BringWindowToTop({_tabWindowHandle}) for the integrated exe {foregroundHwnd}");
                    BringWindowToTop(_tabWindowHandle);
                    return;
                }

                if (foregroundHwnd == _tabWindowHandle)
                {
                    // deferred: wait for the activation to settle and the mouse to be released,
                    // otherwise SetForegroundWindow(exe) races with the activation in progress.
                    FocusOnTabContentWhenMouseReleased();
                }
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning(ex);
            }
        }

        private static IntPtr GetHostHwndSafe(HostBase? host)
        {
            try
            {
                return host?.GetHostHwnd() ?? IntPtr.Zero;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"Failed to get host hwnd: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        #endregion

        /****
         * THE PURPOSE OF THIS FUNCTION IS TO:
         * - LET YOUR LOCAL DESKTOP WINDOW GET FOCUS WHEN YOU MOVE THE CURSOR OUT OF THE RDP WINDOW
         * - LET THE RDP WINDOW GET FOCUS WHEN YOU MOVE THE CURSOR INTO THE RDP WINDOW
         * - CAUTION: PAY ATTENTION TO THE RESIZE OF THE RDP WINDOW, IT MAY CAUSE THE CURSOR TO MOVE OUT OF THE RDP WINDOW, SO WE NEED TO CHECK IF THE LEFT MOUSE BUTTON IS PRESSED OR NOT
         ***/

        #region RunForRdp

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Win32Point point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        private const uint GaRoot = 2;

        private static Point GetMousePosition()
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        private static bool IsMouseInside(Window window)
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            Point mousePos = new Point(w32Mouse.X, w32Mouse.Y);
            Point windowPos = new Point(-1, -1);
            Point windowBottomRight = new Point(-1, -1);
            IntPtr myHandle = IntPtr.Zero;
            Execute.OnUIThreadSync(() =>
            {
                if (!window.IsLoaded || window.WindowState == WindowState.Minimized) return; // avoid PointToScreen throwing exception when window is not loaded or minimized
                windowPos = window.PointToScreen(new Point(0, 0));
                windowBottomRight = window.PointToScreen(new Point(window.Width, window.Height));
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                myHandle = helper.Handle;
            });

            if (myHandle == IntPtr.Zero)
                return false;

            bool inRect = mousePos.X >= windowPos.X && mousePos.X <= windowBottomRight.X && mousePos.Y >= windowPos.Y && mousePos.Y <= windowBottomRight.Y;
            if (!inRect)
                return false;

            var hitWindow = WindowFromPoint(w32Mouse);
            if (hitWindow == IntPtr.Zero)
                return false;

            var hitRoot = GetAncestor(hitWindow, GaRoot);
            if (hitRoot != IntPtr.Zero && hitRoot != myHandle)
                return false;

#if DEBUG
            var r = true;
            //SimpleLogHelper.Debug($@"TabWindowView IsMouseInside = {r}: mousePos = ({mousePos.X}, {mousePos.Y}), windowPos = ({windowPos.X}, {windowPos.Y}), windowBottomRight = ({windowBottomRight.X}, {windowBottomRight.Y})");
#endif
            return true;
        }


        private int _rdpStage = 0; // flag: 0 - not connected, 1 - RDP got focus, 2 - RDP lost focus desk got focus(focus can rollback to RDP), 3 - RDP lost focus desk lost focus (focus can cannot rollback to RDP)

        private void RunForRdp()
        {
            if (Vm?.SelectedItem?.Content?.ProtocolServer.Protocol != RDP.ProtocolName)
                return;
            if (Vm?.SelectedItem?.Content?.Status != ProtocolHosts.ProtocolHostStatus.Connected)
                return;

            // Fix the resizing bug introduced by #648, see https://github.com/1Remote/1Remote/issues/797 for more details
            bool isMousePressed = System.Windows.Forms.Control.MouseButtons == MouseButtons.Left
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Right
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Middle;
            if (isMousePressed)
            {
#if DEBUG
                SimpleLogHelper.Debug("Tab focus: Mouse is pressed, do nothing");
#endif
                return;
            }

            var nowActivatedWindowHandle = GetForegroundWindow();
            var desktopHandle = GetDesktopWindow();

#if DEBUG
            SimpleLogHelper.Debug($"Tab focus: tabHwnd = {_tabWindowHandle}, nowActivatedWindowHandle = {nowActivatedWindowHandle}, desktopHandle = {desktopHandle}");
#endif

            bool isMouseInside = IsMouseInside(this);

            if (_rdpStage == 1 && !isMouseInside)
            {
                // 1 - RDP has focus AND mouse is not inside the tab window, then switch focus to desktop, user input will not be sent to RDP
                _rdpStage = 2;
                SetForegroundWindow(desktopHandle);
            }
            else if (_rdpStage == 2)
            {
                // if focus is on another window, then stage = 3
                if (nowActivatedWindowHandle != desktopHandle)
                {
                    _rdpStage = 3;
                }
                // mouse back to tab window, then focus back to RDP
                else if (isMouseInside)
                {
                    SetForegroundWindow(_tabWindowHandle);
                    _rdpStage = 1;
                }
            }
            else if (_rdpStage == 3)
            {
                // 3 - neither RDP nor local desktop has focus, cannot rollback to RDP, do nothing
            }

            if (_rdpStage != 1 && isMouseInside && _tabWindowHandle == nowActivatedWindowHandle)
            {
                _rdpStage = 1;
            }
        }


        private void RunForRdpV2()
        {
            if (Vm?.SelectedItem?.Content?.ProtocolServer.Protocol != RDP.ProtocolName)
                return;
            if (Vm?.SelectedItem?.Content?.Status != ProtocolHosts.ProtocolHostStatus.Connected)
                return;
            if (IoC.Get<ConfigurationService>().General.TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow)
            {
                bool isMousePressed = System.Windows.Forms.Control.MouseButtons == MouseButtons.Left
                                      || System.Windows.Forms.Control.MouseButtons == MouseButtons.Right
                                      || System.Windows.Forms.Control.MouseButtons == MouseButtons.Middle;
                var nowActivatedWindowHandle = GetForegroundWindow();
                IntPtr rdpHandle = IntPtr.Zero;
                if (Vm?.SelectedItem?.Content is AxMsRdpClient09Host rdpHost)
                {
                    rdpHandle = _tabWindowHandle;
                }
                else
                {
                    // rdpHandle = ihfw.GetHostHwnd();
                    // throw new NotImplementedException();
                    return;
                }

                if (IsMouseInside(this))
                {
                    if (nowActivatedWindowHandle != rdpHandle)
                    {
                        SimpleLogHelper.Debug("TabWindowView.RunForRdpV2: SetForegroundWindow(rdpHandle)");
                        SetForegroundWindow(rdpHandle);
                    }
                }
                else if (nowActivatedWindowHandle == rdpHandle)
                {
                    // !isMousePressed is to fix the resizing bug introduced by #648
                    // Stay focused while the mouse is pressed to avoid losing focus when resizing the RDP window,
                    // see https://github.com/1Remote/1Remote/issues/797 for more details
                    if (!isMousePressed)
                    {
                        // RDP has focus AND mouse is not inside the tab window, then switch focus to desktop, user input will not be sent to RDP.
                        SimpleLogHelper.Debug("TabWindowView.RunForRdpV2: SetForegroundWindow(desktop)");
                        SetForegroundWindow(GetDesktopWindow());
                    }
                }
            }

            #endregion
        }
    }
}
