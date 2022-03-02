using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;
using PRM.Core.I;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;
using Shawn.Utils.Wpf.Controls;
using Color = System.Drawing.Color;

namespace PRM.View.ProtocolHosts
{
    internal static class AxMsRdpClient9NotSafeForScriptingExAdd
    {
        public static void SetExtendedProperty(this AxHost axHost, string propertyName, object value)
        {
            try
            {
                ((IMsRdpExtendedSettings)axHost.GetOcx()).set_Property(propertyName, ref value);
            }
            catch (Exception ee)
            {
                SimpleLogHelper.Error(ee);
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


    public sealed partial class AxMsRdpClient09Host : HostBase, IDisposable
    {
        private AxMsRdpClient9NotSafeForScriptingEx _rdp = null;
        private readonly ProtocolServerRDP _rdpServer = null;
        /// <summary>
        /// system scale factor, 100 = 100%, 200 = 200%
        /// </summary>
        private uint _primaryScaleFactor = 100;
        private uint _lastScaleFactor = 0;

        private bool _flagHasConnected = false;
        private bool _flagHasLogin = false;

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
                _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen ??= false;
                InitRdp(width, height);
                GlobalEventHelper.OnScreenResolutionChanged += OnScreenResolutionChanged;
            }
            else
                throw new ArgumentException($"Send {protocolServer.GetType()} to RdpHost!");
        }

        ~AxMsRdpClient09Host()
        {
            Console.WriteLine($"Release {this.GetType().Name}({this.GetHashCode()})");
            Dispose();
        }

        public void Dispose()
        {
            Console.WriteLine($"Dispose {this.GetType().Name}({this.GetHashCode()})");
            try
            {
                GlobalEventHelper.OnScreenResolutionChanged -= OnScreenResolutionChanged;
            }
            catch
            {
            }

            RdpDispose();
            _resizeEndTimer?.Dispose();
        }

        private void OnScreenResolutionChanged()
        {
            lock (this)
            {
                if (_rdp?.FullScreen == true)
                {
                    _rdp.FullScreen = false;
                }
            }
        }

        private void RdpInitServerInfo()
        {
            #region server info

            // server info
            _rdp.Server = _rdpServer.Address;
            _rdp.Domain = _rdpServer.Domain;
            _rdp.UserName = _rdpServer.UserName;
            _rdp.AdvancedSettings2.RDPPort = _rdpServer.GetPort();


            if (string.IsNullOrWhiteSpace(_rdpServer.LoadBalanceInfo) == false)
            {
                var loadBalanceInfo = _rdpServer.LoadBalanceInfo;
                if (loadBalanceInfo.Length % 2 == 1)
                    loadBalanceInfo += " ";
                loadBalanceInfo += "\r\n";
                var bytes = Encoding.UTF8.GetBytes(loadBalanceInfo);
                _rdp.AdvancedSettings2.LoadBalanceInfo = Encoding.Unicode.GetString(bytes);
            }



            var secured = (MSTSCLib.IMsTscNonScriptable)_rdp.GetOcx();
            secured.ClearTextPassword = Context.DataService.DecryptOrReturnOriginalString(_rdpServer.Password);
            _rdp.FullScreenTitle = _rdpServer.DisplayName + " - " + _rdpServer.SubTitle;

            #endregion server info
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
            _rdp.AdvancedSettings7.ConnectToAdministerServer = _rdpServer.IsAdministrativePurposes == true;
        }

        private void CreateRdp()
        {
            lock (this)
            {
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
            }
        }

        private void RdpInitConnBar()
        {
            SimpleLogHelper.Debug("RDP Host: init conn bar");
            _rdp.AdvancedSettings6.DisplayConnectionBar = _rdpServer.IsFullScreenWithConnectionBar == true;
            _rdp.AdvancedSettings6.ConnectionBarShowPinButton = true;
            _rdp.AdvancedSettings6.PinConnectionBar = false;
            _rdp.AdvancedSettings6.ConnectionBarShowMinimizeButton = true;
            _rdp.AdvancedSettings6.ConnectionBarShowRestoreButton = true;
            _rdp.AdvancedSettings6.BitmapVirtualCache32BppSize = 48;
            //((IMsRdpClientNonScriptable5) _rdp.GetOcx()).devi = _rdpServer.EnableDiskDrives;
        }


        public void NotifyRedirectDeviceChange(int msg, uint wParam, int lParam)
        {
            const int WM_DEVICECHANGE = 0x0219;
            // see https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable-notifyredirectdevicechange
            if (msg == WM_DEVICECHANGE
                && ((IMsRdpClientNonScriptable3)_rdp.GetOcx()).RedirectDynamicDevices)
                ((IMsRdpClientNonScriptable3)_rdp.GetOcx()).NotifyRedirectDeviceChange(wParam, lParam);
        }

        private void RdpInitRedirect()
        {
            SimpleLogHelper.Debug("RDP Host: init Redirect");

            #region Redirect
            // Specifies whether dynamically attached PnP devices that are enumerated while in a session are available for redirection. https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable3-redirectdynamicdevices
            ((IMsRdpClientNonScriptable3)_rdp.GetOcx()).RedirectDynamicDevices = _rdpServer.EnableDiskDrives == true;
            // Specifies or retrieves whether dynamically attached Plug and Play (PnP) drives that are enumerated while in a session are available for redirection. https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable3-redirectdynamicdrives
            ((IMsRdpClientNonScriptable3)_rdp.GetOcx()).RedirectDynamicDrives = _rdpServer.EnableDiskDrives == true;

            _rdp.AdvancedSettings9.RedirectDrives = _rdpServer.EnableDiskDrives == true;
            _rdp.AdvancedSettings9.RedirectClipboard = _rdpServer.EnableClipboard == true;
            _rdp.AdvancedSettings9.RedirectPrinters = _rdpServer.EnablePrinters == true;
            _rdp.AdvancedSettings9.RedirectPOSDevices = _rdpServer.EnablePorts == true;
            _rdp.AdvancedSettings9.RedirectSmartCards = _rdpServer.EnableSmartCardsAndWinHello == true;

            if (_rdpServer.EnableKeyCombinations == true)
            {
                // - 0 Apply key combinations only locally at the client computer.
                // - 1 Apply key combinations at the remote server.
                // - 2 Apply key combinations to the remote server only when the client is running in full-screen mode. This is the default value.
                _rdp.SecuredSettings3.KeyboardHookMode = 1;
            }
            else
                _rdp.SecuredSettings3.KeyboardHookMode = 0;

            if (_rdpServer.AudioRedirectionMode == EAudioRedirectionMode.RedirectToLocal)
            {
                // - 0 (Audio redirection is enabled and the option for redirection is "Bring to this computer". This is the default mode.)
                // - 1 (Audio redirection is enabled and the option is "Leave at remote computer". The "Leave at remote computer" option is supported only when connecting remotely to a host computer that is running Windows Vista. If the connection is to a host computer that is running Windows Server 2008, the option "Leave at remote computer" is changed to "Do not play".)
                // - 2 (Audio redirection is enabled and the mode is "Do not play".)
                _rdp.SecuredSettings3.AudioRedirectionMode = 0;

                // - 0 Dynamic audio quality. This is the default audio quality setting. The server dynamically adjusts audio output quality in response to network conditions and the client and server capabilities.
                // - 1 Medium audio quality. The server uses a fixed but compressed format for audio output.
                // - 2 High audio quality. The server provides audio output in uncompressed PCM format with lower processing overhead for latency.
                _rdp.AdvancedSettings8.AudioQualityMode = 0;
            }
            else if (_rdpServer.AudioRedirectionMode == EAudioRedirectionMode.LeaveOnRemote)
            {
                // - 1 (Audio redirection is enabled and the option is "Leave at remote computer". The "Leave at remote computer" option is supported only when connecting remotely to a host computer that is running Windows Vista. If the connection is to a host computer that is running Windows Server 2008, the option "Leave at remote computer" is changed to "Do not play".)
                _rdp.SecuredSettings3.AudioRedirectionMode = 1;
            }
            else if (_rdpServer.AudioRedirectionMode == EAudioRedirectionMode.Disabled)
            {
                // - 2 Disable sound redirection; do not play sounds at the server.
                _rdp.SecuredSettings3.AudioRedirectionMode = 2;
            }

            if (_rdpServer.EnableAudioCapture == true)
            {
                // indicates whether the default audio input device is redirected from the client to the remote session
                _rdp.AdvancedSettings8.AudioCaptureRedirectionMode = true;
            }
            else
            {
                _rdp.AdvancedSettings8.AudioCaptureRedirectionMode = false;
            }

            #endregion Redirect
        }

        private void RdpInitDisplay(double width = 0, double height = 0, bool isReconn = false)
        {
            #region Display

            _primaryScaleFactor = ReadScaleFactor();
            SimpleLogHelper.Debug($"RDP Host: init Display with ScaleFactor = {_primaryScaleFactor}, W = {width}, H = {height}");

            if (this._rdpServer.IsScaleFactorFollowSystem == false && this._rdpServer.ScaleFactorCustomValue != null)
            {
                _rdp.SetExtendedProperty("DesktopScaleFactor", this._rdpServer.ScaleFactorCustomValue ?? _primaryScaleFactor);
            }
            else
            {
                _rdp.SetExtendedProperty("DesktopScaleFactor", _primaryScaleFactor);
            }
            _rdp.SetExtendedProperty("DeviceScaleFactor", (uint)100);
            if (_rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.Stretch || _rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.StretchFullScreen)
                _rdp.AdvancedSettings2.SmartSizing = true;
            // to enhance user experience, i let the form handled full screen
            _rdp.AdvancedSettings6.ContainerHandledFullScreen = 1;

            // pre-set the rdp width & height
            switch (_rdpServer.RdpWindowResizeMode)
            {
                case ERdpWindowResizeMode.Stretch:
                case ERdpWindowResizeMode.Fixed:
                    _rdp.DesktopWidth = (int)(_rdpServer.RdpWidth ?? 800);
                    _rdp.DesktopHeight = (int)(_rdpServer.RdpHeight ?? 600);
                    break;
                case ERdpWindowResizeMode.FixedFullScreen:
                case ERdpWindowResizeMode.StretchFullScreen:
                    var size = GetScreenSize();
                    _rdp.DesktopWidth = size.Width;
                    _rdp.DesktopHeight = size.Height;
                    break;
                case ERdpWindowResizeMode.AutoResize:
                case null:
                default:
                    // default case, set rdp size to tab window size.
                    if (width < 100)
                        width = 800;
                    if (height < 100)
                        height = 600;

                    // if isReconn == false, then width is Tab width, true width = Tab width * ScaleFactor
                    // if isReconn == true, then width is DesktopWidth, ScaleFactor should == 100
                    if (isReconn)
                    {
                        _rdp.DesktopWidth = (int)(width);
                        _rdp.DesktopHeight = (int)(height);
                    }
                    else
                    {
                        _rdp.DesktopWidth = (int)(width * (_primaryScaleFactor / 100.0));
                        _rdp.DesktopHeight = (int)(height * (_primaryScaleFactor / 100.0));
                    }
                    break;
            }



            switch (_rdpServer.RdpFullScreenFlag)
            {
                case ERdpFullScreenFlag.Disable:
                    base.CanFullScreen = false;
                    break;

                case ERdpFullScreenFlag.EnableFullAllScreens:
                    base.CanFullScreen = true;
                    ((IMsRdpClientNonScriptable5)_rdp.GetOcx()).UseMultimon = true;
                    break;
                case ERdpFullScreenFlag.EnableFullScreen:
                default:
                    base.CanFullScreen = true;
                    break;
            }

            #endregion Display

            SimpleLogHelper.Debug($"RDP Host: Display init end: RDP.DesktopWidth = {_rdp.DesktopWidth}, RDP.DesktopWidth = {_rdp.DesktopWidth},");
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
                }
            }
            SimpleLogHelper.Debug("RdpInit: DisplayPerformance = " + _rdpServer.DisplayPerformance + ", flag = " + Convert.ToString(nDisplayPerformanceFlag, 2));
            _rdp.AdvancedSettings9.PerformanceFlags = nDisplayPerformanceFlag;

