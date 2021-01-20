using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Shawn.Utils;

namespace PRM.Core.Protocol.Putty
{
    public class PuttyOptionItem
    {
        private PuttyOptionItem() { }
        public static PuttyOptionItem Create(PuttyOptionKey key, int value)
        {
            return new PuttyOptionItem
            {
                Key = key.ToString(),
                Value = value,
                ValueKind = RegistryValueKind.DWord,
            };
        }
        public static PuttyOptionItem Create(PuttyOptionKey key, string value)
        {
            return new PuttyOptionItem
            {
                Key = key.ToString(),
                Value = value,
                ValueKind = RegistryValueKind.String,
            };
        }
        public string Key;
        public object Value;
        public RegistryValueKind ValueKind;
    }

    public enum PuttyOptionKey
    {
        #region Enum
        TerminalType,
        TerminalSpeed,
        TerminalModes,
        ProxyExcludeList,
        ProxyHost,
        ProxyUsername,
        ProxyPassword,
        ProxyTelnetCommand,
        Environment,
        UserName,
        LocalUserName,
        Cipher,
        KEX,
        RekeyBytes,
        GSSLibs,
        GSSCustom,
        LogHost,
        PublicKeyFile,
        RemoteCommand,
        Answerback,
        BellWaveFile,
        WinTitle,
        /// <summary>
        /// Default Foreground
        /// </summary>
        Colour0,
        /// <summary>
        /// Default Bold Foreground
        /// </summary>
        Colour1,
        /// <summary>
        /// Default Background
        /// </summary>
        Colour2,
        /// <summary>
        /// Default Bold Background
        /// </summary>
        Colour3,
        /// <summary>
        /// Cursor Text
        /// </summary>
        Colour4,
        /// <summary>
        /// Cursor Color
        /// </summary>
        Colour5,
        /// <summary>
        ///  ANSI Black
        /// </summary>
        Colour6,
        /// <summary>
        /// ANSI Black Bold
        /// </summary>
        Colour7,
        /// <summary>
        /// ANSI Red
        /// </summary>
        Colour8,
        /// <summary>
        /// ANSI Red Bold
        /// </summary>
        Colour9,
        /// <summary>
        /// ANSI Green
        /// </summary>
        Colour10,
        /// <summary>
        /// ANSI Green Bold
        /// </summary>
        Colour11,
        /// <summary>
        /// ANSI Yellow
        /// </summary>
        Colour12,
        /// <summary>
        /// ANSI Yellow Bold
        /// </summary>
        Colour13,
        /// <summary>
        /// ANSI Blue
        /// </summary>
        Colour14,
        /// <summary>
        /// ANSI Blue Bold
        /// </summary>
        Colour15,
        /// <summary>
        /// ANSI Magenta
        /// </summary>
        Colour16,
        /// <summary>
        /// ANSI Magenta Bold
        /// </summary>
        Colour17,
        /// <summary>
        /// ANSI Cyan
        /// </summary>
        Colour18,
        /// <summary>
        /// ANSI Cyan Bold
        /// </summary>
        Colour19,
        /// <summary>
        /// ANSI White
        /// </summary>
        Colour20,
        /// <summary>
        /// ANSI White Bold
        /// </summary>
        Colour21,
        Wordness0,
        Wordness32,
        Wordness64,
        Wordness96,
        Wordness128,
        Wordness160,
        Wordness192,
        Wordness224,
        LineCodePage,
        Printer,
        X11Display,
        X11AuthFile,
        PortForwardings,
        BoldFont,
        WideFont,
        WideBoldFont,
        SerialLine,
        WindowClass,
        Present,
        LogType,
        LogFlush,
        SSHLogOmitPasswords,
        SSHLogOmitData,
        PortNumber,
        CloseOnExit,
        WarnOnClose,
        PingInterval,
        PingIntervalSecs,
        TCPNoDelay,
        TCPKeepalives,
        AddressFamily,
        ProxyDNS,
        ProxyLocalhost,
        ProxyMethod,
        ProxyPort,
        UserNameFromEnvironment,
        NoPTY,
        Compression,
        TryAgent,
        AgentFwd,
        GssapiFwd,
        ChangeUsername,
        RekeyTime,
        SshNoAuth,
        SshBanner,
        AuthTIS,
        AuthKI,
        AuthGSSAPI,
        SshNoShell,
        SshProt,
        SSH2DES,
        RFCEnviron,
        PassiveTelnet,
        BackspaceIsDelete,
        RXVTHomeEnd,
        LinuxFunctionKeys,
        NoApplicationKeys,
        NoApplicationCursors,
        NoMouseReporting,
        NoRemoteResize,
        NoAltScreen,
        NoRemoteWinTitle,
        RemoteQTitleAction,
        NoDBackspace,
        NoRemoteCharset,
        ApplicationCursorKeys,
        ApplicationKeypad,
        NetHackKeypad,
        AltF4,
        AltSpace,
        AltOnly,
        ComposeKey,
        CtrlAltKeys,
        TelnetKey,
        TelnetRet,
        LocalEcho,
        LocalEdit,
        AlwaysOnTop,
        FullScreenOnAltEnter,
        HideMousePtr,
        SunkenEdge,
        WindowBorder,
        CurType,
        BlinkCur,
        Beep,
        BeepInd,
        BellOverload,
        BellOverloadN,
        BellOverloadT,
        BellOverloadS,
        ScrollbackLines,
        DECOriginMode,
        AutoWrapMode,
        LFImpliesCR,
        CRImpliesLF,
        DisableArabicShaping,
        DisableBidi,
        WinNameAlways,
        TermWidth,
        TermHeight,
        FontIsBold,
        FontCharSet,
        Font,
        FontHeight,
        FontQuality,
        FontVTMode,
        UseSystemColours,
        TryPalette,
        ANSIColour,
        Xterm256Colour,
        BoldAsColour,
        RawCNP,
        PasteRTF,
        MouseIsXterm,
        MouseOverride,
        RectSelect,
        CJKAmbigWide,
        UTF8Override,
        CapsLockCyr,
        ScrollBar,
        ScrollBarFullScreen,
        ScrollOnKey,
        ScrollOnDisp,
        EraseToScrollback,
        LockSize,
        BCE,
        BlinkText,
        X11Forward,
        X11AuthType,
        LocalPortAcceptAll,
        RemotePortAcceptAll,
        BugIgnore1,
        BugPlainPW1,
        BugRSA1,
        BugIgnore2,
        BugHMAC2,
        BugDeriveKey2,
        BugRSAPad2,
        BugPKSessID2,
        BugRekey2,
        BugMaxPkt2,
        StampUtmp,
        LoginShell,
        ScrollbarOnLeft,
        ShadowBold,
        ShadowBoldOffset,
        SerialSpeed,
        SerialDataBits,
        SerialStopHalfbits,
        SerialParity,
        SerialFlowControl,
#if UseKiTTY
        /* For KiTTY */
        Autocommand,
        HostName,
        Protocol,
#endif
        #endregion
    }


