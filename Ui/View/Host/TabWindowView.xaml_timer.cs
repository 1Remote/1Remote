using System.Runtime.InteropServices;
using System.Timers;
using System;
using System.Windows;
using System.Windows.Forms;
using _1RM.Model.Protocol;
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
                RunForRdp();
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


        private int _rdpStage = 0; // 0 - not connected, 1 - RDP got focus, 2 - RDP lost focus desk got focus(focus can rollback to RDP), 3 - RDP lost focus desk lost focus (focus can not rollback to RDP)
        private void RunForRdp()
        {
            if (Vm?.SelectedItem?.Content?.ProtocolServer.Protocol != RDP.ProtocolName)
                return;
            if (Vm?.SelectedItem?.Content?.Status != ProtocolHosts.ProtocolHostStatus.Connected)
                return;

            var nowActivatedWindowHandle = GetForegroundWindow();
            var desktopHandle = GetDesktopWindow();

#if DEBUG
            SimpleLogHelper.Debug($"tabHwnd = {_myHandle}, nowActivatedWindowHandle = {nowActivatedWindowHandle}, desktopHandle = {desktopHandle}");
#endif

            bool isMouseInside = IsMouseInside(this);

            if (_rdpStage == 1 && !isMouseInside)
            {
                // 1 - RDP got focus  AND mouse is not inside the tab window, then switch focus to desktop, user input will not be sent to RDP
                _rdpStage = 2;
                SetForegroundWindow(desktopHandle);
            }
            else if (_rdpStage == 2)
            {
                // if focus to other window, then stage = 3
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
                // 3 - neither RDP nor local desk lost focus, can not rollback to RDP, do nothing
            }

            if (_rdpStage != 1 && isMouseInside && _myHandle == nowActivatedWindowHandle)
            {
                _rdpStage = 1;
            }
        } 
        #endregion
    }
}