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

  // -- 常规组字段（与 WPF Options 文案一一对应）--
  'settings.f.language': 'Language',
  'settings.f.closeButtonBehavior': 'Close button behavior',
  'settings.f.confirmBeforeClosingSession': 'Confirm before closing',
  'settings.f.showSessionIconInSessionWindow': 'Show current session icon instead of the app icon when connected',
  'settings.f.requireSecondaryVerification': 'Windows credentials verification is required to view passwords',
  'settings.f.tabWindowCloseButtonOnLeft': 'Place the close button on the left side to prevent accidental touches',
  'settings.f.tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow': 'Set focus to local desktop when the mouse is moved out of RDP desktop',
  'settings.f.copyPortWhenCopyAddress': 'Copy the port along with the address when copying',
  'settings.f.doNotCheckNewVersion': 'Do not check for new version',
  'settings.o.close.exit': 'Exit',
  'settings.o.close.minimize': 'Minimize to system tray',

  // -- 外观组 --
  'settings.appearance.accent': 'Color',
  'settings.appearance.fontSize': 'Font size',
  'settings.appearance.classic': 'Themes',
  'settings.langAbout.language': 'Language',

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
  'editor.f.ArgumentList': 'Cmd parameter',
  'editor.f.AskPasswordWhenConnect': 'Ask for password when open connect',
  'editor.f.AudioQualityMode': 'server_editor_advantage_sound_quality',
  'editor.f.AudioRedirectionMode': 'server_editor_advantage_sounds',
  'editor.f.ColorHex': 'Color',
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
  'editor.f.IsPingBeforeConnect': 'Check if address is available before connect',
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
  'settings.r.f.arguments': 'Cmd parameter',
  'settings.r.f.env': 'Environment variables',
}

// WPF {0}/{1} → web 具名占位符（仅当目标 WPF 键的值含 {N} 时需要）
const PLACEHOLDERS = {
  'tree.deleteFolderConfirm': ['{name}', '{n}'], // {0}=文件夹名，{1}=服务器数
}
const FALLBACK_PARAMS = ['{n}', '{m}']

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

/** WPF 值 → vue-i18n 消息：{N} 转具名占位符；残留特殊字符用字面量语法转义 */
function toI18nMessage(webKey, v) {
  const params = PLACEHOLDERS[webKey] || FALLBACK_PARAMS
  let out = ''
  let i = 0
  for (const ch of v) {
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
    i += ch.length
  }
  return out
}

const bcp47 = (lower) => lower.split('-').map((p, i) => (i === 0 ? p : p.toUpperCase())).join('-')

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
console.log(`web 键总数 ${webKeys.length}，映射 WPF 键 ${Object.keys(MAPPING).length} 个（${(Object.keys(MAPPING).length / webKeys.length * 100).toFixed(1)}%）\n`)
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
      out[k] = toI18nMessage(k, dict[wpfKey])
      filled++
    } else {
      out[k] = enUS[k]
    }
  }
  writeFileSync(path.join(OUT_DIR, `${lang}.json`), JSON.stringify(out, null, 2) + '\n')
  const pct = (filled / webKeys.length * 100).toFixed(1)
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
