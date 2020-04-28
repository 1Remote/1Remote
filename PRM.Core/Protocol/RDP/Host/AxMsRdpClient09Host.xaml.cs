using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AxMSTSCLib;
using MSTSCLib;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Color = System.Drawing.Color;
using MessageBox = System.Windows.MessageBox;

namespace Shawn.Ulits.RDP
{
    /// <summary>
    /// AxMsRdpClient09Host.xaml 的交互逻辑
    /// </summary>
    public sealed partial class AxMsRdpClient09Host : ProtocolHostBase
    {
        private readonly AxMsRdpClient9NotSafeForScriptingEx _rdp = null;
        private readonly ProtocolServerRDP _rdpServer = null;
        private uint _scaleFactor = 100;
        private bool _isDisconned = false;
        private bool _isConnecting = false;

        /*
        public void SetParent(Window win)
        {
            if (_host4WindowMode != win && _host4WindowMode != null && !_host4WindowMode.IsLoaded)
            {
                try
                {
                    _host4WindowMode.Loaded -= ParentWindowOnLoaded;
                }
                catch (Exception)
                {
                    // ignored
                }

                try
                {
                    _host4WindowMode.StateChanged -= ParentWindowOnStateChanged;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            _host4WindowMode = win;
            if (_isFirstTimeParentBind)
            {
                _isFirstTimeParentBind = false;
                if (_host4WindowMode.IsLoaded)
                {
                    InitRdp();
                    Conn();
                }
                else
                {
                    _host4WindowMode.Loaded += ParentWindowOnLoaded;
                }
            }
            _host4WindowMode.StateChanged += ParentWindowOnStateChanged;
            _host4WindowMode.Closed += (sender, args) =>
            {
                try
                {
                    _rdp?.Dispose();
                }
                catch (Exception)
                {
                }
            };
            _host4WindowMode.Icon = _rdpServer.IconImg;
            _host4WindowMode.Title = _rdpServer.DispName + " @ " + _rdpServer.Address;
        }
        */



        public AxMsRdpClient09Host(ProtocolServerBase protocolServer, double width = 0, double height = 0) : base(protocolServer, true)
        {
            InitializeComponent();
            if (protocolServer.GetType() == typeof(ProtocolServerRDP))
            {
                _rdpServer = (ProtocolServerRDP)protocolServer;
                _rdp = new AxMsRdpClient9NotSafeForScriptingEx();
                ((System.ComponentModel.ISupportInitialize)(_rdp)).BeginInit();
                // set fill to make rdp widow, so that we can enable RDP SmartSizing
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
                _rdp.OnDisconnected += RdpcOnDisconnected;
                _rdp.OnConfirmClose += RdpOnOnConfirmClose;
                _rdp.OnLoginComplete += RdpOnOnLoginComplete;
                ((System.ComponentModel.ISupportInitialize)(_rdp)).EndInit();
                RdpHost.Child = _rdp;
                InitRdp(width, height);
            }
            else
                _rdp = null;
        }

        private void InitRdp(double width = 0, double height = 0)
        {
            _rdp.CreateControl();
            #region server info
            // server info
            _rdp.Server = _rdpServer.Address;
            _rdp.UserName = _rdpServer.UserName;
            _rdp.AdvancedSettings2.RDPPort = _rdpServer.Port;
            var secured = (MSTSCLib.IMsTscNonScriptable)_rdp.GetOcx();
            // TODO 密钥的 RSA 解密
            secured.ClearTextPassword = _rdpServer.Password;
            _rdp.FullScreenTitle = _rdpServer.DispName + " - " + _rdpServer.SubTitle;
            #endregion


            // enable CredSSP, will use CredSsp if the client supports.
            _rdp.AdvancedSettings7.EnableCredSspSupport = true;
            _rdp.AdvancedSettings5.AuthenticationLevel = 0;
            _rdp.AdvancedSettings5.EnableAutoReconnect = true;
            // setting PublicMode to false allows the saving of credentials, which prevents
            _rdp.AdvancedSettings6.PublicMode = false;
            _rdp.AdvancedSettings5.EnableWindowsKey = 1;
            _rdp.AdvancedSettings5.GrabFocusOnConnect = true;
            //// ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings6-connecttoadministerserver
            //_rdp.AdvancedSettings7.ConnectToAdministerServer = true;


            #region conn bar
            _rdp.AdvancedSettings6.DisplayConnectionBar = true;
            _rdp.AdvancedSettings6.ConnectionBarShowPinButton = true;
            _rdp.AdvancedSettings6.PinConnectionBar = false;
            _rdp.AdvancedSettings6.ConnectionBarShowMinimizeButton = true;
            _rdp.AdvancedSettings6.ConnectionBarShowRestoreButton = true;
            _rdp.AdvancedSettings6.BitmapVirtualCache32BppSize = 48;
            #endregion

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
                _rdp.SecuredSettings3.KeyboardHookMode = 2;
            }

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
                _rdp.AdvancedSettings8.AudioCaptureRedirectionMode = false;
            }
            #endregion

