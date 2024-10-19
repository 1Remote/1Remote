using _1RM.Model.Protocol;
using AxMSTSCLib;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Shawn.Utils;
using MSTSCLib;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;
using System.Windows;
using System.Diagnostics;
using System.Windows.Forms.VisualStyles;
using Shawn.Utils.Wpf.Image;

#if !DEV_RDP
using _1RM.Service;
using _1RM.Model;
using _1RM.Service.Locality;
using _1RM.Utils;
#endif

namespace _1RM.View.Host.ProtocolHosts
{
#if DEV_RDP
    internal static class AxMsRdpClient9NotSafeForScriptingExAdd
    {
        public static void SetExtendedProperty(this AxHost axHost, string propertyName, object value)
        {
            try
            {
                ((IMsRdpExtendedSettings)axHost.GetOcx()).set_Property(propertyName, ref value);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }
    }

    internal class AxMsRdpClient9NotSafeForScriptingEx : AxMSTSCLib.AxMsRdpClient9NotSafeForScripting
    {
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            // Fix for the missing focus issue on the rdp client component
            if (m.Msg == 0x0021) // WM_MOUSEACTIVATE
            {
                if (!this.ContainsFocus)
                {
                    this.Focus();
                }
            }
            base.WndProc(ref m);
        }
    }
#endif
    public partial class RdpHostForm : HostBaseWinform
    {
        private readonly AxMsRdpClient9NotSafeForScriptingEx _rdpClient;
        private readonly RDP _rdpSettings;

        private uint _primaryScaleFactor = 100;
        private DateTime _lastLoginTime = DateTime.MinValue;
        /// <summary>
        /// 标记已经成功连接过服务器，如果没有成功连接过，则在断开时显示错误信息
        /// </summary>
        private bool _flagHasConnected = false;
        private readonly WinformMaskLayer _maskLayer = new WinformMaskLayer();

#if DEV_RDP
        public ProtocolHostStatus Status { get; set; }
        public bool CanFullScreen { get; protected set; }
        public Action<string>? OnProtocolClosed { get; set; } = null;
        public string ConnectionId = "";
        public Action? OnCanResizeNowChanged { get; set; } = null;
#endif

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x112;
            const int SC_CLOSE = 0xF060;
            const int SC_MINIMIZE = 0xF020;
            const int SC_MAXIMIZE1 = 0xF030;// maximize button
            const int SC_MAXIMIZE2 = 0xF032;// double click title bar
            const int SC_RESTORE = 0xF122;
            if (m.Msg == WM_SYSCOMMAND)
            {
                if (m.WParam.ToInt32() == SC_MAXIMIZE1 ||
                    m.WParam.ToInt32() == SC_MAXIMIZE2)
                {
                    GoFullScreen();
                    //_rdpClient.FullScreen = !_rdpClient.FullScreen;
                    //IoC.Get<SessionControlService>().MoveSessionToFullScreen(ConnectionId);
                    return;
                }
            }
            base.WndProc(ref m); // <--- TODO Cannot access a disposed object.
        }


