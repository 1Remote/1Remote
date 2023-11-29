using System.Runtime.InteropServices;
using System.Timers;
using System;
using System.Windows.Forms;
using Shawn.Utils;
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
    }
}