            #region Others

            // enable CredSSP, will use CredSsp if the client supports.
            _rdp.AdvancedSettings9.EnableCredSspSupport = true;

            //- 0: If server authentication fails, connect to the computer without warning (Connect and don't warn me)
            //- 1: If server authentication fails, don't establish a connection (Don't connect)
            //- 2: If server authentication fails, show a warning and allow me to connect or refuse the connection (Warn me)
            //- 3: No authentication requirement specified.
            _rdp.AdvancedSettings9.AuthenticationLevel = 0;

            // setting PublicMode to false allows the saving of credentials, which prevents
            _rdp.AdvancedSettings9.PublicMode = false;
            _rdp.AdvancedSettings9.EnableAutoReconnect = true;


            // - 0 Apply key combinations only locally at the client computer.
            // - 1 Apply key combinations at the remote server.
            // - 2 Apply key combinations to the remote server only when the client is running in full-screen mode. This is the default value.
            _rdp.SecuredSettings3.KeyboardHookMode = 2;

            #endregion

            #region Display

            SetScaleFactor();
            _rdp.SetExtendedProperty("DesktopScaleFactor", _scaleFactor);
            _rdp.SetExtendedProperty("DeviceScaleFactor", (uint)100);
            _rdp.AdvancedSettings2.SmartSizing = _rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.Stretch;
            // to enhance user experience, i let the form handled full screen
            _rdp.AdvancedSettings6.ContainerHandledFullScreen = 1;

            if (width > 100 && height > 100)
            {
                _rdp.Width = (int)width;
                _rdp.Height = (int)height;
            }

            //if (Parent != null)
            //{ 
            //    _rdp.Width  = (int)Parent.Width;
            //    _rdp.Height = (int)Parent.Height;
            //}

            switch (_rdpServer.RdpFullScreenFlag)
            {
                case ERdpFullScreenFlag.Disable:
                    base.CanFullScreen = false;
                    break;
                case ERdpFullScreenFlag.EnableFullScreen:
                    // depends on window status of last session
                    if (_rdpServer.AutoSetting?.FullScreen_LastSessionIsFullScreen ?? false)
                    {
                        var screen = GetScreen(_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex);
                        if (screen == null)
                        {
                            var t = GetCurrentScreen();
                            _rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex = t.Item1;
                            screen = GetScreen(_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex);
                        }
                        if (screen != null)
                        {
                            _rdp.DesktopWidth = screen.Bounds.Width;
                            _rdp.DesktopHeight = screen.Bounds.Height;
                        }
                        _rdp.FullScreen = true;
                    }
                    base.CanFullScreen = true;
                    break;
                case ERdpFullScreenFlag.EnableFullAllScreens:
                    ((IMsRdpClientNonScriptable5)_rdp.GetOcx()).UseMultimon = true;
                    base.CanFullScreen = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            #endregion

            #region DisplayPerformance
            // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings-performanceflags
            int nDisplayPerformanceFlag = 0;
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
            SimpleLogHelper.Log("RdpInit: DisplayPerformance = " + _rdpServer.DisplayPerformance + ", flag = " + Convert.ToString(nDisplayPerformanceFlag, 2));
            _rdp.AdvancedSettings9.PerformanceFlags = nDisplayPerformanceFlag;

            #endregion
        }


        #region Base Interface
        public override void Conn()
        {
            _isConnecting = true;
            _isDisconned = false;
            GridLoading.Visibility = Visibility.Visible;
            RdpHost.Visibility = Visibility.Collapsed;
            _rdp?.Connect();
        }

        public override void DisConn()
        {
            _isConnecting = false;
            if (!_isDisconned)
            {
                _isDisconned = true;
                if (_rdp != null
                    && _rdp.Connected > 0)
                    _rdp.Disconnect();
            }
        }

        public override void GoFullScreen()
        {
            // full screen on current 
            var t = GetCurrentScreen();
            if (t != null)
                _rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex = t.Item1;
            _rdp.FullScreen = true;
        }

        public override bool IsConnected()
        {
            return this._isDisconned == false && _rdp?.Connected > 0;
        }

