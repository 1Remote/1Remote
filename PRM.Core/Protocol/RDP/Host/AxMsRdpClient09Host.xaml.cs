using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;
using PRM.Core.DB;
using PRM.Core.Model;
using Shawn.Utils;
using Shawn.Utils.RDP;
using Application = System.Windows.Application;
using Color = System.Drawing.Color;
using MessageBox = System.Windows.MessageBox;
using MessageBoxOptions = System.Windows.MessageBoxOptions;

namespace PRM.Core.Protocol.RDP.Host
{
    public sealed partial class AxMsRdpClient09Host : ProtocolHostBase, IDisposable
    {
        private AxMsRdpClient9NotSafeForScriptingEx _rdp = null;
        private readonly ProtocolServerRDP _rdpServer = null;
        private uint _primaryScaleFactor = 100;

        private bool _flagHasConnected = false;
        private bool _isLastTimeFullScreen = false;
         
        private int _retryCount = 0;
        private const int MaxRetryCount = 20;

        public AxMsRdpClient09Host(PrmContext context, ProtocolServerBase protocolServer, double width = 0, double height = 0) : base(context, protocolServer, true)
        {
            InitializeComponent();

            GridMessageBox.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;

            if (protocolServer is ProtocolServerRDP rdp)
            {
                _rdpServer = rdp;
                _isLastTimeFullScreen = _rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens
                                        || _rdpServer.IsConnWithFullScreen
                                        || (_rdpServer.AutoSetting?.FullScreenLastSessionIsFullScreen ?? false);
                InitRdp(width, height);
                GlobalEventHelper.OnScreenResolutionChanged += OnScreenResolutionChanged;
            }
            else
                throw new ArgumentException($"Send {protocolServer.GetType()} to RdpHost!");
        }

        public void Dispose()
        {
            try
            {
                GlobalEventHelper.OnScreenResolutionChanged -= OnScreenResolutionChanged;
            }
            catch
            {
            }
            RdpHost.Child = null;
            RdpDispose();
            _resizeEndTimer?.Dispose();
            RdpHost?.Dispose();
            RdpHost = null;
        }

        private void OnScreenResolutionChanged()
        {
            if (_rdp.FullScreen == true)
            {
                _rdp.FullScreen = false;
            }
        }

        private void RdpInitStatic()
        {
            SimpleLogHelper.Debug("RDP Host: init Static");
            // enable CredSSP, will use CredSsp if the client supports.
            _rdp.AdvancedSettings7.EnableCredSspSupport = true;
            _rdp.AdvancedSettings2.EncryptionEnabled = 1;
            _rdp.AdvancedSettings5.AuthenticationLevel = 0;
            _rdp.AdvancedSettings5.EnableAutoReconnect = true;
            // setting PublicMode to false allows the saving of credentials, which prevents
            _rdp.AdvancedSettings6.PublicMode = false;
            _rdp.AdvancedSettings5.EnableWindowsKey = 1;
            _rdp.AdvancedSettings5.GrabFocusOnConnect = true;
            _rdp.AdvancedSettings2.keepAliveInterval = 1000 * 60 * 1; // 1000 = 1000 ms
            _rdp.AdvancedSettings2.overallConnectionTimeout = 600; // The new time, in seconds. The maximum value is 600, which represents 10 minutes.


            // enable CredSSP, will use CredSsp if the client supports.
            _rdp.AdvancedSettings9.EnableCredSspSupport = true;

            //- 0: If server authentication fails, connect to the computer without warning (Connect and don't warn me)
            //- 1: If server authentication fails, don't establish a connection (Don't connect)
            //- 2: If server authentication fails, show a warning and allow me to connect or refuse the connection (Warn me)
            //- 3: No authentication requirement specified.
            _rdp.AdvancedSettings9.AuthenticationLevel = 0;



            // - 0 Apply key combinations only locally at the client computer.
            // - 1 Apply key combinations at the remote server.
            // - 2 Apply key combinations to the remote server only when the client is running in full-screen mode. This is the default value.
            _rdp.SecuredSettings3.KeyboardHookMode = 2;


            // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings6-connecttoadministerserver
            _rdp.AdvancedSettings7.ConnectToAdministerServer = _rdpServer.IsAdministrativePurposes;
        }

