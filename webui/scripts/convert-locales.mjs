// Plan 3 Task 7: 一次性将 Ui/Resources/Languages/*.xaml（WPF ResourceDictionary）
// 转换为 webui/src/locales/*.json（vue-i18n 平面键）。zh-CN/en-US 为手写基准，
// 本脚本只生成其余 12 个文件（重建可重复运行，输出确定性）。
//
// 用法：node scripts/convert-locales.mjs [--check]
//   默认：解析 XAML → 按 MAPPING 填充 → 写 12 个 JSON → 打印填充率报告 + 平价校验
//   --check：不写文件，仅校验磁盘上 14 个 locale 键集与 en-US 完全一致（验收门禁用）
//
// 映射策略（见计划 全局约定 4）：
//   - web 键 → WPF 键 多数 1:1 语义对齐（按 en 文案语义匹配，少数组合/近似）；
//   - 无 WPF 对应的 web 专有键（statusbar/tree 行/编辑器 schema 细节等）保留英文
//     （回退链 该语言→en-US，见 locales/index.js）；
//   - WPF {0}/{1} → web {name}：PLACEHOLDERS 按键配置（当前映射表命中的 WPF 键
//     均不含占位符，此为防御逻辑）；残留的 vue-i18n 特殊字符 { } | @ 用字面量转义。
import { readFileSync, writeFileSync, readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))
const XAML_DIR = path.resolve(here, '../../Ui/Resources/Languages')
const OUT_DIR = path.resolve(here, '../src/locales')

// 元数据键（语言名/语言码）不是 UI 文案，跳过
const META_KEYS = new Set(['key', 'language_name', 'language_code-ISO', 'language_code-google'])