        public override bool IsConnecting()
        {
            return _isConnecting;
        }

        #endregion



        #region event handler

        #region connection

        #region Disconn Reason
        enum EDiscReason
        {
            // https://docs.microsoft.com/en-us/windows/win32/termserv/extendeddisconnectreasoncode
            exDiscReasonNoInfo                            = 0,
            exDiscReasonAPIInitiatedDisconnect            = 1,
            exDiscReasonAPIInitiatedLogoff                = 2,
            exDiscReasonServerIdleTimeout                 = 3,
            exDiscReasonServerLogonTimeout                = 4,
            exDiscReasonReplacedByOtherConnection         = 5,
            exDiscReasonOutOfMemory                       = 6,
            exDiscReasonServerDeniedConnection            = 7,
            exDiscReasonServerDeniedConnectionFips        = 8,
            exDiscReasonServerInsufficientPrivileges      = 9,
            exDiscReasonServerFreshCredsRequired          = 10,
            exDiscReasonRpcInitiatedDisconnectByUser      = 11,
            exDiscReasonLogoffByUser                      = 2,
            exDiscReasonLicenseInternal                   = 256,
            exDiscReasonLicenseNoLicenseServer            = 257,
            exDiscReasonLicenseNoLicense                  = 258,
            exDiscReasonLicenseErrClientMsg               = 259,
            exDiscReasonLicenseHwidDoesntMatchLicense     = 260,
            exDiscReasonLicenseErrClientLicense           = 261,
            exDiscReasonLicenseCantFinishProtocol         = 262,
            exDiscReasonLicenseClientEndedProtocol        = 263,
            exDiscReasonLicenseErrClientEncryption        = 264,
            exDiscReasonLicenseCantUpgradeLicense         = 265,
            exDiscReasonLicenseNoRemoteConnections        = 266,
            exDiscReasonLicenseCreatingLicStoreAccDenied  = 267,
            exDiscReasonRdpEncInvalidCredentials          = 768,
            exDiscReasonProtocolRangeStart                = 4096,
            exDiscReasonProtocolRangeEnd                  = 32767
        }
        #endregion
        private void RdpcOnDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            ResizeEndStopFireDelegate();
            if (this.OnResizeEnd != null)
                this.OnResizeEnd -= ReSizeRdp;

