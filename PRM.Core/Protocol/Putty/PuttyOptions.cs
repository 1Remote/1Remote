using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Shawn.Utils;

namespace PRM.Core.Protocol.Putty
{
    public class PuttyOptions
    {
        public readonly List<PuttyOptionItem> Options = new List<PuttyOptionItem>();
        public readonly string SessionName;

        public PuttyOptions(string sessionName, string overwritePath = null)
        {
            SessionName = sessionName;
            InitDefault();
            if (!string.IsNullOrEmpty(overwritePath))
            {
                var overWrite = KittyPortableSessionConfigReader.Read(overwritePath);
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
            if (Options.Any(x => String.Equals(x.Key, EnumKittyOptionKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase)))
            {
                var oldItem = Options.First(x => string.Equals(x.Key, EnumKittyOptionKey.AltF4.ToString(), StringComparison.CurrentCultureIgnoreCase));
                oldItem.Value = 0;
            }
            else
                Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AltF4.ToString(), 0x00000000)); // DISABLED ALTF4
        }

        private static string GetDefaultIni()
        {
            var uri = new Uri("pack://application:,,,/PRM.Core;component/Protocol/Putty/KittyDefaultOptons.ini", UriKind.Absolute);
            var s = Application.GetResourceStream(uri).Stream;
            byte[] bytes = new byte[s.Length];
            s.Read(bytes, 0, (int)s.Length);
            var txt = Encoding.UTF8.GetString(bytes);
            return txt;
        }

        private void InitDefault()
        {
            Options.Clear();

            #region Default

            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TerminalType.ToString(), "xterm"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TerminalSpeed.ToString(), "38400,38400"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TerminalModes.ToString(), "INTR=A,QUIT=A,ERASE=A,KILL=A,EOF=A,EOL=A,EOL2=A,START=A,STOP=A,SUSP=A,DSUSP=A,REPRINT=A,WERASE=A,LNEXT=A,FLUSH=A,SWTCH=A,STATUS=A,DISCARD=A,IGNPAR=A,PARMRK=A,INPCK=A,ISTRIP=A,INLCR=A,IGNCR=A,ICRNL=A,IUCLC=A,IXON=A,IXANY=A,IXOFF=A,IMAXBEL=A,ISIG=A,ICANON=A,XCASE=A,ECHO=A,ECHOE=A,ECHOK=A,ECHONL=A,NOFLSH=A,TOSTOP=A,IEXTEN=A,ECHOCTL=A,ECHOKE=A,PENDIN=A,OPOST=A,OLCUC=A,ONLCR=A,OCRNL=A,ONOCR=A,ONLRET=A,CS7=A,CS8=A,PARENB=A,PARODD=A,"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyExcludeList.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyHost.ToString(), "proxy"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyUsername.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyPassword.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyTelnetCommand.ToString(), "connect %host %port\\n"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Environment.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.UserName.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LocalUserName.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Cipher.ToString(), "aes,blowfish,3des,WARN,arcfour,des"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.KEX.ToString(), "dh-gex-sha1,dh-group14-sha1,dh-group1-sha1,rsa,WARN"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RekeyBytes.ToString(), "1G"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.GSSLibs.ToString(), "gssapi32,sspi,custom"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.GSSCustom.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LogHost.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.PublicKeyFile.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RemoteCommand.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Answerback.ToString(), "PuTTY"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BellWaveFile.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.WinTitle.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour0.ToString(), "187,187,187"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour1.ToString(), "255,255,255"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour2.ToString(), "0,0,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour3.ToString(), "85,85,85"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour4.ToString(), "0,0,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour5.ToString(), "0,255,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour6.ToString(), "0,0,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour7.ToString(), "85,85,85"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour8.ToString(), "187,0,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour9.ToString(), "255,85,85"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour10.ToString(), "0,187,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour11.ToString(), "85,255,85"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour12.ToString(), "187,187,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour13.ToString(), "255,255,85"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour14.ToString(), "0,0,187"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour15.ToString(), "85,85,255"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour16.ToString(), "187,0,187"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour17.ToString(), "255,85,255"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour18.ToString(), "0,187,187"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour19.ToString(), "85,255,255"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour20.ToString(), "187,187,187"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Colour21.ToString(), "255,255,255"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness0.ToString(), "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness32.ToString(), "0,1,2,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness64.ToString(), "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,2"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness96.ToString(), "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness128.ToString(), "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness160.ToString(), "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness192.ToString(), "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Wordness224.ToString(), "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LineCodePage.ToString(), "UTF-8"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Printer.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.X11Display.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.X11AuthFile.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.PortForwardings.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BoldFont.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.WideFont.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.WideBoldFont.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SerialLine.ToString(), "COM1"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.WindowClass.ToString(), ""));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Present.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LogType.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LogFlush.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SSHLogOmitPasswords.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SSHLogOmitData.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.PortNumber.ToString(), 0x00000016));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.CloseOnExit.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.WarnOnClose.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.PingInterval.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.PingIntervalSecs.ToString(), 0x0000003c));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TCPNoDelay.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TCPKeepalives.ToString(), 0x0000001E)); // seconds between keepalives
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AddressFamily.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyDNS.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyLocalhost.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyMethod.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ProxyPort.ToString(), 0x00000050));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.UserNameFromEnvironment.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoPTY.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Compression.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TryAgent.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AgentFwd.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.GssapiFwd.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ChangeUsername.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RekeyTime.ToString(), 0x0000003c));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SshNoAuth.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SshBanner.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AuthTIS.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AuthKI.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AuthGSSAPI.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SshNoShell.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SshProt.ToString(), 0x00000002));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SSH2DES.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RFCEnviron.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.PassiveTelnet.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BackspaceIsDelete.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RXVTHomeEnd.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LinuxFunctionKeys.ToString(), 0x00000002));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoApplicationKeys.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoApplicationCursors.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoMouseReporting.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoRemoteResize.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoAltScreen.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoRemoteWinTitle.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RemoteQTitleAction.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoDBackspace.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NoRemoteCharset.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ApplicationCursorKeys.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ApplicationKeypad.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.NetHackKeypad.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AltSpace.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AltOnly.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ComposeKey.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.CtrlAltKeys.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TelnetKey.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TelnetRet.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LocalEcho.ToString(), 0x00000002));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LocalEdit.ToString(), 0x00000002));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AlwaysOnTop.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.FullScreenOnAltEnter.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.HideMousePtr.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SunkenEdge.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.WindowBorder.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.CurType.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BlinkCur.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Beep.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BeepInd.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BellOverload.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BellOverloadN.ToString(), 0x00000005));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BellOverloadT.ToString(), 0x000007d0));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BellOverloadS.ToString(), 0x00001388));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ScrollbackLines.ToString(), 0x00002000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.DECOriginMode.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.AutoWrapMode.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LFImpliesCR.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.CRImpliesLF.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.DisableArabicShaping.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.DisableBidi.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.WinNameAlways.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TermWidth.ToString(), 0x00000050));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TermHeight.ToString(), 0x00000018));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.FontIsBold.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.FontCharSet.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Font.ToString(), "Consolas"));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.FontHeight.ToString(), 12));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.FontCharSet.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.FontQuality.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.FontVTMode.ToString(), 0x00000004));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.UseSystemColours.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.TryPalette.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ANSIColour.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Xterm256Colour.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BoldAsColour.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RawCNP.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.PasteRTF.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.MouseIsXterm.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.MouseOverride.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RectSelect.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.CJKAmbigWide.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.UTF8Override.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.CapsLockCyr.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ScrollBar.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ScrollBarFullScreen.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ScrollOnKey.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ScrollOnDisp.ToString(), 0x00000f001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.EraseToScrollback.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LockSize.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BCE.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BlinkText.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.X11Forward.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.X11AuthType.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LocalPortAcceptAll.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.RemotePortAcceptAll.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugIgnore1.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugPlainPW1.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugRSA1.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugIgnore2.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugHMAC2.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugDeriveKey2.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugRSAPad2.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugPKSessID2.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugRekey2.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.BugMaxPkt2.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.StampUtmp.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.LoginShell.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ScrollbarOnLeft.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ShadowBold.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.ShadowBoldOffset.ToString(), 0x00000001));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SerialSpeed.ToString(), 0x00002580));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SerialDataBits.ToString(), 0x00000008));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SerialStopHalfbits.ToString(), 0x00000002));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SerialParity.ToString(), 0x00000000));
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.SerialFlowControl.ToString(), 0x00000001));
#if UseKiTTY
            Options.Add(PuttyOptionItem.Create(EnumKittyOptionKey.Autocommand.ToString(), ""));