// ---------------------------------------------------------------------------
// web 键 → WPF 键 映射表（未列出的键 = 无 WPF 对应，保留 en-US 值）
const MAPPING = {
  // -- 树 / 列表 --
  'tree.reconnecting': 'Reconnecting',
  'tree.tags': 'Tags',
  'tree.newFolder': 'Create a folder',
  'tree.renameFolder': 'Rename',
  'tree.deleteFolderConfirm': 'Delete folder XXXX and move its contents to parent folder',
  'col.name': 'Name',
  'col.protocol': 'Protocol',
  'col.tags': 'Tags',

  // -- 批量 / 右键 / 行 --
  'batch.connect': 'Connect',
  'batch.edit': 'server_editor_bulk_editing',
  'batch.export': 'Export',
  'ctx.connect': 'Connect',
  'ctx.newWindow': 'Connect (New window)',
  'ctx.edit': 'Edit',
  'ctx.duplicate': 'Duplicate',
  'ctx.copyAddress': 'server_card_operate_copy_address',
  'ctx.copyUsername': 'server_card_operate_copy_username',
  'ctx.shortcut': 'Create desktop shortcut',
  'ctx.delete': 'Delete',
  'row.select': 'Select',
  'row.connect': 'Connect',
  'row.edit': 'Edit',

  // -- 空态 / 页面 / 导入 --
  'page.settings': 'Options',
  'topbar.newServer': 'New server',
  'import.button': 'Import',
  'import.done': 'import_done_0_items_added',

  // -- 设置框架 --
  'settings.back': 'Back',
  'settings.save': 'Save',
  'settings.nav.general': 'system_options_general_title',
  'settings.nav.launcher': 'Launcher',
  'settings.nav.data': 'system_options_data_security_database',
  'settings.nav.credentials': 'Credentials',
  'settings.nav.appearance': 'Themes',
  'settings.nav.about': 'About',

  // -- 常规组字段（与 WPF Options 文案一一对应）--
  'settings.f.language': 'Language',
  'settings.f.closeButtonBehavior': 'Close button behavior',
  'settings.f.confirmBeforeClosingSession': 'Confirm before closing',
  'settings.f.showSessionIconInSessionWindow': 'Show current session icon instead of the app icon when connected',
  'settings.f.requireSecondaryVerification': 'Windows credentials verification is required to view passwords',
  'settings.f.tabWindowCloseButtonOnLeft': 'Place the close button on the left side to prevent accidental touches',
  'settings.f.tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow':
    'Set focus to local desktop when the mouse is moved out of RDP desktop',
  'settings.f.copyPortWhenCopyAddress': 'Copy the port along with the address when copying',
  'settings.f.doNotCheckNewVersion': 'Do not check for new version',
  'settings.o.close.exit': 'Exit',
  'settings.o.close.minimize': 'Minimize to system tray',

  // -- 外观组 --
  'settings.appearance.accent': 'Color',
  'settings.appearance.fontSize': 'Font size',
  'settings.appearance.classic': 'Themes',

  // -- 编辑器：凭据/批量 --
  'editor.credSelectHint': 'Select credentials within the same database',
  'editor.differentValues': 'server_editor_different_options',
  'editor.bulkField.displayName': 'Name',
  'editor.bulkField.note': 'Note',
  'editor.bulkField.tags': 'Tags',
  'editor.bulkField.colorHex': 'Color',
  'editor.bulkField.iconBase64': 'Icon',
  'editor.bulkField.address': 'Hostname',
  'editor.bulkField.port': 'Port',
  'editor.bulkField.userName': 'User',
  'editor.bulkField.password': 'Password',
  'editor.bulkField.askPasswordWhenConnect': 'Ask for password when open connect',
  'editor.bulkField.startupAutoCommand': 'server_editor_advantage_ssh_startup_auto_command',
  'editor.bulkField.startupPath': 'server_editor_advantage_sftp_startup_path',
  'editor.bulkField.rdpFileAdditionalSettings': 'Additional settings',

  // -- 编辑器：字段标签（editor.f.*）--
  'editor.f.AddBlankAfterKey': 'append blank after prefix',
  'editor.f.AddBlankAfterValue': 'append blank after value',
  'editor.f.Address': 'Hostname',
  'editor.f.AlternateCredentials': 'Alternative',
  'editor.f.AlwaysOpenInNewTabWindow': 'Always open in new window',
  'editor.f.ArgumentList': 'Cmd parameter',
  'editor.f.AskPasswordWhenConnect': 'Ask for password when open connect',
  'editor.f.AudioQualityMode': 'server_editor_advantage_sound_quality',
  'editor.f.AudioRedirectionMode': 'server_editor_advantage_sounds',
  'editor.f.availabilityDetection': 'Availability detection',
  'editor.f.ColorHex': 'Color',
  // batch8 Task C #7②①③④：连接脚本组/公共组开关/运行器/取值表（ServerEditorPageView.xaml
  // 与 AlternativeCredentialListView.xaml 的 14 语言词条）
  'editor.f.CommandAfterDisconnected': 'Script after disconnected',
  'editor.f.CommandBeforeConnected': 'Script before connect',
  'editor.f.HideCommandBeforeConnectedWindow': 'Hide script window',
  'editor.f.SelectedRunnerName': 'Selected runner',
  'editor.f.Selections': 'Selections',
  'editor.f.IsAutoAlternateAddressSwitching': 'Automatic address switching',
  'editor.f.Description': 'Description',
  'editor.f.DisplayName': 'Name',
  'editor.f.DisplayPerformance': 'server_editor_display_rdp_performance',
  'editor.f.EnableAudioCapture': 'server_editor_advantage_audio_capture',
  'editor.f.EnableClipboard': 'server_editor_advantage_clipboard',
  'editor.f.EnableDiskDrives': 'server_editor_advantage_disk_drives',
  'editor.f.EnableKeyCombinations': 'server_editor_advantage_key_combinations',
  'editor.f.EnablePorts': 'server_editor_advantage_ports',
  'editor.f.EnablePrinters': 'server_editor_advantage_printers',
  'editor.f.EnableRedirectCameras': 'Cameras',
  'editor.f.EnableRedirectDrivesPlugIn': 'Drives plug in later',
  'editor.f.EnableSmartCardsAndWinHello': 'server_editor_advantage_smart_cards',
  'editor.f.ExePath': 'Exe path',
  'editor.f.GatewayHostName': 'server_editor_gateway_server_host_name',
  'editor.f.GatewayLogonMethod': 'server_editor_gateway_logon_method',
  'editor.f.GatewayMode': 'server_editor_gateway_mode',
  'editor.f.GatewayPassword': 'Password',
  'editor.f.GatewayUserName': 'User',
  'editor.f.IconBase64': 'Icon',
  'editor.f.IsAdministrativePurposes': 'server_editor_advantage_admin',
  'editor.f.IsConnWithFullScreen': 'server_editor_display_full_screen_flag',
  'editor.f.IsFullScreenWithConnectionBar': 'Display the connection bar when use the full screen',
  'editor.f.IsNullable': 'Nullable',
  'editor.f.IsPinTheConnectionBarByDefault': 'Pin the connection bar by default',
  'editor.f.IsScaleFactorFollowSystem': 'Follow system',
  'editor.f.Key': 'Prefix',
  'editor.f.MstscModeEnabled': 'mstsc.exe mode',
  'editor.f.Name': 'Name',
  'editor.f.Note': 'Note',
  'editor.f.OpenSftpOnConnected': 'Open SFTP when connected',
  'editor.f.Password': 'Password',
  'editor.f.Port': 'Port',
  'editor.f.PrivateKey': 'Private key',
  'editor.f.PrivateKeyPath': 'Private key',
  'editor.f.RdpControlAdditionalSettings': 'Additional settings',
  'editor.f.RdpFileAdditionalSettings': 'Additional settings',
  'editor.f.RdpFullScreenFlag': 'server_editor_display_full_screen_flag',
  'editor.f.RdpWindowResizeMode': 'Resolution',
  'editor.f.RemoteApplicationName': 'server_editor_remote_app_name',
  'editor.f.RemoteApplicationProgram': 'server_editor_remote_app_fullname',
  'editor.f.resources': 'server_editor_advantage_resources',
  'editor.f.RunWithHosting': 'Hosting',
  'editor.f.ScaleFactorCustomValue': 'Custom',
  'editor.f.SshVersion': 'server_editor_advantage_ssh_version',
  'editor.f.StartupAutoCommand': 'server_editor_advantage_ssh_startup_auto_command',
  'editor.f.StartupPath': 'server_editor_advantage_sftp_startup_path',
  'editor.f.Tags': 'Tags',
  'editor.f.Type': 'Type',
  'editor.f.UsePrivateKeyForConnect': 'Use private key',
  'editor.f.UserName': 'User',

  // -- 编辑器：枚举选项（editor.o.*）--
  'editor.o.fullScreen.Disable': 'server_editor_display_full_screen_flag_window',
  'editor.o.fullScreen.EnableFullScreen': 'server_editor_display_full_screen_flag_full_screen',
  'editor.o.fullScreen.EnableFullAllScreens': 'server_editor_display_full_screen_flag_all_screens',
  'editor.o.resize.AutoResize': 'Fit to window',
  'editor.o.resize.Stretch': 'Custom resolution (stretch)',
  'editor.o.resize.Fixed': 'Custom resolution (fixed)',
  'editor.o.resize.StretchFullScreen': 'Full-screen resolution (stretch)',
  'editor.o.resize.FixedFullScreen': 'Full-screen resolution (fixed)',
  'editor.o.performance.Auto': 'server_editor_display_rdp_performance_auto',
  'editor.o.performance.Low': 'server_editor_display_rdp_performance_low',
  'editor.o.performance.Middle': 'server_editor_display_rdp_performance_middle',
  'editor.o.performance.High': 'server_editor_display_rdp_performance_high',
  'editor.o.audioMode.RedirectToLocal': 'server_editor_advantage_sounds_on_local',
  'editor.o.audioMode.LeaveOnRemote': 'server_editor_advantage_sounds_on_remote',
  'editor.o.audioMode.Disabled': 'server_editor_advantage_sounds_disabled',
  'editor.o.audioQuality.Dynamic': 'server_editor_advantage_sound_quality_dynamic',
  'editor.o.audioQuality.Medium': 'server_editor_advantage_sound_quality_medium',
  'editor.o.audioQuality.High': 'server_editor_advantage_sound_quality_high',
  'editor.o.gatewayMode.AutoDetect': 'server_editor_gateway_mode_automatically_detect',
  'editor.o.gatewayMode.UseThese': 'server_editor_gateway_mode_use_these',
  'editor.o.gatewayMode.DoNotUse': 'server_editor_gateway_mode_do_not_use',
  'editor.o.gatewayLogon.Password': 'server_editor_gateway_logon_method_psw',
  'editor.o.gatewayLogon.SmartCard': 'server_editor_gateway_logon_method_smart_card',
  'editor.o.appArgType.Normal': 'Normal',
  'editor.o.appArgType.Selection': 'Selections',
  'editor.o.appArgType.Const': 'Const value',
  // fix-batch4 Task A #6：IsPingBeforeConnect（可用性检测行）的控件列说明文字
  'editor.o.checkAddressAvailable': 'Check if address is available before connect',
  // batch8 Task C #7④：备用地址自动切换开关的控件列说明（AlternativeCredentialListView.xaml:111-113）
  'editor.o.autoAlternateAddressSwitchingHint':
    'When the default host or port is unavailable the alternate addresses will be tried in sequence',

  // -- 编辑器：输入框 placeholder（editor.ph.*，fix-batch4 Task C）--
  // 仅 DynamicResource Tag（WPF 14 语言有译文）的键走映射；字面量英文 Tag 的键
  // （address/password/startupPath 等，WPF 所有语言同显英文）不映射——生成 locale
  // 回退 en-US 原文，即 14 语言同值。
  'editor.ph.inheritDefault': 'Leave blank to inherit the default value',
  'editor.ph.remoteAppName': 'server_editor_remote_app_name_tag',
  'editor.ph.remoteAppProgram': 'server_editor_remote_app_fullname_tag',
  'editor.ph.appProtocolDisplayName': 'Optional',
  'editor.ph.externalKittySession': 'server_editor_advantage_ssh_startup_auto_kitty_session_tip',
  // batch8 Task C #7②：连接前后脚本的 Tag（ServerEditorPageView.xaml:167/202，
  // DynamicResource 14 语言词条；含 e.g. 示例路径）
  'editor.ph.commandBeforeConnected': 'Run bat before connect',
  'editor.ph.commandAfterDisconnected': 'Run bat after disconnected',

  // -- 编辑器：分组 / 窗口 --
  'editor.group.basic': 'server_editor_group_title_common',
  'editor.group.credential': 'Credentials',
  'editor.group.display': 'server_editor_group_title_display',
  'editor.group.mstsc': 'mstsc.exe mode',
  'editor.group.advanced': 'server_editor_group_title_advantage',
  'editor.group.gateway': 'server_editor_group_title_gateway',
  'editor.group.connection': 'Connection Settings',
  'editor.dataSource': 'Data Source name',
  'editor.protocol': 'Protocol',
  // 批次7 #2：头部下拉框前缀标签（协议/数据库）。'Protocol' 与
  // system_options_data_security_database 在 WPF 14 语言均有译文 → 走映射
  'editor.headProtocolLabel': 'Protocol',
  'editor.headDsLabel': 'system_options_data_security_database',
  'editor.close': 'Close',
  'editor.cancel': 'Cancel',
  'editor.save': 'Save',
  'editor.deleteYes': 'Delete',

  // -- 凭据库 --
  'cv.ds': 'Data Source name',
  'cv.edit': 'Edit',
  'cv.nameRequired': 'Can not be empty!',
  'cv.reveal': 'Please complete the windows credentials verification',
  'cv.revealWaiting': 'Before proceeding with sensitive operations, we need to make sure it is you.',
  'cv.revealFailed': 'Verification failed. Please try again.',

  // -- 标签管理 --
  'tagm.pin': 'Pin',
  'tagm.unpin': 'Unpin',
  'tagm.rename': 'Rename',

  // -- 启动器组 --
  'settings.l.enabled': 'system_options_quick_launcher_enable',
  'settings.l.hotkey': 'system_options_quick_launcher_hotkey',
  'settings.l.showCredentials': 'Show credentials info',
  'settings.l.allowSaveInfo': 'Allow Quick Connect to save entered address information',
  'settings.l.conflict': 'hotkey_registered_fail',

  // -- 数据源组 --
  'settings.d.add': 'Add',
  'settings.d.test': 'Test',
  'settings.d.testOk': 'Success',
  'settings.d.testFailed': 'Failed',
  'settings.d.edit': 'Edit',
  'settings.d.type': 'Type',
  'settings.d.name': 'Name',
  'settings.d.f.host': 'Hostname',
  'settings.d.f.port': 'Port',
  'settings.d.f.database': 'system_options_data_security_database',
  'settings.d.f.user': 'User',
  'settings.d.f.password': 'Password',
  'settings.d.deleted': 'Success',
  'settings.d.deleteFailed': 'Failed',

  // -- 运行器组 --
  'settings.r.selected': 'Selected runner',
  'settings.r.internal': 'Default',
  'settings.r.f.env': 'Environment variables',
  // fix batch7 Task E #13/#14：PuTTY 主题/字体/字号、私钥参数、增删校验文案（WPF 运行器
  // 设置页词条 1:1；主题/字体/字符集为下拉标签，字号自由数字）。
  // batch8 Task D #10：arguments/argsPrivateKey 改为"启动参数（通过密码/私钥）"组合文案，
  // WPF 无对应词条（'Cmd parameter' 与 'Login with password' 是两个独立键）→ 撤出映射，
  // 12 生成语言回落 en-US，zh 系走手写（zh-CN 手写基准 + zh-TW OVERRIDES）
  'settings.r.addTitle': 'New runner name',
  'settings.r.nameRequired': 'Can not be empty!',
  'settings.r.nameExists': 'XXX is already existed!',
  'settings.r.f.theme': 'Themes',
  'settings.r.f.font': 'Font',
  'settings.r.f.fontSize': 'Font size',
  // batch8 Task D #12：RunWithHosting 解释文本与行 ToolTip——WPF ExternalRunnerSettings.xaml
  // 的 14 语言词条 1:1 移植（注意映射键是 XAML 的 x:Key，en 译文与键名不同属正常）
  'settings.r.f.hostingHint': 'Caution: some exe can not be hosted in 1Remote.',
  'settings.r.f.hostingTitle': 'Hosting this exe in 1Remote tab view?',

  // -- 关于页（fix batch6 Task D #7）：5 个有 WPF 词条的键，14 语言文案从
  //    AboutPageView.xaml 的 DynamicResource 键移植；纯技术标签（Author/Support/
  //    Make contributions/Included Components/Update/Version/标语）为 WPF 硬编码英文，
  //    不映射——生成 locale 回退 en-US 原文，即 14 语言同值 --
  'about.howToUse': 'about_page_how_to_use',
  'about.contributeText':
    'I hope that you find this app useful. If you would like to support my work, you can buy me a coffee or give a nice review. Thanks!',
  'about.giveSuggestions': 'Give suggestions',
  'about.buyCoffee': 'Buy a coffee',
  'about.giveReview': 'Give nice review',
}

