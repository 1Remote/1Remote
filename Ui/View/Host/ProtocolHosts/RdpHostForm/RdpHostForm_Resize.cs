using System;
using System.Threading;
using _1RM.Model.Protocol;
using System.Windows.Forms;
using System.Windows;
using System.Timers;
using Shawn.Utils;
using Stylet;
using System.Threading.Tasks;
using System.Windows.Input;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class RdpHostForm : HostBaseWinform
    {
#if DEV_RDP
        public Window? ParentWindow { get; set; }
#endif

        private readonly System.Timers.Timer _loginResizeTimer = new System.Timers.Timer(300) { Enabled = false, AutoReset = false };

        private void ResizeInit()
        {
            _loginResizeTimer.Elapsed += (sender, args) =>
            {
                _loginResizeTimer.Stop();
                try
                {
                    var nw = (uint)(_rdpClient.Width);
                    var nh = (uint)(_rdpClient.Height);
                    // tip: the control default width is 288
                    if (_rdpClient.DesktopWidth > nw
                        || _rdpClient.DesktopHeight > nh)
                    {
                        SimpleLogHelper.DebugInfo($@"_loginResizeTimer start run... current w,h = {_rdpClient?.DesktopWidth}, {_rdpClient?.DesktopHeight}, new w,h = {nw}, {nh}");
                        ReSizeRdpToControlSize();
                    }
                    else
                    {
                        _lastLoginTime = DateTime.MinValue;
                    }
                }
                finally
                {
                    if (DateTime.Now < _lastLoginTime.AddMinutes(1))
                    {
                        _loginResizeTimer.Start();
                    }
                    else
                    {
                        SimpleLogHelper.DebugWarning($@"_loginResizeTimer stop");
                    }
                }
            };
            _loginResizeTimer.Start();



            ParentWindowResize_StartWatch();
        }



        #region WindowOnResizeEnd

        private readonly System.Timers.Timer _resizeEndTimer = new(500) { Enabled = false, AutoReset = false };
        private readonly object _resizeEndLocker = new();
        private bool _canAutoResizeByWindowSizeChanged = true;

        ///// <summary>
        ///// when tab window goes to min from max, base.SizeChanged invoke and size will get bigger, normal to min will not tiger this issue, don't know why.
        ///// so stop resize when window status change to min until status restore.
        ///// </summary>
        ///// <param name="isEnable"></param>
        //public override void ToggleAutoResize(bool isEnable)
        //{
        //    lock (_resizeEndLocker)
        //    {
        //        _canAutoResizeByWindowSizeChanged = isEnable;
        //    }
        //}

        private void ParentWindowResize_StartWatch()
        {
            lock (_resizeEndLocker)
            {
                _resizeEndTimer.Elapsed -= ResizeEndTimerOnElapsed;
                _resizeEndTimer.Elapsed += ResizeEndTimerOnElapsed;
                SizeChanged -= WindowSizeChanged;
                SizeChanged += WindowSizeChanged;
            }
        }

        private void ParentWindowResize_StopWatch()
        {
            lock (_resizeEndLocker)
            {
                _resizeEndTimer.Stop();
                _resizeEndTimer.Elapsed -= ResizeEndTimerOnElapsed;
                base.SizeChanged -= WindowSizeChanged;
            }
        }

        private uint _previousWidth = 0;
        private uint _previousHeight = 0;
        private void WindowSizeChanged(object sender, EventArgs e)
        {
            if (ParentWindow?.WindowState != System.Windows.WindowState.Minimized
                && _canAutoResizeByWindowSizeChanged
                && this._rdpSettings.RdpWindowResizeMode == ERdpWindowResizeMode.AutoResize)
            {
                // start a timer to resize RDP after 500ms
                var nw = (uint)this.Width;
                var nh = (uint)this.Height;
                if (nw != _previousWidth || nh != _previousHeight)
                {
                    _previousWidth = (uint)this.Width;
                    _previousHeight = (uint)this.Height;
                    Execute.OnUIThreadSync(() =>
                    {
                        _resizeEndTimer.Stop();
                        _resizeEndTimer.Start();
                    });
                }
            }
        }

        private void ResizeEndTimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            ReSizeRdpToControlSize();
        }

        #endregion WindowOnResizeEnd



        private static bool _isReSizeRdpToControlSizeRunning = false;
        void ReSizeRdpToControlSize()
        {
            if (_rdpClient.Connected != 1 // https://learn.microsoft.com/en-us/windows/win32/termserv/imstscax-connected
                || _rdpClient.FullScreen != false
                || _rdpSettings.RdpWindowResizeMode != ERdpWindowResizeMode.AutoResize) return;


            lock (this)
            {
                if (_isReSizeRdpToControlSizeRunning == true)
                {
                    SimpleLogHelper.Debug($@"ReSizeRdpToControlSize return by isReSizeRdpToControlSizeRunning == true");
                    return;
                }
                _isReSizeRdpToControlSizeRunning = true;
            }


            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    // Window drag an drop resize only after mouse button release, 当拖动最大化的窗口时，需检测鼠标按键释放后再调整分辨率，详见：https://github.com/1Remote/1Remote/issues/553
                    var isPressed = false;
                    Execute.OnUIThreadSync(() => { isPressed = Mouse.LeftButton == MouseButtonState.Pressed; });
                    if (!isPressed)
                        break;
#if DEBUG
                    SimpleLogHelper.Debug($@"RDP ReSizeRdpToControlSize  delay since mouse is pressed");
#endif
                    Thread.Sleep(300);
                }

                var nw = (uint)(_rdpClient.Width);
                var nh = (uint)(_rdpClient.Height);
                // tip: the control default width is 288
                if (_rdpClient.DesktopWidth != nw
                    || _rdpClient.DesktopHeight != nh)
                {
                    SetRdpResolution(nw, nh, false);
                }

                lock (this)
                {
                    _isReSizeRdpToControlSizeRunning = false;
                }
            });
        }


        private uint _lastScaleFactor = 0;
        /// <summary>
        /// if focus == false, then set size only if new size != old size
        /// </summary>
        private void SetRdpResolution(uint w, uint h, bool focus = false)
        {
            if (w <= 0 || h <= 0) return;

            lock (_resizeEndLocker)
            {
                if (_canAutoResizeByWindowSizeChanged == false) return;
            }

            _primaryScaleFactor = ScreenInfoEx.GetPrimaryScreenScaleFactor();
            var newScaleFactor = _primaryScaleFactor;
            if (this._rdpSettings is { IsScaleFactorFollowSystem: false, ScaleFactorCustomValue: { } })
                newScaleFactor = this._rdpSettings.ScaleFactorCustomValue ?? _primaryScaleFactor;
            bool needUpdate = focus
                         || _rdpClient.DesktopWidth != w
                         || _rdpClient.DesktopHeight != h
                         || newScaleFactor != _lastScaleFactor;
            if (newScaleFactor != 100)
            {
                // in this case we allow 1pix error
                needUpdate = focus
                        || Math.Abs((int)(_rdpClient.DesktopWidth) - (int)w) > 1
                        || Math.Abs((int)(_rdpClient.DesktopHeight) - (int)h) > 1
                        || newScaleFactor != _lastScaleFactor;
            }
            SimpleLogHelper.Debug($@"SetRdpResolution needUpdate = {needUpdate}, UpdateSessionDisplaySettings, by: W = {_rdpClient.DesktopWidth} -> {w}, H = {_rdpClient.DesktopHeight} -> {h}, ScaleFactor = {_lastScaleFactor} -> {newScaleFactor}, focus = {focus}");
            if (needUpdate)
                Execute.OnUIThreadSync(() =>
                {
                    try
                    {
                        _lastScaleFactor = newScaleFactor;
                        _rdpClient.UpdateSessionDisplaySettings(w, h, w, h, 0, newScaleFactor, 100);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e);
                    }
                });
        }
    }
}
