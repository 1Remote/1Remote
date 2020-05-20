// Documentation:
// https://www.donkz.nl/overview-rdp-file-settings/
// https://docs.microsoft.com/en-us/windows-server/remote/remote-desktop-services/clients/rdp-files

using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RdpHelper
{
    public sealed class RdpConfig
    {
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
        [RdpConfName("redirectprinters:i:")]
        public int RedirectPrinters { get; set; } = 0;
        [RdpConfName("redirectcomports:i:")]
        public int RedirectComPorts { get; set; } = 0;
        [RdpConfName("redirectsmartcards:i:")]
        public int RedirectSmartCards { get; set; } = 0;
        [RdpConfName("redirectclipboard:i:")]
        public int RedirectClipboard { get; set; } = 0;
        [RdpConfName("redirectposdevices:i:")]
        public int RedirectPosDevices { get; set; } = 0;
        [RdpConfName("autoreconnection enabled:i:")]
        public int AutoReconnectionEnabled { get; set; } = 1;
        [RdpConfName("authentication level:i:")]
        public int AuthenticationLevel { get; set; } = 2;
        [RdpConfName("prompt for credentials:i:")]
        public int PromptForCredentials { get; set; } = 0;
        [RdpConfName("negotiate security layer:i:")]
        public int NegotiateSecurityLayer { get; set; } = 1;
        [RdpConfName("remoteapplicationmode:i:")]
        public int RemoteApplicationMode { get; set; } = 0;
        [RdpConfName("alternate shell:s:")]
        public string AlternateShell { get; set; } = "";
        [RdpConfName("shell working directory:s:")]
        public string ShellWorkingDirectory { get; set; } = "";
        [RdpConfName("gatewayhostname:s:")]
        public string GatewayHostname { get; set; } = "";
        [RdpConfName("gatewayusagemethod:i:")]
        public int GatewayUsageMethod { get; set; } = 4;
        [RdpConfName("gatewaycredentialssource:i:")]
        public int GatewayCredentialsSource { get; set; } = 4;
        [RdpConfName("gatewayprofileusagemethod:i:")]
        public int GatewayProfileUsageMethod { get; set; } = 0;
        [RdpConfName("promptcredentialonce:i:")]
        public int PromptCredentialOnce { get; set; } = 0;
        [RdpConfName("use redirection server name:i:")]
        public int UseRedirectionServerName { get; set; } = 0;
        [RdpConfName("drivestoredirect:s:")]
        public string DrivestoreDirect { get; set; } = "";
        [RdpConfName("redirectdirectx:i:")]
        public int RedirectDirectX { get; set; } = 1;

        [RdpConfName("full address:s:")]
        private string FullAddress { get; set; } = "";
        [RdpConfName("username:s:")]
        private string Username { get; set; } = "";
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
                RdpConfNameAttribute attr = typeof(RdpConfig)
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