// WPF {0}/{1} → web 具名占位符（仅当目标 WPF 键的值含 {N} 时需要）
const PLACEHOLDERS = {
  'tree.deleteFolderConfirm': ['{name}', '{n}'], // {0}=文件夹名，{1}=服务器数
  'settings.r.nameExists': ['{name}'], // {0}=运行器名（WPF XXX is already existed!）
}
const FALLBACK_PARAMS = ['{n}', '{m}']

// web 键 → WPF 译文的固定包装 [前缀, 后缀]（fix-batch4 Task A #5）：web 文案在 WPF
// 原文之外还带 web 专有固定字面量时使用。IsAdministrativePurposes 的 WPF 表单里实际
// 展示为 "/admini" 前缀 + server_editor_advantage_admin 说明（RdpFormView），纯映射
// 无法表达 → 包装器补齐，保证再生成不丢失该格式。
const WRAP = {
  'editor.f.IsAdministrativePurposes': ['/admini (', ')'],
}

// 生成 locale 的手写覆盖（fix-batch7 #7）：web 专有键需要某个生成语言的文案、WPF 又无
// 可映射词条时在此登记——否则重跑生成会把它静默回落 en-US（手写进 JSON 不稳定）。
// 仅对未映射键生效（映射键走 XAML 译文）。值用 \u 转义书写以保持脚本"非注释 CJK=0"
// 门禁（可读形式见上一行注释）。
const OVERRIDES = {
  'zh-TW': {
    // 搜尋伺服器（Ctrl+F）
    'search.placeholder': '\u641c\u5c0b\u4f3a\u670d\u5668\uff08Ctrl+F\uff09',
    // 搜尋伺服器（Ctrl+F）——batch8 #3：search.title 與 placeholder 同文（tooltip 一致，刪 Ctrl+K 表述）
    'search.title': '\u641c\u5c0b\u4f3a\u670d\u5668\uff08Ctrl+F\uff09',
    // 選擇資料夾內所有伺服器（含子資料夾）——batch8 #2：文件夾行複選框 title
    'row.selectFolder':
      '\u9078\u64c7\u8cc7\u6599\u593e\u5167\u6240\u6709\u4f3a\u670d\u5668\uff08\u542b\u5b50\u8cc7\u6599\u593e\uff09',
    // fix batch7 Task E #13/#14\uff1aweb \u5c08\u6709\u9375\uff08\u589e\u522a\u904b\u884c\u5668\u6309\u9215/\u78ba\u8a8d/\u5167\u5efa\u4e0d\u53ef\u522a\u63d0\u793a\u3001\u5b57\u5143\u96c6\u3001
    // \u7279\u6b8a\u5b57\u5143\u3001\u5b8f\u63d0\u793a\uff09\u2014\u2014WPF \u7121\u5c0d\u61c9\u689d\u76ee\uff0c\u767b\u8a18\u5f8c\u518d\u751f\u6210\u624d\u4e0d\u6703\u56de\u843d en-US\uff08\u503c\u4fdd\u6301 \u \u8f49\u7fa9\uff0c
    // \u53ef\u8b80\u5f62\u5f0f\u898b\u63d0\u4ea4\u8aaa\u660e\uff09
    'settings.r.add': '\u65b0\u589e\u57f7\u884c\u5668',
    'settings.r.deleteTitle': '\u522a\u9664\u57f7\u884c\u5668',
    'settings.r.deleteConfirm':
      '\u522a\u9664\u57f7\u884c\u5668\u300c{name}\u300d\uff1f\u6b64\u64cd\u4f5c\u4e0d\u53ef\u64a4\u92b7\u3002',
    'settings.r.internalNoDelete': '\u5167\u5efa\u57f7\u884c\u5668\u4e0d\u53ef\u522a\u9664\u3002',
    'settings.r.f.charset': '\u5b57\u5143\u96c6',
    'settings.r.f.special': '\u7279\u6b8a\u5b57\u5143',
    // \u7279\u6b8a\u5b57\u7b26\uff08\u5982\u4f7f\u7528\u8005\u540d\u7a31\u4e2d\u7684 @\uff09\u53ef\u80fd\u9700\u8981\u7528 %XX \u8a9e\u6cd5\u8f49\u7fa9\uff0c\u6bcf\u884c\u4e00\u500b KEY=VALUE\uff0c\u4f8b\u5982 @=%40
    // \u2014\u2014@ \u7528 vue-i18n \u5b57\u9762\u91cf\u8f49\u7fa9 {'@'}\uff08#20\uff1a\u88f8 @ \u662f\u93c8\u63a5\u6d88\u606f\u8a9e\u6cd5\uff0c\u89f8\u767c\u6bcf\u6b21\u6e32\u67d3\u7684
    // Message compilation error \u63a7\u5236\u53f0\u5237\u5c4f\uff1ben-US/zh-CN \u540c\u6b3e\u5df2\u8f49\u7fa9\uff0c\u751f\u6210\u8a9e\u8a00\u56de\u843d en-US\uff09
    'settings.r.f.specialHint':
      "\u7279\u6b8a\u5b57\u5143\uff08\u5982\u4f7f\u7528\u8005\u540d\u7a31\u4e2d\u7684 {'@'}\uff09\u53ef\u80fd\u9700\u8981\u7528 %XX \u8a9e\u6cd5\u8f49\u7fa9\uff0c\u6bcf\u884c\u4e00\u500b KEY=VALUE\uff0c\u4f8b\u5982 {'@'}=%40",
    'settings.r.f.macroHint': '\u53ef\u7528\u5de8\u96c6\uff1a',
    // batch8 Task D #10/#11\uff1aweb \u5c08\u6709\u9375\uff08\u555f\u52d5\u53c3\u6578\u7d44\u5408\u6587\u6848\u3001\u700f\u89bd\u6309\u9215\u3001\u5167\u5efa\u8def\u5f91\u7ba1\u7406\u63d0\u793a\u3001
    // \u6a94\u6848\u9078\u64c7\u8996\u7a97\u5931\u6557\uff09\u2014\u2014WPF \u7121\u5c0d\u61c9\u689d\u76ee\uff0c\u767b\u8a18\u5f8c\u518d\u751f\u6210\u624d\u4e0d\u6703\u56de\u843d en-US
    'settings.r.f.arguments': '\u555f\u52d5\u53c3\u6578\uff08\u900f\u904e\u5bc6\u78bc\uff09',
    'settings.r.f.argsPrivateKey': '\u555f\u52d5\u53c3\u6578\uff08\u900f\u904e\u79c1\u5bc6\u91d1\u9470\uff09',
    'settings.r.f.browse': '\u700f\u89bd\u2026',
    'settings.r.internalExeManaged':
      '\u5167\u5efa\u57f7\u884c\u5668\u8def\u5f91\u7531\u61c9\u7528\u7a0b\u5f0f\u7ba1\u7406\u3002',
    'settings.r.pickFailed': '\u958b\u555f\u6a94\u6848\u9078\u64c7\u8996\u7a97\u5931\u6557\u3002',
  },
}