        private void RdpInitConnBar()
        {
            SimpleLogHelper.Debug("RDP Host: init conn bar");
            _rdp.AdvancedSettings6.DisplayConnectionBar = _rdpServer.IsFullScreenWithConnectionBar;
            _rdp.AdvancedSettings6.ConnectionBarShowPinButton = true;
            _rdp.AdvancedSettings6.PinConnectionBar = false;
            _rdp.AdvancedSettings6.ConnectionBarShowMinimizeButton = true;
            _rdp.AdvancedSettings6.ConnectionBarShowRestoreButton = true;
            _rdp.AdvancedSettings6.BitmapVirtualCache32BppSize = 48;
        }

        private void RdpInitRedirect()
        {
            SimpleLogHelper.Debug("RDP Host: init Redirect");
            #region Redirect

            _rdp.AdvancedSettings9.RedirectDrives = _rdpServer.EnableDiskDrives;
            _rdp.AdvancedSettings9.RedirectClipboard = _rdpServer.EnableClipboard;
            _rdp.AdvancedSettings9.RedirectPrinters = _rdpServer.EnablePrinters;
            _rdp.AdvancedSettings9.RedirectPOSDevices = _rdpServer.EnablePorts;
            _rdp.AdvancedSettings9.RedirectSmartCards = _rdpServer.EnableSmartCardsAndWinHello;

            if (_rdpServer.EnableKeyCombinations)
            {
                // - 0 Apply key combinations only locally at the client computer.
                // - 1 Apply key combinations at the remote server.
                // - 2 Apply key combinations to the remote server only when the client is running in full-screen mode. This is the default value.
                _rdp.SecuredSettings3.KeyboardHookMode = 1;
            }
            else
                _rdp.SecuredSettings3.KeyboardHookMode = 0;

            if (_rdpServer.EnableSounds)
            {
                // - 0 Redirect sounds to the client. This is the default value.
                // - 1 Play sounds at the remote computer.
                // - 2 Disable sound redirection; do not play sounds at the server.
                _rdp.SecuredSettings3.AudioRedirectionMode = 0;
                // - 0 (Audio redirection is enabled and the option for redirection is "Bring to this computer". This is the default mode.)
                // - 1 (Audio redirection is enabled and the option is "Leave at remote computer". The "Leave at remote computer" option is supported only when connecting remotely to a host computer that is running Windows Vista. If the connection is to a host computer that is running Windows Server 2008, the option "Leave at remote computer" is changed to "Do not play".)
                // - 2 (Audio redirection is enabled and the mode is "Do not play".)
                _rdp.AdvancedSettings6.AudioRedirectionMode = 0;

                // - 0 Dynamic audio quality. This is the default audio quality setting. The server dynamically adjusts audio output quality in response to network conditions and the client and server capabilities.
                // - 1 Medium audio quality. The server uses a fixed but compressed format for audio output.
                // - 2 High audio quality. The server provides audio output in uncompressed PCM format with lower processing overhead for latency.
                _rdp.AdvancedSettings8.AudioQualityMode = 0;
            }
            else
            {
                // - 2 Disable sound redirection; do not play sounds at the server.
                _rdp.SecuredSettings3.AudioRedirectionMode = 2;
                _rdp.AdvancedSettings6.AudioRedirectionMode = 2;
            }

            if (_rdpServer.EnableAudioCapture)
            {
                // indicates whether the default audio input device is redirected from the client to the remote session
                _rdp.AdvancedSettings8.AudioCaptureRedirectionMode = true;
            }
            else
            {
                _rdp.AdvancedSettings8.AudioCaptureRedirectionMode = false;
            }
            #endregion

        }

