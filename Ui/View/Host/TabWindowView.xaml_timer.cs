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

        private IntPtr _lastActivatedWindowHandle = IntPtr.Zero;

        private void Timer4CheckForegroundWindowOnElapsed(object? sender, ElapsedEventArgs e)
        {
            _timer4CheckForegroundWindow.Stop();
            try
            {
                RunForRdpV2();
                RunForIntegrate();
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
        /// 0. Record the current ActivatedWindowHandle every time
        /// 1. If the current ActivatedWindowHandle is the integrated exe, move the Tab to the foreground one time (BringWindowToTop(_myHandle);, achieving that after clicking the integrated exe, the tab is brought to the front and not obscured by other programs.
        /// 2. If isTimer is False and the current focus is on the Tab, then set the focus on the integrated exe. (To ensure that the focus is not lost after clicking on the tab label)
        /// </summary>
        private void RunForIntegrate()
        {
            IntPtr hWnd = IntPtr.Zero;
            if (Vm?.SelectedItem?.Content?.GetProtocolHostType() == ProtocolHostType.Integrate)
            {
                try
                {
                    hWnd = this.Vm.SelectedItem.Content.GetHostHwnd();
                }
                catch (Exception ex)
                {
                    SimpleLogHelper.Warning($"Failed to get host hwnd: {ex.Message}");
                }
            }

            var nowActivatedWindowHandle = GetForegroundWindow();
            if (hWnd != IntPtr.Zero)
            {
                //SimpleLogHelper.Debug($"TabWindowView: isTimer = {isTimer}, nowActivatedWindowHandle = {nowActivatedWindowHandle}, _lastActivatedWindowHandle = {_lastActivatedWindowHandle}, _myHandle = {_myHandle}");
                // bring Tab window to top, when the host content is Integrate.
                if (nowActivatedWindowHandle == hWnd && _lastActivatedWindowHandle != hWnd)
                {
                    SimpleLogHelper.Debug($@"TabWindowView: BringWindowToTop({_myHandle})");
                    BringWindowToTop(_myHandle);
                }
            }

            // focus content when tab is focused when the focus is back to tab window
            /****
             * In the past, the following if statement included the additional condition
             * `&& System.Windows.Forms.Control.MouseButtons != MouseButtons.Left`,
             * and the following comment explains why it is necessary:
             *
             * "why `System.Windows.Forms.Control.MouseButtons != MouseButtons.Left` is
             *  needed: Without IT, once the tab gains focus, the timer will immediately
             *  transfer the focus to the integrated window, causing the tab to be
             *  unselectable or undraggable."
             *
             * However, it had to be removed to resolve issue #1052.
             * Even after its removal, the undesirable behavior described in the comment
             * did not occur.
             *
             * 2026-02-06 update: After further testing, removal of the condition will cause
             * the following issue: once the tab gains focus, the timer will immediately
             * transfer the focus to the integrated window, causing the tab(with a ssh session)
             * to be `unable to resize` or `close button not working`, so I add it back temporarily: https://github.com/1Remote/1Remote/issues/1066
             ***/
            if (nowActivatedWindowHandle == _myHandle && _lastActivatedWindowHandle != _myHandle 
                                                      && System.Windows.Forms.Control.MouseButtons != MouseButtons.Left)
            {
                SimpleLogHelper.Debug($@"TabWindowView: Vm?.SelectedItem?.Content?.FocusOnMe()");
                Vm?.SelectedItem?.Content?.FocusOnMe();
            }
            _lastActivatedWindowHandle = nowActivatedWindowHandle;
        }

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
            Point mousePos = GetMousePosition();
            Point windowPos = new Point(-1, -1);
            Point windowBottomRight = new Point(-1, -1);
            Execute.OnUIThreadSync(() =>
            {
                windowPos = window.PointToScreen(new Point(0, 0));
                windowBottomRight = window.PointToScreen(new Point(window.Width, window.Height));
            });
#if DEBUG
            var r = mousePos.X >= windowPos.X && mousePos.X <= windowBottomRight.X && mousePos.Y >= windowPos.Y &&
                    mousePos.Y <= windowBottomRight.Y;
            SimpleLogHelper.Debug($@"TabWindowView IsMouseInside = {r}: mousePos = ({mousePos.X}, {mousePos.Y}), windowPos = ({windowPos.X}, {windowPos.Y}), windowBottomRight = ({windowBottomRight.X}, {windowBottomRight.Y})");
#endif
            return mousePos.X >= windowPos.X && mousePos.X <= windowBottomRight.X && mousePos.Y >= windowPos.Y && mousePos.Y <= windowBottomRight.Y;
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
            SimpleLogHelper.Debug($"Tab focus: tabHwnd = {_myHandle}, nowActivatedWindowHandle = {nowActivatedWindowHandle}, desktopHandle = {desktopHandle}");
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
                    SetForegroundWindow(_myHandle);
                    _rdpStage = 1;
                }
            }
            else if (_rdpStage == 3)
            {
                // 3 - neither RDP nor local desktop has focus, cannot rollback to RDP, do nothing
            }

            if (_rdpStage != 1 && isMouseInside && _myHandle == nowActivatedWindowHandle)
            {
                _rdpStage = 1;
            }
        }


        private void RunForRdpV2()
        {
            if(IoC.Get<ConfigurationService>().General.TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow == false)
                return;

            if (Vm?.SelectedItem?.Content?.ProtocolServer.Protocol != RDP.ProtocolName)
                return;
            //if (Vm?.SelectedItem?.Content is not IntegrateHostForWinFrom ihfw)
            //    return;
            if (Vm?.SelectedItem?.Content?.Status != ProtocolHosts.ProtocolHostStatus.Connected)
                return;

            // Fix the resizing bug introduced by #648, see https://github.com/1Remote/1Remote/issues/797 for more details
            bool isMousePressed = System.Windows.Forms.Control.MouseButtons == MouseButtons.Left
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Right
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Middle;
            if (isMousePressed)
            {
                //SimpleLogHelper.Debug("Tab focus: Mouse is pressed, do nothing");
                return;
            }

            var nowActivatedWindowHandle = GetForegroundWindow();
            var desktopHandle = GetDesktopWindow();
            IntPtr rdpHandle = IntPtr.Zero;
            if (Vm?.SelectedItem?.Content is AxMsRdpClient09Host rdpHost)
            {
                rdpHandle = _myHandle;
            }
            else
            {
                //rdpHandle = ihfw.GetHostHwnd();
                throw new NotImplementedException();
            }

            bool isMouseInside = IsMouseInside(this);
//#if DEBUG
//            SimpleLogHelper.Debug($"Tab focus: isMouseInside = {isMouseInside}, rdpHandle = {rdpHandle}, nowActivatedWindowHandle = {nowActivatedWindowHandle}, desktopHandle = {desktopHandle}");
//#endif
            if (!isMouseInside && rdpHandle == nowActivatedWindowHandle)
            {
                // 1 - RDP has focus AND mouse is not inside the tab window, then switch focus to desktop, user input will not be sent to RDP
                SetForegroundWindow(desktopHandle);
            }
            else if (isMouseInside && (nowActivatedWindowHandle == desktopHandle || nowActivatedWindowHandle == IntPtr.Zero))
            {
                // 2 - desktop has focus
                SetForegroundWindow(rdpHandle);
            }

            #endregion
        }
    }
}