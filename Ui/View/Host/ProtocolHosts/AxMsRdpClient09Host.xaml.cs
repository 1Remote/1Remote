using System;
using System.Diagnostics;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using _1RM.Utils.RdpFile;
using MSTSCLib;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using Color = System.Drawing.Color;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Timer = System.Timers.Timer;

namespace _1RM.View.Host.ProtocolHosts
{
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


    public sealed partial class AxMsRdpClient09Host : HostBase, IDisposable
    {
        private AxMsRdpClient9NotSafeForScriptingEx? _rdpClient = null;
        //private readonly DataSourceBase? _dataSource;
        private readonly RDP _rdpSettings;
        /// <summary>
        /// system scale factor, 100 = 100%, 200 = 200%
        /// </summary>
        private uint _primaryScaleFactor = 100;

        private bool _flagHasConnected = false;

        private int _retryCount = 0;
        private const int MAX_RETRY_COUNT = 20;

        private readonly System.Timers.Timer _loginResizeTimer;
        private DateTime _lastLoginTime = DateTime.MinValue;


        public static AxMsRdpClient09Host Create(RDP rdp, int width = 0, int height = 0)
        {
            AxMsRdpClient09Host? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new AxMsRdpClient09Host(rdp, width, height);
            });
            return view!;
        }

        private AxMsRdpClient09Host(RDP rdp, int width = 0, int height = 0) : base(rdp, true)
        {
            InitializeComponent();

            GridMessageBox.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;

            _rdpSettings = rdp;

            _loginResizeTimer = new Timer(300) { Enabled = false, AutoReset = false };
            _loginResizeTimer.Elapsed += (sender, args) =>
            {
                _loginResizeTimer.Stop();
                try
                {
                    var nw = (uint)(_rdpClient?.Width ?? 0);
                    var nh = (uint)(_rdpClient?.Height ?? 0);
                    // tip: the control default width is 288
                    if (_rdpClient?.DesktopWidth > nw
                        || _rdpClient?.DesktopHeight > nh)
                    {
                        SimpleLogHelper.DebugInfo($@"_loginResizeTimer start run... {_rdpClient?.DesktopWidth}, {nw}, {_rdpClient?.DesktopHeight}, {nh}");
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

            InitRdp(width, height);
            GlobalEventHelper.OnScreenResolutionChanged += OnScreenResolutionChanged;
        }

        ~AxMsRdpClient09Host()
        {
            SimpleLogHelper.Debug($"Release {this.GetType().Name}({this.GetHashCode()})");
            Dispose();
        }

        public void Dispose()
        {
            SimpleLogHelper.Debug($"Disposing {this.GetType().Name}({this.GetHashCode()})");
            _resizeEndTimer?.Dispose();
            _loginResizeTimer?.Dispose();
            RdpClientDispose();
            SimpleLogHelper.Debug($"Dispose done {this.GetType().Name}({this.GetHashCode()})");
        }

        private void OnScreenResolutionChanged()
        {
            lock (this)
            {
                // 全屏模式下客户端机器发生了屏幕分辨率改变，则将RDP还原到窗口模式（仿照 MSTSC 的逻辑）
                if (_rdpClient?.FullScreen == true)
                {
                    _rdpClient.FullScreen = false;
                }
            }
        }

        /// <summary>
        /// init server connection info: user name\ psw \ port \ LoadBalanceInfo...
        /// </summary>
        private void RdpInitServerInfo()
        {
            #region server info
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            // server connection info: user name\ psw \ port ...
            _rdpClient.Server = _rdpSettings.Address;
            _rdpClient.Domain = _rdpSettings.Domain;
            _rdpClient.UserName = _rdpSettings.UserName;
            _rdpClient.AdvancedSettings2.RDPPort = _rdpSettings.GetPort();


            if (string.IsNullOrWhiteSpace(_rdpSettings.LoadBalanceInfo) == false)
            {
                var loadBalanceInfo = _rdpSettings.LoadBalanceInfo;
                if (loadBalanceInfo.Length % 2 == 1)
                    loadBalanceInfo += " ";
                loadBalanceInfo += "\r\n";
                var bytes = Encoding.UTF8.GetBytes(loadBalanceInfo);
                _rdpClient.AdvancedSettings2.LoadBalanceInfo = Encoding.Unicode.GetString(bytes);
            }



            var secured = (MSTSCLib.IMsTscNonScriptable)_rdpClient.GetOcx();
            secured.ClearTextPassword = UnSafeStringEncipher.DecryptOrReturnOriginalString(_rdpSettings.Password);
            _rdpClient.FullScreenTitle = _rdpSettings.DisplayName + " - " + _rdpSettings.SubTitle;

            #endregion server info
        }

        private void RdpInitStatic()
        {
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            SimpleLogHelper.Debug("RDP Host: init Static");
            // enable CredSSP, will use CredSsp if the client supports.
            _rdpClient.AdvancedSettings7.EnableCredSspSupport = true;
            _rdpClient.AdvancedSettings2.EncryptionEnabled = 1;
            _rdpClient.AdvancedSettings5.AuthenticationLevel = 0;
            _rdpClient.AdvancedSettings5.EnableAutoReconnect = true;
            // setting PublicMode to false allows the saving of credentials, which prevents
            _rdpClient.AdvancedSettings6.PublicMode = false;
            _rdpClient.AdvancedSettings5.EnableWindowsKey = 1;
            _rdpClient.AdvancedSettings5.GrabFocusOnConnect = true;
            _rdpClient.AdvancedSettings2.keepAliveInterval = 1000 * 60 * 1; // 1000 = 1000 ms
            _rdpClient.AdvancedSettings2.overallConnectionTimeout = 600; // The new time, in seconds. The maximum value is 600, which represents 10 minutes.

            // enable CredSSP, will use CredSsp if the client supports.
            _rdpClient.AdvancedSettings9.EnableCredSspSupport = true;

            //- 0: If server authentication fails, connect to the computer without warning (Connect and don't warn me)
            //- 1: If server authentication fails, don't establish a connection (Don't connect)
            //- 2: If server authentication fails, show a warning and allow me to connect or refuse the connection (Warn me)
            //- 3: No authentication requirement specified.
            _rdpClient.AdvancedSettings9.AuthenticationLevel = 0;

            // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings6-connecttoadministerserver
            _rdpClient.AdvancedSettings7.ConnectToAdministerServer = _rdpSettings.IsAdministrativePurposes == true;
        }

        private void CreateRdpClient()
        {
            lock (this)
            {
                _rdpClient = new AxMsRdpClient9NotSafeForScriptingEx();

                SimpleLogHelper.Debug("RDP Host: init new AxMsRdpClient9NotSafeForScriptingEx()");

                ((System.ComponentModel.ISupportInitialize)(_rdpClient)).BeginInit();
                _rdpClient.Dock = DockStyle.Fill;
                _rdpClient.Enabled = true;
                _rdpClient.BackColor = Color.Black;
                // set call back
                _rdpClient.OnRequestGoFullScreen += (sender, args) =>
                {
                    SimpleLogHelper.Debug("RDP Host:  OnRequestGoFullScreen");
                    OnGoToFullScreenRequested();
                };
                _rdpClient.OnRequestLeaveFullScreen += (sender, args) =>
                {
                    SimpleLogHelper.Debug("RDP Host:  OnRequestLeaveFullScreen");
                    OnConnectionBarRestoreWindowCall();
                };
                _rdpClient.OnRequestContainerMinimize += (sender, args) =>
                {
                    SimpleLogHelper.Debug("RDP Host:  OnRequestContainerMinimize");
                    if (ParentWindow is FullScreenWindowView)
                    {
                        ParentWindow.WindowState = WindowState.Minimized;
                    }
                };
                _rdpClient.OnDisconnected += OnRdpClientDisconnected;
                _rdpClient.OnConfirmClose += (sender, args) =>
                {
                    // invoke in the full screen mode.
                    SimpleLogHelper.Debug("RDP Host:  RdpOnConfirmClose");
                    base.OnClosed?.Invoke(base.ConnectionId);
                };
                _rdpClient.OnConnected += OnRdpClientConnected;
                _rdpClient.OnLoginComplete += OnRdpClientLoginComplete;
                ((System.ComponentModel.ISupportInitialize)(_rdpClient)).EndInit();
                RdpHost.Child = _rdpClient;

                SimpleLogHelper.Debug("RDP Host: init CreateControl();");
                _rdpClient.CreateControl();
            }
        }

        private void RdpInitConnBar()
        {
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            SimpleLogHelper.Debug("RDP Host: init conn bar");
            _rdpClient.AdvancedSettings6.DisplayConnectionBar = _rdpSettings.IsFullScreenWithConnectionBar == true;
            if (_rdpClient.AdvancedSettings6.DisplayConnectionBar)
            {
                _rdpClient.AdvancedSettings6.ConnectionBarShowPinButton = true;
                _rdpClient.AdvancedSettings6.PinConnectionBar = _rdpSettings.IsPinTheConnectionBarByDefault == true;
            }
            _rdpClient.AdvancedSettings6.ConnectionBarShowMinimizeButton = true;
            _rdpClient.AdvancedSettings6.ConnectionBarShowRestoreButton = true;
            _rdpClient.AdvancedSettings6.BitmapVirtualCache32BppSize = 48;
        }

        public void NotifyRedirectDeviceChange(int msg, IntPtr wParam, IntPtr lParam)
        {
            const int WM_DEVICECHANGE = 0x0219;

            /* from https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable-notifyredirectdevicechange
             *      https://learn.microsoft.com/en-us/windows/win32/devio/wm-devicechange
             * wParam case when msg == WM_DEVICECHANGE:
             * DBT_DEVNODES_CHANGED     0x0007      A device has been added to or removed from the system. param = 0
             * DBT_DEVICEARRIVAL        0x8000      A device or piece of media has been inserted and is now available. param = A pointer to a structure identifying the device inserted. 
             */
            SimpleLogHelper.Debug($"RDP: NotifyRedirectDeviceChange Receive(0x{msg:X}, 0x{wParam:X}, 0x{lParam:X})");
            if (msg == WM_DEVICECHANGE
                && _rdpClient != null
                && ((IMsRdpClientNonScriptable3)_rdpClient.GetOcx()).RedirectDynamicDevices)
            {
                new MsRdpClientNonScriptableWrapper(_rdpClient.GetOcx()).NotifyRedirectDeviceChange(wParam, lParam);
            }
        }

        private void RdpInitRedirect()
        {
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            SimpleLogHelper.Debug("RDP Host: init Redirect");


            #region Redirect

            // purpose is not clear
            ((IMsRdpClientNonScriptable3)_rdpClient.GetOcx()).RedirectDynamicDrives = true; // Specifies or retrieves whether dynamically attached Plug and Play (PnP) drives that are enumerated while in a session are available for redirection. https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable3-redirectdynamicdrives

            if (_rdpSettings.EnableDiskDrives == true
                || _rdpSettings.EnableRedirectDrivesPlugIn == true)
            {
                _rdpClient.AdvancedSettings9.RedirectDrives = true;

                // enable then usb disk can be redirect
                if (_rdpSettings.EnableRedirectDrivesPlugIn == true)
                {
                    ((IMsRdpClientNonScriptable3)_rdpClient.GetOcx()).RedirectDynamicDevices = true; // Specifies whether dynamically attached PnP devices that are enumerated while in a session are available for redirection. https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable3-redirectdynamicdevices
                    RedirectDevice();
                }
            }

            // disable local disk
            if (_rdpSettings.EnableDiskDrives == false)
            {
                var ocx = (MSTSCLib.IMsRdpClientNonScriptable7)_rdpClient.GetOcx();
                ocx.DriveCollection.RescanDrives(false);
                for (int i = 0; i < ocx.DriveCollection.DriveCount; i++)
                {
                    ocx.DriveCollection.DriveByIndex[(uint)i].RedirectionState = false;
                }
            }


            _rdpClient.AdvancedSettings9.RedirectClipboard = _rdpSettings.EnableClipboard == true;
            _rdpClient.AdvancedSettings9.RedirectPrinters = _rdpSettings.EnablePrinters == true;
            _rdpClient.AdvancedSettings9.RedirectPOSDevices = _rdpSettings.EnablePorts == true;
            _rdpClient.AdvancedSettings9.RedirectSmartCards = _rdpSettings.EnableSmartCardsAndWinHello == true;


            if (_rdpSettings.EnableKeyCombinations == true)
            {
                // - 0 Apply key combinations only locally at the client computer.
                // - 1 Apply key combinations at the remote server.
                // - 2 Apply key combinations to the remote server only when the client is running in full-screen mode. This is the default value.
                _rdpClient.SecuredSettings3.KeyboardHookMode = 1;
            }
            else
            {
                _rdpClient.SecuredSettings3.KeyboardHookMode = 0;
            }

            if (_rdpSettings.AudioRedirectionMode == EAudioRedirectionMode.RedirectToLocal)
            {
                // - 0 (Audio redirection is enabled and the option for redirection is "Bring to this computer". This is the default mode.)
                // - 1 (Audio redirection is enabled and the option is "Leave at remote computer". The "Leave at remote computer" option is supported only when connecting remotely to a host computer that is running Windows Vista. If the connection is to a host computer that is running Windows Server 2008, the option "Leave at remote computer" is changed to "Do not play".)
                // - 2 (Audio redirection is enabled and the mode is "Do not play".)
                _rdpClient.SecuredSettings3.AudioRedirectionMode = 0;

                // Only set AudioQuality Moode when AudioRedirectionMode == RedirectToLocal
                if (_rdpSettings.AudioQualityMode == EAudioQualityMode.Dynamic)
                {
                    // - 0 Dynamic audio quality. This is the default audio quality setting. The server dynamically adjusts audio output quality in response to network conditions and the client and server capabilities.
                    // - 1 Medium audio quality. The server uses a fixed but compressed format for audio output.
                    // - 2 High audio quality. The server provides audio output in uncompressed PCM format with lower processing overhead for latency.
                    _rdpClient.AdvancedSettings8.AudioQualityMode = 0;
                }
                else if (_rdpSettings.AudioQualityMode == EAudioQualityMode.Medium)
                {
                    // - 1 Medium audio quality. The server uses a fixed but compressed format for audio output.
                    _rdpClient.AdvancedSettings8.AudioQualityMode = 1;
                }
                else if (_rdpSettings.AudioQualityMode == EAudioQualityMode.High)
                {
                    // - 2 High audio quality. The server provides audio output in uncompressed PCM format with lower processing overhead for latency.
                    _rdpClient.AdvancedSettings8.AudioQualityMode = 2;
                }

            }
            else if (_rdpSettings.AudioRedirectionMode == EAudioRedirectionMode.LeaveOnRemote)
            {
                // - 1 (Audio redirection is enabled and the option is "Leave at remote computer". The "Leave at remote computer" option is supported only when connecting remotely to a host computer that is running Windows Vista. If the connection is to a host computer that is running Windows Server 2008, the option "Leave at remote computer" is changed to "Do not play".)
                _rdpClient.SecuredSettings3.AudioRedirectionMode = 1;
            }
            else if (_rdpSettings.AudioRedirectionMode == EAudioRedirectionMode.Disabled)
            {
                // - 2 Disable sound redirection; do not play sounds at the server.
                _rdpClient.SecuredSettings3.AudioRedirectionMode = 2;
            }

            if (_rdpSettings.EnableAudioCapture == true)
            {
                // indicates whether the default audio input device is redirected from the client to the remote session
                _rdpClient.AdvancedSettings8.AudioCaptureRedirectionMode = true;
            }
            else
            {
                _rdpClient.AdvancedSettings8.AudioCaptureRedirectionMode = false;
            }
            #endregion Redirect
        }


        public void RedirectDevice()
        {
            var ocx = _rdpClient?.GetOcx() as MSTSCLib.IMsRdpClientNonScriptable7;
            if (ocx == null)
                return;
            ocx.CameraRedirConfigCollection.RedirectByDefault = false;
            if (_rdpSettings.EnableRedirectCameras == true)
            {
                // enumerates connected camera devices
                ocx.CameraRedirConfigCollection.Rescan();
                for (int i = 0; i < ocx.CameraRedirConfigCollection.Count; i++)
                {
                    var camera = ocx.CameraRedirConfigCollection.ByIndex[(uint)i];
                    camera.Redirected = true;
                }
            }

            ocx.DeviceCollection.RescanDevices(false);
            for (uint i = 0; i < ocx.DeviceCollection.DeviceCount; i++)
            {
                var d = ocx.DeviceCollection.DeviceByIndex[i];
                SimpleLogHelper.Debug(d.FriendlyName);
                SimpleLogHelper.Debug(d.DeviceDescription);
                d.RedirectionState = true;
            }
        }


        private void RdpInitDisplay(int width = 0, int height = 0, bool isReconnecting = false)
        {
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            #region Display

            _primaryScaleFactor = ScreenInfoEx.GetPrimaryScreenScaleFactor();
            SimpleLogHelper.Debug($"RDP Host: init Display with ScaleFactor = {_primaryScaleFactor}, W = {width}, H = {height}, isReconnecting = {isReconnecting}");

            if (this._rdpSettings.IsScaleFactorFollowSystem == false && this._rdpSettings.ScaleFactorCustomValue != null)
            {
                _rdpClient.SetExtendedProperty("DesktopScaleFactor", this._rdpSettings.ScaleFactorCustomValue ?? _primaryScaleFactor);
            }
            else
            {
                _rdpClient.SetExtendedProperty("DesktopScaleFactor", _primaryScaleFactor);
            }
            _rdpClient.SetExtendedProperty("DeviceScaleFactor", (uint)100);
            if (_rdpSettings.RdpWindowResizeMode == ERdpWindowResizeMode.Stretch || _rdpSettings.RdpWindowResizeMode == ERdpWindowResizeMode.StretchFullScreen)
                _rdpClient.AdvancedSettings2.SmartSizing = true;
            // to enhance user experience, i let the form handled full screen
            _rdpClient.AdvancedSettings6.ContainerHandledFullScreen = 1;

            // pre-set the rdp width & height
            switch (_rdpSettings.RdpWindowResizeMode)
            {
                case ERdpWindowResizeMode.Stretch:
                case ERdpWindowResizeMode.Fixed:
                    _rdpClient.DesktopWidth = (int)(_rdpSettings.RdpWidth ?? 800);
                    _rdpClient.DesktopHeight = (int)(_rdpSettings.RdpHeight ?? 600);
                    break;
                case ERdpWindowResizeMode.FixedFullScreen:
                case ERdpWindowResizeMode.StretchFullScreen:
                    {
                        var size = GetScreenSizeIfRdpIsFullScreen();
                        _rdpClient.DesktopWidth = size.Width;
                        _rdpClient.DesktopHeight = size.Height;
                        break;
                    }
                case ERdpWindowResizeMode.AutoResize:
                case null:
                default:
                    {
                        // default case, set rdp size to tab window size.
                        if (width < 100)
                            width = 800;
                        if (height < 100)
                            height = 600;


                        //if (isReconnecting == true)
                        //{
                        //    // if isReconnecting == true, then width is DesktopWidth, ScaleFactor should be 100
                        //    _rdpClient.DesktopWidth = (int)(width);
                        //    _rdpClient.DesktopHeight = (int)(height);
                        //}
                        //else
                        {
                            // if isReconnecting == false, then width is Tab width, true width = Tab width * ScaleFactor
                            if (_rdpSettings.IsThisTimeConnWithFullScreen())
                            {
                                var size = GetScreenSizeIfRdpIsFullScreen();
                                _rdpClient.DesktopWidth = size.Width;
                                _rdpClient.DesktopHeight = size.Height;
                                SimpleLogHelper.DebugInfo($"RDP Host: init Display set FullScreen DesktopWidth = {_rdpClient.DesktopWidth},  DesktopHeight = {_rdpClient.DesktopHeight}");
                            }
                            else
                            {
                                _rdpClient.DesktopWidth = (int)(width * (_primaryScaleFactor / 100.0));
                                _rdpClient.DesktopHeight = (int)(height * (_primaryScaleFactor / 100.0));
                                SimpleLogHelper.DebugInfo(@$"RDP Host: init Display set DesktopWidth = {width} * {(_primaryScaleFactor / 100.0):F3} = {_rdpClient.DesktopWidth},  DesktopHeight = {height} * {(_primaryScaleFactor / 100.0):F3} = {_rdpClient.DesktopHeight},     RdpControl.Width = {_rdpClient.Width}, RdpControl.Height = {_rdpClient.Height}");
                                if (_primaryScaleFactor > 100)
                                {
                                    // size compensation since https://github.com/1Remote/1Remote/issues/537
                                    int c = (_primaryScaleFactor % 100) switch
                                    {
                                        50 => 1,
                                        75 => 2,
                                        _ => 0
                                    };
                                    if (ColorAndBrushHelper.ColorIsTransparent(_rdpSettings.ColorHex) != true)
                                    {
                                        c *= 2;
                                    }
                                    if (c < _rdpClient.DesktopWidth && c < _rdpClient.DesktopHeight)
                                    {
                                        //_rdpClient.DesktopWidth -= c;
                                        _rdpClient.DesktopHeight -= c;
                                    }
                                    SimpleLogHelper.DebugInfo($"RDP Host: init Display set DesktopWidth = {_rdpClient.DesktopWidth},  DesktopHeight = {_rdpClient.DesktopHeight}");
                                }
                            }
                        }

                        break;
                    }
            }



            switch (_rdpSettings.RdpFullScreenFlag)
            {
                case ERdpFullScreenFlag.Disable:
                    base.CanFullScreen = false;
                    break;

                case ERdpFullScreenFlag.EnableFullAllScreens:
                    base.CanFullScreen = true;
                    ((IMsRdpClientNonScriptable5)_rdpClient.GetOcx()).UseMultimon = true;
                    break;
                case ERdpFullScreenFlag.EnableFullScreen:
                default:
                    base.CanFullScreen = true;
                    break;
            }

            #endregion Display

            // 2022.07.23 try to fix the rdp error code 4360, ref: https://forum.asg-rd.com/showthread.php?tid=11016&page=2
            _rdpClient.AdvancedSettings8.BitmapPersistence = 0;
            _rdpClient.AdvancedSettings8.CachePersistenceActive = 0;

            SimpleLogHelper.Debug($"RDP Host: Display init end: RDP.DesktopWidth = {_rdpClient.DesktopWidth}, RDP.DesktopHeight = {_rdpClient.DesktopHeight},");
        }

        private void RdpInitPerformance()
        {
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            SimpleLogHelper.Debug("RDP Host: init Performance");

            #region Performance

            // if win11 disable BandwidthDetection, make a workaround for #437 to hide info button after OS Win11 22H2 to avoid app crash when click the info button on Win11
            // detail: https://github.com/1Remote/1Remote/issues/437
            try
            {
                if (_1RM.Utils.WindowsApi.WindowsVersionHelper.IsWindows1122H2OrHigher()) // Win11 22H2
                {
                    _rdpClient.AdvancedSettings9.BandwidthDetection = false;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings-performanceflags
            int nDisplayPerformanceFlag = 0;
            if (_rdpSettings.DisplayPerformance != EDisplayPerformance.Auto)
            {
                // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings7-networkconnectiontype
                // CONNECTION_TYPE_MODEM (1 (0x1)) Modem (56 Kbps)
                // CONNECTION_TYPE_BROADBAND_LOW (2 (0x2)) Low-speed broadband (256 Kbps to 2 Mbps) CONNECTION_TYPE_SATELLITE (3 (0x3)) Satellite (2 Mbps to 16 Mbps, with high latency)
                // CONNECTION_TYPE_BROADBAND_HIGH (4 (0x4)) High-speed broadband (2 Mbps to 10 Mbps) CONNECTION_TYPE_WAN (5 (0x5)) Wide area network (WAN) (10 Mbps or higher, with high latency)
                // CONNECTION_TYPE_LAN (6 (0x6)) Local area network (LAN) (10 Mbps or higher)
                _rdpClient.AdvancedSettings8.NetworkConnectionType = 1;
                switch (_rdpSettings.DisplayPerformance)
                {
                    case EDisplayPerformance.Auto:
                        break;

                    case EDisplayPerformance.Low:
                        // 8,16,24,32
                        _rdpClient.ColorDepth = 8;
                        nDisplayPerformanceFlag += 0x00000001;//TS_PERF_DISABLE_WALLPAPER;      Wallpaper on the desktop is not displayed.
                        nDisplayPerformanceFlag += 0x00000002;//TS_PERF_DISABLE_FULLWINDOWDRAG; Full-window drag is disabled; only the window outline is displayed when the window is moved.
                        nDisplayPerformanceFlag += 0x00000004;//TS_PERF_DISABLE_MENUANIMATIONS; Menu animations are disabled.
                        nDisplayPerformanceFlag += 0x00000008;//TS_PERF_DISABLE_THEMING ;       Themes are disabled.
                        nDisplayPerformanceFlag += 0x00000020;//TS_PERF_DISABLE_CURSOR_SHADOW;  No shadow is displayed for the cursor.
                        nDisplayPerformanceFlag += 0x00000040;//TS_PERF_DISABLE_CURSORSETTINGS; Cursor blinking is disabled.
                        break;

                    case EDisplayPerformance.Middle:
                        _rdpClient.ColorDepth = 16;
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
                        _rdpClient.ColorDepth = 32;
                        nDisplayPerformanceFlag += 0x00000080;//TS_PERF_ENABLE_FONT_SMOOTHING;        Enable font smoothing.
                        nDisplayPerformanceFlag += 0x00000100;//TS_PERF_ENABLE_DESKTOP_COMPOSITION ;  Enable desktop composition.
                        break;
                }
            }
            SimpleLogHelper.Debug("RdpInit: DisplayPerformance = " + _rdpSettings.DisplayPerformance + ", flag = " + Convert.ToString(nDisplayPerformanceFlag, 2));
            _rdpClient.AdvancedSettings9.PerformanceFlags = nDisplayPerformanceFlag;

            #endregion Performance
        }

        private void RdpInitGateway()
        {
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            SimpleLogHelper.Debug("RDP Host: init Gateway");

            #region Gateway

            // Specifies whether Remote Desktop Gateway (RD Gateway) is supported.
            if (_rdpClient.TransportSettings.GatewayIsSupported != 0
                && _rdpSettings.GatewayMode != EGatewayMode.DoNotUseGateway)
            {
                // https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewayprofileusagemethod
                _rdpClient.TransportSettings2.GatewayProfileUsageMethod = 1; // Use explicit settings, as specified by the user.

                // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewayusagemethod
                _rdpClient.TransportSettings.GatewayUsageMethod = _rdpSettings.GatewayMode switch
                {
                    EGatewayMode.UseTheseGatewayServerSettings =>
                    1 // 1 : Always use an RD Gateway server. In the RDC client UI, the Bypass RD Gateway server for local addresses check box is cleared.
                    ,
                    EGatewayMode.AutomaticallyDetectGatewayServerSettings =>
                    2 // 2 : Use an RD Gateway server if a direct connection cannot be made to the RD Session Host server. In the RDC client UI, the Bypass RD Gateway server for local addresses check box is selected.
                    ,
                    _ => throw new ArgumentOutOfRangeException()
                };

                _rdpClient.TransportSettings2.GatewayHostname = _rdpSettings.GatewayHostName;
                //_rdpClient.TransportSettings2.GatewayDomain = "XXXXX";

                // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewaycredssource
                // TSC_PROXY_CREDS_MODE_USERPASS (0): Use a password (NTLM) as the authentication method for RD Gateway.
                // TSC_PROXY_CREDS_MODE_SMARTCARD (1): Use a smart card as the authentication method for RD Gateway.
                // TSC_PROXY_CREDS_MODE_ANY (4): Use any authentication method for RD Gateway.
                switch (_rdpSettings.GatewayLogonMethod)
                {
                    case EGatewayLogonMethod.SmartCard:
                        _rdpClient.TransportSettings.GatewayCredsSource = 1; // TSC_PROXY_CREDS_MODE_SMARTCARD
                        break;

                    case EGatewayLogonMethod.Password:
                        _rdpClient.TransportSettings.GatewayCredsSource = 0; // TSC_PROXY_CREDS_MODE_USERPASS
                        _rdpClient.TransportSettings2.GatewayUsername = _rdpSettings.GatewayUserName;
                        _rdpClient.TransportSettings2.GatewayPassword = _rdpSettings.GatewayPassword;
                        break;

                    default:
                        _rdpClient.TransportSettings.GatewayCredsSource = 4; // TSC_PROXY_CREDS_MODE_ANY
                        break;
                }

                _rdpClient.TransportSettings2.GatewayCredSharing = 0;
            }

            #endregion Gateway
        }

        private void InitRdp(int width = 0, int height = 0, bool isReconnecting = false)
        {
            if (Status != ProtocolHostStatus.NotInit)
                return;
            try
            {
                Status = ProtocolHostStatus.Initializing;
                RdpClientDispose();
                CreateRdpClient();
                RdpInitServerInfo();
                RdpInitStatic();
                RdpInitConnBar();
                RdpInitRedirect();
                RdpInitDisplay(width, height, isReconnecting);
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
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            Dispatcher.Invoke(() =>
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
                    _rdpClient.Connect();
                }
                catch (Exception e)
                {
                    GridMessageBox.Visibility = Visibility.Visible;
                    TbMessageTitle.Visibility = Visibility.Collapsed;
                    TbMessage.Text = e.Message;
                }
                Status = ProtocolHostStatus.Connected;
            });
        }

        public override void Close()
        {
            this.Dispose();
            base.Close();
        }

        protected override void GoFullScreen()
        {
            if (_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.Disable
                || ParentWindow is not FullScreenWindowView
                || _rdpClient?.FullScreen == true)
            {
                return;
            }
            Debug.Assert(_rdpClient != null); if (_rdpClient == null) return;
            _rdpClient.FullScreen = true; // this will invoke OnRequestGoFullScreen -> MakeNormal2FullScreen
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

        #endregion Base Interface


        #region WindowOnResizeEnd

        private readonly System.Timers.Timer _resizeEndTimer = new(500) { Enabled = false, AutoReset = false };
        private readonly object _resizeEndLocker = new();
        private bool _canAutoResizeByWindowSizeChanged = true;

        /// <summary>
        /// when tab window goes to min from max, base.SizeChanged invoke and size will get bigger, normal to min will not tiger this issue, don't know why.
        /// so stop resize when window status change to min until status restore.
        /// </summary>
        /// <param name="isEnable"></param>
        public override void ToggleAutoResize(bool isEnable)
        {
            lock (_resizeEndLocker)
            {
                _canAutoResizeByWindowSizeChanged = isEnable;
            }
        }

        private void ParentWindowResize_StartWatch()
        {
            lock (_resizeEndLocker)
            {
                _resizeEndTimer.Elapsed -= ResizeEndTimerOnElapsed;
                _resizeEndTimer.Elapsed += ResizeEndTimerOnElapsed;
                base.SizeChanged -= WindowSizeChanged;
                base.SizeChanged += WindowSizeChanged;
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
        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ParentWindow?.WindowState != WindowState.Minimized
                && _canAutoResizeByWindowSizeChanged
                && this._rdpSettings.RdpWindowResizeMode == ERdpWindowResizeMode.AutoResize)
            {
                // start a timer to resize RDP after 500ms
                var nw = (uint)e.NewSize.Width;
                var nh = (uint)e.NewSize.Height;
                if (nw != _previousWidth || nh != _previousHeight)
                {
                    _previousWidth = (uint)e.NewSize.Width;
                    _previousHeight = (uint)e.NewSize.Height;
                    Execute.OnUIThreadSync(() =>
                    {
                        _resizeEndTimer.Stop();
                        _resizeEndTimer.Start();
                        _loginResizeTimer.Stop();
                    });
                }
            }
        }

        private void ResizeEndTimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            ReSizeRdpToControlSize();
        }

        #endregion WindowOnResizeEnd


        private void RdpClientDispose()
        {
            GlobalEventHelper.OnScreenResolutionChanged -= OnScreenResolutionChanged;
            lock (this)
            {
                Execute.OnUIThreadSync(() =>
                {
                    try
                    {
                        _rdpClient?.Dispose();
                        _rdpClient = null;
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e);
                    }
                });
            }
            SimpleLogHelper.Debug("RDP Host: _rdpClient.Dispose()");
        }




        private static bool _isReSizeRdpToControlSizeRunning = false;
        /// <summary>
        /// set remote resolution to _rdpClient size if is AutoResize
        /// if focus == false, then set size only if new size != old size
        /// </summary>
        private void ReSizeRdpToControlSize()
        {
            if (!_flagHasConnected
                || _rdpClient?.FullScreen != false
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

                var nw = (uint)(_rdpClient?.Width ?? 0);
                var nh = (uint)(_rdpClient?.Height ?? 0);
                // tip: the control default width is 288
                if (_rdpClient?.DesktopWidth != nw
                    || _rdpClient?.DesktopHeight != nh)
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
                         || _rdpClient?.DesktopWidth != w
                         || _rdpClient?.DesktopHeight != h
                         || newScaleFactor != _lastScaleFactor;
            if (newScaleFactor != 100)
            {
                // in this case we allow 1pix error
                needUpdate = focus
                        || Math.Abs((int)(_rdpClient?.DesktopWidth ?? 0) - (int)w) > 1
                        || Math.Abs((int)(_rdpClient?.DesktopHeight ?? 0) - (int)h) > 1
                        || newScaleFactor != _lastScaleFactor;
            }
            SimpleLogHelper.Debug($@"SetRdpResolution needUpdate = {needUpdate}, UpdateSessionDisplaySettings, by: W = {_rdpClient?.DesktopWidth} -> {w}, H = {_rdpClient?.DesktopHeight} -> {h}, ScaleFactor = {_lastScaleFactor} -> {newScaleFactor}, focus = {focus}");
            if (needUpdate)
                Execute.OnUIThreadSync(() =>
                {
                    try
                    {
                        _lastScaleFactor = newScaleFactor;
                        _rdpClient?.UpdateSessionDisplaySettings(w, h, w, h, 0, newScaleFactor, 100);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e);
                    }
                });
        }

        private System.Drawing.Rectangle GetScreenSizeIfRdpIsFullScreen()
        {
            if (_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
            {
                if (_rdpSettings.IsTmpSession() == false)
                    LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, true, -1);
                return ScreenInfoEx.GetAllScreensSize();
            }

            int screenIndex = LocalityConnectRecorder.RdpCacheGet(_rdpSettings.Id)?.FullScreenLastSessionScreenIndex ?? -1;
            if (screenIndex < 0
                || screenIndex >= System.Windows.Forms.Screen.AllScreens.Length)
            {
                screenIndex = this.ParentWindow != null ? ScreenInfoEx.GetCurrentScreen(this.ParentWindow).Index : ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition()).Index;
            }
            if (_rdpSettings.IsTmpSession() == false)
                LocalityConnectRecorder.RdpCacheUpdate(_rdpSettings.Id, true, screenIndex);
            return System.Windows.Forms.Screen.AllScreens[screenIndex].Bounds;
        }

        /// <summary>
        /// set the parent window of rdp, if parent window is FullScreenWindowView and it's loaded, go full screen
        /// </summary>
        /// <param name="value"></param>
        public override void SetParentWindow(WindowBase? value)
        {
            base.SetParentWindow(value);
            if (value is FullScreenWindowView && value.IsLoaded && value.IsClosed == false)
            {
                this.GoFullScreen();
            }
        }

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        public override void FocusOnMe()
        {
            Execute.OnUIThread(() =>
            {
                // Kill logical focus
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(RdpHost), null);
                Keyboard.ClearFocus();
                RdpHost.Focus();
                if (_rdpClient is { } rdp)
                {
                    // try to fix https://github.com/1Remote/1Remote/issues/530, but failed
                    rdp.Focus();
                    //rdp.Show();
                    //rdp.Update();
                    //rdp.Refresh();
                    //rdp.BringToFront();
                }
            });
        }
    }
}