        private void RdpInitDisplay(double width = 0, double height = 0, bool isReconn = false)
        {
            SimpleLogHelper.Debug("RDP Host: init Display");
            #region Display

            ReadScaleFactor();
            _rdp.SetExtendedProperty("DesktopScaleFactor", _primaryScaleFactor);
            _rdp.SetExtendedProperty("DeviceScaleFactor", (uint)100);
            if (_rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.Stretch
            || _rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.StretchFullScreen)
                _rdp.AdvancedSettings2.SmartSizing = true;
            // to enhance user experience, i let the form handled full screen
            _rdp.AdvancedSettings6.ContainerHandledFullScreen = 1;


            if (_rdpServer.RdpFullScreenFlag != ERdpFullScreenFlag.EnableFullAllScreens)
                switch (_rdpServer.RdpWindowResizeMode)
                {
                    case ERdpWindowResizeMode.Stretch:
                    case ERdpWindowResizeMode.Fixed:
                        _rdp.DesktopWidth = (int)(_rdpServer.RdpWidth);
                        _rdp.DesktopHeight = (int)(_rdpServer.RdpHeight);
                        break;
                    case ERdpWindowResizeMode.StretchFullScreen:
                    case ERdpWindowResizeMode.FixedFullScreen:
                        var screenSize = GetScreenSize();
                        _rdp.DesktopWidth = (int)(screenSize.Width);
                        _rdp.DesktopHeight = (int)(screenSize.Height);
                        break;
                    case ERdpWindowResizeMode.AutoResize:
                    default:
                        if (width > 100 && height > 100)
                        {
                            // if isReconn == false, then width is Tab width, true width = Tab width * ScaleFactor
                            // if isReconn == true, then width is DesktopWidth, ScaleFactor should == 100
                            if (isReconn)
                                _primaryScaleFactor = 100;
                            _rdp.DesktopWidth = (int)(width * (_primaryScaleFactor / 100.0));
                            _rdp.DesktopHeight = (int)(height * (_primaryScaleFactor / 100.0));
                        }
                        else
                        {
                            _rdp.DesktopWidth = (int)(800 * (_primaryScaleFactor / 100.0));
                            _rdp.DesktopHeight = (int)(600 * (_primaryScaleFactor / 100.0));
                        }
                        break;
                }


            switch (_rdpServer.RdpFullScreenFlag)
            {
                case ERdpFullScreenFlag.Disable:
                    base.CanFullScreen = false;
                    break;
                case ERdpFullScreenFlag.EnableFullScreen:
                    base.CanFullScreen = true;
                    if (_isLastTimeFullScreen)
                    {
                        var screenSize = GetScreenSize();
                        _rdp.DesktopWidth = (int)(screenSize.Width);
                        _rdp.DesktopHeight = (int)(screenSize.Height);
                        _rdp.FullScreen = true;
                    }
                    break;
                case ERdpFullScreenFlag.EnableFullAllScreens:
                    base.CanFullScreen = true;
                    // every time reconnect, EnableFullAllScreens will go to full screen.
                    if (Screen.AllScreens.Length == 1)
                    {
                        var screenSize = GetScreenSize();
                        _rdp.DesktopWidth = (int)(screenSize.Width);
                        _rdp.DesktopHeight = (int)(screenSize.Height);
                    }
                    ((IMsRdpClientNonScriptable5)_rdp.GetOcx()).UseMultimon = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            #endregion
        }

        private void RdpInitPerformance()
        {
            SimpleLogHelper.Debug("RDP Host: init Performance");
            #region Performance
            // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings-performanceflags
            int nDisplayPerformanceFlag = 0;
            if (_rdpServer.DisplayPerformance != EDisplayPerformance.Auto)
            {
                _rdp.AdvancedSettings9.BandwidthDetection = false;
                // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings7-networkconnectiontype
                // CONNECTION_TYPE_MODEM (1 (0x1)) Modem (56 Kbps)
                // CONNECTION_TYPE_BROADBAND_LOW (2 (0x2)) Low-speed broadband (256 Kbps to 2 Mbps) CONNECTION_TYPE_SATELLITE (3 (0x3)) Satellite (2 Mbps to 16 Mbps, with high latency)
                // CONNECTION_TYPE_BROADBAND_HIGH (4 (0x4)) High-speed broadband (2 Mbps to 10 Mbps) CONNECTION_TYPE_WAN (5 (0x5)) Wide area network (WAN) (10 Mbps or higher, with high latency)
                // CONNECTION_TYPE_LAN (6 (0x6)) Local area network (LAN) (10 Mbps or higher)
                _rdp.AdvancedSettings8.NetworkConnectionType = 1;
                switch (_rdpServer.DisplayPerformance)
                {
                    case EDisplayPerformance.Auto:
                        break;
                    case EDisplayPerformance.Low:
                        // 8,16,24,32
                        _rdp.ColorDepth = 8;
                        nDisplayPerformanceFlag += 0x00000001;//TS_PERF_DISABLE_WALLPAPER;      Wallpaper on the desktop is not displayed.
                        nDisplayPerformanceFlag += 0x00000002;//TS_PERF_DISABLE_FULLWINDOWDRAG; Full-window drag is disabled; only the window outline is displayed when the window is moved.
                        nDisplayPerformanceFlag += 0x00000004;//TS_PERF_DISABLE_MENUANIMATIONS; Menu animations are disabled.
                        nDisplayPerformanceFlag += 0x00000008;//TS_PERF_DISABLE_THEMING ;       Themes are disabled.
                        nDisplayPerformanceFlag += 0x00000020;//TS_PERF_DISABLE_CURSOR_SHADOW;  No shadow is displayed for the cursor.
                        nDisplayPerformanceFlag += 0x00000040;//TS_PERF_DISABLE_CURSORSETTINGS; Cursor blinking is disabled.
                        break;
                    case EDisplayPerformance.Middle:
                        _rdp.ColorDepth = 16;
                        nDisplayPerformanceFlag += 0x00000001;//TS_PERF_DISABLE_WALLPAPER;      Wallpaper on the desktop is not displayed.
                        nDisplayPerformanceFlag += 0x00000002;//TS_PERF_DISABLE_FULLWINDOWDRAG; Full-window drag is disabled; only the window outline is displayed when the window is moved.
                        nDisplayPerformanceFlag += 0x00000004;//TS_PERF_DISABLE_MENUANIMATIONS; Menu animations are disabled.
                        nDisplayPerformanceFlag += 0x00000008;//TS_PERF_DISABLE_THEMING ;       Themes are disabled.
                        nDisplayPerformanceFlag += 0x00000020;//TS_PERF_DISABLE_CURSOR_SHADOW;  No shadow is displayed for the cursor.
                        nDisplayPerformanceFlag += 0x00000040;//TS_PERF_DISABLE_CURSORSETTINGS; Cursor blinking is disabled.
                        nDisplayPerformanceFlag += 0x00000080;//TS_PERF_ENABLE_FONT_SMOOTHING;        Enable font smoothing.
                        nDisplayPerformanceFlag += 0x00000100;//TS_PERF_ENABLE_DESKTOP_COMPOSITION ;  Enable desktop composition.

                        break;
                    case EDisplayPerformance.High:
                        _rdp.ColorDepth = 32;
                        nDisplayPerformanceFlag += 0x00000080;//TS_PERF_ENABLE_FONT_SMOOTHING;        Enable font smoothing.
                        nDisplayPerformanceFlag += 0x00000100;//TS_PERF_ENABLE_DESKTOP_COMPOSITION ;  Enable desktop composition.
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            SimpleLogHelper.Debug("RdpInit: DisplayPerformance = " + _rdpServer.DisplayPerformance + ", flag = " + Convert.ToString(nDisplayPerformanceFlag, 2));
            _rdp.AdvancedSettings9.PerformanceFlags = nDisplayPerformanceFlag;
            #endregion
        }

        private void RdpInitGateway()
        {
            SimpleLogHelper.Debug("RDP Host: init Gateway");
            #region Gateway
            // Specifies whether Remote Desktop Gateway (RD Gateway) is supported.
            if (_rdp.TransportSettings.GatewayIsSupported != 0
                && _rdpServer.GatewayMode != EGatewayMode.DoNotUseGateway)
            {
                // https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewayprofileusagemethod
                _rdp.TransportSettings2.GatewayProfileUsageMethod = 1; // Use explicit settings, as specified by the user.

                // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewayusagemethod
                _rdp.TransportSettings.GatewayUsageMethod = _rdpServer.GatewayMode switch
                {
                    EGatewayMode.UseTheseGatewayServerSettings =>
                    1 // 1 : Always use an RD Gateway server. In the RDC client UI, the Bypass RD Gateway server for local addresses check box is cleared.
                    ,
                    EGatewayMode.AutomaticallyDetectGatewayServerSettings =>
                    2 // 2 : Use an RD Gateway server if a direct connection cannot be made to the RD Session Host server. In the RDC client UI, the Bypass RD Gateway server for local addresses check box is selected.
                    ,
                    _ => throw new ArgumentOutOfRangeException()
                };

                _rdp.TransportSettings2.GatewayHostname = _rdpServer.GatewayHostName;
                //_rdp.TransportSettings2.GatewayDomain = "XXXXX";


                // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewaycredssource
                // TSC_PROXY_CREDS_MODE_USERPASS (0): Use a password (NTLM) as the authentication method for RD Gateway.
                // TSC_PROXY_CREDS_MODE_SMARTCARD (1): Use a smart card as the authentication method for RD Gateway.
                // TSC_PROXY_CREDS_MODE_ANY (4): Use any authentication method for RD Gateway.
                switch (_rdpServer.GatewayLogonMethod)
                {
                    case EGatewayLogonMethod.SmartCard:
                        _rdp.TransportSettings.GatewayCredsSource = 1; // TSC_PROXY_CREDS_MODE_SMARTCARD 
                        break;
                    case EGatewayLogonMethod.Password:
                        _rdp.TransportSettings.GatewayCredsSource = 0; // TSC_PROXY_CREDS_MODE_USERPASS
                        _rdp.TransportSettings2.GatewayUsername = _rdpServer.GatewayUserName;
                        _rdp.TransportSettings2.GatewayPassword = Context.DbOperator.DecryptOrReturnOriginalString(_rdpServer.GatewayPassword);
                        break;
                    default:
                        _rdp.TransportSettings.GatewayCredsSource = 4; // TSC_PROXY_CREDS_MODE_ANY 
                        break;
                }

                _rdp.TransportSettings2.GatewayCredSharing = 0;
            }
            #endregion
        }

        private void InitRdp(double width = 0, double height = 0, bool isReconn = false)
        {
            if (Status != ProtocolHostStatus.NotInit)
                return;

            Status = ProtocolHostStatus.Initializing;


            RdpDispose();
            _rdp = new AxMsRdpClient9NotSafeForScriptingEx();

            SimpleLogHelper.Debug("RDP Host: init new AxMsRdpClient9NotSafeForScriptingEx()");

            ((System.ComponentModel.ISupportInitialize)(_rdp)).BeginInit();
            _rdp.Dock = DockStyle.Fill;
            _rdp.Enabled = true;
            _rdp.BackColor = Color.Black;
            // set call back
            _rdp.OnRequestGoFullScreen += (sender, args) =>
            {
                MakeNormal2FullScreen();
            };
            _rdp.OnRequestLeaveFullScreen += (sender, args) =>
            {
                MakeFullScreen2Normal();
            };
            _rdp.OnRequestContainerMinimize += (sender, args) => { MakeForm2Minimize(); };
            _rdp.OnDisconnected += RdpOnDisconnected;
            _rdp.OnConfirmClose += RdpOnConfirmClose;
            _rdp.OnConnected += RdpOnOnConnected;
            _rdp.OnLoginComplete += RdpOnOnLoginComplete;
            ((System.ComponentModel.ISupportInitialize)(_rdp)).EndInit();
            RdpHost.Child = _rdp;


            SimpleLogHelper.Debug("RDP Host: init CreateControl();");
            _rdp.CreateControl();

            SimpleLogHelper.Debug("RDP Host: init server info");
            #region server info
            // server info
            _rdp.Server = _rdpServer.Address;
            _rdp.UserName = _rdpServer.UserName;
            _rdp.AdvancedSettings2.RDPPort = _rdpServer.GetPort();
            var secured = (MSTSCLib.IMsTscNonScriptable)_rdp.GetOcx();
            secured.ClearTextPassword = Context.DbOperator.DecryptOrReturnOriginalString(_rdpServer.Password);
            _rdp.FullScreenTitle = _rdpServer.DispName + " - " + _rdpServer.SubTitle;
            #endregion


            RdpInitStatic();
            RdpInitConnBar();
            RdpInitRedirect();
            RdpInitDisplay(width, height, isReconn);
            RdpInitPerformance();
            RdpInitGateway();

            Status = ProtocolHostStatus.Initialized;
        }


        #region Base Interface
        public override void Conn()
        {
            if (Status == ProtocolHostStatus.Connected
            || Status == ProtocolHostStatus.Connecting)
            {
                return;
            }
            Status = ProtocolHostStatus.Connecting;
            GridLoading.Visibility = Visibility.Visible;
            RdpHost.Visibility = Visibility.Collapsed;
            _rdp.Connect();
        }

        public override void ReConn()
        {
            SimpleLogHelper.Debug("RDP Host: RDP ReConn, Status = " + Status);
            if (Status != ProtocolHostStatus.Connected
                && Status != ProtocolHostStatus.Disconnected)
            {
                return;
            }
            Status = ProtocolHostStatus.WaitingForReconnect;

            double width = _rdp.Width;
            double height = _rdp.Height;
            RdpHost.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;
            GridMessageBox.Visibility = Visibility.Collapsed;
            _invokeOnClosedWhenDisconnected = false;
            RdpDispose();

            Status = ProtocolHostStatus.NotInit;
            InitRdp(width, height, true);
            Conn();

            _invokeOnClosedWhenDisconnected = true;
        }

        public override void Close()
        {
            RdpDispose();
            base.Close();
        }

        public override void GoFullScreen()
        {
            Debug.Assert(this.ParentWindow != null);

            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen)
            {
                _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = ScreenInfoEx.GetCurrentScreen(this.ParentWindow).Index;
            }
            else
                _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = -1;
            Context.DbOperator.DbUpdateServer(_rdpServer);
            _rdp.FullScreen = true;
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Native;
        }

        public override IntPtr GetHostHwnd()
        {
            return IntPtr.Zero;
        }

        public override bool CanResizeNow()
        {
            return Status == ProtocolHostStatus.Connected;
        }
        #endregion



        #region event handler

        #region connection


        private bool _invokeOnClosedWhenDisconnected = true;
        private void RdpOnDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            SimpleLogHelper.Debug("RDP Host: RdpOnDisconnected");
            if(_rdp == null)
                return;

            _isLastTimeFullScreen = _rdp.FullScreen;

            Status = ProtocolHostStatus.Disconnected;
            ResizeEndStopFireDelegate();
            if (this._onResizeEnd != null)
                this._onResizeEnd -= ReSizeRdp;

            const int UI_ERR_NORMAL_DISCONNECT = 0xb08;
            string reason = _rdp?.GetErrorDescription((uint)e.discReason, (uint)_rdp.ExtendedDisconnectReason);
            if (e.discReason != UI_ERR_NORMAL_DISCONNECT)
                SimpleLogHelper.Warning($"RDP({_rdpServer.DispName}) exit with error code {e.discReason}({reason})");

            // disconnectReasonByServer (3 (0x3))
            // https://docs.microsoft.com/zh-cn/windows/win32/termserv/imstscaxevents-ondisconnected?redirectedfrom=MSDN

            if (e.discReason != UI_ERR_NORMAL_DISCONNECT
                && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedDisconnect
                && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedLogoff
                && !string.IsNullOrWhiteSpace(reason))
            {
                BtnReconn.Visibility = Visibility.Collapsed;
                RdpHost.Visibility = Visibility.Collapsed;
                GridMessageBox.Visibility = Visibility.Visible;
                if (_flagHasConnected == true
                    && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonReplacedByOtherConnection
                    && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonOutOfMemory
                    && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonLogoffByUser
                    && _retryCount < MaxRetryCount)
                {
                    ++_retryCount;
                    TbMessageTitle.Visibility = Visibility.Visible;
                    TbMessageTitle.Text = SystemConfig.Instance.Language.GetText("host_reconecting_info") + $"({_retryCount}/{MaxRetryCount})";
                    TbMessage.Text = reason;
                    ReConn();
                }
                else
                {
                    TbMessageTitle.Visibility = Visibility.Collapsed;
                    BtnReconn.Visibility = Visibility.Visible;
                    TbMessage.Text = reason;
                }
                this.ParentWindow.FlashIfNotActive();
            }
            else
            {
                RdpDispose();
                if (_invokeOnClosedWhenDisconnected)
                    base.OnClosed?.Invoke(base.ConnectionId);
            }
        }

        private void RdpOnOnConnected(object sender, EventArgs e)
        {
            SimpleLogHelper.Debug("RDP Host:  RdpOnOnConnected");
            this.ParentWindow.FlashIfNotActive();

            _flagHasConnected = true;
            Status = ProtocolHostStatus.Connected;

            RdpHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Collapsed;
        }

        private void RdpOnOnLoginComplete(object sender, EventArgs e)
        {
            SimpleLogHelper.Debug("RDP Host:  RdpOnOnLoginComplete");

            RdpHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Collapsed;

            ResizeEndStartFireDelegate();
            if (this._onResizeEnd == null)
                this._onResizeEnd += ReSizeRdp;

            ReSizeRdp();
        }

        private void RdpOnConfirmClose(object sender, IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            SimpleLogHelper.Debug("RDP Host:  RdpOnConfirmClose");

            base.OnClosed?.Invoke(base.ConnectionId);
        }

        #endregion

        /// <summary>
        /// set remote resolution to _rdp size if is AutoResize
        /// </summary>
        private void ReSizeRdp()
        {
            if (_rdp.FullScreen == false
                && _rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.AutoResize)
            {
                var nw = (uint)_rdp.Width;
                var nh = (uint)_rdp.Height;
                SetRdpResolution(nw, nh);
            }
        }

        private void SetRdpResolution(uint w, uint h)
        {
            try
            {
                Console.WriteLine($@"RDP resize: W = {w}, H = {h}");
                var p = _primaryScaleFactor;
                ReadScaleFactor();
                if (_rdp.DesktopWidth != w || _rdp.DesktopHeight != h || p != _primaryScaleFactor)
                    _rdp.UpdateSessionDisplaySettings(w, h, w, h, 0, _primaryScaleFactor, 100);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        private double _normalWidth = 800;
        private double _normalHeight = 600;
        private double _normalTop = 0;
        private double _normalLeft = 0;
        private void MakeNormal2FullScreen(bool saveSize = true)
        {
            // make sure ParentWindow is FullScreen Window
            Debug.Assert(ParentWindow != null);
            if (ParentWindow is ITab)
            {
                // full-all-screen session switch to TabWindow, and click "Reconn" button, will entry this case. 
                _rdp.FullScreen = false;
                _isLastTimeFullScreen = false;
                return;
            }

            _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = true;

            var screenSize = GetScreenSize();
            if (saveSize)
            {
                _normalWidth = ParentWindow.Width;
                _normalHeight = ParentWindow.Height;
                _normalTop = ParentWindow.Top;
                _normalLeft = ParentWindow.Left;
            }

            ParentWindow.WindowState = WindowState.Normal;
            ParentWindow.WindowStyle = WindowStyle.None;
            ParentWindow.ResizeMode = ResizeMode.NoResize;

            ParentWindow.Width = screenSize.Width / (_primaryScaleFactor / 100.0);
            ParentWindow.Height = screenSize.Height / (_primaryScaleFactor / 100.0);
            ParentWindow.Left = screenSize.Left / (_primaryScaleFactor / 100.0);
            ParentWindow.Top = screenSize.Top / (_primaryScaleFactor / 100.0);
            // WARNING!: EnableFullAllScreens do not need to SetRdpResolution
            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen)
            {
                SetRdpResolution((uint)screenSize.Width, (uint)screenSize.Height);
            }
            _isLastTimeFullScreen = true;
        }


        private System.Drawing.Rectangle GetScreenSize()
        {
            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
            {
                return ScreenInfoEx.GetAllScreensSize();
            }
            else if (_rdpServer.AutoSetting.FullScreenLastSessionScreenIndex >= 0
                     && _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex < System.Windows.Forms.Screen.AllScreens.Length)
            {
                return System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreenLastSessionScreenIndex].Bounds;
            }
            return System.Windows.Forms.Screen.PrimaryScreen.Bounds;
        }

        private void MakeFullScreen2Normal()
        {
            // make sure ParentWindow is FullScreen Window
            Debug.Assert(ParentWindow != null);
            if (ParentWindow is ITab)
            {
                _isLastTimeFullScreen = false;
                return;
            }

            _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = false;
            //ParentWindow.Topmost = false;
            ParentWindow.ResizeMode = ResizeMode.CanResize;
            ParentWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            ParentWindow.WindowState = WindowState.Normal;
            ParentWindow.Width = _normalWidth;
            ParentWindow.Height = _normalHeight;
            ParentWindow.Top = _normalTop;
            ParentWindow.Left = _normalLeft;
            base.OnFullScreen2Window?.Invoke(base.ConnectionId);
            _isLastTimeFullScreen = false;
        }
        private void MakeForm2Minimize()
        {
            Debug.Assert(ParentWindow != null);
            ParentWindow.WindowState = WindowState.Minimized;
        }

        #endregion


        private void ReadScaleFactor()
        {
            try
            {
                // !must use PrimaryScreen scale factor
                _primaryScaleFactor = (uint)(100 * System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth);
                //_primaryScaleFactor = (uint)(100 * ScreenInfoEx.GetCurrentScreen(this.ParentWindow).ScaleFactor);
            }
            catch (Exception)
            {
                _primaryScaleFactor = 100;
            }
            finally
            {
                if (_primaryScaleFactor < 100)
                    _primaryScaleFactor = 100;
            }
        }


        #region WindowOnResizeEnd

        public delegate void ResizeEndDelegage();
        private ResizeEndDelegage _onResizeEnd;
        private readonly System.Timers.Timer _resizeEndTimer = new System.Timers.Timer(500) { Enabled = false };
        private readonly object _resizeEndLocker = new object();
        private bool _resizeEndStartFire = false;

        private void ResizeEndStartFireDelegate()
        {
            if (_resizeEndStartFire == false)
                lock (_resizeEndLocker)
                {
                    if (_resizeEndStartFire == false)
                    {
                        _resizeEndStartFire = true;
                        _resizeEndTimer.Elapsed += _InvokeResizeEndEnd;
                        base.SizeChanged += _ResizeEnd_WindowSizeChanged;
                    }
                }
        }
        private void ResizeEndStopFireDelegate()
        {
            if (_resizeEndStartFire == true)
                lock (_resizeEndLocker)
                {
                    if (_resizeEndStartFire == true)
                    {
                        _resizeEndStartFire = false;
                        _resizeEndTimer.Stop();
                        try
                        {
                            _resizeEndTimer.Elapsed -= _InvokeResizeEndEnd;
                        }
                        catch
                        {
                        }

                        try
                        {
                            base.SizeChanged -= _ResizeEnd_WindowSizeChanged;
                        }
                        catch
                        {
                        }
                    }
                }
        }
        private void _ResizeEnd_WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_canAutoResize)
            {
                _resizeEndTimer.Stop();
                _resizeEndTimer.Start();
            }
        }
        private void _InvokeResizeEndEnd(object sender, ElapsedEventArgs e)
        {
            _resizeEndTimer.Stop();
            _onResizeEnd?.Invoke();
        }
        #endregion


        private bool _canAutoResize = true;

        /// <summary>
        /// when tab window goes to min from max, base.SizeChanged invoke and size will get bigger, normal to min will not tiger this issue, don't know why.
        /// so stop resize when window status change to min until status restore.
        /// </summary>
        /// <param name="isEnable"></param>
        public override void ToggleAutoResize(bool isEnable)
        {
            _canAutoResize = isEnable;
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            RdpDispose();
            if (_invokeOnClosedWhenDisconnected)
                base.OnClosed?.Invoke(base.ConnectionId);
        }

        private void BtnReconn_OnClick(object sender, RoutedEventArgs e)
        {
            ReConn();
        }

        private void RdpDispose()
        {
            if (_rdp != null)
            {
                var tmp = _rdp;
                var t = new Task(() =>
                {
                    if (tmp.Connected > 0)
                        tmp.Disconnect();
                    tmp.Dispose();
                    tmp = null;
                });
                t.Start();
                _rdp = null;
            }
            SimpleLogHelper.Debug("RDP Host: _rdp.Dispose()");
        }
    }
}