    public class PuttyOptions
    {
        public readonly List<PuttyOptionItem> Options = new List<PuttyOptionItem>();
        public readonly string SessionName;
        private readonly string PuttyKeyFilePath = "";
        public PuttyOptions(string sessionName, FileInfo puttyKeyFileInfo)
        {
            SessionName = sessionName;
            if (puttyKeyFileInfo?.Exists ?? false)
            {
                PuttyKeyFilePath = puttyKeyFileInfo.FullName;
            }
            InitDefault();
        }
        public PuttyOptions(string sessionName, string puttyKeyString = "")
        {
            SessionName = sessionName;
            if (!string.IsNullOrEmpty(puttyKeyString))
            {
                var tmpPath = Path.GetTempFileName();
                File.WriteAllText(tmpPath, puttyKeyString);
                PuttyKeyFilePath = tmpPath;
            }
            InitDefault();
        }

        private void InitDefault()
        {
            Options.Clear();

            #region Default

            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TerminalType, "xterm"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TerminalSpeed, "38400,38400"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TerminalModes, "INTR=A,QUIT=A,ERASE=A,KILL=A,EOF=A,EOL=A,EOL2=A,START=A,STOP=A,SUSP=A,DSUSP=A,REPRINT=A,WERASE=A,LNEXT=A,FLUSH=A,SWTCH=A,STATUS=A,DISCARD=A,IGNPAR=A,PARMRK=A,INPCK=A,ISTRIP=A,INLCR=A,IGNCR=A,ICRNL=A,IUCLC=A,IXON=A,IXANY=A,IXOFF=A,IMAXBEL=A,ISIG=A,ICANON=A,XCASE=A,ECHO=A,ECHOE=A,ECHOK=A,ECHONL=A,NOFLSH=A,TOSTOP=A,IEXTEN=A,ECHOCTL=A,ECHOKE=A,PENDIN=A,OPOST=A,OLCUC=A,ONLCR=A,OCRNL=A,ONOCR=A,ONLRET=A,CS7=A,CS8=A,PARENB=A,PARODD=A,"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyExcludeList, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyHost, "proxy"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyUsername, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyPassword, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyTelnetCommand, "connect %host %port\\n"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Environment, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.UserName, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LocalUserName, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Cipher, "aes,blowfish,3des,WARN,arcfour,des"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.KEX, "dh-gex-sha1,dh-group14-sha1,dh-group1-sha1,rsa,WARN"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RekeyBytes, "1G"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.GSSLibs, "gssapi32,sspi,custom"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.GSSCustom, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LogHost, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.PublicKeyFile, PuttyKeyFilePath));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RemoteCommand, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Answerback, "PuTTY"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BellWaveFile, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.WinTitle, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour0, "187,187,187"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour1, "255,255,255"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour2, "0,0,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour3, "85,85,85"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour4, "0,0,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour5, "0,255,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour6, "0,0,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour7, "85,85,85"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour8, "187,0,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour9, "255,85,85"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour10, "0,187,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour11, "85,255,85"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour12, "187,187,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour13, "255,255,85"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour14, "0,0,187"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour15, "85,85,255"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour16, "187,0,187"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour17, "255,85,255"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour18, "0,187,187"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour19, "85,255,255"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour20, "187,187,187"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Colour21, "255,255,255"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness0, "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness32, "0,1,2,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness64, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,2"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness96, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness128, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness160, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness192, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Wordness224, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LineCodePage, "UTF-8"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Printer, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.X11Display, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.X11AuthFile, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.PortForwardings, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BoldFont, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.WideFont, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.WideBoldFont, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SerialLine, "COM1"));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.WindowClass, ""));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Present, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LogType, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LogFlush, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SSHLogOmitPasswords, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SSHLogOmitData, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.PortNumber, 0x00000016));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.CloseOnExit, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.WarnOnClose, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.PingInterval, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.PingIntervalSecs, 0x0000003c));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TCPNoDelay, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TCPKeepalives, 0x0000001E)); // seconds between keepalives
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AddressFamily, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyDNS, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyLocalhost, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyMethod, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ProxyPort, 0x00000050));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.UserNameFromEnvironment, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoPTY, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Compression, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TryAgent, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AgentFwd, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.GssapiFwd, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ChangeUsername, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RekeyTime, 0x0000003c));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SshNoAuth, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SshBanner, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AuthTIS, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AuthKI, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AuthGSSAPI, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SshNoShell, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SshProt, 0x00000002));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SSH2DES, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RFCEnviron, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.PassiveTelnet, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BackspaceIsDelete, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RXVTHomeEnd, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LinuxFunctionKeys, 0x00000002));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoApplicationKeys, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoApplicationCursors, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoMouseReporting, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoRemoteResize, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoAltScreen, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoRemoteWinTitle, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RemoteQTitleAction, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoDBackspace, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NoRemoteCharset, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ApplicationCursorKeys, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ApplicationKeypad, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.NetHackKeypad, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AltF4, 0x00000000)); // DISABLED ALTF4
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AltSpace, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AltOnly, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ComposeKey, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.CtrlAltKeys, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TelnetKey, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TelnetRet, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LocalEcho, 0x00000002));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LocalEdit, 0x00000002));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AlwaysOnTop, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.FullScreenOnAltEnter, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.HideMousePtr, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SunkenEdge, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.WindowBorder, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.CurType, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BlinkCur, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Beep, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BeepInd, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BellOverload, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BellOverloadN, 0x00000005));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BellOverloadT, 0x000007d0));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BellOverloadS, 0x00001388));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ScrollbackLines, 0x00002000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.DECOriginMode, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.AutoWrapMode, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LFImpliesCR, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.CRImpliesLF, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.DisableArabicShaping, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.DisableBidi, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.WinNameAlways, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TermWidth, 0x00000050));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TermHeight, 0x00000018));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.FontIsBold, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.FontCharSet, 0x00000000));
            using (var font = new Font("Consolas", 10))
            {
                if (font?.Name == "Consolas")
                    Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Font, "Consolas"));
                else
                    Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Font, "Courier New"));
            }
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.FontHeight, 12));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.FontCharSet, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.FontQuality, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.FontVTMode, 0x00000004));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.UseSystemColours, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.TryPalette, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ANSIColour, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Xterm256Colour, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BoldAsColour, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RawCNP, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.PasteRTF, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.MouseIsXterm, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.MouseOverride, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RectSelect, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.CJKAmbigWide, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.UTF8Override, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.CapsLockCyr, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ScrollBar, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ScrollBarFullScreen, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ScrollOnKey, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ScrollOnDisp, 0x00000f001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.EraseToScrollback, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LockSize, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BCE, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BlinkText, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.X11Forward, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.X11AuthType, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LocalPortAcceptAll, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.RemotePortAcceptAll, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugIgnore1, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugPlainPW1, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugRSA1, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugIgnore2, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugHMAC2, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugDeriveKey2, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugRSAPad2, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugPKSessID2, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugRekey2, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.BugMaxPkt2, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.StampUtmp, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.LoginShell, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ScrollbarOnLeft, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ShadowBold, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.ShadowBoldOffset, 0x00000001));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SerialSpeed, 0x00002580));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SerialDataBits, 0x00000008));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SerialStopHalfbits, 0x00000002));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SerialParity, 0x00000000));
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.SerialFlowControl, 0x00000001));
#if UseKiTTY
            Options.Add(PuttyOptionItem.Create(PuttyOptionKey.Autocommand, ""));