        public RdpHostForm(RDP rdpSettings, int width = 800, int height = 600) : base(rdpSettings, true)
        {
            InitializeComponent();

#if !DEV_RDP
            this.FormBorderStyle = FormBorderStyle.None;
#endif
            Icon = rdpSettings.IconImg.ToIcon();
            _rdpSettings = rdpSettings;

            this.Name = this.Text = $"{rdpSettings.DisplayName} @{rdpSettings.Address}";
            this.BackColor = Color.Black;
            this.StartPosition = FormStartPosition.WindowsDefaultBounds;

            _rdpClient = new AxMsRdpClient9NotSafeForScriptingEx();
            ((System.ComponentModel.ISupportInitialize)(_rdpClient)).BeginInit();
            // set fill to make rdp widow, so that we can enable RDP SmartSizing
            _rdpClient.Dock = DockStyle.Fill;
            _rdpClient.Enabled = true;
            _rdpClient.BackColor = Color.White;
            // set call back
            _rdpClient.OnRequestGoFullScreen += (sender, args) =>
            {
                SimpleLogHelper.Debug("RDP Host:  OnRequestGoFullScreen");
                OnGoToFullScreenRequested();
            };
            _rdpClient.OnRequestLeaveFullScreen += (sender, args) =>
            {
                SimpleLogHelper.Debug("RDP Host:  OnRequestLeaveFullScreen");
                OnRequestLeaveFullScreen();
            };
            _rdpClient.OnRequestContainerMinimize += (sender, args) =>
            {
                SimpleLogHelper.Debug("RDP Host:  OnRequestContainerMinimize");
                if (_rdpClient.FullScreen)
                {
                    this.WindowState = FormWindowState.Minimized;
                }
            };
            _rdpClient.OnDisconnected += OnRdpClientDisconnected;
            _rdpClient.OnConfirmClose += (_, _) =>
            {
                // invoke in the full screen mode.
                SimpleLogHelper.Debug("RDP Host:  RdpOnConfirmClose");
                OnProtocolClosed?.Invoke(ConnectionId);
#if DEV_RDP
                Environment.Exit(-1);
#endif
            };
            _rdpClient.OnConnected += OnRdpClientConnected;
            _rdpClient.OnLoginComplete += OnRdpClientLoginComplete;
            ((System.ComponentModel.ISupportInitialize)(_rdpClient)).EndInit();


            this.Controls.Add(_rdpClient);


            this.Width = width;
            this.Height = height;

            Load += (sender, args) =>
            {
#if !DEV_RDP
                GlobalEventHelper.OnScreenResolutionChanged += OnScreenResolutionChanged;
#endif
                _maskLayer.ReconnectOnClick += ReconnectOnClick;
                _maskLayer.DismissOnClick += DismissOnClick;
                this.Controls.Add(_maskLayer);
                _maskLayer.BringToFront();
                _rdpClient.Hide();
                InitRdp(width, height);
                _rdpClient.Connect();
            };

            Closing += (sender, args) =>
            {
                Activated -= OnActivated;
#if !DEV_RDP
                GlobalEventHelper.OnScreenResolutionChanged -= OnScreenResolutionChanged;
#endif
            };
        }

        private void OnActivated(object? sender, EventArgs e)
        {
            WindowExtensions.StopFlash(this.Handle); // <--- TODO Cannot access a disposed object.
        }

        private void DismissOnClick()
        {
            OnProtocolClosed?.Invoke(ConnectionId);
            this.Close();
        }

        private void ReconnectOnClick()
        {
            ReConn();
        }


