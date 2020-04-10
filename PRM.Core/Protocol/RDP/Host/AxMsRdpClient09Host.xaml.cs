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
        private bool _enableAutoResize = true;
        private uint _scaleFactor = 100;
        private bool _isFirstTimeParentBind = true;
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
                    InitRdp();
                    Conn();
                }
                else
                {
                    _parentWindow.Loaded += ParentWindowOnLoaded;
                }
            }
            _parentWindow.StateChanged += ParentWindowOnStateChanged;
            _parentWindow.Closed += (sender, args) =>
            {
                try
                {
                    _rdp?.Dispose();
                }
                catch (Exception)
                {
                }
            };
            _parentWindow.Icon = _rdpServer.IconImg;
            _parentWindow.Title = _rdpServer.DispName + " @ " + _rdpServer.Address;
        }

        private void ParentWindowOnLoaded(object sender, RoutedEventArgs e)
        {
            InitRdp();
            Conn();
            _parentWindow.Loaded -= ParentWindowOnLoaded;
        }

        private void ParentWindowOnStateChanged(object sender, EventArgs e)
        {
            if (_parentWindow.WindowState == WindowState.Maximized)
            {
                if (_rdpServer.RdpFullScreenFlag != ERdpFullScreenFlag.Disable)
                {
                    GoFullScreen();
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
                _rdp.OnLoginComplete += RdpOnOnLoginComplete;
                ((System.ComponentModel.ISupportInitialize)(_rdp)).EndInit();
                RdpHost.Child = _rdp;

                SetParent(_parentWindow);
            }
            else
                _rdp = null;
        }

        private void InitRdp()
        {
            #region server info
            // server info
            _rdp.Server = _rdpServer.Address;
            _rdp.UserName = _rdpServer.UserName;
            _rdp.AdvancedSettings2.RDPPort = _rdpServer.Port;
            var secured = (MSTSCLib.IMsTscNonScriptable)_rdp.GetOcx();
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
            //// ref: https://docs.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings6-connecttoadministerserver
            //_rdp.AdvancedSettings7.ConnectToAdministerServer = true;


            #region conn bar
            _rdp.AdvancedSettings6.DisplayConnectionBar = true;
            _rdp.AdvancedSettings6.ConnectionBarShowPinButton = true;
            _rdp.AdvancedSettings6.ConnectionBarShowMinimizeButton = true;
            _rdp.AdvancedSettings6.ConnectionBarShowRestoreButton = true;
            _rdp.AdvancedSettings6.BitmapVirtualCache32BppSize = 48;
            #endregion



            #region Display

            SetScaleFactor();
            _rdp.SetExtendedProperty("DesktopScaleFactor", _scaleFactor);
            _rdp.SetExtendedProperty("DeviceScaleFactor", (uint)100);
            _rdp.AdvancedSettings2.SmartSizing = _rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.Stretch;
            _rdp.AdvancedSettings6.ContainerHandledFullScreen = 1;
            if (_rdpServer.RdpWidth > 0 && _rdpServer.RdpHeight > 0)
            {
                _parentWindow.Width = _rdpServer.RdpWidth;
                _parentWindow.Height = _rdpServer.RdpHeight;
            }
            switch (_rdpServer.RdpFullScreenFlag)
            {
                case ERdpFullScreenFlag.Disable:
                    break;
                case ERdpFullScreenFlag.EnableFullScreen:
                    if (_rdpServer.AutoSetting?.FullScreen_LastSessionIsFullScreen ?? false)
                    {
                        var screen = GetScreen(_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex);
                        if (screen == null)
                        {
                            var t = GetCurrentScreen(_parentWindow);
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
                    break;
                case ERdpFullScreenFlag.EnableFullAllScreens:
                    ((IMsRdpClientNonScriptable5)_rdp.GetOcx()).UseMultimon = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            #endregion

        }


        #region Base Interface
        public sealed override void Conn()
        {
            GridLoading.Visibility = Visibility.Visible;
            RdpHost.Visibility = Visibility.Collapsed;
            _rdp?.Connect();
        }

        public override void DisConn()
        {
            _rdp?.Disconnect();
        }

        public override void GoFullScreen()
        {
            // full screen on current 
            var t = GetCurrentScreen(_parentWindow);
            if (t != null)
                _rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex = t.Item1;

            if (_rdp?.Connected == 1 &&
                _rdpServer.RdpFullScreenFlag != ERdpFullScreenFlag.Disable)
                _rdp.FullScreen = true;
        }
        #endregion



        #region event handler

        #region connection

        private void RdpOnConnected(object sender, EventArgs e)
        {
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
                string disconnectedText = $"{_rdpServer.DispName}({_rdpServer.Address}) : {reason}";
                System.Windows.MessageBox.Show(disconnectedText, SystemConfig.GetInstance().Language.GetText("messagebox_title_info"));
            }

            _parentWindow.Close();
        }

        private void RdpOnOnLoginComplete(object sender, EventArgs e)
        {
            RdpHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
        }

        private void RdpOnOnConfirmClose(object sender, IMsTscAxEvents_OnConfirmCloseEvent e)
        {
            _rdp?.Disconnect();
        }

        #endregion

        private void ReSizeRdp()
        {
            if (_enableAutoResize && _rdpServer.RdpWindowResizeMode == ERdpWindowResizeMode.AutoResize)
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
            }
        }


        private double _normalWidth = 800;
        private double _normalHeight = 600;
        private double _normalTop = 0;
        private double _normalLeft = 0;
        private void MakeForm2FullScreen(bool saveSize = true)
        {
            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.Disable)
                return;

            _rdpServer.AutoSetting.FullScreen_LastSessionIsFullScreen = true;
            _enableAutoResize = false;

            // TODO process scaling
            //Graphics graphics = new System.Windows.Forms.Form().CreateGraphics();
            //Console.WriteLine("DPI: " + graphics.DpiX);

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

            if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens)
            {
                System.Drawing.Rectangle entireSize = System.Drawing.Rectangle.Empty;
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                    entireSize = System.Drawing.Rectangle.Union(entireSize, screen.Bounds);
                _parentWindow.Width = entireSize.Width / (_scaleFactor / 100.0); ;
                _parentWindow.Height = entireSize.Height / (_scaleFactor / 100.0); ;
                _parentWindow.Left = entireSize.Left / (_scaleFactor / 100.0); ;
                _parentWindow.Top = entireSize.Top / (_scaleFactor / 100.0); ;
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

                _parentWindow.Width = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Width / (_scaleFactor / 100.0);
                _parentWindow.Height = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Height / (_scaleFactor / 100.0);
                _parentWindow.Left = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Left / (_scaleFactor / 100.0);
                _parentWindow.Top = System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Top / (_scaleFactor / 100.0);
                _rdp.Width = (int)System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Width;
                _rdp.Height = (int)System.Windows.Forms.Screen.AllScreens[_rdpServer.AutoSetting.FullScreen_LastSessionScreenIndex].Bounds.Height;
                SetRdpResolution((uint)_rdp.Width, (uint)_rdp.Height);
            }
            _parentWindow.Topmost = true;
        }
        private void MakeForm2Normal()
        {
            _rdpServer.AutoSetting.FullScreen_LastSessionIsFullScreen = false;
            _parentWindow.ResizeMode = ResizeMode.CanResize;
            _parentWindow.Topmost = false;
            _parentWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            _parentWindow.WindowState = WindowState.Normal;
            _parentWindow.Width = _normalWidth;
            _parentWindow.Height = _normalHeight;
            _parentWindow.Top = _normalTop;
            _parentWindow.Left = _normalLeft;
            SetRdpResolution((uint)_rdp.Width, (uint)_rdp.Height);
            _enableAutoResize = true;
        }
        private void MakeForm2Minimize()
        {
            _parentWindow.WindowState = WindowState.Minimized;
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
        private static Tuple<int, System.Windows.Forms.Screen> GetCurrentScreen(Window window)
        {
            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(window).Handle);
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
