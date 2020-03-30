using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Shawn.Ulits.RDP
{
    /// <summary>
    /// AxMsRdpClient09Host.xaml 的交互逻辑
    /// </summary>
    public partial class AxMsRdpClient09Host : UserControl
    {
        private readonly AxMsRdpClient9NotSafeForScriptingEx _rdp = null;

        /*
            0 The control is not connected.
            1 The control is connected.
            2 The control is establishing a connection.
            @ref https://docs.microsoft.com/en-us/windows/win32/termserv/imstscax-connected
         */
        public bool IsConnected => _rdp?.Connected > 0;

        public AxMsRdpClient09Host()
        {
            InitializeComponent();
        }


        


        #region WindowOnResizeEnd
        public delegate void ResizeEndDelegage();

        public ResizeEndDelegage OnResizeEnd;

        private readonly System.Timers.Timer __resizeEndTimer = new System.Timers.Timer(500) { Enabled = false };
        private object __resizeEndLocker = new object();
        private bool __resizeEndStartFire = false;

        private void ResizeEndStartFireDelegate()
        {
            if (__resizeEndStartFire == false)
                lock (__resizeEndLocker)
                {
                    if (__resizeEndStartFire == false)
                    {
                        __resizeEndStartFire = true;
                        __resizeEndTimer.Elapsed += InvokeResizeEndEnd;
                        this.SizeChanged += _ResizeEnd_WindowSizeChanged;
                    }
                }
        }
        private void ResizeEndStopFireDelegate()
        {
            if (__resizeEndStartFire == true)
                lock (__resizeEndLocker)
                {
                    if (__resizeEndStartFire == true)
                    {
                        __resizeEndStartFire = false;
                        __resizeEndTimer.Stop();
                        try
                        {
                            __resizeEndTimer.Elapsed -= InvokeResizeEndEnd;
                        }
                        catch (Exception e)
                        {
                            // ignored
                        }

                        try
                        {
                            this.SizeChanged -= _ResizeEnd_WindowSizeChanged;
                        }
                        catch (Exception e)
                        {
                            // ignored
                        }
                    }
                }
        }

        private void _ResizeEnd_WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            __resizeEndTimer.Stop();
            __resizeEndTimer.Start();
        }

        private void InvokeResizeEndEnd(object sender, ElapsedEventArgs e)
        {
            __resizeEndTimer.Stop();
            OnResizeEnd?.Invoke();
        }
        #endregion
    }
}
