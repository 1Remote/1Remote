// Documentation:
// https://www.donkz.nl/overview-rdp-file-settings/
// https://docs.microsoft.com/en-us/windows-server/remote/remote-desktop-services/clients/rdp-files

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Shawn.Utils;

namespace _1RM.Utils.RdpFile
{
    /// <summary>
    /// REF:
    /// https://www.donkz.nl/overview-rdp-file-settings/
    /// https://docs.microsoft.com/en-us/windows-server/remote/remote-desktop-services/clients/rdp-files
    /// </summary>
    public sealed class RdpConfig
    {
        /// <summary>
        /// (1) The remote session will appear in a window; (2) The remote session will appear full screen
        /// </summary>
        [RdpConfName("screen mode id:i:")]
        public int ScreenModeId { get; set; } = 2;

        [RdpConfName("use multimon:i:")]
        public int UseMultimon { get; set; } = 0;

        [RdpConfName("desktopwidth:i:")]
        public int DesktopWidth { get; set; } = 1600;

        [RdpConfName("desktopheight:i:")]
        public int DesktopHeight { get; set; } = 900;

        /// <summary>
        /// Determines whether or not the local computer scales the content of the remote session to fit the window size.
        /// 0 The local window content won't scale when resized
        /// 1 The local window content will scale when resized
        /// </summary>
        [RdpConfName("smart sizing:i:")]
        public int SmartSizing { get; set; } = 0;

        /// <summary>
        /// Determines whether the resolution of the remote session is automatically updated when the local window is resized.
        /// 0 Session resolution remains static for the duration of the session
        /// 1 Session resolution updates as the local window resizes
        /// </summary>
        [RdpConfName("dynamic resolution:i:")]
        public int DynamicResolution { get; set; } = 1;

        [RdpConfName("session bpp:i:")]
        public int SessionBpp { get; set; } = 32;

        /// <summary>
        /// winposstr:s:0,m,l,t,r,b
        /// m = mode ( 1 = use coords for window position, 3 = open as a maximized window )
        /// l = left
        /// t = top
        /// r = right  (ie Window width)
        /// b = bottom (ie Window height)
        /// winposstr:s:0,1,100,100,800,600  ---- Opens up a 800x600 window 100 pixels in from the left edge of your leftmost monitor and 100 pixels down from the upper edge.
        /// </summary>
        [RdpConfName("winposstr:s:")]
        public string Winposstr { get; set; } = "";

        /// <summary>
        /// Determines whether bulk compression is enabled when it is transmitted by RDP to the local computer.
        /// </summary>
        [RdpConfName("compression:i:")]
        public int Compression { get; set; } = 1;

        /// <summary>
        /// Determines when Windows key combinations (WIN key, ALT+TAB) are applied to the remote session for desktop connections.
        /// 0 Windows key combinations are applied on the local computer
        /// 1 Windows key combinations are applied on the remote computer when in focus
        /// 2 Windows key combinations are applied on the remote computer in full screen mode only
        /// </summary>
        [RdpConfName("keyboardhook:i:")]
        public int KeyboardHook { get; set; } = 2;

        /// <summary>
        /// Microphone redirection:Indicates whether audio input redirection is enabled.
        /// - 0: Disable audio capture from the local device
        /// - 1: Enable audio capture from the local device and redirection to an audio application in the remote session
        /// </summary>
        [RdpConfName("audiocapturemode:i:")]
        public int AudioCaptureMode { get; set; } = 0;

        /// <summary>
        /// Determines if the connection will use RDP-efficient multimedia streaming for video playback.
        /// - 0: Don't use RDP efficient multimedia streaming for video playback
        /// - 1: Use RDP-efficient multimedia streaming for video playback when possible
        /// </summary>
        [RdpConfName("videoplaybackmode:i:")]
        public int VideoPlaybackMode { get; set; } = 1;

