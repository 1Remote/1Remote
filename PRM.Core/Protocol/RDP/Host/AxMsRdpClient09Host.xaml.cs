using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using MessageBox = System.Windows.MessageBox;

namespace Shawn.Ulits.RDP
{
    /// <summary>
    /// AxMsRdpClient09Host.xaml 的交互逻辑
    /// </summary>
    public partial class AxMsRdpClient09Host : ProtocolHostBase
    {
        private readonly AxMsRdpClient9NotSafeForScriptingEx _rdp = null;
        private readonly ProtocolServerRDP _rdpServer = null;
        private bool _isFirstTimeParentBind = true;
        private bool _enableAutoResize = true;
        private readonly uint _scaleFactor = 100;

        private Window _parentWindow = null;
        /*
            0 The control is not connected.
            1 The control is connected.
            2 The control is establishing a connection.
            @ref https://docs.microsoft.com/en-us/windows/win32/termserv/imstscax-connected
         */
        public bool IsConnected => _rdp?.Connected > 0;

        public void SetParent(Window win)
        {
            if (_parentWindow != win && _parentWindow != null && !_parentWindow.IsLoaded)
            {
                try
                {
                    _parentWindow.Loaded -= ParentWindowOnLoaded;
                }
                catch (Exception)
                {
                    // ignored
                }

                try
                {
                    _parentWindow.StateChanged -= ParentWindowOnStateChanged;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            _parentWindow = win;
            if (_isFirstTimeParentBind)
            {
                _isFirstTimeParentBind = false;
                if (_parentWindow.IsLoaded)
                {
                    SetRdp();
                    Conn();
                }
                else
                {
                    _parentWindow.Loaded += ParentWindowOnLoaded;
                }
            }
            _parentWindow.StateChanged += ParentWindowOnStateChanged;
            _parentWindow.Icon = _rdpServer.IconImg;
            _parentWindow.Title = _rdpServer.DispName + " @ " + _rdpServer.Address;
        }

        private void ParentWindowOnLoaded(object sender, RoutedEventArgs e)
        {
            SetRdp();
            Conn();
            _parentWindow.Loaded -= ParentWindowOnLoaded;
        }
        private void ParentWindowOnStateChanged(object sender, EventArgs e)
        {
            if (_parentWindow.WindowState == WindowState.Maximized)
            {
                if (_rdpServer.RdpStartupDisplaySize != EStartupDisplaySize.Window)
                {
                    _rdp.FullScreen = true;
                }
            }
        }


        public AxMsRdpClient09Host(ProtocolServerBase protocolServer, Window parentWindow) : base(protocolServer, true)
        {
            InitializeComponent();
            if (protocolServer.GetType() == typeof(ProtocolServerRDP))
            {
                _rdpServer = (ProtocolServerRDP)protocolServer;
                _rdp = new AxMsRdpClient9NotSafeForScriptingEx();
                _parentWindow = parentWindow;

                ((System.ComponentModel.ISupportInitialize)(_rdp)).BeginInit();
                // set fill to make rdp widow, so that we can enable RDP SmartSizing
                _rdp.Dock = DockStyle.Fill;
                _rdp.Enabled = true;
                // set call back
                _rdp.OnRequestGoFullScreen += (sender, args) =>
                {
                    MakeForm2FullScreen();
                };
                _rdp.OnRequestLeaveFullScreen += (sender, args) =>
                {
                    MakeForm2Normal();
                };
                _rdp.OnRequestContainerMinimize += (sender, args) => { MakeForm2Minimize(); };
                _rdp.OnConnected += RdpOnConnected;
                _rdp.OnDisconnected += RdpcOnDisconnected;
                _rdp.OnConfirmClose += RdpOnOnConfirmClose;
                ((System.ComponentModel.ISupportInitialize)(_rdp)).EndInit();
                RdpHost.Child = _rdp;

                SetParent(_parentWindow);
            }
            else
                _rdp = null;
        }

        private void RdpOnOnConfirmClose(object sender, IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            _rdp?.Disconnect();
        }


        private void SetRdp()
        {
            #region server info
            // server info
            _rdp.Server = _rdpServer.Address;
            _rdp.UserName = _rdpServer.UserName;
            _rdp.AdvancedSettings2.RDPPort = _rdpServer.Port;
            var secured = (MSTSCLib.IMsTscNonScriptable) _rdp.GetOcx();
            secured.ClearTextPassword = _rdpServer.Password;
            _rdp.FullScreenTitle = _rdpServer.DispName;
            #endregion

            
            // enable CredSSP, will use CredSsp if the client supports.
            _rdp.AdvancedSettings7.EnableCredSspSupport = true;
            _rdp.AdvancedSettings5.AuthenticationLevel = 0;
            _rdp.AdvancedSettings5.EnableAutoReconnect = true;
            // setting PublicMode to false allows the saving of credentials, which prevents
            _rdp.AdvancedSettings6.PublicMode = false;
            _rdp.AdvancedSettings5.EnableWindowsKey = 1;
            _rdp.AdvancedSettings5.GrabFocusOnConnect = true;
            _rdp.AdvancedSettings7.ConnectToAdministerServer = true;


            #region conn bar
            // set conn bar
            _rdp.AdvancedSettings6.DisplayConnectionBar = true;
            _rdp.AdvancedSettings6.ConnectionBarShowPinButton = true;
            _rdp.AdvancedSettings6.ConnectionBarShowMinimizeButton = true;
            _rdp.AdvancedSettings6.ConnectionBarShowRestoreButton = true;
            _rdp.AdvancedSettings6.BitmapVirtualCache32BppSize = 48;
            #endregion



            #region Display

            _rdp.AdvancedSettings2.SmartSizing = _rdpServer.RdpResizeMode != ERdpResizeMode.Fixed;
            _rdp.AdvancedSettings6.ContainerHandledFullScreen = 1;
            if (_rdpServer.RdpStartupDisplaySize == EStartupDisplaySize.FullAllScreens)
                ((IMsRdpClientNonScriptable5)_rdp.GetOcx()).UseMultimon = true;
            else if (_rdpServer.RdpStartupDisplaySize == EStartupDisplaySize.Window)
            {
                _parentWindow.Width = _rdpServer.RdpWidth;
                _parentWindow.Height = _rdpServer.RdpHeight;
            }

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
                _rdp.SecuredSettings3.AudioRedirectionMode = 2;
                _rdp.AdvancedSettings6.AudioRedirectionMode = 2;
            }

            if (_rdpServer.EnableAudioCapture)
            {
                // indicates whether the default audio input device is redirected from the client to the remote session
                _rdp.AdvancedSettings8.AudioCaptureRedirectionMode = false;
            }

            #endregion


            #region DisplayPerformance

            // TODO 远程性能
            // ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings-performanceflags
            switch (_rdpServer.DisplayPerformance)
            {
                case EDisplayPerformance.Auto:
                    break;
                case EDisplayPerformance.Low:
                    // 8,16,24,32
                    _rdp.ColorDepth = 8;
                    _rdp.AdvancedSettings9.PerformanceFlags |= 0x00000001; //TS_PERF_DISABLE_WALLPAPER;
                    break;
                case EDisplayPerformance.Middle:
                    _rdp.ColorDepth = 16;
                    break;
                case EDisplayPerformance.High:
                    _rdp.ColorDepth = 32;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _rdp.ColorDepth = 8;
            int f = 0;
            f += 0x00000001;//TS_PERF_DISABLE_WALLPAPER;
            f += 0x00000002;//TS_PERF_DISABLE_FULLWINDOWDRAG;
            f += 0x00000004;//TS_PERF_DISABLE_MENUANIMATIONS;
            f += 0x00000008;//TS_PERF_DISABLE_MENUANIMATIONS;
            _rdp.AdvancedSettings9.PerformanceFlags = f;


            //_rdp.AdvancedSettings9.PerformanceFlags |= 0x00000001; //TS_PERF_DISABLE_WALLPAPER;
            //_rdp.AdvancedSettings9.PerformanceFlags |= 0x00000002; //TS_PERF_DISABLE_FULLWINDOWDRAG;
            //_rdp.AdvancedSettings9.PerformanceFlags |= 0x00000004; //TS_PERF_DISABLE_MENUANIMATIONS;

            #endregion
        }


        #region Base Interface
        public sealed override void Conn()
        {
            _rdp?.Connect();
        }

        public override void DisConn()
        {
            _rdp?.Disconnect();
        }

        public override void GoFullScreen()
        {
            var screen = Screen.FromControl(_rdp);
            var nw = (uint)screen.Bounds.Width;
            var nh = (uint)screen.Bounds.Height;
            _rdp.UpdateSessionDisplaySettings(nw, nh, nw, nh, 1, 200, 100);
            _rdp.FullScreen = true;
        }
        #endregion





        #region event handler


        #region connection

        private void RdpOnConnected(object sender, EventArgs e)
        {
            ResizeEndStartFireDelegate();
            if (this.OnResizeEnd == null)
                this.OnResizeEnd += ReSizeRdp;
        }


        #region Disconnect Reason
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
                string disconnectedText = $"TXT:远程桌面 {_rdp.Server} 连接已断开！{reason}";
                System.Windows.MessageBox.Show(disconnectedText, "TXT:远程连接");
            }

            _parentWindow.Close();
        }
        #endregion

