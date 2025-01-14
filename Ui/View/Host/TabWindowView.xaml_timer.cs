using System.Runtime.InteropServices;
using System.Timers;
using System;
using System.Windows;
using System.Windows.Forms;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Stylet;
using ProtocolHostType = _1RM.View.Host.ProtocolHosts.ProtocolHostType;
using Timer = System.Timers.Timer;
using System.Windows.Interop;

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
                RunForRdpWinform();
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

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private void RunForIntegrate()
        {
            if (Vm?.SelectedItem?.Content?.GetProtocolHostType() != ProtocolHostType.Integrate)
                return;

            var hWnd = this.Vm.SelectedItem.Content.GetHostHwnd();
            if (hWnd == IntPtr.Zero) return;

            var nowActivatedWindowHandle = GetForegroundWindow();

            // bring Tab window to top, when the host content is Integrate.
            if (nowActivatedWindowHandle == hWnd && nowActivatedWindowHandle != _lastActivatedWindowHandle)
            {
                SimpleLogHelper.Debug($@"TabWindowView: _lastActivatedWindowHandle = ({_lastActivatedWindowHandle})
TabWindowView: nowActivatedWindowHandle = ({nowActivatedWindowHandle}), hWnd = {hWnd}
TabWindowView: BringWindowToTop({_myHandle})");
                BringWindowToTop(_myHandle);
            }
            // focus content when tab is focused and host is Integrate and left mouse is not pressed
            else if (nowActivatedWindowHandle == _myHandle && System.Windows.Forms.Control.MouseButtons != MouseButtons.Left)
            {
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

        private static bool IsMouseInside(Window window, IntPtr windowHandle, IntPtr rdpHandle)
        {
            Point mousePos = GetMousePosition();
            Point windowPos = new Point(-1, -1);
            Point windowBottomRight = new Point(-1, -1);
            Execute.OnUIThreadSync(() =>
            {
                windowPos = window.PointToScreen(new Point(0, 0));
                windowBottomRight = window.PointToScreen(new Point(window.Width, window.Height));
            });
            bool isInside = mousePos.X >= windowPos.X && mousePos.X <= windowBottomRight.X && mousePos.Y >= windowPos.Y && mousePos.Y <= windowBottomRight.Y;

            //if (isInside)
            //{
            //    // 获取鼠标当前位置的窗口句柄
            //    POINT pt = new POINT((int)mousePos.X, (int)mousePos.Y);
            //    IntPtr hWndAtPoint = WindowFromPoint(pt);
            //    SimpleLogHelper.Debug($@"WindowFromPoint = {hWndAtPoint}, windowHandle = {windowHandle}, rdpHandle = {rdpHandle}");
            //    //// 获取当前窗口的句柄
            //    //IntPtr hWndCurrent = new WindowInteropHelper(window).Handle;
            //    // 检查鼠标位置的窗口是否是当前窗口
            //    if (hWndAtPoint != windowHandle && hWndAtPoint != rdpHandle)
            //    {
            //        isInside = false;
            //    }
            //}

            return isInside;
        }


        private void RunForRdp()
        {
            if (Vm?.SelectedItem?.Content?.ProtocolServer.Protocol != RDP.ProtocolName)
                return;
            if (Vm?.SelectedItem?.Content is not IntegrateHostForWinFrom ihfw)
                return;

            // Fix the resizing bug introduced by #648, see https://github.com/1Remote/1Remote/issues/797 for more details
            bool isMousePressed = System.Windows.Forms.Control.MouseButtons == MouseButtons.Left
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Right
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Middle;
            if (isMousePressed)
            {
//#if DEBUG
//				SimpleLogHelper.Debug("Tab focus: Mouse is pressed, do nothing");
//#endif
                return;
            }

            var nowActivatedWindowHandle = GetForegroundWindow();
            var desktopHandle = GetDesktopWindow();
            var rdpHandle = ihfw.GetHostHwnd();
            bool isMouseInside = IsMouseInside(this, _myHandle, rdpHandle);
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
        }





        private void RunForRdpWinform()
        {
            if (Vm?.SelectedItem?.Content?.ProtocolServer.Protocol != RDP.ProtocolName)
                return;
            if (Vm?.SelectedItem?.Content is not IntegrateHostForWinFrom ihfw)
                return;

            // Fix the resizing bug introduced by #648, see https://github.com/1Remote/1Remote/issues/797 for more details
            bool isMousePressed = System.Windows.Forms.Control.MouseButtons == MouseButtons.Left
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Right
                                  || System.Windows.Forms.Control.MouseButtons == MouseButtons.Middle;
            if (isMousePressed)
            {
                //#if DEBUG
                //				SimpleLogHelper.Debug("Tab focus: Mouse is pressed, do nothing");
                //#endif
                return;
            }

            var nowActivatedWindowHandle = GetForegroundWindow();
            var desktopHandle = GetDesktopWindow();
            var rdpHandle = ihfw.GetHostHwnd();
            bool isMouseInside = IsMouseInside(this, _myHandle, rdpHandle);
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
        }

        #endregion
    }
}