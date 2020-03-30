using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;
using OpaqueLayer;
using Shawn.Ulits.RDP;

namespace RdpRunner
{
    public class RDPForm : Form
    {
        private readonly AxMsRdpClient9NotSafeForScripting _rdp = null;
        private readonly ServerRDP _config = null;
        private readonly OpaqueCommand _cmd = new OpaqueCommand();

        public RDPForm(ServerRDP config)
        {
            this._config = config;
            if (this._config.RdpStartupDisplaySize == ServerRDP.EStartupDisplaySize.Window &&
                (this._config.RdpWidth <= 0 || this._config.RdpHeight <= 0))
                this._config.RdpStartupDisplaySize = ServerRDP.EStartupDisplaySize.FullCurrentScreen;


            if (!string.IsNullOrEmpty(config.IconBase64))
                this.Icon = IconFromImage(ImageFromBase64(config.IconBase64));
            this.Name = this.Text = $"{config.DispName} @{config.Address}";
            this.BackColor = Color.White;
            this.StartPosition = FormStartPosition.WindowsDefaultBounds;



            _rdp = new AxMsRdpClient9NotSafeForScripting();
            ((System.ComponentModel.ISupportInitialize)(_rdp)).BeginInit();
            // set fill to make rdp widow, so that we can enable RDP SmartSizing
            _rdp.Dock = DockStyle.Fill;
            _rdp.Enabled = true;
            _rdp.BackColor = Color.White;
            // set call back
            _rdp.OnRequestGoFullScreen += (sender, args) =>
            {
                MakeForm2FullScreen();
            };
            _rdp.OnRequestLeaveFullScreen += (sender, args) => { MakeForm2Normal(); };
            _rdp.OnRequestContainerMinimize += (sender, args) => { MakeForm2Minimize(); };
            _rdp.OnDisconnected += RdpcOnDisconnected;
            _rdp.OnConnected += RdpOnConnected;
            _rdp.AutoSize = true;   // make _rdp resize to content size
            ((System.ComponentModel.ISupportInitialize)(_rdp)).EndInit();
            this.Controls.Add(_rdp);
            this.Show();

            RdpInit();

            //_rdp.SetExtendedProperty("DesktopScaleFactor", this.GetDesktopScaleFactor()); this.SetExtendedProperty("DeviceScaleFactor", this.GetDeviceScaleFactor());


            _rdp.Connect();
            _cmd.ShowLoadingLayer(this);
        }

