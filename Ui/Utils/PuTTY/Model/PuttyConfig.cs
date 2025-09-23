using _1RM.Service;
using Microsoft.Win32;
using Shawn.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace _1RM.Utils.PuTTY.Model
{
    public class PuttyConfig
    {
        public readonly List<PuttyConfigKeyValuePair> Options = new List<PuttyConfigKeyValuePair>();
        public readonly string PuttySessionId;
        public readonly string SessionId;

        /// <summary>
        /// read existed config files.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<PuttyConfigKeyValuePair> Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new List<PuttyConfigKeyValuePair>();
            }

            var lines = File.ReadAllLines(path);
            var ret = new List<PuttyConfigKeyValuePair>(lines.Length);
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
                        ret.Add(new PuttyConfigKeyValuePair() { Key = para, Value = val, ValueKind = RegistryValueKind.DWord });
                    }
                    else
                    {
                        ret.Add(new PuttyConfigKeyValuePair() { Key = para, Value = val, ValueKind = RegistryValueKind.String });
                    }
                }
            }
            return ret;
        }

        public PuttyConfig(string sessionId)
        {
            PuttySessionId = SessionId = sessionId;
            if (!PuttySessionId.StartsWith($"{Assert.APP_NAME}_"))
            {
                throw new NotSupportedException($"A wrong session id is generated: {sessionId}");
            }
            InitDefault();

            // DISABLED ALT + F4
            if (Options.Any(x => string.Equals(x.Key, EnumConfigKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase)))
            {
                var oldItem = Options.First(x => string.Equals(x.Key, EnumConfigKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase));
                oldItem.Value = 0;
            }
            else
                Set(EnumConfigKey.AltF4, 0x00000000); // DISABLED ALTF4
        }

        /// <summary>
        /// Apply the overwrite session to current session.
        /// user can choose an ini session file to overwrite current session.
        /// </summary>
        /// <param name="overwritePath"></param>
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

            Set(EnumConfigKey.TerminalType, "xterm");
            Set(EnumConfigKey.TerminalSpeed, "38400,38400");
            Set(EnumConfigKey.TerminalModes, "INTR=A,QUIT=A,ERASE=A,KILL=A,EOF=A,EOL=A,EOL2=A,START=A,STOP=A,SUSP=A,DSUSP=A,REPRINT=A,WERASE=A,LNEXT=A,FLUSH=A,SWTCH=A,STATUS=A,DISCARD=A,IGNPAR=A,PARMRK=A,INPCK=A,ISTRIP=A,INLCR=A,IGNCR=A,ICRNL=A,IUCLC=A,IXON=A,IXANY=A,IXOFF=A,IMAXBEL=A,ISIG=A,ICANON=A,XCASE=A,ECHO=A,ECHOE=A,ECHOK=A,ECHONL=A,NOFLSH=A,TOSTOP=A,IEXTEN=A,ECHOCTL=A,ECHOKE=A,PENDIN=A,OPOST=A,OLCUC=A,ONLCR=A,OCRNL=A,ONOCR=A,ONLRET=A,CS7=A,CS8=A,PARENB=A,PARODD=A,");
            Set(EnumConfigKey.ProxyExcludeList, "");
            Set(EnumConfigKey.ProxyHost, "proxy");
            Set(EnumConfigKey.ProxyUsername, "");
            Set(EnumConfigKey.ProxyPassword, "");
            Set(EnumConfigKey.ProxyTelnetCommand, "connect%20%25host%20%25port%5Cn");
            Set(EnumConfigKey.Environment, "");
            Set(EnumConfigKey.UserName, "");
            Set(EnumConfigKey.LocalUserName, "");
            Set(EnumConfigKey.Cipher, "aes,chacha20,3des,WARN,des,blowfish,arcfour");
            Set(EnumConfigKey.KEX, "ecdh,dh-gex-sha1,dh-group14-sha1,rsa,WARN,dh-group1-sha1");
            Set(EnumConfigKey.RekeyBytes, "1G");
            Set(EnumConfigKey.GSSLibs, "gssapi32,sspi,custom");
            Set(EnumConfigKey.GSSCustom, "");
            Set(EnumConfigKey.LogHost, "");
            Set(EnumConfigKey.PublicKeyFile, "");
            Set(EnumConfigKey.RemoteCommand, "");
            Set(EnumConfigKey.Answerback, "PuTTY");
            Set(EnumConfigKey.BellWaveFile, "");
            Set(EnumConfigKey.WinTitle, "");
            Set(EnumConfigKey.Colour0, "187,187,187");
            Set(EnumConfigKey.Colour1, "255,255,255");
            Set(EnumConfigKey.Colour2, "0,0,0");
            Set(EnumConfigKey.Colour3, "85,85,85");
            Set(EnumConfigKey.Colour4, "0,0,0");
            Set(EnumConfigKey.Colour5, "0,255,0");
            Set(EnumConfigKey.Colour6, "0,0,0");
            Set(EnumConfigKey.Colour7, "85,85,85");
            Set(EnumConfigKey.Colour8, "187,0,0");
            Set(EnumConfigKey.Colour9, "255,85,85");
            Set(EnumConfigKey.Colour10, "0,187,0");
            Set(EnumConfigKey.Colour11, "85,255,85");
            Set(EnumConfigKey.Colour12, "187,187,0");
            Set(EnumConfigKey.Colour13, "255,255,85");
            Set(EnumConfigKey.Colour14, "0,0,187");
            Set(EnumConfigKey.Colour15, "85,85,255");
            Set(EnumConfigKey.Colour16, "187,0,187");
            Set(EnumConfigKey.Colour17, "255,85,255");
            Set(EnumConfigKey.Colour18, "0,187,187");
            Set(EnumConfigKey.Colour19, "85,255,255");
            Set(EnumConfigKey.Colour20, "187,187,187");
            Set(EnumConfigKey.Colour21, "255,255,255");
            Set(EnumConfigKey.Wordness0, "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
            Set(EnumConfigKey.Wordness32, "0,1,2,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1");
            Set(EnumConfigKey.Wordness64, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,2");
            Set(EnumConfigKey.Wordness96, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1");
            Set(EnumConfigKey.Wordness128, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1");
            Set(EnumConfigKey.Wordness160, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1");
            Set(EnumConfigKey.Wordness192, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2");
            Set(EnumConfigKey.Wordness224, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2");
            Set(EnumConfigKey.LineCodePage, "");
            Set(EnumConfigKey.Printer, "");
            Set(EnumConfigKey.X11Display, "");
            Set(EnumConfigKey.X11AuthFile, "");
            Set(EnumConfigKey.PortForwardings, "");
            Set(EnumConfigKey.BoldFont, "");
            Set(EnumConfigKey.WideFont, "");
            Set(EnumConfigKey.WideBoldFont, "");
            Set(EnumConfigKey.SerialLine, "COM1");
            Set(EnumConfigKey.WindowClass, "");
            Set(EnumConfigKey.Present, 0x00000001);
            Set(EnumConfigKey.LogType, 0x00000000);
            Set(EnumConfigKey.LogFlush, 0x00000001);
            Set(EnumConfigKey.SSHLogOmitPasswords, 0x00000001);
            Set(EnumConfigKey.SSHLogOmitData, 0x00000000);
            Set(EnumConfigKey.PortNumber, 0x00000016);
            Set(EnumConfigKey.CloseOnExit, 0x00000001);
            Set(EnumConfigKey.WarnOnClose, 0x00000000);
            Set(EnumConfigKey.PingInterval, 0x00000000);
            Set(EnumConfigKey.PingIntervalSecs, 0x0000003c);
            Set(EnumConfigKey.TCPNoDelay, 0x00000001);
            Set(EnumConfigKey.TCPKeepalives, 0x0000001E); // seconds between keepalives
            Set(EnumConfigKey.AddressFamily, 0x00000000);
            Set(EnumConfigKey.ProxyDNS, 0x00000001);
            Set(EnumConfigKey.ProxyLocalhost, 0x00000000);
            Set(EnumConfigKey.ProxyMethod, 0x00000000);
            Set(EnumConfigKey.ProxyPort, 0x00000050);
            Set(EnumConfigKey.UserNameFromEnvironment, 0x00000000);
            Set(EnumConfigKey.NoPTY, 0x00000000);
            Set(EnumConfigKey.Compression, 0x00000001);
            Set(EnumConfigKey.TryAgent, 0x00000001);
            Set(EnumConfigKey.AgentFwd, 0x00000000);
            Set(EnumConfigKey.GssapiFwd, 0x00000000);
            Set(EnumConfigKey.ChangeUsername, 0x00000000);
            Set(EnumConfigKey.RekeyTime, 0x0000003c);
            Set(EnumConfigKey.SshNoAuth, 0x00000000);
            Set(EnumConfigKey.SshBanner, 0x00000001);
            Set(EnumConfigKey.AuthTIS, 0x00000000);
            Set(EnumConfigKey.AuthKI, 0x00000001);
            Set(EnumConfigKey.AuthGSSAPI, 0x00000001);
            Set(EnumConfigKey.SshNoShell, 0x00000000);
            Set(EnumConfigKey.SshProt, 0x00000002);
            Set(EnumConfigKey.SSH2DES, 0x00000000);
            Set(EnumConfigKey.RFCEnviron, 0x00000000);
            Set(EnumConfigKey.PassiveTelnet, 0x00000000);
            Set(EnumConfigKey.BackspaceIsDelete, 0x00000001);
            Set(EnumConfigKey.RXVTHomeEnd, 0x00000000);
            Set(EnumConfigKey.LinuxFunctionKeys, 0x00000002);
            Set(EnumConfigKey.NoApplicationKeys, 0x00000000);
            Set(EnumConfigKey.NoApplicationCursors, 0x00000000);
            Set(EnumConfigKey.NoMouseReporting, 0x00000000);
            Set(EnumConfigKey.NoRemoteResize, 0x00000001);
            Set(EnumConfigKey.NoAltScreen, 0x00000000);
            Set(EnumConfigKey.NoRemoteWinTitle, 0x00000000);
            Set(EnumConfigKey.RemoteQTitleAction, 0x00000001);
            Set(EnumConfigKey.NoDBackspace, 0x00000000);
            Set(EnumConfigKey.NoRemoteCharset, 0x00000000);
            Set(EnumConfigKey.ApplicationCursorKeys, 0x00000000);
            Set(EnumConfigKey.ApplicationKeypad, 0x00000000);
            Set(EnumConfigKey.NetHackKeypad, 0x00000000);
            Set(EnumConfigKey.AltSpace, 0x00000000);
            Set(EnumConfigKey.AltOnly, 0x00000000);
            Set(EnumConfigKey.ComposeKey, 0x00000000);
            Set(EnumConfigKey.CtrlAltKeys, 0x00000001);
            Set(EnumConfigKey.TelnetKey, 0x00000000);
            Set(EnumConfigKey.TelnetRet, 0x00000001);
            Set(EnumConfigKey.LocalEcho, 0x00000002);
            Set(EnumConfigKey.LocalEdit, 0x00000002);
            Set(EnumConfigKey.AlwaysOnTop, 0x00000000);
            Set(EnumConfigKey.FullScreenOnAltEnter, 0x00000000);
            Set(EnumConfigKey.HideMousePtr, 0x00000000);
            Set(EnumConfigKey.SunkenEdge, 0x00000000);
            Set(EnumConfigKey.WindowBorder, 0x00000000);
            Set(EnumConfigKey.CurType, 0x00000000);
            Set(EnumConfigKey.BlinkCur, 0x00000000);
            Set(EnumConfigKey.Beep, 0x00000001);
            Set(EnumConfigKey.BeepInd, 0x00000000);
            Set(EnumConfigKey.BellOverload, 0x00000001);
            Set(EnumConfigKey.BellOverloadN, 0x00000005);
            Set(EnumConfigKey.BellOverloadT, 0x000007d0);
            Set(EnumConfigKey.BellOverloadS, 0x00001388);
            Set(EnumConfigKey.ScrollbackLines, 0x00002000);
            Set(EnumConfigKey.DECOriginMode, 0x00000000);
            Set(EnumConfigKey.AutoWrapMode, 0x00000001);
            Set(EnumConfigKey.LFImpliesCR, 0x00000000);
            Set(EnumConfigKey.CRImpliesLF, 0x00000000);
            Set(EnumConfigKey.DisableArabicShaping, 0x00000000);
            Set(EnumConfigKey.DisableBidi, 0x00000000);
            Set(EnumConfigKey.WinNameAlways, 0x00000001);
            Set(EnumConfigKey.TermWidth, 0x00000050);
            Set(EnumConfigKey.TermHeight, 0x00000018);
            Set(EnumConfigKey.FontIsBold, 0x00000000);
            Set(EnumConfigKey.FontCharSet, 0x00000000);
            Set(EnumConfigKey.Font, "Consolas");
            Set(EnumConfigKey.FontHeight, 12);
            Set(EnumConfigKey.FontCharSet, 0x00000000);
            Set(EnumConfigKey.FontQuality, 0x00000000);
            Set(EnumConfigKey.FontVTMode, 0x00000004);
            Set(EnumConfigKey.UseSystemColours, 0x00000000);
            Set(EnumConfigKey.TryPalette, 0x00000000);
            Set(EnumConfigKey.ANSIColour, 0x00000001);
            Set(EnumConfigKey.Xterm256Colour, 0x00000001);
            Set(EnumConfigKey.BoldAsColour, 0x00000001);
            Set(EnumConfigKey.RawCNP, 0x00000000);
            Set(EnumConfigKey.PasteRTF, 0x00000000);
            Set(EnumConfigKey.MouseIsXterm, 0x00000000);
            Set(EnumConfigKey.MouseOverride, 0x00000001);
            Set(EnumConfigKey.RectSelect, 0x00000000);
            Set(EnumConfigKey.CJKAmbigWide, 0x00000000);
            Set(EnumConfigKey.UTF8Override, 0x00000001);
            Set(EnumConfigKey.CapsLockCyr, 0x00000000);
            Set(EnumConfigKey.ScrollBar, 0x00000001); // ScrollBar在kitty终端中的作用是启用或禁用滚动条
            Set(EnumConfigKey.ScrollBarFullScreen, 0x00000001);
            Set(EnumConfigKey.ScrollOnKey, 0x00000000);
            Set(EnumConfigKey.ScrollOnDisp, 0x00000f001);
            Set(EnumConfigKey.ScrollbarOnLeft, 0x00000000);
            Set(EnumConfigKey.EraseToScrollback, 0x00000001);
            Set(EnumConfigKey.LockSize, 0x00000000);
            Set(EnumConfigKey.BCE, 0x00000001);
            Set(EnumConfigKey.BlinkText, 0x00000000);
            Set(EnumConfigKey.X11Forward, 0x00000000);
            Set(EnumConfigKey.X11AuthType, 0x00000001);
            Set(EnumConfigKey.LocalPortAcceptAll, 0x00000000);
            Set(EnumConfigKey.RemotePortAcceptAll, 0x00000000);
            Set(EnumConfigKey.BugIgnore1, 0x00000000);
            Set(EnumConfigKey.BugIgnore2, 0x00000000);
            Set(EnumConfigKey.BugPlainPW1, 0x00000000);
            Set(EnumConfigKey.BugRSA1, 0x00000000);
            Set(EnumConfigKey.BugHMAC2, 0x00000000);
            Set(EnumConfigKey.BugDeriveKey2, 0x00000000);
            Set(EnumConfigKey.BugRSAPad2, 0x00000000);
            Set(EnumConfigKey.BugPKSessID2, 0x00000000);
            Set(EnumConfigKey.BugRekey2, 0x00000000);
            Set(EnumConfigKey.BugMaxPkt2, 0x00000000);
            Set(EnumConfigKey.StampUtmp, 0x00000001);
            Set(EnumConfigKey.LoginShell, 0x00000001);
            Set(EnumConfigKey.ShadowBold, 0x00000000);
            Set(EnumConfigKey.ShadowBoldOffset, 0x00000001);
            Set(EnumConfigKey.SerialSpeed, 0x00002580);
            Set(EnumConfigKey.SerialDataBits, 0x00000008);
            Set(EnumConfigKey.SerialStopHalfbits, 0x00000002);
            Set(EnumConfigKey.SerialParity, 0x00000000);
            Set(EnumConfigKey.SerialFlowControl, 0x00000001);

            #endregion Default
        }

        public void Set(EnumConfigKey key, int value)
        {
            Set(key, value.ToString());
        }

        public void Set(EnumConfigKey key, string value)
        {
            if (Options.Any(x => x.Key == key.ToString()))
            {
                var item = Options.First(x => x.Key == key.ToString());
                item.Value = value;
            }
            else
            {
                Options.Add(PuttyConfigKeyValuePair.Create(key.ToString(), value));
            }
        }

        /// <summary>
        /// del from reg table
        /// </summary>
        private static void DelFromPuttyRegistryTable(string sessionId)
        {
            string regPath = $"Software\\SimonTatham\\PuTTY\\Sessions\\{sessionId}";
            try
            {
                SimpleLogHelper.Debug($"Deleting configs from {regPath}");
                Registry.CurrentUser.DeleteSubKeyTree(regPath);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        public void SaveToConfig()
        {
            // save to reg table
            string regPath = $"Software\\SimonTatham\\PuTTY\\Sessions\\{PuttySessionId}";
            SimpleLogHelper.Debug($"Saving configs to registry: {regPath}");
            using var regKey = Registry.CurrentUser.CreateSubKey(regPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (regKey != null)
            {
                foreach (var item in Options)
                {
                    try
                    {
                        regKey.SetValue(item.Key, item.Value, item.ValueKind);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e, $"Putty config error: can't set up key(value)=> {item.Key}({item.ValueKind})");
                    }
                }
            }
            else
            {
                SimpleLogHelper.Error($"Failed to create registry key for {regPath}");
            }
        }

        public void DeleteFromConfig()
        {
            DelFromPuttyRegistryTable(PuttySessionId);
        }


        public static void CleanUpOldConfig()
        {
            using var key = Registry.CurrentUser.OpenSubKey($"Software\\SimonTatham\\PuTTY\\Sessions", true);
            if (key == null) return;
            var subKeyNames = key.GetSubKeyNames();
            foreach (var sessionId in subKeyNames)
            {
                try
                {
                    if (sessionId.StartsWith($"{Assert.APP_NAME}_"))
                    {
                        DelFromPuttyRegistryTable(sessionId);
                    }
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Warning(e);
                }
            }
        }




        public static string GetInternalPuttyExeFullName()
        {
            string kittyExeName = $"putty_portable_{Assert.APP_NAME}.exe";
            if (!Directory.Exists(AppPathHelper.Instance.PuttyDirPath))
                Directory.CreateDirectory(AppPathHelper.Instance.PuttyDirPath);
            var kittyExeFullName = Path.Combine(AppPathHelper.Instance.PuttyDirPath, kittyExeName);
            return kittyExeFullName;
        }
    }
}