// ---------------------------------------------------------------------------

function decodeXml(s) {
  return s
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&')
}

function parseXaml(file) {
  const text = readFileSync(file, 'utf8')
  const dict = {}
  const re = /<s:String x:Key="([^"]+)">(.*)<\/s:String>/g
  let m
  while ((m = re.exec(text)) !== null) {
    if (META_KEYS.has(m[1])) continue
    dict[m[1]] = decodeXml(m[2])
  }
  return dict
}

/** WPF 值 → vue-i18n 消息：{N} 转具名占位符；残留特殊字符用字面量语法转义。
 * 修复（fix batch7 Task E 顺带）：原实现 for-of 迭代器与手工跳读 i 并行推进——命中 {N}
 * 替换并 i+=len 后，迭代器仍按原串顺序吐出占位符剩余字符（如 {0} 的 "0}"），被当作
 * 字面量二次转义追加（生成值出现 "`{name}0{'}'}`" 垃圾尾巴，tree.deleteFolderConfirm
 * 在 12 个生成语言里早已带此缺陷）。改为纯索引循环，跳读后不再回读已消费字符。 */
function toI18nMessage(webKey, v) {
  const params = PLACEHOLDERS[webKey] || FALLBACK_PARAMS
  let out = ''
  let i = 0
  while (i < v.length) {
    const ch = v[i]
    if (ch === '{') {
      const m = /^(\{\d+\})/.exec(v.slice(i))
      if (m) {
        const idx = Number(m[0].slice(1, -1))
        out += params[idx] ?? `{p${idx}}`
        i += m[0].length
        continue
      }
      out += "{'{'}"
    } else if (ch === '}') out += "{'}'}"
    else if (ch === '|') out += "{'|'}"
    else if (ch === '@') out += "{'@'}"
    else out += ch
    i += 1
  }
  return out
}

