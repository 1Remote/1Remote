/**
 * RDP「额外指令」（RdpControlAdditionalSettings）属性名候选表 —— 键值行编辑器的
 * 自动补全数据源（FIELD.KEY_VALUE_LINES）。
 *
 * 来源（WPF 补全源的一比一移植）：RDP.GetRdpControlAdditionalSettingKeys()
 * （Ui/Model/Protocol/RDP.cs:419-510）在 WPF 运行时用**反射**枚举
 *   ① MSTSCLib.IMsRdpClientAdvancedSettings8 与 ② AxMSTSCLib.AxMsRdpClient10
 * 的全部 public 可写、类型为 int/bool/string 的属性（BindingFlags.Instance|Public，
 * 含 AxMsRdpClient10 自 WinForms Control 继承的属性），减去 RDP.cs:426-463 的
 * excludeKeys 排除表（Server/Domain/UserName/RDPPort/Width/Height/Handle 等 26 项——
 * 它们由 1Remote 自身 UI 接管，不允许用户覆写），再拼类型后缀：
 *   int/bool → ':i:'，string → ':s:'，即候选串形如 'EnableAutoReconnect:i:'。
 * 排序 = Distinct 后 OrderBy(x => x.ToLower()[0])（LINQ 稳定排序：按首字母小写分桶，
 * 桶内保持反射元数据顺序）。
 *
 * web 端无法反射 COM interop 程序集 → 取静态近似：本清单由同一套过滤逻辑对仓库内
 * lib/AxMSTSCLib.dll + lib/MSTSCLib.dll（Ui.csproj:216-222 引用版本）反射导出生成，
 * 共 103 项，与 WPF 运行时列表逐项一致（含 Control 系遗留项 AllowDrop/TabIndex/
 * TabStop/Text/Capture/CausesValidation/RightToLeft/UseWaitCursor/IsAccessible——
 * WPF 原列表同样未排除，为保真不作删改）。若未来升级 MSTSC AxInterop 程序集，
 * 需按上述规则重新导出。
 *
 * 后缀语义（值类型提示，与 WPF 解析器 RDP.cs:515 SplitAdditionalSettings 对应）：
 *   ':i:' 值须为整数（int/bool 型属性，bool 写 0/1）；':s:' 值为任意字符串。
 */
export const RDP_CONTROL_ADDITIONAL_SETTING_KEYS = [
  'Compress:i:',
  'ContainerHandledFullScreen:i:',
  'CachePersistenceActive:i:',
  'ConnectToServerConsole:i:',
  'ClearTextPassword:s:',
  'ConnectionBarShowMinimizeButton:i:',
  'ConnectionBarShowRestoreButton:i:',
  'ConnectionBarShowPinButton:i:',
  'ConnectingText:s:',
  'ColorDepth:i:',
  'ConnectedStatusText:s:',
  'Capture:i:',
  'CausesValidation:i:',
  'BitmapPeristence:i:',
  'BitmapCacheSize:i:',
  'BitmapVirtualCacheSize:i:',
  'brushSupportLevel:i:',
  'BitmapPersistence:i:',
  'BitmapVirtualCache16BppSize:i:',
  'BitmapVirtualCache24BppSize:i:',
  'BitmapVirtualCache32BppSize:i:',
  'BandwidthDetection:i:',
  'allowBackgroundInput:i:',
  'AcceleratorPassthrough:i:',
  'AuthenticationServiceClass:s:',
  'AudioCaptureRedirectionMode:i:',
  'AccessibleDefaultActionDescription:s:',
  'AccessibleDescription:s:',
  'AccessibleName:s:',
  'AllowDrop:i:',
  'KeyBoardLayoutStr:s:',
  'keepAliveInterval:i:',
  'KeyboardType:i:',
  'KeyboardSubType:i:',
  'KeyboardFunctionKey:i:',
  'PluginDlls:s:',
  'PersistCacheDirectory:s:',
  'PinConnectionBar:i:',
  'PerformanceFlags:i:',
  'PublicMode:i:',
  'PCB:s:',
  'IconFile:s:',
  'IconIndex:i:',
  'InputEventsAtOnce:i:',
  'IsAccessible:i:',
  'DisableRdpdr:i:',
  'DedicatedTerminal:i:',
  'DisableCtrlAltDel:i:',
  'DoubleClickDetect:i:',
  'DisplayConnectionBar:i:',
  'DisconnectedText:s:',
  'SmoothScroll:i:',
  'ShadowBitmap:i:',
  'SasSequence:i:',
  'ScaleBitmapCachesByBPP:i:',
  'shutdownTimeout:i:',
  'singleConnectionTimeout:i:',
  'SmartSizing:i:',
  'StartConnected:i:',
  'TransportType:i:',
  'Text:s:',
  'TabIndex:i:',
  'TabStop:i:',
  'EncryptionEnabled:i:',
  'EnableWindowsKey:i:',
  'EnableAutoReconnect:i:',
  'EnableCredSspSupport:i:',
  'EnableSuperPan:i:',
  'MaximizeShell:i:',
  'minInputSendInterval:i:',
  'maxEventCount:i:',
  'MinutesToIdleTimeout:i:',
  'MaxReconnectAttempts:i:',
  'HotKeyFullScreen:i:',
  'HotKeyCtrlEsc:i:',
  'HotKeyAltEsc:i:',
  'HotKeyAltTab:i:',
  'HotKeyAltShiftTab:i:',
  'HotKeyAltSpace:i:',
  'HotKeyCtrlAltDel:i:',
  'HotKeyFocusReleaseLeft:i:',
  'HotKeyFocusReleaseRight:i:',
  'orderDrawThreshold:i:',
  'overallConnectionTimeout:i:',
  'NumBitmapCaches:i:',
  'NotifyTSPublicKey:i:',
  'NegotiateSecurityLayer:i:',
  'WinceFixedPalette:i:',
  'RdpdrLocalPrintingDocName:s:',
  'RdpdrClipCleanTempDirString:s:',
  'RdpdrClipPasteInfoString:s:',
  'RedirectDrives:i:',
  'RedirectPrinters:i:',
  'RedirectPorts:i:',
  'RedirectSmartCards:i:',
  'RedirectClipboard:i:',
  'RedirectDevices:i:',
  'RedirectPOSDevices:i:',
  'RelativeMouseMode:i:',
  'RedirectDirectX:i:',
  'RightToLeft:i:',
  'GrabFocusOnConnect:i:',
  'UseWaitCursor:i:',
]