            #endregion Performance
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
                        _rdp.TransportSettings2.GatewayPassword = Context.DataService.DecryptOrReturnOriginalString(_rdpServer.GatewayPassword);
                        break;

                    default:
                        _rdp.TransportSettings.GatewayCredsSource = 4; // TSC_PROXY_CREDS_MODE_ANY
                        break;
                }

                _rdp.TransportSettings2.GatewayCredSharing = 0;
            }

            #endregion Gateway
        }

        private void InitRdp(double width = 0, double height = 0, bool isReconn = false)
        {
            if (Status != ProtocolHostStatus.NotInit)
                return;
            try
            {
                Status = ProtocolHostStatus.Initializing;
                RdpDispose();
                CreateRdp();
                RdpInitServerInfo();
                RdpInitStatic();
                RdpInitConnBar();
                RdpInitRedirect();
                RdpInitDisplay(width, height, isReconn);
                RdpInitPerformance();
                RdpInitGateway();

                Status = ProtocolHostStatus.Initialized;
            }
            catch (Exception e)
            {
                GridMessageBox.Visibility = Visibility.Visible;
                TbMessageTitle.Visibility = Visibility.Collapsed;
                TbMessage.Text = e.Message;

                Status = ProtocolHostStatus.NotInit;
            }
        }

        #region Base Interface

        public override void Conn()
        {
            try
            {
                if (Status == ProtocolHostStatus.Connected || Status == ProtocolHostStatus.Connecting)
                {
                    return;
                }
                Status = ProtocolHostStatus.Connecting;
                GridLoading.Visibility = Visibility.Visible;
                RdpHost.Visibility = Visibility.Collapsed;
                _rdp.Connect();
            }
            catch (Exception e)
            {
                Status = ProtocolHostStatus.Connected;
                GridMessageBox.Visibility = Visibility.Visible;
                TbMessageTitle.Visibility = Visibility.Collapsed;
                TbMessage.Text = e.Message;
            }
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

            // if parent is FullScreenWindow, go to full screen.
            if (!(ParentWindow is ITab))
            {
                SimpleLogHelper.Debug("RDP Host: ReConn with full screen");
                GoFullScreen();
            }

            _invokeOnClosedWhenDisconnected = true;
        }

        public override void Close()
        {
            this.Dispatcher.Invoke(() =>
            {
                Grid?.Children?.Clear();
            });
            RdpDispose();
            base.Close();
        }

        public override void GoFullScreen()
        {
            Debug.Assert(this.ParentWindow != null);

            switch (_rdpServer.RdpFullScreenFlag)
            {
                case ERdpFullScreenFlag.Disable:
                    return;
                case ERdpFullScreenFlag.EnableFullScreen:
                case null:
                    _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = ScreenInfoEx.GetCurrentScreen(this.ParentWindow).Index;
                    break;
                case ERdpFullScreenFlag.EnableFullAllScreens:
                    _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = -1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Context.DataService.Database_UpdateServer(_rdpServer);
            _rdp.FullScreen = true; // this will invoke OnRequestGoFullScreen -> MakeNormal2FullScreen
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
            return Status == ProtocolHostStatus.Connected && _flagHasLogin == true;
        }

        #endregion Base Interface

        #region event handler

        #region connection

        private bool _invokeOnClosedWhenDisconnected = true;

        private void RdpOnDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            SimpleLogHelper.Debug("RDP Host: RdpOnDisconnected");

            lock (this)
            {
                if (_rdp == null)
                    return;

                Status = ProtocolHostStatus.Disconnected;
                ResizeEndStopFireDelegate();
                if (this._onResizeEnd != null)
                    this._onResizeEnd -= ReSizeRdpOnResizeEnd;

                const int UI_ERR_NORMAL_DISCONNECT = 0xb08;
                string reason = _rdp?.GetErrorDescription((uint)e.discReason, (uint)_rdp.ExtendedDisconnectReason);
                if (e.discReason != UI_ERR_NORMAL_DISCONNECT)
                    SimpleLogHelper.Warning($"RDP({_rdpServer.DisplayName}) exit with error code {e.discReason}({reason})");

                // disconnectReasonByServer (3 (0x3))
                // https://docs.microsoft.com/zh-cn/windows/win32/termserv/imstscaxevents-ondisconnected?redirectedfrom=MSDN

                try
                {
                    _resizeEndTimer?.Dispose();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }

                if (!string.IsNullOrWhiteSpace(reason)
                    && (_flagHasConnected != true ||
                     e.discReason != UI_ERR_NORMAL_DISCONNECT
                     && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedDisconnect
                     && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedLogoff
                     && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonNoInfo                // log out from win2008 will reply exDiscReasonNoInfo
                     && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonLogoffByUser          // log out from win10 will reply exDiscReasonLogoffByUser
                     && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonRpcInitiatedDisconnectByUser    // log out from win2016 will reply exDiscReasonLogoffByUser
                    ))
                {
                    BtnReconn.Visibility = Visibility.Collapsed;
                    RdpHost.Visibility = Visibility.Collapsed;
                    GridMessageBox.Visibility = Visibility.Visible;
                    if (_flagHasConnected == true
                        && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonReplacedByOtherConnection
                        && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonOutOfMemory
                        && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerDeniedConnection
                        && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerDeniedConnectionFips
                        && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonServerInsufficientPrivileges
                        && _rdp?.ExtendedDisconnectReason != ExtendedDisconnectReasonCode.exDiscReasonNoInfo  // conn to a power-off PC will get exDiscReasonNoInfo
                        && _retryCount < MaxRetryCount)
                    {
                        ++_retryCount;
                        TbMessageTitle.Visibility = Visibility.Visible;
                        TbMessageTitle.Text = Context.LanguageService.Translate("host_reconecting_info") + $"({_retryCount}/{MaxRetryCount})";
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

            _flagHasLogin = true;
            OnCanResizeNowChanged?.Invoke();
            RdpHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Collapsed;
            ResizeEndStartFireDelegate();
            try
            {
                this._onResizeEnd -= ReSizeRdpOnResizeEnd;
            }
            finally
            {
                this._onResizeEnd += ReSizeRdpOnResizeEnd;
            }

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(1000);
                lock (this)
                {
                    ReSizeRdpOnResizeEnd(); 
                }
            });
        }

        private void RdpOnConfirmClose(object sender, IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            SimpleLogHelper.Debug("RDP Host:  RdpOnConfirmClose");

            base.OnClosed?.Invoke(base.ConnectionId);
        }

        #endregion connection

        /// <summary>
        /// set remote resolution to _rdp size if is AutoResize
        /// </summary>
        private void ReSizeRdpOnResizeEnd()
        {
            if (_rdp?.FullScreen == false
                && _rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.AutoResize)
            {
                var nw = (uint)(_rdp?.Width ?? 0);
                var nh = (uint)(_rdp?.Height ?? 0);
                SetRdpResolution(nw, nh);
            }
        }

        private void SetRdpResolution(uint w, uint h)
        {
            if (w > 0 && h > 0)
            {
                try
                {
                    SimpleLogHelper.Debug($@"RDP resize to: W = {w}, H = {h}, ScaleFactor = {_primaryScaleFactor}");
                    _primaryScaleFactor = ReadScaleFactor();
                    var newScaleFactor = _primaryScaleFactor;
                    if (this._rdpServer.IsScaleFactorFollowSystem == false && this._rdpServer.ScaleFactorCustomValue != null)
                        newScaleFactor = this._rdpServer.ScaleFactorCustomValue ?? _primaryScaleFactor;
                    if (_rdp?.DesktopWidth != w || _rdp?.DesktopHeight != h || newScaleFactor != _lastScaleFactor)
                    {
                        _rdp?.UpdateSessionDisplaySettings(w, h, w, h, 0, newScaleFactor, 100);
                        _lastScaleFactor = newScaleFactor;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private void MakeNormal2FullScreen()
        {
            // make sure ParentWindow is FullScreen Window
            Debug.Assert(ParentWindow != null);
            if (ParentWindow is ITab)
            {
                // full-all-screen session switch to TabWindow, and click "Reconn" button, will entry this case.
                _rdp.FullScreen = false;
                _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = false;
                return;
            }

            _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = true;

            var screenSize = GetScreenSize();

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
            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen)
            {
                switch (_rdpServer.RdpWindowResizeMode)
                {
                    case null:
                    case ERdpWindowResizeMode.AutoResize:
                    case ERdpWindowResizeMode.FixedFullScreen:
                        SetRdpResolution((uint)screenSize.Width, (uint)screenSize.Height);
                        break;
                    case ERdpWindowResizeMode.Stretch:
                    case ERdpWindowResizeMode.Fixed:
                        SetRdpResolution((uint)(_rdpServer.RdpWidth ?? 800), (uint)(_rdpServer.RdpHeight ?? 600));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = true;

            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen)
            {
                _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = ScreenInfoEx.GetCurrentScreen(this.ParentWindow).Index;
            }
            else
                _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = -1;
            Context.DataService.Database_UpdateServer(_rdpServer);
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
            _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = false;
            // make sure ParentWindow is FullScreen Window
            Debug.Assert(ParentWindow != null);
            if (ParentWindow is ITab)
            {
                return;
            }

            // ! don not remove
            ParentWindow.Topmost = false;
            ParentWindow.ResizeMode = ResizeMode.CanResize;
            ParentWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            ParentWindow.WindowState = WindowState.Normal;

            _rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = false;
            base.OnFullScreen2Window?.Invoke(base.ConnectionId);
            Context.DataService.Database_UpdateServer(_rdpServer);
        }

        private void MakeForm2Minimize()
        {
            Debug.Assert(ParentWindow != null);
            ParentWindow.WindowState = WindowState.Minimized;
        }

        #endregion event handler

        private static uint ReadScaleFactor()
        {
            uint sf = 100;
            try
            {
                // !must use PrimaryScreen scale factor
                sf = (uint)(100 * System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth);
            }
            catch (Exception)
            {
                sf = 100;
            }
            finally
            {
                if (sf < 100)
                    sf = 100;
            }
            return sf;
        }

        #region WindowOnResizeEnd

        private bool _canAutoResizeByWindowSizeChanged = true;

        /// <summary>
        /// when tab window goes to min from max, base.SizeChanged invoke and size will get bigger, normal to min will not tiger this issue, don't know why.
        /// so stop resize when window status change to min until status restore.
        /// </summary>
        /// <param name="isEnable"></param>
        public override void ToggleAutoResize(bool isEnable)
        {
            _canAutoResizeByWindowSizeChanged = isEnable;
        }

        private delegate void ResizeEndDelegage();

        private ResizeEndDelegage _onResizeEnd;
        private readonly System.Timers.Timer _resizeEndTimer = new System.Timers.Timer(500) { Enabled = false };
        private readonly object _resizeEndLocker = new object();
        private void ResizeEndStartFireDelegate()
        {
            lock (_resizeEndLocker)
            {
                try
                {
                    _resizeEndTimer.Elapsed -= _InvokeResizeEndEnd;
                }
                finally
                {
                    _resizeEndTimer.Elapsed += _InvokeResizeEndEnd;
                }

                try
                {
                    base.SizeChanged -= _ResizeEnd_WindowSizeChanged;
                }
                finally
                {
                    base.SizeChanged += _ResizeEnd_WindowSizeChanged;
                }
            }
        }

        private void ResizeEndStopFireDelegate()
        {
            lock (_resizeEndLocker)
            {
                try
                {
                    _resizeEndTimer?.Stop();
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

        private void _ResizeEnd_WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_canAutoResizeByWindowSizeChanged && this._rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.AutoResize)
            {
                try
                {
                    _resizeEndTimer?.Stop();
                    _resizeEndTimer?.Start();
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private void _InvokeResizeEndEnd(object sender, ElapsedEventArgs e)
        {
            try
            {
                _resizeEndTimer?.Stop();
                _onResizeEnd?.Invoke();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        #endregion WindowOnResizeEnd

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
                try
                {
                    GlobalEventHelper.OnScreenResolutionChanged -= OnScreenResolutionChanged;
                }
                catch
                {
                }
                var tmp = _rdp;
                var t = new Task(() =>
                {
                    try
                    {
                        if (tmp.Connected > 0)
                            tmp.Disconnect();
                        tmp.Dispose();
                    }
                    finally
                    {
                        tmp = null;
                    }
                });
                t.Start();
                lock (this)
                {
                    _rdp = null;
                }
            }
            SimpleLogHelper.Debug("RDP Host: _rdp.Dispose()");
        }
    }
}