const bcp47 = (lower) =>
  lower
    .split('-')
    .map((p, i) => (i === 0 ? p : p.toUpperCase()))
    .join('-')

// ---------------------------------------------------------------------------

const enUS = JSON.parse(readFileSync(path.join(OUT_DIR, 'en-US.json'), 'utf8'))
const webKeys = Object.keys(enUS)

// 校验映射表本身：web 键必须存在于 en-US.json，WPF 键必须存在于 en-us.xaml
const enXaml = parseXaml(path.join(XAML_DIR, 'en-us.xaml'))
const badWeb = Object.keys(MAPPING).filter((k) => !(k in enUS))
const badWpf = [...new Set(Object.values(MAPPING))].filter((k) => !(k in enXaml))
if (badWeb.length || badWpf.length) {
  console.error('MAPPING 校验失败: 未知 web 键:', badWeb, ' 未知 WPF 键:', badWpf)
  process.exit(1)
}

// WRAP 键必须同时在 MAPPING 中：未映射的键走 en-US 回退分支，前后缀会被静默丢弃
const badWrap = Object.keys(WRAP).filter((k) => !(k in MAPPING))
if (badWrap.length) {
  console.error('WRAP 校验失败: 键不在 MAPPING 中（前后缀不会生效）:', badWrap)
  process.exit(1)
}