        private void RdpInit()
        {
            _rdp.Top = 0;
            _rdp.Left = 0;

            switch (_config.RdpStartupDisplaySize)
            {
                case ServerRDP.EStartupDisplaySize.Window:
                    this.Width = (_config.RdpWidth + (this.Width - base.DisplayRectangle.Width));
                    this.Height = (_config.RdpHeight + (this.Height - base.DisplayRectangle.Height));
                    break;
                case ServerRDP.EStartupDisplaySize.FullCurrentScreen:
                case ServerRDP.EStartupDisplaySize.FullAllScreens:
                default:
                    Screen screen = Screen.FromControl(this);
                    this.Width = (screen.Bounds.Width + (this.Width - base.DisplayRectangle.Width)) / 2;
                    this.Height = (screen.Bounds.Height + (this.Height - base.DisplayRectangle.Height)) / 2;
                    break;
            }


            // server info
            _rdp.FullScreenTitle = this.Text;
            _rdp.Server = _config.Address;
            _rdp.AdvancedSettings2.RDPPort = _config.Port;
            _rdp.UserName = _config.UserName;
            MSTSCLib.IMsTscNonScriptable secured = (MSTSCLib.IMsTscNonScriptable)_rdp.GetOcx();
            secured.ClearTextPassword = _config.Password;

            #region Display

            switch (_config.RdpStartupDisplaySize)
            {
                case ServerRDP.EStartupDisplaySize.Window:
                    _rdp.DesktopWidth = _config.RdpWidth;
                    _rdp.DesktopHeight = _config.RdpHeight;
                    break;
                case ServerRDP.EStartupDisplaySize.FullCurrentScreen:
                    // get current monitor size
                    Screen screen = Screen.FromControl(this);
                    _rdp.DesktopWidth = screen.Bounds.Width;
                    _rdp.DesktopHeight = screen.Bounds.Height;
                    break;
                case ServerRDP.EStartupDisplaySize.FullAllScreens:
                    ((IMsRdpClientNonScriptable5)_rdp.GetOcx()).UseMultimon = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (_config.RdpResizeMode)
            {
                case ServerRDP.ERdpResizeMode.Fixed:
                    _rdp.AdvancedSettings9.SmartSizing = false;
                    break;
                case ServerRDP.ERdpResizeMode.Sizable:
                    _rdp.AdvancedSettings9.SmartSizing = true;
                    break;
                case ServerRDP.ERdpResizeMode.AutoSize:
                    _rdp.AdvancedSettings9.SmartSizing = true;
                    break;
            }
            // 8,16,24,32
            _rdp.ColorDepth = 32;
            // to enhance user experience, i let the form handled full screen
            _rdp.AdvancedSettings9.ContainerHandledFullScreen = 1;
            #endregion


            // set conn bar
            _rdp.AdvancedSettings9.DisplayConnectionBar = true;
            _rdp.AdvancedSettings9.ConnectionBarShowPinButton = true;
            _rdp.AdvancedSettings9.BitmapVirtualCache32BppSize = 48;
            _rdp.AdvancedSettings9.ConnectionBarShowMinimizeButton = true;
            _rdp.AdvancedSettings9.ConnectionBarShowRestoreButton = true;

            #region Resource
            // resource
            _rdp.AdvancedSettings9.EnableWindowsKey = 1;
            _rdp.AdvancedSettings9.GrabFocusOnConnect = true;
            _rdp.AdvancedSettings9.RedirectDrives = true;
            _rdp.AdvancedSettings9.RedirectClipboard = true;
            _rdp.AdvancedSettings9.RedirectPrinters = true;
            _rdp.AdvancedSettings9.RedirectPOSDevices = true;
            _rdp.AdvancedSettings9.RedirectSmartCards = true;
            #endregion

            #region Media
            // - 0 Redirect sounds to the client. This is the default value.
            // - 1 Play sounds at the remote computer.
            // - 2 Disable sound redirection; do not play sounds at the server.
            _rdp.SecuredSettings3.AudioRedirectionMode = 0;
            // - 0 (Audio redirection is enabled and the option for redirection is "Bring to this computer". This is the default mode.)
            // - 1 (Audio redirection is enabled and the option is "Leave at remote computer". The "Leave at remote computer" option is supported only when connecting remotely to a host computer that is running Windows Vista. If the connection is to a host computer that is running Windows Server 2008, the option "Leave at remote computer" is changed to "Do not play".)
            // - 2 (Audio redirection is enabled and the mode is "Do not play".)
            _rdp.AdvancedSettings9.AudioRedirectionMode = 0;

            // - 0 Dynamic audio quality. This is the default audio quality setting. The server dynamically adjusts audio output quality in response to network conditions and the client and server capabilities.
            // - 1 Medium audio quality. The server uses a fixed but compressed format for audio output.
            // - 2 High audio quality. The server provides audio output in uncompressed PCM format with lower processing overhead for latency.
            _rdp.AdvancedSettings9.AudioQualityMode = 0;
            // indicates whether the default audio input device is redirected from the client to the remote session
            _rdp.AdvancedSettings9.AudioCaptureRedirectionMode = false;
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
        }


        private void ReSizeRdp()
        {
            if (_config.RdpResizeMode == ServerRDP.ERdpResizeMode.AutoSize)
            {
                var nw = (uint)_rdp.Width;
                var nh = (uint) _rdp.Height;
                if (nw != _rdp.DesktopWidth || nh != _rdp.DesktopHeight)
                {
                    //_rdp.Reconnect(nw, nh);
                    _rdp.UpdateSessionDisplaySettings(nw, nh, nw, nh, 1, 100, 100);
                }
            }
        }
        private bool _isFirstTime = true;
        private void RdpOnConnected(object sender, EventArgs e)
        {
            _cmd.HideOpaqueLayer();
            if (_config.DisplayMode == ServerRDP.EDisplayMode.FullScreen)
            {
                _rdp.FullScreen = true;
            }

            if (_isFirstTime)
            {
                _isFirstTime = false;
                this.ResizeEnd += (o, args) => { ReSizeRdp(); };
            }
        }

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
        private void RdpcOnDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            const int UI_ERR_NORMAL_DISCONNECT = 0xb08;
            string reason = _rdp.GetErrorDescription((uint)e.discReason, (uint)_rdp.ExtendedDisconnectReason);
            if (e.discReason != UI_ERR_NORMAL_DISCONNECT
                && e.discReason != (int)EDiscReason.exDiscReasonAPIInitiatedDisconnect
                && e.discReason != (int)EDiscReason.exDiscReasonAPIInitiatedLogoff
                && reason != "")
            {
                string disconnectedText = $"TXT:远程桌面 {_rdp.Server} 连接已断开！{reason}";
                if (MessageBox.Show(disconnectedText, "TXT:远程连接") == DialogResult.OK)
                    ;
            }
            Environment.Exit(0);
        }