#endif

            #endregion Default
        }

        public void Set(EnumKittyOptionKey key, int value)
        {
            Set(key, value.ToString());
        }

        public void Set(EnumKittyOptionKey key, string value)
        {
            if (Options.Any(x => x.Key == key.ToString()))
            {
                var item = Options.First(x => x.Key == key.ToString());
                item.Value = value;
            }
            else
            {
                Options.Add(PuttyOptionItem.Create(key.ToString(), value));
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

        /// <summary>
        /// del from reg table
        /// </summary>
        private void DelFromKittyRegistryTable()
        {
            string regPath = $"Software\\9bis.com\\KiTTY\\Sessions\\{SessionName}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(regPath);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void SaveToKittyPortableConfig(string kittyPath)
        {
            try
            {
                string configPath = Path.Combine(kittyPath, "Sessions", SessionName.Replace(" ", "%20"));
                var sb = new StringBuilder();
                foreach (var item in Options)
                {
                    if (item.Value != null)
                        sb.AppendLine($@"{item.Key}\{item.Value}\");
                }
                File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
        }

        private void DelFromKittyPortableConfig(string kittyPath)
        {
            try
            {
                string configPath = Path.Combine(kittyPath, "Sessions", SessionName.Replace(" ", "%20"));
                if (File.Exists(configPath))
                    File.Delete(configPath);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
        }

        public void SaveToKittyConfig(string kittyPath)
        {
            SaveToKittyPortableConfig(kittyPath);
            SaveToKittyRegistryTable();
        }

        public void DelFromKittyConfig(string kittyPath)
        {
            DelFromKittyPortableConfig(kittyPath);
            DelFromKittyRegistryTable();
        }
    }
}