#endif
            #endregion
        }


        public void Set(PuttyOptionKey key, int value)
        {
            if (Options.Any(x => x.Key == key.ToString()))
            {
                var item = Options.First(x => x.Key == key.ToString());
                Debug.Assert(item != null);
                Debug.Assert(item.ValueKind == RegistryValueKind.DWord);
                item.Value = value;
            }
            else
            {
                Options.Add(PuttyOptionItem.Create(key, value));
            }
        }
        public void Set(PuttyOptionKey key, string value)
        {
            if (Options.Any(x => x.Key == key.ToString()))
            {
                var item = Options.First(x => x.Key == key.ToString());
                Debug.Assert(item != null);
                Debug.Assert(item.ValueKind == RegistryValueKind.String);
                item.Value = value;
            }
            else
            {
                Options.Add(PuttyOptionItem.Create(key, value));
            }
        }

        /// <summary>
        /// save to reg table
        /// </summary>
        public void SaveToPuttyRegistryTable()
        {
            string regPath = $"Software\\SimonTatham\\PuTTY\\Sessions\\{SessionName}";
            using (var regKey = Registry.CurrentUser.CreateSubKey(regPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (regKey != null)
                {
                    foreach (var item in Options)
                    {
                        if (item.Value != null)
                            regKey.SetValue(item.Key, item.Value, item.ValueKind);
                    }
                }
            }
        }

        /// <summary>
        /// del from reg table
        /// </summary>
        public void DelFromPuttyRegistryTable()
        {
            if (File.Exists(PuttyKeyFilePath))
                File.Delete(PuttyKeyFilePath);
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
        private void DelFromKittyRegistryTable()
        {
            if (File.Exists(PuttyKeyFilePath))
                File.Delete(PuttyKeyFilePath);
            string regPath = $"Software\\9bis.com\\KiTTY\\Sessions\\{SessionName}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(regPath);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Info("Try to delete KiTTY registry table but fail: ", PuttyKeyFilePath, e);
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
