// Documentation:
// https://www.donkz.nl/overview-rdp-file-settings/
// https://docs.microsoft.com/en-us/windows-server/remote/remote-desktop-services/clients/rdp-files

using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Shawn.Utils.RdpFile
{
    /// <summary>
    /// REF:
    /// https://www.donkz.nl/overview-rdp-file-settings/
    /// https://docs.microsoft.com/en-us/windows-server/remote/remote-desktop-services/clients/rdp-files
    /// </summary>
    public sealed class RdpConfig
    {
        [RdpConfName("span monitors:i:")]
        public int SpanMonitors { get; set; } = 1;

        [RdpConfName("screen mode id:i:")]
        public int ScreenModeId { get; set; } = 2;

        [RdpConfName("use multimon:i:")]
        public int UseMultimon { get; set; } = 0;

        [RdpConfName("desktopwidth:i:")]
        public int DesktopWidth { get; set; } = 1600;

        [RdpConfName("desktopheight:i:")]
        public int DesktopHeight { get; set; } = 900;

        [RdpConfName("smart sizing:i:")]
        public int SmartSizing { get; set; } = 0;

        [RdpConfName("dynamic resolution:i:")]
        public int DynamicResolution { get; set; } = 1;

        [RdpConfName("session bpp:i:")]
        public int SessionBpp { get; set; } = 32;

        [RdpConfName("winposstr:s:")]
        public string Winposstr { get; set; } = "0,3,0,0,800,600";

        [RdpConfName("compression:i:")]
        public int Compression { get; set; } = 1;

        [RdpConfName("keyboardhook:i:")]
        public int KeyboardHook { get; set; } = 2;

        [RdpConfName("audiocapturemode:i:")]
        public int AudioCaptureMode { get; set; } = 0;

        [RdpConfName("videoplaybackmode:i:")]
        public int VideoPlaybackMode { get; set; } = 1;

        [RdpConfName("connection type:i:")]
        public int ConnectionType { get; set; } = 2;

        [RdpConfName("networkautodetect:i:")]
        public int NetworkAutodetect { get; set; } = 1;

        [RdpConfName("bandwidthautodetect:i:")]
        public int BandwidthAutodetect { get; set; } = 1;

        [RdpConfName("displayconnectionbar:i:")]
        public int DisplayConnectionBar { get; set; } = 1;

        [RdpConfName("enableworkspacereconnect:i:")]
        public int EnableWorkspaceReconnect { get; set; } = 0;

        [RdpConfName("disable wallpaper:i:")]
        public int DisableWallpaper { get; set; } = 1;

        [RdpConfName("allow font smoothing:i:")]
        public int AllowFontSmoothing { get; set; } = 0;

        [RdpConfName("allow desktop composition:i:")]
        public int AllowDesktopComposition { get; set; } = 0;

        [RdpConfName("disable full window drag:i:")]
        public int DisableFullWindowDrag { get; set; } = 1;

        [RdpConfName("disable menu anims:i:")]
        public int DisableMenuAnims { get; set; } = 1;

        [RdpConfName("disable themes:i:")]
        public int DisableThemes { get; set; } = 0;

        [RdpConfName("disable cursor setting:i:")]
        public int DisableCursorSetting { get; set; } = 0;

        [RdpConfName("bitmapcachepersistenable:i:")]
        public int BitmapCachePersistenable { get; set; } = 1;

        [RdpConfName("audiomode:i:")]
        public int AudioMode { get; set; } = 1;

        /// <summary>
        /// Determines whether the clipboard on the client computer will be redirected and available in the remote session and vice versa.
        /// 0 - Do not redirect the clipboard.
        /// 1 - Redirect the clipboard.
        /// </summary>
        [RdpConfName("redirectclipboard:i:")]
        public int RedirectClipboard { get; set; } = 0;

        [RdpConfName("redirectcomports:i:")]
        public int RedirectComPorts { get; set; } = 0;

        [RdpConfName("redirectdirectx:i:")]
        public int RedirectDirectX { get; set; } = 1;

        [RdpConfName("redirectdrives:i:")]
        public int RedirectDrives { get; set; } = 1;

        [RdpConfName("redirectposdevices:i:")]
        public int RedirectPosDevices { get; set; } = 0;

        [RdpConfName("redirectprinters:i:")]
        public int RedirectPrinters { get; set; } = 0;

        [RdpConfName("redirectsmartcards:i:")]
        public int RedirectSmartCards { get; set; } = 0;

        [RdpConfName("autoreconnection enabled:i:")]
        public int AutoReconnectionEnabled { get; set; } = 1;

        /// <summary>
        /// Defines the server authentication level settings.
        /// - 0: If server authentication fails, connect to the computer without warning (Connect and don't warn me)
        /// - 1: If server authentication fails, don't establish a connection (Don't connect)
        /// - 2: If server authentication fails, show a warning and allow me to connect or refuse the connection(Warn me)
        /// - 3: No authentication requirement specified.
        /// </summary>
        [RdpConfName("authentication level:i:")]
        public int AuthenticationLevel { get; set; } = 2;

        [RdpConfName("prompt for credentials:i:")]
        public int PromptForCredentials { get; set; } = 0;

        [RdpConfName("negotiate security layer:i:")]
        public int NegotiateSecurityLayer { get; set; } = 1;

        [RdpConfName("remoteapplicationmode:i:")]
        public int RemoteApplicationMode { get; set; } = 0;

        /// <summary>
        /// Specifies the name of the RemoteApp in the client interface while starting the RemoteApp.App display name. For example, "Excel 2016."
        /// </summary>
        [RdpConfName("remoteapplicationname:s:")]
        public string RemoteApplicationName { get; set; } = "";

        /// <summary>
        /// Specifies the alias or executable name of the RemoteApp. Valid alias or name. For example, "EXCEL."
        /// </summary>
        [RdpConfName("remoteapplicationprogram:s:")]
        public string RemoteApplicationProgram { get; set; } = "";

        /// <summary>
        /// Specifies whether the Remote Desktop client should check the remote computer for RemoteApp capabilities.
        /// 0 - Check the remote computer for RemoteApp capabilities before logging in.
        /// 1 - Do not check the remote computer for RemoteApp capabilities.Note: This setting must be set to 1 when connecting to Windows XP SP3, Vista or 7 computers with RemoteApps configured on them. This is the default behavior of RDP+.
        /// </summary>
        [RdpConfName("disableremoteappcapscheck:i:")]
        public int DisableRemoteAppCapsCheck { get; set; } = 1;

        [RdpConfName("alternate shell:s:")]
        public string AlternateShell { get; set; } = "";

        [RdpConfName("shell working directory:s:")]
        public string ShellWorkingDirectory { get; set; } = "";

        [RdpConfName("gatewayhostname:s:")]
        public string GatewayHostname { get; set; } = "";

        /// <summary>
        /// Specifies when to use an RD Gateway for the connection.
        /// - 0: Don't use an RD Gateway
        /// - 1: Always use an RD Gateway
        /// - 2: Use an RD Gateway if a direct connection cannot be made to the RD Session Host
        /// - 3: Use the default RD Gateway settings
        /// - 4: Don't use an RD Gateway, bypass gateway for local addresses
        /// Setting this property value to 0 or 4 are effectively equivalent, but setting this property to 4 enables the option to bypass local addresses.
        /// </summary>
        [RdpConfName("gatewayusagemethod:i:")]
        public int GatewayUsageMethod { get; set; } = 4;

        /// <summary>
        /// Specifies the RD Gateway authentication method.
        /// - 0: Ask for password (NTLM)
        /// - 1: Use smart card
        /// - 2: Use the credentials for the currently logged on user.
        /// - 3: Prompt the user for their credentials and use basic authentication
        /// - 4: Allow user to select later
        /// - 5: Use cookie-based authentication
        /// </summary>
        [RdpConfName("gatewaycredentialssource:i:")]
        public int GatewayCredentialsSource { get; set; } = 4;

        /// <summary>
        /// Specifies whether to use default RD Gateway settings.
        /// - 0: Use the default profile mode, as specified by the administrator
        /// - 1: Use explicit settings, as specified by the user
        /// </summary>
        [RdpConfName("gatewayprofileusagemethod:i:")]
        public int GatewayProfileUsageMethod { get; set; } = 0;

        [RdpConfName("promptcredentialonce:i:")]
        public int PromptCredentialOnce { get; set; } = 0;

        [RdpConfName("use redirection server name:i:")]
        public int UseRedirectionServerName { get; set; } = 0;

        [RdpConfName("rdgiskdcproxy:i:")]
        public int RdgiskdcProxy { get; set; } = 0;

        [RdpConfName("kdcproxyname:s:")]
        public string KdcProxyName { get; set; } = "";

        /// <summary>
        /// 	Determines which supported Plug and Play devices on the client computer will be redirected and available in the remote session.
        /// No value specified - Do not redirect any supported Plug and Play devices.
        /// * - Redirect all supported Plug and Play devices, including ones that are connected later.
        /// DynamicDevices - Redirect any supported Plug and Play devices that are connected later.
        /// The hardware ID for one or more Plug and Play devices - Redirect the specified supported Plug and Play device(s).
        /// </summary>
        [RdpConfName("devicestoredirect:s:")]
        public string DeviceStoreDirect { get; set; } = "*";

        /// <summary>
        /// Determines which local disk drives on the client computer will be redirected and available in the remote session.
        /// No value specified - Do not redirect any drives.
        /// * - Redirect all disk drives, including drives that are connected later.
        /// DynamicDrives - Redirect any drives that are connected later.
        /// The drive and labels for one or more drives - Redirect the specified drive(s). e.g. "C:\;D:\;"
        /// </summary>
        [RdpConfName("drivestoredirect:s:")]
        public string DriveStoreDirect { get; set; } = "*";

        /// <summary>
        /// "*" or ""
        /// </summary>
        [RdpConfName("camerastoredirect:s:")]
        public string CameraStoreDirect { get; set; } = "";

        [RdpConfName("full address:s:")]
        private string FullAddress { get; set; } = "";

        [RdpConfName("username:s:")]
        private string Username { get; set; } = "";

        /// <summary>
        /// The user password in a binary hash value. Will be overruled by RDP+.
        /// </summary>
        [RdpConfName("password 51:b:")]
        private string Password { get; set; }

        public RdpConfig(string address, string username, string password)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException(nameof(address));
            }

            FullAddress = address;
            Username = username;

            if ((password ?? "") != "")
            {
                Password = BitConverter.ToString(DataProtection.ProtectData(Encoding.Unicode.GetBytes(password), "")).Replace("-", "");
            }
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();

            foreach (var prop in typeof(RdpConfig).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(x => x.Name != nameof(this.Password)))
            {
                foreach (RdpConfNameAttribute attr in prop.GetCustomAttributes(typeof(RdpConfNameAttribute), false))
                {
                    str.AppendLine(attr.Name + prop.GetValue(this));
                }
            }

            if ((this.Password ?? "") != "")
            {
                var attr = typeof(RdpConfig)
                    .GetProperty(nameof(this.Password), BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetCustomAttributes(typeof(RdpConfNameAttribute), false)
                    .First() as RdpConfNameAttribute;
                str.AppendLine(attr.Name + this.Password);
            }

            return str.ToString();
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class RdpConfNameAttribute : Attribute
    {
        public string Name { get; private set; }

        public RdpConfNameAttribute(string name)
        {
            this.Name = name;
        }
    }
}