        /// <summary>
        /// in old version, newer is networkautodetect
        /// The "connection tye" Remote Desktop option specifies which type of internet connection the remote connection is using, in terms of available bandwidth. Depending on the option you select, the Remote Desktop connection will change performance-related settings, including font smoothing, animations, Windows Aero, themes, desktop backgrounds, and so on.
        /// 1 Modem (56Kbps)
        /// 2 Low-speed broadband (256Kbps---2Mbps)
        /// 3 Satellite (2Mbps---16Mbps with high latency)
        /// 4 High-speed broadband (2Mbps---10Mbps)
        /// 5 WAN (10Mbps or higher with high latency)
        /// 6 LAN (10Mbps or higher)
        /// 7 Automatic bandwidth detection
        /// </summary>
        [RdpConfName("connection type:i:")]
        public int ConnectionType { get; set; } = 7;

        /// <summary>
        /// Determines whether automatic network type detection is enabled
        /// </summary>
        [RdpConfName("networkautodetect:i:")]
        public int NetworkAutodetect { get; set; } = 1;

        /// <summary>
        /// - 0: Disable automatic network type detection
        /// - 1: Enable automatic network type detection
        /// </summary>
        [RdpConfName("bandwidthautodetect:i:")]
        public int BandwidthAutodetect { get; set; } = 1;

        [RdpConfName("displayconnectionbar:i:")]
        public int DisplayConnectionBar { get; set; } = 1;

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

        /// <summary>
        /// This setting determines whether bitmaps are cached on the local computer. This setting corresponds to the selection in the Bitmap caching check box on the Experience tab of Remote Desktop Connection Options.
        /// </summary>
        [RdpConfName("bitmapcachepersistenable:i:")]
        public int BitmapCachePersistenable { get; set; } = 1;

        /// <summary>
        /// - 0: Play sounds on the local computer (Play on this computer)
        /// - 1: Play sounds on the remote computer(Play on remote computer)
        /// - 2: Do not play sounds(Do not play)
        /// </summary>
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


        /// <summary>
        /// [2021-11-23 not work see #125]Determines whether local disk drives on the client computer will be redirected and available in the remote session.
        /// </summary>
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

        #region RemoteApp
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

        #endregion

        #region Gateway
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
        #endregion

        [RdpConfName("promptcredentialonce:i:")]
        public int PromptCredentialOnce { get; set; } = 0;

        [RdpConfName("use redirection server name:i:")]
        public int UseRedirectionServerName { get; set; } = 0;

        [RdpConfName("rdgiskdcproxy:i:")]
        public int RdgiskdcProxy { get; set; } = 0;

        [RdpConfName("kdcproxyname:s:")]
        public string KdcProxyName { get; set; } = "";

        /// <summary>
        /// Determines which supported Plug and Play devices on the client computer will be redirected and available in the remote session.
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
        /// Configures which cameras to redirect.
        /// This setting uses a semicolon-delimited list of KSCATEGORY_VIDEO_CAMERA interfaces of cameras enabled for redirection.
        /// "*" or ""
        /// </summary>
        [RdpConfName("camerastoredirect:s:")]
        public string CameraStoreDirect { get; set; } = "*";

        /// <summary>
        /// Specifies the name of the domain in which the user account that will be used to sign in to the remote computer is located.
        /// </summary>
        [RdpConfName("domain:s:")]
        public string Domain { get; set; } = "";

        /// <summary>
        /// loadbalanceinfo:s:tsv://MS Terminal Services Plugin.1.Wortell_sLab_Ses
        /// https://social.technet.microsoft.com/wiki/contents/articles/10392.rd-connection-broker-ha-and-the-rdp-properties-on-the-client.aspx
        /// </summary>
        [RdpConfName("loadbalanceinfo:s:")]
        public string LoadBalanceInfo { get; set; } = "";


        [RdpConfName("full address:s:")]
        public string FullAddress { get; set; } = "";

        [RdpConfName("username:s:")]
        public string Username { get; set; } = "";

        /// <summary>
        /// The user password in a binary hash value. Will be overruled by RDP+.
        /// </summary>
        [RdpConfName("password 51:b:")]
        public string Password { get; set; } = "";

        private readonly string _additionalSettings;