// OVERRIDES 键必须是 en-US 现存键：登记错键名会破坏键集平价（--check 门禁拦截的是
// 生成后的结果，这里在源头拦截）
const badOverride = Object.values(OVERRIDES)
  .flatMap((kv) => Object.keys(kv))
  .filter((k) => !(k in enUS))
if (badOverride.length) {
  console.error('OVERRIDES 校验失败: 未知 web 键:', badOverride)
  process.exit(1)
}

const xamlFiles = readdirSync(XAML_DIR).filter((f) => f.endsWith('.xaml'))
const langs = xamlFiles.map((f) => bcp47(f.replace(/\.xaml$/, '')))
const GENERATE = langs.filter((l) => l !== 'zh-CN' && l !== 'en-US') // 12 个生成目标

// languages.js 一致性：XAML 文件集 ↔ LANGUAGES 清单
const langsJs = readFileSync(path.join(OUT_DIR, 'languages.js'), 'utf8')
const missingInJs = langs.filter((l) => !langsJs.includes(`'${l}'`))
if (missingInJs.length) {
  console.error('languages.js 缺少语言码:', missingInJs)
  process.exit(1)
}

if (process.argv.includes('--check')) {
  // 门禁模式：14 个 locale 键集与 en-US 完全一致
  let fail = false
  for (const l of [...langs, 'en-US', 'zh-CN'].filter((x, i, a) => a.indexOf(x) === i)) {
    const keys = Object.keys(JSON.parse(readFileSync(path.join(OUT_DIR, `${l}.json`), 'utf8')))
    const onlyHere = keys.filter((k) => !(k in enUS))
    const onlyEn = webKeys.filter((k) => !keys.includes(k))
    if (onlyHere.length || onlyEn.length) {
      fail = true
      console.error(`[i18n parity] ${l}: 多 ${onlyHere.length} 键 / 少 ${onlyEn.length} 键`)
    }
  }
  console.log(fail ? '[i18n parity] FAIL' : `[i18n parity] OK: ${webKeys.length} 键 × 14 语言一致`)
  process.exit(fail ? 1 : 0)
}