            const int UI_ERR_NORMAL_DISCONNECT = 0xb08;
            string reason = _rdp.GetErrorDescription((uint)e.discReason, (uint)_rdp.ExtendedDisconnectReason);
            if (e.discReason != UI_ERR_NORMAL_DISCONNECT
                && e.discReason != (int)EDiscReason.exDiscReasonAPIInitiatedDisconnect
                && e.discReason != (int)EDiscReason.exDiscReasonAPIInitiatedLogoff
                && reason != "")
            {
                string disconnectedText = $"{_rdpServer.DispName}({_rdpServer.Address}) : {reason}";
                System.Windows.MessageBox.Show(disconnectedText, SystemConfig.GetInstance().Language.GetText("messagebox_title_info"));
            }
        }

        private void RdpOnOnLoginComplete(object sender, EventArgs e)
        {
            _isConnecting = false;

            if (_rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.AutoResize)
            {
                ResizeEndStartFireDelegate();
                if (this.OnResizeEnd == null)
                    this.OnResizeEnd += ReSizeRdp;
            }

            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen && _rdpServer.AutoSetting.FullScreen_LastSessionIsFullScreen)
            {
                _rdp.FullScreen = true;
            }

            RdpHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
        }

        private void RdpOnOnConfirmClose(object sender, IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            DisConn();
            base.OnClose?.Invoke(ProtocolServer.Id);
        }

        #endregion

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
            // todo: handle different rdp version of the server
            try
            {
                //_rdp.Reconnect(nw, nh);
                SetScaleFactor();
                _rdp.UpdateSessionDisplaySettings(w, h, w, h, 0, _scaleFactor, 100);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine(e.StackTrace);
            }
        }


        private double _normalWidth = 800;
        private double _normalHeight = 600;
        private double _normalTop = 0;
        private double _normalLeft = 0;
        private void MakeNormal2FullScreen(bool saveSize = true)
        {
            Debug.Assert(Parent != null);
            _rdpServer.AutoSetting.FullScreen_LastSessionIsFullScreen = true;

            if (saveSize)
            {
                _normalWidth = Parent.Width;
                _normalHeight = Parent.Height;
                _normalTop = Parent.Top;
                _normalLeft = Parent.Left;
            }

            Parent.WindowState = WindowState.Normal;
            Parent.WindowStyle = WindowStyle.None;
            Parent.ResizeMode = ResizeMode.NoResize;

            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
            {
                System.Drawing.Rectangle entireSize = System.Drawing.Rectangle.Empty;
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                    entireSize = System.Drawing.Rectangle.Union(entireSize, screen.Bounds);
                Parent.Width = entireSize.Width / (_scaleFactor / 100.0); ;
                Parent.Height = entireSize.Height / (_scaleFactor / 100.0); ;
                Parent.Left = entireSize.Left / (_scaleFactor / 100.0); ;
                Parent.Top = entireSize.Top / (_scaleFactor / 100.0); ;
            }
            else
            {
                if (_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex < 0
                    || _rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex >= System.Windows.Forms.Screen.AllScreens.Length)
                {
                    for (int i = 0; i < System.Windows.Forms.Screen.AllScreens.Length; i++)
                    {
                        if (Equals(System.Windows.Forms.Screen.PrimaryScreen, System.Windows.Forms.Screen.AllScreens[i]))
                        {
                            _rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex = i;
                            break;
                        }
                    }
                }

                Parent.Width = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Width / (_scaleFactor / 100.0);
                Parent.Height = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Height / (_scaleFactor / 100.0);
                Parent.Left = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Left / (_scaleFactor / 100.0);
                Parent.Top = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Top / (_scaleFactor / 100.0);
                _rdp.Width = (int)System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Width;
                _rdp.Height = (int)System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Height;
                SetRdpResolution((uint)_rdp.Width, (uint)_rdp.Height);
            }
            Parent.Topmost = true;
        }
        private void MakeFullScreen2Normal()
        {
            Debug.Assert(Parent != null);
            _rdpServer.AutoSetting.FullScreen_LastSessionIsFullScreen = false;
            Parent.Topmost = false;
            Parent.ResizeMode = ResizeMode.CanResize;
            Parent.Topmost = false;
            Parent.WindowStyle = WindowStyle.SingleBorderWindow;
            Parent.WindowState = WindowState.Normal;
            Parent.Width = _normalWidth;
            Parent.Height = _normalHeight;
            Parent.Top = _normalTop;
            Parent.Left = _normalLeft;
            SetRdpResolution((uint)_rdp.Width, (uint)_rdp.Height);
            base.OnFullScreen2Window?.Invoke(_rdpServer.Id);
        }
        private void MakeForm2Minimize()
        {
            Debug.Assert(Parent != null);
            Parent.WindowState = WindowState.Minimized;
        }

        #endregion

        private void SetScaleFactor()
        {
            try
            {
                _scaleFactor = (uint)(100 * System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width /
                                       SystemParameters.PrimaryScreenWidth);
            }
            catch (Exception)
            {
                _scaleFactor = 100;
            }
            finally
            {
                if (_scaleFactor < 100)
                    _scaleFactor = 100;
            }
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
                        __resizeEndTimer.Elapsed += _InvokeResizeEndEnd;
                        base.SizeChanged += _ResizeEnd_WindowSizeChanged;
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
                            __resizeEndTimer.Elapsed -= _InvokeResizeEndEnd;
                        }
                        catch (Exception e)
                        {
                            // ignored
                        }

                        try
                        {
                            base.SizeChanged -= _ResizeEnd_WindowSizeChanged;
                        }
                        catch (Exception e)
                        {
                            // ignored
                        }
                    }
                }
        }
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _ResizeEnd_WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            __resizeEndTimer.Stop();
            __resizeEndTimer.Start();
        }
        private void _InvokeResizeEndEnd(object sender, ElapsedEventArgs e)
        {
            __resizeEndTimer.Stop();
            OnResizeEnd?.Invoke();
        }
        #endregion



        private static System.Windows.Forms.Screen GetScreen(int screenIndex)

        {
            if (screenIndex < 0
                || screenIndex >= System.Windows.Forms.Screen.AllScreens.Length)
            {
                return null;
            }
            return System.Windows.Forms.Screen.AllScreens[screenIndex];
        }
        private Tuple<int, System.Windows.Forms.Screen> GetCurrentScreen()
        {
            var screen = System.Windows.Forms.Screen.FromControl(_rdp);
            for (int i = 0; i < System.Windows.Forms.Screen.AllScreens.Length; i++)
            {
                if (Equals(screen, System.Windows.Forms.Screen.AllScreens[i]))
                {
                    return new Tuple<int, Screen>(i, screen);
                }
            }
            return null;
        }
    }
}
