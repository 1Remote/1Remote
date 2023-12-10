using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AxMSTSCLib;
using MSTSCLib;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class AxMsRdpClient09Host : HostBase, IDisposable
    {
        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            this.Dispose();
            base.OnClosed?.Invoke(base.ConnectionId);
        }
        private void BtnReconn_OnClick(object sender, RoutedEventArgs e)
        {
            ReConn();
        }

        public override void ReConn()
        {
            Debug.Assert(_rdpClient != null);
            if (Status != ProtocolHostStatus.Connected
                && Status != ProtocolHostStatus.Disconnected)
            {
                SimpleLogHelper.Warning($"RDP Host: Call ReConn, but current status = " + Status);
                return;
            }
            else
            {
                SimpleLogHelper.Warning($"RDP Host: Call ReConn");
            }
            Status = ProtocolHostStatus.WaitingForReconnect;

            RdpHost.Visibility = System.Windows.Visibility.Collapsed;
            GridLoading.Visibility = System.Windows.Visibility.Visible;
            GridMessageBox.Visibility = System.Windows.Visibility.Collapsed;
            RdpClientDispose();

            Status = ProtocolHostStatus.NotInit;

            int w = 0;
            int h = 0;
            if (ParentWindow is TabWindowView tab)
            {
                var size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(this._rdpSettings.ColorHex) == true);
                w = (int)size.Width;
                h = (int)size.Height;
            }
            InitRdp(w, h, true);
            Conn();
        }


        private void ParentWindowSetToWindow()
        {
            // make sure ParentWindow is FullScreen Window
            if (ParentWindow is not FullScreenWindowView)
            {
                return;
            }

            if (ParentWindow is FullScreenWindowView { IsLoaded: false })
            {
                return;
            }

            ParentWindow.Topmost = false;
            ParentWindow.ResizeMode = ResizeMode.CanResize;
            ParentWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            ParentWindow.WindowState = WindowState.Normal;
            ParentWindow.Width = FullScreenWindowView.DESIGN_WIDTH / (_primaryScaleFactor / 100.0);
            ParentWindow.Height = FullScreenWindowView.DESIGN_HEIGHT / (_primaryScaleFactor / 100.0);
            var screenEx = ScreenInfoEx.GetCurrentScreen(this.ParentWindow);
            ParentWindow.Top = screenEx.VirtualWorkingAreaCenter.Y - ParentWindow.Height / 2;
            ParentWindow.Left = screenEx.VirtualWorkingAreaCenter.X - ParentWindow.Width / 2;
        }



        #region event handler

        private void OnRdpClientDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            SimpleLogHelper.Debug("RDP Host: RdpOnDisconnected");

            lock (this)
            {
                var flagHasConnected = this._flagHasConnected;
                _flagHasConnected = false;

                Status = ProtocolHostStatus.Disconnected;
                ParentWindowResize_StopWatch();

                const int UI_ERR_NORMAL_DISCONNECT = 0xb08;
                string reason = _rdpClient?.GetErrorDescription((uint)e.discReason, (uint)_rdpClient.ExtendedDisconnectReason) ?? "";
                if (e.discReason != UI_ERR_NORMAL_DISCONNECT)
                    SimpleLogHelper.Warning($"RDP({_rdpSettings.DisplayName}) exit with error code {e.discReason}({reason})");

                // disconnectReasonByServer (3 (0x3))
                // https://docs.microsoft.com/zh-cn/windows/win32/termserv/imstscaxevents-ondisconnected?redirectedfrom=MSDN


                if (!string.IsNullOrWhiteSpace(reason)
                    && (flagHasConnected != true ||
                        e.discReason != UI_ERR_NORMAL_DISCONNECT
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedDisconnect
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedLogoff
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonNoInfo                // log out from win2008 will reply exDiscReasonNoInfo
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonLogoffByUser          // log out from win10 will reply exDiscReasonLogoffByUser
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonRpcInitiatedDisconnectByUser    // log out from win2016 will reply exDiscReasonLogoffByUser
                    ))
                {
                    BtnReconn.Visibility = Visibility.Collapsed;
                    RdpHost.Visibility = Visibility.Collapsed;
                    GridMessageBox.Visibility = Visibility.Visible;
                    if (flagHasConnected == true
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonReplacedByOtherConnection
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonOutOfMemory
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerDeniedConnection
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerDeniedConnectionFips
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerInsufficientPrivileges
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonNoInfo  // conn to a power-off PC will get exDiscReasonNoInfo
                        && _retryCount < MAX_RETRY_COUNT)
                    {
                        ++_retryCount;
                        TbMessageTitle.Visibility = Visibility.Visible;
                        TbMessageTitle.Text = IoC.Translate("host_reconecting_info") + $"({_retryCount}/{MAX_RETRY_COUNT})";
                        TbMessage.Text = reason;
                        this.ReConn();
                    }
                    else
                    {
                        TbMessageTitle.Visibility = Visibility.Collapsed;
                        BtnReconn.Visibility = Visibility.Visible;
                        TbMessage.Text = reason;
                        ParentWindowSetToWindow();
                    }
                    this.ParentWindow?.FlashIfNotActive();
                }
                else
                {
                    RdpClientDispose();
                    base.OnClosed?.Invoke(base.ConnectionId);
                }
            }
        }

        private void OnRdpClientConnected(object? sender, EventArgs e)
        {
            SimpleLogHelper.Debug("RDP Host:  RdpOnOnConnected");
            this.ParentWindow?.FlashIfNotActive();

            _lastLoginTime = DateTime.Now;
            _loginResizeTimer.Start();

            _flagHasConnected = true;
            Execute.OnUIThread(() =>
            {
                RdpHost.Visibility = Visibility.Visible;
                GridLoading.Visibility = Visibility.Collapsed;
                GridMessageBox.Visibility = Visibility.Collapsed;

                // if parent is FullScreenWindowView, go to full screen.
                if (ParentWindow is FullScreenWindowView)
                {
                    SimpleLogHelper.Debug("RDP Host: ReConn with full screen");
                    GoFullScreen();
                }
            });
        }

        private void OnRdpClientLoginComplete(object? sender, EventArgs e)
        {
            SimpleLogHelper.Debug("RDP Host:  OnRdpClientLoginComplete");

            OnCanResizeNowChanged?.Invoke();
            RdpHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Collapsed;
            ParentWindowResize_StartWatch();
            _resizeEndTimer?.Stop();
            _resizeEndTimer?.Start();
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(5000);
                _resizeEndTimer?.Stop();
            });
        }


        private void OnGoToFullScreenRequested()
        {
            Debug.Assert(_rdpClient != null);
            // make sure ParentWindow is FullScreen Window
            Debug.Assert(ParentWindow != null);
            switch (ParentWindow)
            {
                case null:
                    return;
                case TabWindowView:
                {
                    // full-all-screen session switch to TabWindow, and click "Reconn" button, will entry this case.
                    _rdpClient!.FullScreen = false;
                    if (_rdpSettings.IsTmpSession() == false)
                    {
                        LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, false);
                    }
                    return;
                }
            }


            var screenSize = this.GetScreenSizeIfRdpIsFullScreen();

            // ! don not remove
            ParentWindow.WindowState = WindowState.Normal;
            ParentWindow.WindowStyle = WindowStyle.None;
            ParentWindow.ResizeMode = ResizeMode.NoResize;

            ParentWindow.Width = screenSize.Width / (_primaryScaleFactor / 100.0);
            ParentWindow.Height = screenSize.Height / (_primaryScaleFactor / 100.0);
            ParentWindow.Left = screenSize.Left / (_primaryScaleFactor / 100.0);
            ParentWindow.Top = screenSize.Top / (_primaryScaleFactor / 100.0);

            SimpleLogHelper.Debug($"RDP to FullScreen resize ParentWindow to : W = {ParentWindow.Width}, H = {ParentWindow.Height}, while screen size is {screenSize.Width} × {screenSize.Height}, ScaleFactor = {_primaryScaleFactor}");

            // WARNING!: EnableFullAllScreens do not need SetRdpResolution
            if (_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen)
            {
                switch (_rdpSettings.RdpWindowResizeMode)
                {
                    case null:
                    case ERdpWindowResizeMode.AutoResize:
                    case ERdpWindowResizeMode.FixedFullScreen:
                    case ERdpWindowResizeMode.StretchFullScreen:
                        SetRdpResolution((uint)screenSize.Width, (uint)screenSize.Height, true);
                        break;
                    case ERdpWindowResizeMode.Stretch:
                    case ERdpWindowResizeMode.Fixed:
                        SetRdpResolution((uint)(_rdpSettings.RdpWidth ?? 800), (uint)(_rdpSettings.RdpHeight ?? 600), true);
                        break;
                    default:
                        MsAppCenterHelper.Error(new ArgumentOutOfRangeException($"{_rdpSettings.RdpWindowResizeMode} is not processed!"));
                        SetRdpResolution((uint)screenSize.Width, (uint)screenSize.Height, true);
                        break;
                }
            }
        }

        private void OnConnectionBarRestoreWindowCall()
        {
            // make sure ParentWindow is FullScreen Window
            if (ParentWindow is not FullScreenWindowView)
            {
                return;
            }

            // !do not remove
            ParentWindowSetToWindow();
            if (_rdpSettings.IsTmpSession() == false)
            {
                LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, false);
            }
            base.OnFullScreen2Window?.Invoke(base.ConnectionId);
        }

        #endregion event handler
    }
}