        private void OnRdpClientConnected(object? sender, EventArgs e)
        {
            SimpleLogHelper.Debug("RDP Host:  RdpOnOnConnected, rdpClient.Connected = " + _rdpClient.Connected);
            if (!this.Handle.IsActivated())
            {
                if (AttachedHost != null)
                {
                    AttachedHost.ParentWindow?.FlashIfNotActive();
                }
                else
                {
                    this.Handle.Flash();
                }
            }


            _lastLoginTime = DateTime.Now;
            ResizeInit();
            _flagHasConnected = true;
            Execute.OnUIThread(() =>
            {
                _rdpClient.Show();
                _maskLayer.Hide();
                if (AttachedHost == null)
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
            ReSizeRdpToControlSize();

            // TODO _resizeEndTimer.Start();
        }

        public override void GoFullScreen()
        {
            Execute.OnUIThreadSync(() =>
            {
                Debug.Assert(_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.Disable);
                DetachFromHostBase();
                //if (_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.Disable)
                //{
                //    return;
                //}
                _rdpClient.FullScreen = true; // this will invoke OnRequestGoFullScreen -> MakeNormal2FullScreen
            });
        }

        private int _retryCount = 0;
        private const int MAX_RETRY_COUNT = 20;
        private void OnRdpClientDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            SimpleLogHelper.Debug("RDP Host: RdpOnDisconnected");

            lock (this)
            {
                var flagHasConnected = this._flagHasConnected;
                _flagHasConnected = false;

                SetStatus(ProtocolHostStatus.Disconnected);
                ParentWindowResize_StopWatch();

                const int UI_ERR_NORMAL_DISCONNECT = 0xb08;
                string reason = _rdpClient?.GetErrorDescription((uint)e.discReason, (uint)_rdpClient.ExtendedDisconnectReason) ?? "";
                if (e.discReason != UI_ERR_NORMAL_DISCONNECT)
                    SimpleLogHelper.Warning($"RDP({_rdpSettings.DisplayName}) exit with error code {e.discReason}({reason})");

                // disconnectReasonByServer (3 (0x3))
                // https://docs.microsoft.com/zh-cn/windows/win32/termserv/imstscaxevents-ondisconnected?redirectedfrom=MSDN


                if (!string.IsNullOrWhiteSpace(reason)
                    && (flagHasConnected != true
                        || e.discReason != UI_ERR_NORMAL_DISCONNECT
                            && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedDisconnect
                            && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedLogoff
                            && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonNoInfo                // log out from win2008 will reply exDiscReasonNoInfo
                            && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonLogoffByUser          // log out from win10 will reply exDiscReasonLogoffByUser
                            && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonRpcInitiatedDisconnectByUser    // log out from win2016 will reply exDiscReasonLogoffByUser
                    ))
                {
                    if (flagHasConnected == true
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonReplacedByOtherConnection
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonOutOfMemory
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerDeniedConnection
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerDeniedConnectionFips
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerInsufficientPrivileges
                        && _rdpClient?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonNoInfo  // conn to a power-off PC will get exDiscReasonNoInfo
                        && _retryCount < MAX_RETRY_COUNT)
                    {
                        // 自动重连
                        ++_retryCount;
#if DEV_RDP
                        _maskLayer.ShowMessage(reason, "host_reconecting_info" + $"({_retryCount}/{MAX_RETRY_COUNT})");
#else
                        _maskLayer.ShowMessage(reason, IoC.Translate("host_reconecting_info") + $"({_retryCount}/{MAX_RETRY_COUNT})");
#endif
                        ReConn();
                    }
                    else
                    {
                        // 显示错误提示
                        //ParentWindowSetToWindow();
                        if (FormBorderStyle == FormBorderStyle.None)
                        {
                            this.FormBorderStyle = FormBorderStyle.Sizable;
                            this.Width = 800;
                            this.Height = 600;
                            var si = ScreenInfoEx.GetCurrentScreen(this.Handle);
                            this.Left = si.VirtualBounds.Left + si.VirtualBounds.Width / 2 - this.Width / 2;
                            this.Top = si.VirtualBounds.Top + si.VirtualBounds.Height / 2 - this.Height / 2;
                        }
                        _maskLayer.ShowMessage(reason);
                    }


                    if (!this.Handle.IsActivated())
                    {
                        if (AttachedHost != null)
                        {
                            AttachedHost.ParentWindow?.FlashIfNotActive();
                        }
                        else
                        {
                            this.Handle.Flash();
                        }
                    }
                }
                else
                {
                    //RdpClientDispose();
                    OnProtocolClosed?.Invoke(ConnectionId);
                }
            }
        }

        private void OnRequestLeaveFullScreen()
        {
#if !DEV_RDP
            // !do not remove
            //ParentWindowSetToWindow();
            if (_rdpSettings.IsTmpSession() == false)
            {
                LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, false);
            }
            base.OnFullScreen2Window?.Invoke(base.ConnectionId);
#if DEBUG
            if (base.OnFullScreen2Window == null)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.Width = 800;
                this.Height = 600;
            }
#endif
#else
            // TODO 获取 tab 的尺寸，并设置为对应尺寸
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Width = 800;
            this.Height = 600;
            var si = ScreenInfoEx.GetCurrentScreen(this.Handle);
            this.Left = si.VirtualBounds.Left + si.VirtualBounds.Width / 2 - this.Width / 2;
            this.Top = si.VirtualBounds.Top + si.VirtualBounds.Height / 2 - this.Height / 2;
#endif
        }

        private void OnGoToFullScreenRequested()
        {
            //#if !DEV_RDP
            //            if (ParentWindow is TabWindowView)
            //            {
            //                // full-all-screen session switch to TabWindow, and click "Reconn" button, will entry this case.
            //                _rdpClient.FullScreen = false;
            //                if (_rdpSettings.IsTmpSession() == false)
            //                {
            //                    LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, false);
            //                }
            //                return;
            //            }
            //#endif


            var screenSize = this.GetScreenSizeIfRdpIsFullScreen();

            // ! don not remove
            this.WindowState = FormWindowState.Normal;
            //ParentWindow.WindowState = WindowState.Normal;
            //ParentWindow.WindowStyle = WindowStyle.None;
            //ParentWindow.ResizeMode = ResizeMode.NoResize;

            this.ShowInTaskbar = true;

            this.FormBorderStyle = FormBorderStyle.None;
            this.Width = (int)(screenSize.Width / (_primaryScaleFactor / 100.0));
            this.Height = (int)(screenSize.Height / (_primaryScaleFactor / 100.0));
            this.Left = (int)(screenSize.Left / (_primaryScaleFactor / 100.0));
            this.Top = (int)(screenSize.Top / (_primaryScaleFactor / 100.0));

            SimpleLogHelper.Debug($"RDP to FullScreen resize ParentWindow to : W = {Width}, H = {Height}, while screen size is {screenSize.Width} × {screenSize.Height}, ScaleFactor = {_primaryScaleFactor}");

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
#if !DEV_RDP
                        MsAppCenterHelper.Error(new ArgumentOutOfRangeException($"{_rdpSettings.RdpWindowResizeMode} is not processed!"));
#endif
                        SetRdpResolution((uint)screenSize.Width, (uint)screenSize.Height, true);
                        break;
                }
            }
        }

        private void OnScreenResolutionChanged()
        {
            lock (this)
            {
                // 全屏模式下客户端机器发生了屏幕分辨率改变，则将RDP还原到窗口模式（仿照 MSTSC 的逻辑）
                if (_rdpClient?.FullScreen == true)
                {
                    Execute.OnUIThreadSync(() =>
                    {
                        _rdpClient.FullScreen = false;
                    });
                }
            }
        }


        private System.Drawing.Rectangle GetScreenSizeIfRdpIsFullScreen()
        {
            if (_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
            {
                return ScreenInfoEx.GetAllScreensSize();
            }

            int screenIndex = -1;
            if (screenIndex < 0
                || screenIndex >= System.Windows.Forms.Screen.AllScreens.Length)
            {
                screenIndex = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition()).Index;
            }
            return System.Windows.Forms.Screen.AllScreens[screenIndex].Bounds;
        }

        public override void Conn()
        {
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            Execute.OnUIThread(() =>
            {
                try
                {
                    if (GetStatus() == ProtocolHostStatus.Connected || GetStatus() == ProtocolHostStatus.Connecting)
                    {
                        return;
                    }

                    SetStatus(ProtocolHostStatus.Connecting);
                    //GridLoading.Visibility = Visibility.Visible;
                    //RdpHost.Visibility = Visibility.Collapsed;
                    _rdpClient.Connect();
                }
                catch (Exception e)
                {
                    //GridMessageBox.Visibility = Visibility.Visible;
                    //TbMessageTitle.Visibility = Visibility.Collapsed;
                    //TbMessage.Text = e.Message;
                }
                SetStatus(ProtocolHostStatus.Connected);
            });
        }

        public override void ReConn()
        {
            Execute.OnUIThreadSync(() =>
            {
                if (_rdpClient.Connected == 1)
                {
                    return;
                }

                if (GetStatus() != ProtocolHostStatus.Connected
                    && GetStatus() != ProtocolHostStatus.Disconnected)
                {
                    SimpleLogHelper.Warning($"RDP Host: Call ReConn, but current status = " + GetStatus());
                    return;
                }
                else
                {
                    SimpleLogHelper.Warning($"RDP Host: Call ReConn");
                }

                SetStatus(ProtocolHostStatus.WaitingForReconnect);

                SetStatus(ProtocolHostStatus.NotInit);

                int w = 0;
                int h = 0;
#if !DEV_RDP
                if (AttachedHost.ParentWindow is TabWindowView tab)
                {
                    var size = tab.GetTabContentSize(ColorAndBrushHelper.ColorIsTransparent(this._rdpSettings.ColorHex) == true);
                    w = (int)size.Width;
                    h = (int)size.Height;
                }
#endif
                RdpInitDisplay(w, h, true);
                Conn();
            });
        }



#if DEV_RDP
        public bool RdpFull
        {
            get => _rdpClient.FullScreen;
            set => _rdpClient.FullScreen = value;
        }
#endif
    }
}