        private void ReSizeRdp()
        {
            if (_enableAutoResize && _rdpServer.RdpResizeMode == ERdpResizeMode.AutoResize)
            {
                var nw = (uint)_rdp.Width;
                var nh = (uint) _rdp.Height;
                SetRdpResolution(nw, nh, _scaleFactor);
            }
        }

        private void SetRdpResolution(uint w, uint h, uint scaleFactor)
        {
            // todo: handle different rdp version of the server
            //_rdp.Reconnect(nw, nh);
            _rdp.UpdateSessionDisplaySettings(w, h, w, h, 0, scaleFactor, 100);
        }



        private double _normalWidth = 800;
        private double _normalHeight = 600;
        private double _normalTop = 0;
        private double _normalLeft = 0;
        private void MakeForm2FullScreen(bool saveSize = true)
        {
            _enableAutoResize = false;
            float dpiX, dpiY;
            Graphics graphics = new System.Windows.Forms.Form().CreateGraphics();
            Console.WriteLine("DPI: " + graphics.DpiX);
            if (saveSize)
            {
                _normalWidth = _parentWindow.Width;
                _normalHeight = _parentWindow.Height;
                _normalTop = _parentWindow.Top;
                _normalLeft = _parentWindow.Left;
            }

            _parentWindow.WindowState = WindowState.Normal;
            _parentWindow.WindowStyle = WindowStyle.None;
            _parentWindow.ResizeMode = ResizeMode.NoResize;

            if (_rdpServer.RdpStartupDisplaySize == EStartupDisplaySize.FullAllScreens)
            {
                System.Drawing.Rectangle entireSize = System.Drawing.Rectangle.Empty;
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                    entireSize = System.Drawing.Rectangle.Union(entireSize, screen.Bounds);
                _parentWindow.Width = entireSize.Width;
                _parentWindow.Height = entireSize.Height;
                _parentWindow.Left = entireSize.Left;
                _parentWindow.Top = entireSize.Top;
            }
            else
            {
                System.Drawing.Rectangle entireSize = System.Drawing.Rectangle.Empty;
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                    entireSize = System.Drawing.Rectangle.Union(entireSize, screen.Bounds);
                _parentWindow.Width = Screen.PrimaryScreen.Bounds.Width;
                _parentWindow.Height = Screen.PrimaryScreen.Bounds.Height;
                _parentWindow.Left = Screen.PrimaryScreen.Bounds.Left;
                _parentWindow.Top = Screen.PrimaryScreen.Bounds.Top;
            }
            _parentWindow.Topmost = true;
            SetRdpResolution((uint)_rdp.Width, (uint)_rdp.Height, _scaleFactor);
        }
        private void MakeForm2Normal()
        {
            _parentWindow.ResizeMode = ResizeMode.CanResize;
            _parentWindow.Topmost = false;
            _parentWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            _parentWindow.WindowState = WindowState.Normal;
            _parentWindow.Width = _normalWidth;
            _parentWindow.Height = _normalHeight;
            _parentWindow.Top = _normalTop;
            _parentWindow.Left = _normalLeft;
            SetRdpResolution((uint)_rdp.Width, (uint)_rdp.Height, _scaleFactor);
            _enableAutoResize = true;
        }
        private void MakeForm2Minimize()
        {
            _parentWindow.WindowState = WindowState.Minimized;
        }
        #endregion




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
    }
}