// ---------------------------------------------------------------------------
console.log(
  `web 键总数 ${webKeys.length}，映射 WPF 键 ${Object.keys(MAPPING).length} 个（${((Object.keys(MAPPING).length / webKeys.length) * 100).toFixed(1)}%）\n`
)
console.log('语言      填充(映射命中且该语言有值)  填充率')

for (const lang of GENERATE) {
  const xamlLower = lang.toLowerCase() + '.xaml'
  const dict = parseXaml(path.join(XAML_DIR, xamlLower))
  const out = {}
  let filled = 0
  for (const k of webKeys) {
    const wpfKey = MAPPING[k]
    const wpfVal = wpfKey ? (dict[wpfKey] || '').trim() : ''
    if (wpfVal) {
      const [pre, post] = WRAP[k] || ['', '']
      out[k] = toI18nMessage(k, pre + dict[wpfKey] + post)
      filled++
    } else {
      out[k] = OVERRIDES[lang]?.[k] ?? enUS[k]
    }
  }
  writeFileSync(path.join(OUT_DIR, `${lang}.json`), JSON.stringify(out, null, 2) + '\n')
  const pct = ((filled / webKeys.length) * 100).toFixed(1)
  console.log(`${lang.padEnd(9)} ${String(filled).padStart(4)}/${webKeys.length}            ${pct}%`)
}

// 生成后平价自检（含手写的 zh-CN/en-US）
console.log('')
const all = [...GENERATE, 'zh-CN', 'en-US']
let fail = false
for (const l of all) {
  const keys = Object.keys(JSON.parse(readFileSync(path.join(OUT_DIR, `${l}.json`), 'utf8')))
  if (keys.length !== webKeys.length || keys.some((k) => !(k in enUS))) {
    fail = true
    console.error(`[parity] ${l} 键集与 en-US 不一致`)
  }
}
console.log(fail ? '[parity] FAIL' : `[parity] OK: ${all.length} 个 locale × ${webKeys.length} 键一致`)
process.exit(fail ? 1 : 0)
