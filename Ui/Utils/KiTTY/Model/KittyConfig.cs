using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _1RM.Service;
using Microsoft.Win32;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Utils.KiTTY.Model
{
    public class KittyConfig
    {
        public readonly List<KittyConfigKeyValuePair> Options = new List<KittyConfigKeyValuePair>();
        public readonly string SessionName;

        /// <summary>
        /// read existed config files.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<KittyConfigKeyValuePair> Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new List<KittyConfigKeyValuePair>();
            }

            var lines = File.ReadAllLines(path);
            var ret = new List<KittyConfigKeyValuePair>(lines.Length);
            foreach (var s in lines)
            {
                var line = s.Trim('\t', ' ');
                var i0 = line.IndexOf(@"\", StringComparison.Ordinal);
                if (line.EndsWith(@"\", StringComparison.Ordinal))
                {
                    var para = line.Substring(0, i0);
                    var val = line.Substring(i0 + 1).TrimEnd('\\');
                    if (string.IsNullOrWhiteSpace(para) || string.IsNullOrWhiteSpace(val))
                    {
                        continue;
                    }
                    if (double.TryParse(val.Replace(',', '_'), out _))
                    {
                        ret.Add(new KittyConfigKeyValuePair() { Key = para, Value = val, ValueKind = RegistryValueKind.DWord });
                    }
                    else
                    {
                        ret.Add(new KittyConfigKeyValuePair() { Key = para, Value = val, ValueKind = RegistryValueKind.String });
                    }
                }
            }
            return ret;
        }

        public KittyConfig(string sessionName)
        {
            SessionName = sessionName;
            InitDefault();

            // DISABLED ALT + F4
            if (Options.Any(x => string.Equals(x.Key, EnumKittyConfigKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase)))
            {
                var oldItem = Options.First(x => string.Equals(x.Key, EnumKittyConfigKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase));
                oldItem.Value = 0;
            }
            else
                Set(EnumKittyConfigKey.AltF4, 0x00000000); // DISABLED ALTF4
        }

        public void ApplyOverwriteSession(string? overwritePath = null)
        {
            if (!string.IsNullOrEmpty(overwritePath) && File.Exists(overwritePath))
            {
                var overWrite = Read(overwritePath!);
                foreach (var item in overWrite)
                {
                    if (Options.Any(x => string.Equals(x.Key, item.Key, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        var oldItem = Options.First(x => string.Equals(x.Key, item.Key, StringComparison.CurrentCultureIgnoreCase));
                        oldItem.Value = item.Value;
                        oldItem.ValueKind = item.ValueKind;
                    }
                    else
                    {
                        Options.Add(item);
                    }
                }
            }
        }

        private void InitDefault()
        {
            Options.Clear();

            #region Default

            Set(EnumKittyConfigKey.TerminalType, "xterm");
            Set(EnumKittyConfigKey.TerminalSpeed, "38400,38400");
            Set(EnumKittyConfigKey.TerminalModes, "INTR=A,QUIT=A,ERASE=A,KILL=A,EOF=A,EOL=A,EOL2=A,START=A,STOP=A,SUSP=A,DSUSP=A,REPRINT=A,WERASE=A,LNEXT=A,FLUSH=A,SWTCH=A,STATUS=A,DISCARD=A,IGNPAR=A,PARMRK=A,INPCK=A,ISTRIP=A,INLCR=A,IGNCR=A,ICRNL=A,IUCLC=A,IXON=A,IXANY=A,IXOFF=A,IMAXBEL=A,ISIG=A,ICANON=A,XCASE=A,ECHO=A,ECHOE=A,ECHOK=A,ECHONL=A,NOFLSH=A,TOSTOP=A,IEXTEN=A,ECHOCTL=A,ECHOKE=A,PENDIN=A,OPOST=A,OLCUC=A,ONLCR=A,OCRNL=A,ONOCR=A,ONLRET=A,CS7=A,CS8=A,PARENB=A,PARODD=A,");
            Set(EnumKittyConfigKey.ProxyExcludeList, "");
            Set(EnumKittyConfigKey.ProxyHost, "proxy");
            Set(EnumKittyConfigKey.ProxyUsername, "");
            Set(EnumKittyConfigKey.ProxyPassword, "");
            Set(EnumKittyConfigKey.ProxyTelnetCommand, "connect%20%25host%20%25port%5Cn");
            Set(EnumKittyConfigKey.Environment, "");
            Set(EnumKittyConfigKey.UserName, "");
            Set(EnumKittyConfigKey.LocalUserName, "");
            Set(EnumKittyConfigKey.Cipher, "aes,chacha20,3des,WARN,des,blowfish,arcfour");
            Set(EnumKittyConfigKey.KEX, "ecdh,dh-gex-sha1,dh-group14-sha1,rsa,WARN,dh-group1-sha1");
            Set(EnumKittyConfigKey.RekeyBytes, "1G");
            Set(EnumKittyConfigKey.GSSLibs, "gssapi32,sspi,custom");
            Set(EnumKittyConfigKey.GSSCustom, "");
            Set(EnumKittyConfigKey.LogHost, "");
            Set(EnumKittyConfigKey.PublicKeyFile, "");
            Set(EnumKittyConfigKey.RemoteCommand, "");
            Set(EnumKittyConfigKey.Answerback, "KiTTY");
            Set(EnumKittyConfigKey.BellWaveFile, "");
            Set(EnumKittyConfigKey.WinTitle, "");
            Set(EnumKittyConfigKey.Colour0, "187,187,187");
            Set(EnumKittyConfigKey.Colour1, "255,255,255");
            Set(EnumKittyConfigKey.Colour2, "0,0,0");
            Set(EnumKittyConfigKey.Colour3, "85,85,85");
            Set(EnumKittyConfigKey.Colour4, "0,0,0");
            Set(EnumKittyConfigKey.Colour5, "0,255,0");
            Set(EnumKittyConfigKey.Colour6, "0,0,0");
            Set(EnumKittyConfigKey.Colour7, "85,85,85");
            Set(EnumKittyConfigKey.Colour8, "187,0,0");
            Set(EnumKittyConfigKey.Colour9, "255,85,85");
            Set(EnumKittyConfigKey.Colour10, "0,187,0");
            Set(EnumKittyConfigKey.Colour11, "85,255,85");
            Set(EnumKittyConfigKey.Colour12, "187,187,0");
            Set(EnumKittyConfigKey.Colour13, "255,255,85");
            Set(EnumKittyConfigKey.Colour14, "0,0,187");
            Set(EnumKittyConfigKey.Colour15, "85,85,255");
            Set(EnumKittyConfigKey.Colour16, "187,0,187");
            Set(EnumKittyConfigKey.Colour17, "255,85,255");
            Set(EnumKittyConfigKey.Colour18, "0,187,187");
            Set(EnumKittyConfigKey.Colour19, "85,255,255");
            Set(EnumKittyConfigKey.Colour20, "187,187,187");
            Set(EnumKittyConfigKey.Colour21, "255,255,255");
            Set(EnumKittyConfigKey.Wordness0, "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
            Set(EnumKittyConfigKey.Wordness32, "0,1,2,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1");
            Set(EnumKittyConfigKey.Wordness64, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,2");
            Set(EnumKittyConfigKey.Wordness96, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1");
            Set(EnumKittyConfigKey.Wordness128, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1");
            Set(EnumKittyConfigKey.Wordness160, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1");
            Set(EnumKittyConfigKey.Wordness192, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2");
            Set(EnumKittyConfigKey.Wordness224, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2");
            Set(EnumKittyConfigKey.LineCodePage, "");
            Set(EnumKittyConfigKey.Printer, "");
            Set(EnumKittyConfigKey.X11Display, "");
            Set(EnumKittyConfigKey.X11AuthFile, "");
            Set(EnumKittyConfigKey.PortForwardings, "");
            Set(EnumKittyConfigKey.BoldFont, "");
            Set(EnumKittyConfigKey.WideFont, "");
            Set(EnumKittyConfigKey.WideBoldFont, "");
            Set(EnumKittyConfigKey.SerialLine, "COM1");
            Set(EnumKittyConfigKey.WindowClass, "");
            Set(EnumKittyConfigKey.Present, 0x00000001);
            Set(EnumKittyConfigKey.LogType, 0x00000000);
            Set(EnumKittyConfigKey.LogFlush, 0x00000001);
            Set(EnumKittyConfigKey.SSHLogOmitPasswords, 0x00000001);
            Set(EnumKittyConfigKey.SSHLogOmitData, 0x00000000);
            Set(EnumKittyConfigKey.PortNumber, 0x00000016);
            Set(EnumKittyConfigKey.CloseOnExit, 0x00000001);
            Set(EnumKittyConfigKey.WarnOnClose, 0x00000000);
            Set(EnumKittyConfigKey.PingInterval, 0x00000000);
            Set(EnumKittyConfigKey.PingIntervalSecs, 0x0000003c);
            Set(EnumKittyConfigKey.TCPNoDelay, 0x00000001);
            Set(EnumKittyConfigKey.TCPKeepalives, 0x0000001E); // seconds between keepalives
            Set(EnumKittyConfigKey.AddressFamily, 0x00000000);
            Set(EnumKittyConfigKey.ProxyDNS, 0x00000001);
            Set(EnumKittyConfigKey.ProxyLocalhost, 0x00000000);
            Set(EnumKittyConfigKey.ProxyMethod, 0x00000000);
            Set(EnumKittyConfigKey.ProxyPort, 0x00000050);
            Set(EnumKittyConfigKey.UserNameFromEnvironment, 0x00000000);
            Set(EnumKittyConfigKey.NoPTY, 0x00000000);
            Set(EnumKittyConfigKey.Compression, 0x00000001);
            Set(EnumKittyConfigKey.TryAgent, 0x00000001);
            Set(EnumKittyConfigKey.AgentFwd, 0x00000000);
            Set(EnumKittyConfigKey.GssapiFwd, 0x00000000);
            Set(EnumKittyConfigKey.ChangeUsername, 0x00000000);
            Set(EnumKittyConfigKey.RekeyTime, 0x0000003c);
            Set(EnumKittyConfigKey.SshNoAuth, 0x00000000);
            Set(EnumKittyConfigKey.SshBanner, 0x00000001);
            Set(EnumKittyConfigKey.AuthTIS, 0x00000000);
            Set(EnumKittyConfigKey.AuthKI, 0x00000001);
            Set(EnumKittyConfigKey.AuthGSSAPI, 0x00000001);
            Set(EnumKittyConfigKey.SshNoShell, 0x00000000);
            Set(EnumKittyConfigKey.SshProt, 0x00000002);
            Set(EnumKittyConfigKey.SSH2DES, 0x00000000);
            Set(EnumKittyConfigKey.RFCEnviron, 0x00000000);
            Set(EnumKittyConfigKey.PassiveTelnet, 0x00000000);
            Set(EnumKittyConfigKey.BackspaceIsDelete, 0x00000001);
            Set(EnumKittyConfigKey.RXVTHomeEnd, 0x00000000);
            Set(EnumKittyConfigKey.LinuxFunctionKeys, 0x00000002);
            Set(EnumKittyConfigKey.NoApplicationKeys, 0x00000000);
            Set(EnumKittyConfigKey.NoApplicationCursors, 0x00000000);
            Set(EnumKittyConfigKey.NoMouseReporting, 0x00000000);
            Set(EnumKittyConfigKey.NoRemoteResize, 0x00000001);
            Set(EnumKittyConfigKey.NoAltScreen, 0x00000000);
            Set(EnumKittyConfigKey.NoRemoteWinTitle, 0x00000000);
            Set(EnumKittyConfigKey.RemoteQTitleAction, 0x00000001);
            Set(EnumKittyConfigKey.NoDBackspace, 0x00000000);
            Set(EnumKittyConfigKey.NoRemoteCharset, 0x00000000);
            Set(EnumKittyConfigKey.ApplicationCursorKeys, 0x00000000);
            Set(EnumKittyConfigKey.ApplicationKeypad, 0x00000000);
            Set(EnumKittyConfigKey.NetHackKeypad, 0x00000000);
            Set(EnumKittyConfigKey.AltSpace, 0x00000000);
            Set(EnumKittyConfigKey.AltOnly, 0x00000000);
            Set(EnumKittyConfigKey.ComposeKey, 0x00000000);
            Set(EnumKittyConfigKey.CtrlAltKeys, 0x00000001);
            Set(EnumKittyConfigKey.TelnetKey, 0x00000000);
            Set(EnumKittyConfigKey.TelnetRet, 0x00000001);
            Set(EnumKittyConfigKey.LocalEcho, 0x00000002);
            Set(EnumKittyConfigKey.LocalEdit, 0x00000002);
            Set(EnumKittyConfigKey.AlwaysOnTop, 0x00000000);
            Set(EnumKittyConfigKey.FullScreenOnAltEnter, 0x00000000);
            Set(EnumKittyConfigKey.HideMousePtr, 0x00000000);
            Set(EnumKittyConfigKey.SunkenEdge, 0x00000000);
            Set(EnumKittyConfigKey.WindowBorder, 0x00000000);
            Set(EnumKittyConfigKey.CurType, 0x00000000);
            Set(EnumKittyConfigKey.BlinkCur, 0x00000000);
            Set(EnumKittyConfigKey.Beep, 0x00000001);
            Set(EnumKittyConfigKey.BeepInd, 0x00000000);
            Set(EnumKittyConfigKey.BellOverload, 0x00000001);
            Set(EnumKittyConfigKey.BellOverloadN, 0x00000005);
            Set(EnumKittyConfigKey.BellOverloadT, 0x000007d0);
            Set(EnumKittyConfigKey.BellOverloadS, 0x00001388);
            Set(EnumKittyConfigKey.ScrollbackLines, 0x00002000);
            Set(EnumKittyConfigKey.DECOriginMode, 0x00000000);
            Set(EnumKittyConfigKey.AutoWrapMode, 0x00000001);
            Set(EnumKittyConfigKey.LFImpliesCR, 0x00000000);
            Set(EnumKittyConfigKey.CRImpliesLF, 0x00000000);
            Set(EnumKittyConfigKey.DisableArabicShaping, 0x00000000);
            Set(EnumKittyConfigKey.DisableBidi, 0x00000000);
            Set(EnumKittyConfigKey.WinNameAlways, 0x00000001);
            Set(EnumKittyConfigKey.TermWidth, 0x00000050);
            Set(EnumKittyConfigKey.TermHeight, 0x00000018);
            Set(EnumKittyConfigKey.FontIsBold, 0x00000000);
            Set(EnumKittyConfigKey.FontCharSet, 0x00000000);
            Set(EnumKittyConfigKey.Font, "Consolas");
            Set(EnumKittyConfigKey.FontHeight, 12);
            Set(EnumKittyConfigKey.FontCharSet, 0x00000000);
            Set(EnumKittyConfigKey.FontQuality, 0x00000000);
            Set(EnumKittyConfigKey.FontVTMode, 0x00000004);
            Set(EnumKittyConfigKey.UseSystemColours, 0x00000000);
            Set(EnumKittyConfigKey.TryPalette, 0x00000000);
            Set(EnumKittyConfigKey.ANSIColour, 0x00000001);
            Set(EnumKittyConfigKey.Xterm256Colour, 0x00000001);
            Set(EnumKittyConfigKey.BoldAsColour, 0x00000001);
            Set(EnumKittyConfigKey.RawCNP, 0x00000000);
            Set(EnumKittyConfigKey.PasteRTF, 0x00000000);
            Set(EnumKittyConfigKey.MouseIsXterm, 0x00000000);
            Set(EnumKittyConfigKey.MouseOverride, 0x00000001);
            Set(EnumKittyConfigKey.RectSelect, 0x00000000);
            Set(EnumKittyConfigKey.CJKAmbigWide, 0x00000000);
            Set(EnumKittyConfigKey.UTF8Override, 0x00000001);
            Set(EnumKittyConfigKey.CapsLockCyr, 0x00000000);
            Set(EnumKittyConfigKey.ScrollBar, 0x00000001); // ScrollBar在kitty终端中的作用是启用或禁用滚动条
            Set(EnumKittyConfigKey.ScrollBarFullScreen, 0x00000001);
            Set(EnumKittyConfigKey.ScrollOnKey, 0x00000000);
            Set(EnumKittyConfigKey.ScrollOnDisp, 0x00000f001);
            Set(EnumKittyConfigKey.ScrollbarOnLeft, 0x00000000);
            Set(EnumKittyConfigKey.EraseToScrollback, 0x00000001);
            Set(EnumKittyConfigKey.LockSize, 0x00000000);
            Set(EnumKittyConfigKey.BCE, 0x00000001);
            Set(EnumKittyConfigKey.BlinkText, 0x00000000);
            Set(EnumKittyConfigKey.X11Forward, 0x00000000);
            Set(EnumKittyConfigKey.X11AuthType, 0x00000001);
            Set(EnumKittyConfigKey.LocalPortAcceptAll, 0x00000000);
            Set(EnumKittyConfigKey.RemotePortAcceptAll, 0x00000000);
            Set(EnumKittyConfigKey.BugIgnore1, 0x00000000);
            Set(EnumKittyConfigKey.BugPlainPW1, 0x00000000);
            Set(EnumKittyConfigKey.BugRSA1, 0x00000000);
            Set(EnumKittyConfigKey.BugIgnore2, 0x00000000);
            Set(EnumKittyConfigKey.BugHMAC2, 0x00000000);
            Set(EnumKittyConfigKey.BugDeriveKey2, 0x00000000);
            Set(EnumKittyConfigKey.BugRSAPad2, 0x00000000);
            Set(EnumKittyConfigKey.BugPKSessID2, 0x00000000);
            Set(EnumKittyConfigKey.BugRekey2, 0x00000000);
            Set(EnumKittyConfigKey.BugMaxPkt2, 0x00000000);
            Set(EnumKittyConfigKey.StampUtmp, 0x00000001);
            Set(EnumKittyConfigKey.LoginShell, 0x00000001);
            Set(EnumKittyConfigKey.ShadowBold, 0x00000000);
            Set(EnumKittyConfigKey.ShadowBoldOffset, 0x00000001);
            Set(EnumKittyConfigKey.SerialSpeed, 0x00002580);
            Set(EnumKittyConfigKey.SerialDataBits, 0x00000008);
            Set(EnumKittyConfigKey.SerialStopHalfbits, 0x00000002);
            Set(EnumKittyConfigKey.SerialParity, 0x00000000);
            Set(EnumKittyConfigKey.SerialFlowControl, 0x00000001);
            Set(EnumKittyConfigKey.Autocommand, "");

            #endregion Default
        }

        public void Set(EnumKittyConfigKey key, int value)
        {
            Set(key, value.ToString());
        }

        public void Set(EnumKittyConfigKey key, string value)
        {
            if (Options.Any(x => x.Key == key.ToString()))
            {
                var item = Options.First(x => x.Key == key.ToString());
                item.Value = value;
            }
            else
            {
                Options.Add(KittyConfigKeyValuePair.Create(key.ToString(), value));
            }
        }

        /// <summary>
        /// save to reg table
        /// </summary>
        public void SaveToPuttyRegistryTable()
        {
            string regPath = $"Software\\SimonTatham\\PuTTY\\Sessions\\{SessionName}";
            using var regKey = Registry.CurrentUser.CreateSubKey(regPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (regKey == null) return;
            foreach (var item in Options)
            {
                if (item.Value != null)
                    regKey.SetValue(item.Key, item.Value, item.ValueKind);
            }
        }

        /// <summary>
        /// del from reg table
        /// </summary>
        public void DelFromPuttyRegistryTable()
        {
            string regPath = $"Software\\SimonTatham\\PuTTY\\Sessions\\{SessionName}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(regPath);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        /// <summary>
        /// save to reg table
        /// </summary>
        private void SaveToKittyRegistryTable()
        {
            string regPath = $"Software\\9bis.com\\KiTTY\\Sessions\\{SessionName}";
            try
            {
                using var regKey = Registry.CurrentUser.CreateSubKey(regPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (regKey == null) return;
                foreach (var item in Options.Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value != null))
                {
                    try
                    {
                        regKey.SetValue(item.Key, item.Value, item.ValueKind);
                    }
                    catch (Exception e1)
                    {
                        SimpleLogHelper.Warning(e1, $"regKey.SetValue({item.Key}, {item.Value}, {item.ValueKind})");
                    }
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
        }

        public void SaveToKittyConfig(string kittyExePath)
        {
            SaveToKittyPortableConfig(kittyExePath, SessionName, Options);
            SaveToKittyRegistryTable();
        }

        public void DelFromKittyConfig(string kittyPath)
        {
            DelFromKittyPortableConfig(kittyPath, SessionName);
            DelFromKittyRegistryTable(SessionName);
        }






        /// <summary>
        /// del from reg table
        /// </summary>
        private static void DelFromKittyRegistryTable(string sessionName)
        {
            string regPath = $"Software\\9bis.com\\KiTTY\\Sessions\\{sessionName}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(regPath);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private static void SaveToKittyPortableConfig(string kittyExePath, string sessionName, List<KittyConfigKeyValuePair> options)
        {
            try
            {
                string configPath = Path.Combine(Path.GetDirectoryName(kittyExePath)!, "Sessions", sessionName.Replace(" ", "%20"));
                var sb = new StringBuilder();
                foreach (var item in options)
                {
                    if (item.Value != null)
                        sb.AppendLine($@"{item.Key}\{item.Value}\");
                }

                var fi = new FileInfo(configPath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();

                RetryHelper.Try(() =>
                {
                    File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
                }, actionOnError: exception => SentryIoHelper.Error(exception));
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
        }

        private static void DelFromKittyPortableConfig(string kittyPath, string sessionName)
        {
            try
            {
                string configPath = Path.Combine(kittyPath, "Sessions", sessionName.Replace(" ", "%20"));
                if (File.Exists(configPath))
                    File.Delete(configPath);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
        }

        public static void CleanUpOldConfig()
        {
            if (!Directory.Exists(AppPathHelper.Instance.KittyDirPath))
            {
                Directory.CreateDirectory(AppPathHelper.Instance.KittyDirPath);
                return;
            }

            string configPath = Path.Combine(AppPathHelper.Instance.KittyDirPath, "Sessions");
            var di = new DirectoryInfo(configPath);
            if (di.Exists)
            {
                var fis = di.GetFiles();
                foreach (var fi in fis)
                {
                    try
                    {
                        var sessionName = fi.Name;
                        DelFromKittyPortableConfig(AppPathHelper.Instance.KittyDirPath, sessionName);
                        DelFromKittyRegistryTable(sessionName);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Warning(e);
                    }
                }
            }
        }
    }
}