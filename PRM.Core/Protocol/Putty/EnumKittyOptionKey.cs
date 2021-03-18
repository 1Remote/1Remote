namespace PRM.Core.Protocol.Putty
{
    public enum EnumKittyOptionKey
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

        #endregion Enum
    }
}