        #region handled full screen
        private int _normalWidth = 800;
        private int _normalHeight = 600;
        private int _normalTop = 0;
        private int _normalLeft = 0;
        private void MakeForm2FullScreen(bool saveSize = true)
        {
            if (saveSize)
            {
                _normalWidth = this.Width;
                _normalHeight = this.Height;
                _normalTop = this.Top;
                _normalLeft = this.Left;
            }

            this.TopMost = true;
            this.FormBorderStyle = FormBorderStyle.None;

            if (_config.RdpStartupDisplaySize == ServerRDP.EStartupDisplaySize.FullAllScreens)
            {
                System.Drawing.Rectangle entireSize = System.Drawing.Rectangle.Empty;
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                    entireSize = System.Drawing.Rectangle.Union(entireSize, screen.Bounds);
                this.WindowState = FormWindowState.Normal;
                this.Width = entireSize.Width;
                this.Height = entireSize.Height;
                this.Left = entireSize.Left;
                this.Top = entireSize.Top;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                ReSizeRdp();
            }
        }
        private void MakeForm2Normal()
        {
            this.TopMost = false;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.WindowState = FormWindowState.Normal;
            this.Width = _normalWidth;
            this.Height = _normalHeight;
            this.Top = _normalTop;
            this.Left = _normalLeft;
            ReSizeRdp();
        }
        private void MakeForm2Minimize()
        {
            this.WindowState = FormWindowState.Minimized;
        }

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
                    _rdp.FullScreen = !_rdp.FullScreen;
                    return;
                }
                if (m.WParam.ToInt32() == SC_CLOSE)
                {
                    Environment.Exit(0);
                }
            }
            base.WndProc(ref m);
        }
        #endregion






        #region Static

        public static Icon IconFromImage(Image img)
        {
            var ms = new System.IO.MemoryStream();
            var bw = new System.IO.BinaryWriter(ms);
            // Header
            bw.Write((short)0);   // 0 : reserved
            bw.Write((short)1);   // 2 : 1=ico, 2=cur
            bw.Write((short)1);   // 4 : number of images
            // Image directory
            var w = img.Width;
            if (w >= 256) w = 0;
            bw.Write((byte)w);    // 0 : width of image
            var h = img.Height;
            if (h >= 256) h = 0;
            bw.Write((byte)h);    // 1 : height of image
            bw.Write((byte)0);    // 2 : number of colors in palette
            bw.Write((byte)0);    // 3 : reserved
            bw.Write((short)0);   // 4 : number of color planes
            bw.Write((short)0);   // 6 : bits per pixel
            var sizeHere = ms.Position;
            bw.Write((int)0);     // 8 : image size
            var start = (int)ms.Position + 4;
            bw.Write(start);      // 12: offset of image data
            // Image data
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var imageSize = (int)ms.Position - start;
            ms.Seek(sizeHere, System.IO.SeekOrigin.Begin);
            bw.Write(imageSize);
            ms.Seek(0, System.IO.SeekOrigin.Begin);

            // And load it
            return new Icon(ms);
        }

        public static Image ImageFromBytes(byte[] byteArrayIn)
        {
            using (MemoryStream mStream = new MemoryStream(byteArrayIn))
            {
                return Image.FromStream(mStream);
            }
        }

        public static Image ImageFromBase64(string base64)
        {
            return ImageFromBytes(Convert.FromBase64String(base64));
        }

        public static byte[] ImageToBytes(Image img)
        {
            using (MemoryStream mStream = new MemoryStream())
            {
                img.Save(mStream, img.RawFormat);
                return mStream.ToArray();
            }
        }


        public const string Base64Icon1 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAS4SURBVGhDzVlNiB" +
            "xFGN2j4B8KkohRZ7o7ieTgQSVRBBECigiCohgEQdSDRo0JJmaqasQBT4JK8KAHDQqKyh5ijLooCHvQ+Lv+zPT07kIOS4ISCCJGxSAI" +
            "+r6eV9O1M52d7u2e6X3w2J366r3+qqu6qqt6qkx0rmhcFPrm3sgzr3d9/S241PX0X/j7W9c3c/g7jd8vRYG5h5K1i/kr9aWRp5+NfP" +
            "0LEv8vCyPfdGNNoLbQZu2g66nd6Y1Rs+A7iL0AHkAPHYkb4uu/U+o2aVct5A4jwfcHEjwaevrJH2uNGqsNIartW9/xVKMb6IUB7Qyr" +
            "VINeg0zXSegTPEt3MpwJS7XWOaFnHoH2jPXBTfqd4cliuEHmeYZWBTxX2+FxMvFTswxNDgNDbprFhYHh+I31leHJ4vFDJgWnQYdZXB" +
            "rg+avjvwND+nKGxoP2ur3n4kJLvOA/7ZraxlBpWAjUDfR3eaIbmA/HsrZ1fP2AvZCsLywuHeLtNGiQX2J2fZBViyP01cc0/jMMmj6L" +
            "S4csB2HQuBmT0S55Mwl93XYaFRPP9WFMKDdSsnrA7GcxxEVeZdHEEPmNAA15Atc/ZhvWy0XtZZX8mPeaG/tmgX6MxcsgYx7xmX69fJ" +
            "zBsLuPVmdFVGutx0192dWu6lHo1NRVGMdvWZO0RXbEc5CHmZYIeWtxdZknrfnAXI9k+42x7PiNa1klhjy4g3UKMTA7aT0Sjm4p2tC6" +
            "mMXpwAr/nCMQYiyrZtobNWLTA3WLMvOCjufsNqvDzT3I4mGg4oF+RV+H6JnHoy2t8xgegq1bJmmdCWjMK1aHm34HixOgh950zD/PMn" +
            "U79UsjrTOhXW9uhgab0Fj7BYt7cIcceuujaMOelccoYTVlktaZ4U5Unbq5KS7klG1bezQuzAhrViZpnRltTGqJnhvOZc+Rp+6KCzMi" +
            "MSuPtM6F/qbTM59i6tbXoCH/SgEW0LdZJzPcZMoirXMBk9qhnl79IC18ODHM/z6VaBPmWeXT9Azlgry+UX8CeyTzojUL63od62SG1S" +
            "6nmpWGjSLqpq5xtM4F+on+DJKSU594TTrFeC7YRMokrXMButdEi/nhuPz4Izbz9NeM50KsHeLkewq6D3p6Mydj8ZT8kMMUxnPBJuJS" +
            "EmZ4JNL0DGXGnLf/QuiOixY9dUim8+9pdpp1coHaUknrzMDs/ajV4lXpbuyD9MGkIP85gNWWSVpnguyUHe13cSGGynZbGB8L5zzbdg" +
            "z7nNTwk22Qq4s2PpPsq1DwRj/QO6jcwdBIWN1yxufoGTmsp/VZ8dMmc5nswF2N7OsYTpCsyJbmJJ63r/D/u3Lnw0A9BKNbpSePBa0L" +
            "KBvb8Fvc/PT58jVlwVObpEfkMAY5PoW4zJi9GZtccWTIMRiEi65gLTPy1Xvznr6d6a+MqG62hoFWEH42aFQ5PX0EI2ZP6JnrmO7qIK" +
            "9Pi3V9NYbALRiG98vRlHS5MPXCBRn7BmanzMbxGWA85NUlTGf8SEuqKGldHdKSKkpaV4e0pIqS1tUhLamipHV1SEuqKGldHdKSKkpa" +
            "V4e0pIqS1tUhLamipHV1SEuqKGldHZBEZR8IxoYqP+WMFSW+A1bfSy4m8Xk0G6am/gdlIwp9EFIn9QAAAABJRU5ErkJggg==";

        public const string Base64Icon2 = "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1B" +
                                          "AACxjwv8YQUAAAS7SURBVGhDzVlLqBxFFJ2loL443T15T4zanzGRLFyo+EEQIaCIICiKQRBE" +
                                          "XWj8xGDU6eonGXAlqIgLs9CgoKi4iDF+UBCy0PhNBPGBQhYhQQkEEX8YBCGe23NquudNvZnq" +
                                          "6Z7pOXB4r+vWOXWru7o+PY0qse6CTtOJkju8MHnFDdU34FHwb/A3N0wOeZF6xwvV806Q3E7J" +
                                          "/MK7UJ3rhGqXG6lf0IHTdkxWROO04820mR84YfyosTNBfACxN51IPYvrF8D90hH8/adfJ6u7" +
                                          "TLt6IXcYCb27KsGDuPvbz/E7PqsNoeU/vuSGcQd1f8xrMSw/YpV60OtQetd7SUXqYzdKbmHY" +
                                          "Cr7fPQPv3v3Qn+r7hOp3hmeLoQ6FyTMMTQR4bIHHib4fhi1DswMa7g85mc1YXBrw+1r7yvBk" +
                                          "8fSRTgq64UDtY3FlgOev2r8Zqa1YHs5naDpYXNx5JhqTdec0JoN/XT++kqHKgKF9te5Ujscx" +
                                          "PN+fytqGyeBu3ZCsLyyuHOl6N9ipjIH6AvF7WLU8MPQ+pPlf69vLEYsrhywHnt+5DrPpI7Iz" +
                                          "wVT//UDHhJHa14ziayiZHDD7maa7WTQztKJOGx15GG0fYQ4pvSDeySrF0QqXL+qbBepBFg9A" +
                                          "xrwsoPlGbSk68E5arYmW311C+y/mtRO9CpgQLob49b6RYZEd+R4UoO0Sgfa2D2htJy3HT66C" +
                                          "IOsM6UWdy1glhby4q+uUY7KN1mOR0x1d2NB1WGyGGyRP5wTCI7LpNO2o5e6uqluKRRZ0rGU3" +
                                          "5rR7WDwMBGVHrSv+4Aadh1qbu2cxPIRc3cpIayug/ktah4njZhZnwBN6rV8hVJ/ZTN26fpWk" +
                                          "tRW8YHkTNDiEpjl/zuIe8kMO78kHCxt2jB6jhNZUSVpbIz9RtYLk2rSQU3baW/BgWmgJbVYl" +
                                          "aW0NTmo9vT5w4iL3HsW3poWWyHTVkdaFAB0PncknDa+tLkXv/mPBG6xjDZ1IlaR1IUC3N9UH" +
                                          "8XfYCaj7tNkk+ymtzVPGOMNjYdIzVAjQ7ab+eAObxue02fpALbKONbR2gOlHF7VrHNda42hd" +
                                          "COJH/SnMevEBXpxkvBCorZS0LgTcoJepPyZJ/cmLrxgvBGoHWcOTwi7+vZ4+OSRJneTFCuOF" +
                                          "oBPJUxJmeCxMeoas0QyfXAfdMer3iulhXvzBOoVAbaWktTWwDD2gtU4Q3yZJ7ckKin8H0Noq" +
                                          "SWsryEk5p/02LcQ/W7LCZMW0Ex+FTJtxVsNPjkEDWv+p7FyFglezYLKCF3grQ2OR6XKUGdWW" +
                                          "Bj2t14S7MTlPTuB5DW7k8AcZBHorcp/JCcwoX6LyW3LnMVbvdXx1gzxJp91doMzcqZIUX2/T" +
                                          "E2fLryleGG+UJyIfY7xAPcYZU8/YKUeODH4G+ykvmGfi/PQ2jhs3Mf3RwDHkCohi8NO8yZxw" +
                                          "P57MDuyELme6k0G2T81AXeIEnevxHtwln6bSIQkaGi3Nnm+yLf1ChWEnQ36pHbeYzvRhSqos" +
                                          "aV0fTEmVJa3rgympsqR1fTAlVZa0rg+mpMqS1vXBlFRZ0ro+mJIqS1rXB1NSZUnr+mBKqixp" +
                                          "XR/WOpJPSvGjdX3Alqa2n3Kmiqr2gHPxlPKYxc+jdmg0/geHisKVwcdXlgAAAABJRU5ErkJggg==";

        public const string Base64Icon3 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAATdSURBVGhDzVlNiB" +
            "xFFN6j4B8KYkSEENfdqaoxBxWNCCIIigiCohgEQdSDxn/MQSGQQ04BleBBDxoUFBUPUaMuCsIeNP5GQQwo5LAk/sxU9ey6M93TuwFh" +
            "/V7Pq56aTO9O9XTP9n7wsTuv3vv6va7XVdUzU2Vibfnqi0Ij7u8G6s3IyB/Bha6REf4ugcfx/4eRFq+ERt7HIVsX3UBc1g3kfvBvJL" +
            "/mxUCeoJgzi0qyzNZB1BTPZhcj5jEr7yH5l6KWOgSfo0khRsVn+2J297FctaA7jEQ/cpPrGnEMxTzzb6O2nd2GAL9tHSNfwN/f3Vhw" +
            "jl2qARVEd91J6ItYi7t52AtrC9vPCbV8DLErqU4gl3l4czFUUKAO8tBYiFryVug0Uj20LQ9tHtyWo9WMzYUB3R+sLrUnmycPWhTshc" +
            "NAfszm0hBp2Ur1m2J3vFi/gocmg2Zz57m42EJyUS3PtJvyBh4qDbGu3WiLcnga+96nE9nb0BIP2QuhVfazuXSQtlPQANHu34IPs2tx" +
            "QPRzFg6Xdf1KNpcO2g7CRu0WzMzTyclEi1/dwpIc0PodrW7ikPEBob9IEMvw62zaNKwaNR0b+RQKPTlQoJZ72SU/Vo24ygrhRPAEmw" +
            "dAPY/xufSC+TiHreEBlloX8NuG67zqxo71KHT+manhjrxjRbI22Y2egzz03SLo1OLGeS9a8aLYhU02LcayG9SuZZcE9OCe7VOEXS32" +
            "sPRIOHEL7ba6mM3ZCI064ATQHTyJfWNf1oma7q7rW5S+s0WA7x1O7GE2DyMy6pDj+Bt6+Elj5Hk8PATHtzSytBdCrV6zcVgt72JzH+" +
            "jVt1MHI79e9Vi6rX+ZZGkvdILZWTwm9BJKOX/D5h4GW0591v5zRI8y+jHlkaW94S5UeDe7OTG2aclOqxXHEqMnrFiZZGlvtJtY1DiW" +
            "nv3E6D5Hsa7fkxg9YePKJEvnAuL4pVN9iakT1+BZ+i8xBPJd9vGGTaRMsnQuYME4wvG/UFGPWrF4jPOUjXWZZ5fPiuehXKDjG8Xi2q" +
            "enMDsvp4K6fin7eCONHaCYp8JGcp09jqVzgfQ4fgVJifnkgxaGx3PBJlImWToXsHq/QbEo7hQl1UnEtPyex3PBJjLISmbqE44/jldo" +
            "YZIPgTzB47lgE3FJCfPwSGTF85A3lpZ2XIhJOUWxeIs4Quenn+kDjkNt9skFN5myyNLeCLV43MbGRt1LRR22BnonYj9v2NgyydJeoD" +
            "flfqz4KTGu9L536xnRgnm/2+4L9rlZ7UevQW5c3HLeq2B4Kx1EYWGgdvPQSKRxA6QV1ZfD8Sy9LuLWzOX0Bu7GoOOGv5BxdmTLBhy/" +
            "wwHx/d5KJR6JtLqdZnJtcfoCDptY+wXB7Pn0a8pqUJuhGaEvY7AYPE8rJh6T3orN3LAz6GswFPGHG7C1qT5A4Xdy+huj3ZDXI+hF3J" +
            "mvhoWqJWblaBSo57pB/TpOdzzQ8Slaqu9c1rXbsK89iGL3Ji0JZl24KBNtLfbQakxtRy0fNqYv4XQmj6ykipKlq0NWUkXJ0tUhK6mi" +
            "ZOnqkJVUUbJ0dchKqihZujpkJVWULF0dspIqSpauDllJFSVLV4espIqSpasDHTCzEhuXpMfS1YGO/FnJjcs8P+VMFGWdAbfELLmgwy" +
            "cSm+jPo36YmvofkMuYq9S1YjQAAAAASUVORK5CYII=";

        public const string Base64Icon4 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAS1SURBVGhDzVlLiBxFGO7DdvXG+EAxJJqp7onGBx48qPhAEEFQRBAUF4MgiHrQ+EYPCkIOngQV8aAHDQqKyh40xgcKQg4an1FINjvdvRNdNyiBIOILgyDE7+/9aqZmp5Kp3u7Z3g8+duev+r76a6q6Hj1BnViIg1NzrW7NdfRKFqtvwHnwb/A3cA84nSbhc/g7RcnqxU/JCWfksdqGZH8Bj/oQ9feLphurC2izetCJo4eR5HBndLgr1erNXIfPZHH4PGI72ZF/huom6knaNQv5hpHMu0sS3J3F0UNpe7LNakOYba/dkMbqcdRNB7RafcQqzUA6JN+6ldTHeaJuYrEX5tvBJEbzHmiPGB94/s7ilYWjQ0+zaFnI48lr4HGo54dpy6KVw5IpN81wZeCL+tr4yvRkePzgomA6tIPh2gDPX41/R6stB85co1k0HuxdH6xFY7LvSKP/zrbCy1hUG9Jk4grTKYsHMYrv42/9e1uqwztMQ2hkG8O1Q7xNOw5+kSXhnaxaHTD8kMZ/zejobIZrh2wHaWviamwNDxYnk0TtZbs2d2TxxJWULB8w+nnRMHyJoRXDgVa0eS6OHsAC0rU6djRNosdYpTw6regcY5Qn4X0MDwBlU7KBmnqlCF2aqNtodUzMFxt3+IKtXdajsK+tzof49Z6JY5Md8RyUodcWIacWW+e9aHXb4eUQ9DpjiG/qYlYpIA/u0jpViLPiVlqPhKWbn20FpzHsBqbDU5ZAhrgrh07XiRrl03bdGui9oedJdH1Pp9V2hofBE7VpYAb3n/tn1wUnsngIVt3aSGsvYKa82NPq6EaG+8CIvNY3jz7reizd/fr1kdZeyDZF50Ejl1Boo88ZXoQ95bB0fjByjhJGUydp7Q17oeroiauKIJds9lbtLoKeMGZ1ktbe6Cwuaot6c+G0n6O5RN1cBD3RM6uRtC4F6Myl85Ogm4QX4Z//GHiDdbxBXa2kdSmkWr0jWjw632NZDO/uG5Y/T/W1fZbZ5V16FpWCHN9Ei7YPSqeeNWYzm4L1rOMNox0gbq/SsVFEXeceR+tSoJ/oj2DVC3cVH3R4mOWlYBKpk7QuBSznLxf6RC1IUn/KB8zJr1heCiaRATYwUtC9R/0eGanD8gGN7Gd5KdBogJIwi0fCpWeRN344KzgF++yCaGXBENPvaPYH65QCtbWS1t7AwNxr6W+Rk8R2K1D6PYClrY209oLclC3tt0WQ792KoEzBsu+2LcMeV2r6yTXI1s3Z9yok8aopkI7lWm1h0UjYpj3KiupLh57Wx0TeWrNRbuADOtcLGbMjWzyEzn2Ja/Zb8s1DdFeqo+tkJLubg5MpG9v0y04PTpJfU9KN0bkyIvIyBu0/inJZMYsV2/C4M4OvwTJbsMr59lwS3cD0jw/c+S/FGeoJHD0+dRg1zZ25jh7B1L2E6S4PP+L4lLXUhRj+a/M4vF1eTcmQF9PS3XAlFt463Ir/p2TaFVN+Q7CO6YwfSxOqg7RuDq6kqpLWzcGVVFXSujm4kqpKWjcHV1JVSevm4EqqKmndHFxJVSWtm4MrqaqkdXNwJVWVtG4OSKKxHwjGhiZ/yhkrajwDNj9KNpDQ2H8e9UMQ/A9+NxALzyPIcgAAAABJRU5ErkJggg==";

        public const string Base64Icon5 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAATNSURBVGhDzVlLiBxFGJ6jYNzd7p7JRkQI+MSDBxWNCCIIigiC4mIQBFEPvh+YQwTBg6eghrDZ7t4hCREUlT1ofC0KggeNzygEJ2Snq2fHjYogEnxhEAT9quermZrZ2pnq7Zrt/eBjZqrq++qvqer6q7srLjEZrXh+mN7lR+KgHyVf+VHaxve/wNNeJI7hc8EPxV58zlCyeVGtf3+uH4vnEOxP4H+WbGSaeXEZbTYPENyT4KrBYGY+9sPkNczMi36c7gsi8Q7KG5i9vwfbBlHyLO3KhfyHvSh9ayDAoxjIE1Pxye1stgq18MQ2LxS7MUsnB7SLbFIO5IAQBP71bkAfBHF6O6utsP1w+ywvSh6E9ozm8xurNxaDA8Jg9rBqXQjmWjfC52flJ5ctqzYOA0tugcWFAa8vla9cniweP9Ch3BQ6HcfiCIudAb6/9vybO5EezmfVeDD9wvGzmXdkp/8Ec8k1rHIGLO1r1aA0ngpi8S4+3ee2IEzu7XaE/MJi52C+GxyY6vcz8D42LQ5s1e/T/M/JWXEBi51DpoOp/eIGbECPoy95MjneHRTpxekRL166jpL1A2Y/SkMkyphFG4ZamF4YROlj2BmFPjj80bvYJD9q862LlBFOBo+wuA+omwEXVbucXETOuptWa6IWtreh3Wyfdj2XQrC/eSn+oVeUiSnJDr0O8tEqRchTi66z3rTQeIc+GMVqlF7JJhnkhTvYpgiRox6m9Uj0dGl74uAPPovNwMX5fF9HWMvy0Gk6UaN+QW/rgNYJHQeBW7q6UBxi8WrIE7XWwXdB2HwUh9AtrF4Fra0z0toK+LMjpZuKxG0s7gHT+HLPPPlksj566+61d0daW6EaLV2CuE9T+ymLO+hfcul7E3tPDF+jRE/jjrS2hr5RTc0l12eFnS1bjTY9mhVaQpm5JK2tAc0Ope3ecOrXURC27sgKLaF0LknrXMBsqZvODyvVurgCX/6VBTg4vso21qCRU9I6F5C73pRaHA6+lUE9oMxwpsp9nlLaPubI8iY9q3JBHt+oPyVNX1JmW/ctT7ONNZRWp7x7zS7eUVwjx9E6F+gn9WeQwBAAfiCT/8L6XFCBuCStcwG6A5k+Tlfkjz+yH6H4gvW5kGkHWMZM4Vp6W2rR97GKnCGaNVifCyqQPiJgVo+ESc8qa3j11iR0K5keG0bFi8U3NPudbXKBWqektTUwhoe6+lDcKS+wQ5ph7ucAmtYZaW0Feafc1Ybi66wwmM+euynDhukkPgyatscNWn7yNkjXBfV2774KO8ZhrbKBfLWTVSOh6bqUG4UtTXpar4kgbp4n78D7dKYHMioja5RPTT/HQff1bKcKxf34vDl7QjsrJigb2/Kr7lk6R75Nqc61LpYzIh/G+GHzadRjx0w6O7bisJXReQyWLPUJNjff8KLWrQx/ODC9V2NpPOPHyUcGo1KZvRKKxVPV+dZVDHd92HpgedqLli/HErwJh9575KOpbElKGjouTPjKZxX4PpMtOyz5LbOixnDGj1UBOSCty4MpqKKkdXkwBVWUtC4PpqCKktblwRRUUdK6PJiCKkpalwdTUEVJ6/JgCqooaV0eTEEVJa3LA4Io7QXB2IAjTWmvcsYKh2fA8mdJBwIa++tRO1Qq/wNhjFjPCSHmWAAAAABJRU5ErkJggg==";

        #endregion
    }
}
