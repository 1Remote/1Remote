using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace PRM.Core.Protocol.Putty
{
    public class PuttyRegOptionItem
    {

        private PuttyRegOptionItem() { }
        public static PuttyRegOptionItem Create(PuttyRegOptionKey key, int value)
        {
            return new PuttyRegOptionItem
            {
                Key = key.ToString(),
                Value = value,
                ValueKind = RegistryValueKind.DWord,
            };
        }
        public static PuttyRegOptionItem Create(PuttyRegOptionKey key, string value)
        {
            return new PuttyRegOptionItem
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

    public enum PuttyRegOptionKey
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
        #endregion
    }


    public class PuttyOptions
    {
        public readonly List<PuttyRegOptionItem> Options = new List<PuttyRegOptionItem>();
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

            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TerminalType, "xterm"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TerminalSpeed, "38400,38400"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TerminalModes, "INTR=A,QUIT=A,ERASE=A,KILL=A,EOF=A,EOL=A,EOL2=A,START=A,STOP=A,SUSP=A,DSUSP=A,REPRINT=A,WERASE=A,LNEXT=A,FLUSH=A,SWTCH=A,STATUS=A,DISCARD=A,IGNPAR=A,PARMRK=A,INPCK=A,ISTRIP=A,INLCR=A,IGNCR=A,ICRNL=A,IUCLC=A,IXON=A,IXANY=A,IXOFF=A,IMAXBEL=A,ISIG=A,ICANON=A,XCASE=A,ECHO=A,ECHOE=A,ECHOK=A,ECHONL=A,NOFLSH=A,TOSTOP=A,IEXTEN=A,ECHOCTL=A,ECHOKE=A,PENDIN=A,OPOST=A,OLCUC=A,ONLCR=A,OCRNL=A,ONOCR=A,ONLRET=A,CS7=A,CS8=A,PARENB=A,PARODD=A,"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyExcludeList, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyHost, "proxy"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyUsername, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyPassword, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyTelnetCommand, "connect %host %port\\n"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Environment, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.UserName, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LocalUserName, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Cipher, "aes,blowfish,3des,WARN,arcfour,des"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.KEX, "dh-gex-sha1,dh-group14-sha1,dh-group1-sha1,rsa,WARN"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RekeyBytes, "1G"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.GSSLibs, "gssapi32,sspi,custom"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.GSSCustom, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LogHost, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.PublicKeyFile, PuttyKeyFilePath));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RemoteCommand, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Answerback, "PuTTY"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BellWaveFile, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.WinTitle, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour0, "187,187,187"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour1, "255,255,255"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour2, "0,0,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour3, "85,85,85"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour4, "0,0,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour5, "0,255,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour6, "0,0,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour7, "85,85,85"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour8, "187,0,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour9, "255,85,85"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour10, "0,187,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour11, "85,255,85"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour12, "187,187,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour13, "255,255,85"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour14, "0,0,187"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour15, "85,85,255"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour16, "187,0,187"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour17, "255,85,255"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour18, "0,187,187"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour19, "85,255,255"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour20, "187,187,187"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Colour21, "255,255,255"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness0, "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness32, "0,1,2,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness64, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,2"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness96, "1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness128, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness160, "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness192, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Wordness224, "2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,2,2,2,2,2,2,2,2"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LineCodePage, "UTF-8"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Printer, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.X11Display, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.X11AuthFile, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.PortForwardings, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BoldFont, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.WideFont, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.WideBoldFont, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SerialLine, "COM1"));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.WindowClass, ""));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Present, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LogType, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LogFlush, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SSHLogOmitPasswords, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SSHLogOmitData, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.PortNumber, 0x00000016));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.CloseOnExit, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.WarnOnClose, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.PingInterval, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.PingIntervalSecs, 0x0000003c));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TCPNoDelay, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TCPKeepalives, 0x0000001E)); // seconds between keepalives
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AddressFamily, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyDNS, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyLocalhost, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyMethod, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ProxyPort, 0x00000050));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.UserNameFromEnvironment, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoPTY, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Compression, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TryAgent, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AgentFwd, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.GssapiFwd, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ChangeUsername, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RekeyTime, 0x0000003c));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SshNoAuth, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SshBanner, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AuthTIS, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AuthKI, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AuthGSSAPI, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SshNoShell, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SshProt, 0x00000002));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SSH2DES, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RFCEnviron, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.PassiveTelnet, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BackspaceIsDelete, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RXVTHomeEnd, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LinuxFunctionKeys, 0x00000002));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoApplicationKeys, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoApplicationCursors, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoMouseReporting, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoRemoteResize, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoAltScreen, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoRemoteWinTitle, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RemoteQTitleAction, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoDBackspace, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NoRemoteCharset, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ApplicationCursorKeys, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ApplicationKeypad, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.NetHackKeypad, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AltF4, 0x00000000)); // DISABLED ALTF4
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AltSpace, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AltOnly, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ComposeKey, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.CtrlAltKeys, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TelnetKey, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TelnetRet, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LocalEcho, 0x00000002));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LocalEdit, 0x00000002));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AlwaysOnTop, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.FullScreenOnAltEnter, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.HideMousePtr, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SunkenEdge, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.WindowBorder, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.CurType, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BlinkCur, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Beep, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BeepInd, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BellOverload, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BellOverloadN, 0x00000005));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BellOverloadT, 0x000007d0));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BellOverloadS, 0x00001388));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ScrollbackLines, 0x00002000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.DECOriginMode, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.AutoWrapMode, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LFImpliesCR, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.CRImpliesLF, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.DisableArabicShaping, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.DisableBidi, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.WinNameAlways, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TermWidth, 0x00000050));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TermHeight, 0x00000018));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.FontIsBold, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.FontCharSet, 0x00000000));
            using (var font = new Font("Consolas", 10))
            {
                if (font?.Name == "Consolas")
                    Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Font, "Consolas"));
                else
                    Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Font, "Courier New"));
            }
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.FontHeight, 12));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.FontCharSet, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.FontQuality, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.FontVTMode, 0x00000004));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.UseSystemColours, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.TryPalette, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ANSIColour, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.Xterm256Colour, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BoldAsColour, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RawCNP, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.PasteRTF, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.MouseIsXterm, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.MouseOverride, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RectSelect, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.CJKAmbigWide, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.UTF8Override, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.CapsLockCyr, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ScrollBar, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ScrollBarFullScreen, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ScrollOnKey, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ScrollOnDisp, 0x00000f001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.EraseToScrollback, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LockSize, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BCE, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BlinkText, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.X11Forward, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.X11AuthType, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LocalPortAcceptAll, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.RemotePortAcceptAll, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugIgnore1, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugPlainPW1, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugRSA1, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugIgnore2, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugHMAC2, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugDeriveKey2, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugRSAPad2, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugPKSessID2, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugRekey2, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.BugMaxPkt2, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.StampUtmp, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.LoginShell, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ScrollbarOnLeft, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ShadowBold, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.ShadowBoldOffset, 0x00000001));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SerialSpeed, 0x00002580));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SerialDataBits, 0x00000008));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SerialStopHalfbits, 0x00000002));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SerialParity, 0x00000000));
            Options.Add(PuttyRegOptionItem.Create(PuttyRegOptionKey.SerialFlowControl, 0x00000001));

            #endregion
        }


        public void Set(PuttyRegOptionKey key, int value)
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
                Options.Add(PuttyRegOptionItem.Create(key, value));
            }
        }
        public void Set(PuttyRegOptionKey key, string value)
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
                Options.Add(PuttyRegOptionItem.Create(key, value));
            }
        }

        /// <summary>
        /// save to reg table
        /// </summary>
        public void Save()
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
        public void Del()
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
            }
        }
    }
}