        public readonly string Name;
        public RdpConfig(string name, string address, string username, string password, string additionalSettings = "")
        {
            Name = name;
            FullAddress = address;
            Username = username;
            _additionalSettings = additionalSettings;

            if (string.IsNullOrEmpty(password) == false)
            {
                // encryption for rdp file
                Password = BitConverter.ToString(DataProtection.ProtectData(Encoding.Unicode.GetBytes(password), "")).Replace("-", "");
            }
        }

        public override string ToString()
        {
            var settings = new Dictionary<string, string>();

            // set all public properties by reflection
            foreach (var prop in typeof(RdpConfig).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (RdpConfNameAttribute attr in prop.GetCustomAttributes(typeof(RdpConfNameAttribute), false))
                {
                    settings.Add(attr.Name, prop.GetValue(this)!.ToString()!);
                }
            }


            // set additional settings, if existed then replace
            if (string.IsNullOrWhiteSpace(_additionalSettings) == false)
            {
                foreach (var s in _additionalSettings.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    for (var i = 'a'; i <= 'z'; i++)
                    {
                        if (Regex.IsMatch(s, @$":\s*{i}\s*:") == false) continue;
                        var ss = Regex.Split(s, $@":\s*{i}\s*:");
                        if (ss.Length == 2)
                        {
                            var key = $"{ss[0].Trim()}:{i}:";
                            var val = ss[1].Trim();
                            // if existed then replace
                            if (settings.ContainsKey(key))
                                settings[key] = val;
                            // or add
                            else
                                settings.Add(key, val);
                        }
                        break;
                    }
                }
            }

            // if `selectedmonitors` is set, then force set the `screen mode id` to 2 and `use multimon:i:` to 1
            // 若设置了 `selectedmonitors`，则强制打开多显示器模式和全屏模式
            if (settings.ContainsKey("selectedmonitors:i:") && settings["selectedmonitors:i:"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length > 1)
            {
                if (settings.ContainsKey("screen mode id:i:") == false)
                    settings.Add("screen mode id:i:", "2");
                if (settings.ContainsKey("use multimon:i:") == false)
                    settings.Add("use multimon:i:", "1");
                settings["screen mode id:i:"] = "2";
                settings["use multimon:i:"] = "1";
            }

            var str = new StringBuilder();
            foreach (var kv in settings)
            {
                str.AppendLine($"{kv.Key}{kv.Value}");
            }

            return str.ToString();
        }


        public static RdpConfig? FromRdpFile(string rdpFilePath)
        {
            var fi = new FileInfo(rdpFilePath);
            RdpConfig? rdpConfig = null;
            // read txt by line
            var pts = new []{ 's', 'i' };
            bool flag = false;
            if (fi.Exists)
            {
                rdpConfig = new RdpConfig(fi.Name.ReplaceLast(fi.Extension, ""), "", "", "");
                foreach (var line in System.IO.File.ReadLines(rdpFilePath))
                {
                    //var ss = line.Split(":", StringSplitOptions.TrimEntries);
                    foreach (var t in pts)
                    {
                        if (Regex.IsMatch(line, @$":\s*{t}\s*:") == false) continue;
                        var ss = Regex.Split(line, $@":\s*{t}\s*:");
                        if (ss.Length == 2)
                        {
                            var key = $"{ss[0].Trim()}:{t}:";
                            var val = ss[1].Trim();
                            foreach (var prop in typeof(RdpConfig).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(x => x.Name != nameof(Password)))
                            {
                                if (prop.GetCustomAttributes(typeof(RdpConfNameAttribute), false).Cast<RdpConfNameAttribute>().Any(attr => string.Equals(attr.Name, key, StringComparison.CurrentCultureIgnoreCase)))
                                {
                                    flag = true;
                                    if (t == 'i')
                                    {
                                        if (int.TryParse(val, out var iVal))
                                        {
                                            prop.SetValue(rdpConfig, iVal);
                                        }
                                    }
                                    else
                                    {
                                        prop.SetValue(rdpConfig, val);
                                    }
                                }
                            }
                        }

                        break;
                    }
                }
            }
            return flag ? rdpConfig : null;
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
        private class RdpConfNameAttribute : Attribute
        {
            public string Name { get; private set; }

            public RdpConfNameAttribute(string name)
            {
                this.Name = name;
            }
        }
    }
}