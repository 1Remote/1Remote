using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _1RM.Service;
using Microsoft.Win32;
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

        public KittyConfig(string sessionName, string? overwritePath = null)
        {
            SessionName = sessionName;
            InitDefault();
            if (!string.IsNullOrEmpty(overwritePath))
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

            // DISABLED ALT + F4
            if (Options.Any(x => string.Equals(x.Key, EnumKittyConfigKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase)))
            {
                var oldItem = Options.First(x => string.Equals(x.Key, EnumKittyConfigKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase));
                oldItem.Value = 0;
            }
            else
                Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AltF4.ToString(), 0x00000000)); // DISABLED ALTF4
        }

        private void InitDefault()
        {
            Options.Clear();

            #region Default

            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TerminalType.ToString(), "xterm"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TerminalSpeed.ToString(), "38400,38400"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TerminalModes.ToString(), "INTR=A,QUIT=A,ERASE=A,KILL=A,EOF=A,EOL=A,EOL2=A,START=A,STOP=A,SUSP=A,DSUSP=A,REPRINT=A,WERASE=A,LNEXT=A,FLUSH=A,SWTCH=A,STATUS=A,DISCARD=A,IGNPAR=A,PARMRK=A,INPCK=A,ISTRIP=A,INLCR=A,IGNCR=A,ICRNL=A,IUCLC=A,IXON=A,IXANY=A,IXOFF=A,IMAXBEL=A,ISIG=A,ICANON=A,XCASE=A,ECHO=A,ECHOE=A,ECHOK=A,ECHONL=A,NOFLSH=A,TOSTOP=A,IEXTEN=A,ECHOCTL=A,ECHOKE=A,PENDIN=A,OPOST=A,OLCUC=A,ONLCR=A,OCRNL=A,ONOCR=A,ONLRET=A,CS7=A,CS8=A,PARENB=A,PARODD=A,"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyExcludeList.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyHost.ToString(), "proxy"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyUsername.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyPassword.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyTelnetCommand.ToString(), "connect %host %port\\n"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Environment.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.UserName.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LocalUserName.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Cipher.ToString(), "aes,blowfish,3des,WARN,arcfour,des"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.KEX.ToString(), "dh-gex-sha1,dh-group14-sha1,dh-group1-sha1,rsa,WARN"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RekeyBytes.ToString(), "1G"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.GSSLibs.ToString(), "gssapi32,sspi,custom"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.GSSCustom.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LogHost.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.PublicKeyFile.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RemoteCommand.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Answerback.ToString(), "PuTTY"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BellWaveFile.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.WinTitle.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour0.ToString(), "187,187,187"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour1.ToString(), "255,255,255"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour2.ToString(), "0,0,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour3.ToString(), "85,85,85"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour4.ToString(), "0,0,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour5.ToString(), "0,255,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour6.ToString(), "0,0,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour7.ToString(), "85,85,85"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour8.ToString(), "187,0,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour9.ToString(), "255,85,85"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour10.ToString(), "0,187,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour11.ToString(), "85,255,85"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour12.ToString(), "187,187,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour13.ToString(), "255,255,85"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour14.ToString(), "0,0,187"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour15.ToString(), "85,85,255"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour16.ToString(), "187,0,187"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour17.ToString(), "255,85,255"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour18.ToString(), "0,187,187"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour19.ToString(), "85,255,255"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour20.ToString(), "187,187,187"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Colour21.ToString(), "255,255,255"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness0.ToString(), "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness32.ToString(), "0,1,2,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness64.ToString(), "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,2"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness96.ToString(), "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness128.ToString(), "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness160.ToString(), "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness192.ToString(), "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Wordness224.ToString(), "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LineCodePage.ToString(), "UTF-8"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Printer.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.X11Display.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.X11AuthFile.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.PortForwardings.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BoldFont.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.WideFont.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.WideBoldFont.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SerialLine.ToString(), "COM1"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.WindowClass.ToString(), ""));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Present.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LogType.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LogFlush.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SSHLogOmitPasswords.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SSHLogOmitData.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.PortNumber.ToString(), 0x00000016));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.CloseOnExit.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.WarnOnClose.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.PingInterval.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.PingIntervalSecs.ToString(), 0x0000003c));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TCPNoDelay.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TCPKeepalives.ToString(), 0x0000001E)); // seconds between keepalives
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AddressFamily.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyDNS.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyLocalhost.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyMethod.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ProxyPort.ToString(), 0x00000050));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.UserNameFromEnvironment.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoPTY.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Compression.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TryAgent.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AgentFwd.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.GssapiFwd.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ChangeUsername.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RekeyTime.ToString(), 0x0000003c));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SshNoAuth.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SshBanner.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AuthTIS.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AuthKI.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AuthGSSAPI.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SshNoShell.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SshProt.ToString(), 0x00000002));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SSH2DES.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RFCEnviron.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.PassiveTelnet.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BackspaceIsDelete.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RXVTHomeEnd.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LinuxFunctionKeys.ToString(), 0x00000002));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoApplicationKeys.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoApplicationCursors.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoMouseReporting.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoRemoteResize.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoAltScreen.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoRemoteWinTitle.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RemoteQTitleAction.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoDBackspace.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NoRemoteCharset.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ApplicationCursorKeys.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ApplicationKeypad.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.NetHackKeypad.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AltSpace.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AltOnly.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ComposeKey.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.CtrlAltKeys.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TelnetKey.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TelnetRet.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LocalEcho.ToString(), 0x00000002));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LocalEdit.ToString(), 0x00000002));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AlwaysOnTop.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.FullScreenOnAltEnter.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.HideMousePtr.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SunkenEdge.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.WindowBorder.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.CurType.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BlinkCur.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Beep.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BeepInd.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BellOverload.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BellOverloadN.ToString(), 0x00000005));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BellOverloadT.ToString(), 0x000007d0));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BellOverloadS.ToString(), 0x00001388));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ScrollbackLines.ToString(), 0x00002000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.DECOriginMode.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.AutoWrapMode.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LFImpliesCR.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.CRImpliesLF.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.DisableArabicShaping.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.DisableBidi.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.WinNameAlways.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TermWidth.ToString(), 0x00000050));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TermHeight.ToString(), 0x00000018));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.FontIsBold.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.FontCharSet.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Font.ToString(), "Consolas"));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.FontHeight.ToString(), 12));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.FontCharSet.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.FontQuality.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.FontVTMode.ToString(), 0x00000004));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.UseSystemColours.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.TryPalette.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ANSIColour.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Xterm256Colour.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BoldAsColour.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RawCNP.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.PasteRTF.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.MouseIsXterm.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.MouseOverride.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RectSelect.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.CJKAmbigWide.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.UTF8Override.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.CapsLockCyr.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ScrollBar.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ScrollBarFullScreen.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ScrollOnKey.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ScrollOnDisp.ToString(), 0x00000f001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.EraseToScrollback.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LockSize.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BCE.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BlinkText.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.X11Forward.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.X11AuthType.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LocalPortAcceptAll.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.RemotePortAcceptAll.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugIgnore1.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugPlainPW1.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugRSA1.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugIgnore2.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugHMAC2.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugDeriveKey2.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugRSAPad2.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugPKSessID2.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugRekey2.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.BugMaxPkt2.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.StampUtmp.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.LoginShell.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ScrollbarOnLeft.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ShadowBold.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.ShadowBoldOffset.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SerialSpeed.ToString(), 0x00002580));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SerialDataBits.ToString(), 0x00000008));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SerialStopHalfbits.ToString(), 0x00000002));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SerialParity.ToString(), 0x00000000));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.SerialFlowControl.ToString(), 0x00000001));
            Options.Add(KittyConfigKeyValuePair.Create(EnumKittyConfigKey.Autocommand.ToString(), ""));

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
                File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
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