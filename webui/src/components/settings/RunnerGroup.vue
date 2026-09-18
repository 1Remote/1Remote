<script setup>
/**
 * 运行器分组（Plan 3 Task 6，spec §6；fix batch7 Task D #12 全量自动保存；
 * fix batch7 Task E #13 配置对齐 + 逐运行器字段审计；#14 运行器增删；
 * batch8 Task D 五项：#9 添加按钮并入默认运行器行右侧 / #10 添加流程对齐 WPF（只输名称，
 * 创建后选中）+ exe 路径原生文件选择器（POST /api/files/pick-exe）+ 预设自动填充 + 参数
 * 标签改名 / #11 内置运行器 ExePath 只读 / #12 RunWithHosting 解释文本 / #13 宏 chips）：
 * GET/PUT /api/settings/runners，改完即存（无保存按钮/dirty 提示）。
 * - 协议页签（6 个：SSH/Telnet/Serial/VNC/SFTP/FTP，键序以 GET 返回为准）× 每协议：
 *   默认运行器下拉（runner 名单）+ runner 卡片列表；"+ 添加运行器"在默认运行器行右端（#9）。
 * - **PascalCase 直通**（有意简化，plan 记录在案）：runners 数组与 GET 原样往返，只字段化编辑
 *   已知属性。Name 不开放改名（重命名牵扯 SelectedRunnerName 与宏引用一致性，归桌面端）。
 * - 内置运行器（#13）：按"属性存在性"渲染配置位，不硬编码 $type 名单——
 *   PuttyRunner：ExePath（只读 #11：内置运行器随应用分发，路径由应用管理，WPF 侧可改是
 *   便携部署的历史遗留）/ 主题下拉（含色块预览）/ 字体 / 字号 / 字符集（对照 WPF
 *   PuttyRunnerSettings.xaml 全量字段）；KittyRunner[Obsolete]：同上但无字体位；
 *   InternalDefaultRunner（VNC/SFTP/FTP）：无可配置字段 → 只读说明。
 *   主题/字体/字符集选项域来自 GET 的 meta（后端同源 PuttyThemes.Themes /
 *   Fonts.SystemFontFamilies / PuttyRunner.CodePages），前端不硬编码。
 * - 外部运行器（$type=ExternalRunner/ExternalRunnerForSSH）字段化编辑（对照 WPF
 *   ExternalRunnerSettings.xaml / ExternalSshRunnerSettings.xaml 审计补齐）：
 *   ExePath（+ 浏览按钮 → 原生文件对话框）/ Arguments / ArgumentsForPrivateKey（仅 SSH 族，
 *   属性存在性判断）/ EnvironmentVariables / SpecialCharacters（KEY=VALUE 行编辑）/
 *   RunWithHosting（含 WPF 的 Caution 解释文本与行 ToolTip，#12）。
 * - 预设自动填充（#10）：ExePath 变化（手输/选择器）时移植 WPF
 *   ExternalRunnerSettingsViewModel.AutoArguments——仅当 Arguments 为空才填（不覆盖用户
 *   已填参数），文件名命中 winscp/filezilla/kitty/putty/wt/VpxClient/tvnviewer/vncviewer
 *   时按协议写入 Arguments/ArgumentsForPrivateKey（SSH 族）+ RunWithHosting。预设表在 WPF
 *   是 VM 层逻辑（非后端常量），故前端移植而非经 meta 下发（最小改动，owner 指示核实后的结论）。
 * - 增删（#14 + #10 对齐）：PUT 是全量列表保存，前端构造新行/移除行 + 保存即持久化。
 *   添加模态只输入名称（WPF CmdAddRunner 的 InputBox 同款：非空 + 协议内唯一），确认后建
 *   默认值运行器并选中新卡（默认运行器下拉指向它 + 滚动入视野 + 短暂高亮——WPF 里用户在
 *   ListBox 点选新卡后同样会写 SelectedRunnerName）；其余参数（exe 路径/启动参数）在卡片
 *   内继续配置。SSH/SFTP 协议族建 ExternalRunnerForSSH（WPF 同款分支），其余建 ExternalRunner。
 *   删除仅外部运行器（内置无删除钮，徽标 title 提示不可删）；确认后 splice +
 *   selectedRunnerName 回退首项（WPF CmdDeleteRunner 语义）+ 保存。
 * - 宏 chips（#13）：两处启动参数 textarea 下方渲染当前协议 macros（GET 下发
 *   [{name,description}]）为可点胶囊（title=description），点击插入光标位置
 *   （selectionStart 前插 + 光标移到宏尾 + 聚焦）。
 * - 环境变量与特殊字符用独立文本域编辑（数组直编输入体验差）：载入时 数组→行文本；
 *   PUT 前 行文本→数组（空行/无 = 的行丢弃）。文本域按 `${proto}:${runner.Name}`
 *   寻址（而非下标）——增删运行器时下标会漂移，Name 在协议内唯一。
 * - PUT 发送整个 protocols 对象（6 协议全量；后端全量预校验，缺失协议=保持，此处全量最稳）；
 * - 自动保存：下拉/开关立即 PUT；文本输入（ExePath/Arguments/行文本域）debounce 500ms 后 PUT；
 *   响应回读带 hasPending 守卫——PUT 飞行中用户又输入时不回填（applyState 会整体替换
 *   protocols/envTexts，防丢字），本地态即真值。增删走 saveNow（离散操作立即保存），
 *   失败 toast 外不做回滚（后端 GET/PUT 语义与 WPF 内存先行一致）。
 */
import { computed, h, inject, nextTick, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { api } from '../../api'
import { useAutoSave } from '../../composables/useAutoSave'
import HelpLink from '../HelpLink.vue'

// 帮助链接（batch8 Task F #20）：URL 照抄 WPF Hyperlink NavigateUri——
// 运行器文档（External*Settings 各行 (?) 与添加行 (?)，ProtocolRunnerSettingsPageView.xaml:241-247）
const RUNNER_DOC_URL = 'https://1remote.github.io/usage/protocol/runner/'
// PuTTY 主题站（PuttyRunnerSettings.xaml:101-105 Themes 行 (?)；KittyRunnerSettings 同款；
// Character set 行 WPF 也指向此 URL（xaml:150-154，照抄不改））
const PUTTY_THEMES_URL = 'https://putty.org.ru/themes/'

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()

// 下拉展开计数（SettingsView 的 Esc 返回链序，见 SettingsView 文件头注释；与 GeneralGroup 同款）
const escShield = inject('settingsEscShield', null)
function shield(show) {
  if (escShield) escShield.open += show ? 1 : -1
}

const loading = ref(true)
const loadError = ref(false)
const protocols = ref(null) // GET 状态：{ SSH: { selectedRunnerName, runners: [...], macros: [...] } }
const meta = ref(null) // GET meta：{ puttyThemes: [{name, colors}], fonts: [...], codePages: [...] }
const active = ref('') // 当前页签协议键
const envTexts = reactive({}) // `${proto}:${runner.Name}` → 'KEY=VALUE\n…'（环境变量行文本）
const specialTexts = reactive({}) // 同上（SpecialCharacters 行文本）

const isExternal = (r) => !!r && String(r.$type || '').includes('ExternalRunner')

// ---- 字段存在性探针（PascalCase 直通域按属性渲染，兼容 Putty/Kitty/SSH 族差异）----
const hasExePath = (r) => r.ExePath !== undefined
const hasTheme = (r) => r.PuttyThemeName !== undefined
const hasFont = (r) => r.PuttyFont !== undefined
const hasFontSize = (r) => r.PuttyFontSize !== undefined
const hasCharset = (r) => r.LineCodePage !== undefined
// 内置运行器是否有可配置位（无则退回只读说明行）
const hasInternalConfig = (r) =>
  !isExternal(r) && (hasExePath(r) || hasTheme(r) || hasFont(r) || hasFontSize(r) || hasCharset(r))
// SSH 族外部运行器才有 ArgumentsForPrivateKey（ExternalRunnerForSSH 属性存在性判断）
const hasArgsPrivateKey = (r) => isExternal(r) && r.ArgumentsForPrivateKey !== undefined

function kvToText(arr) {
  return (arr || []).map((kv) => `${kv.Key}=${kv.Value}`).join('\n')
}

function initTexts() {
  for (const key of [...Object.keys(envTexts), ...Object.keys(specialTexts)]) {
    delete envTexts[key]
    delete specialTexts[key]
  }
  if (!protocols.value) return
  for (const [p, cfg] of Object.entries(protocols.value)) {
    for (const r of cfg.runners || []) {
      if (isExternal(r)) {
        envTexts[p + ':' + r.Name] = kvToText(r.EnvironmentVariables)
        specialTexts[p + ':' + r.Name] = kvToText(r.SpecialCharacters)
      }
    }
  }
}

function applyState(p) {
  protocols.value = p
  const keys = Object.keys(p || {})
  if (!active.value || !keys.includes(active.value)) active.value = keys[0] || ''
  initTexts()
}

onMounted(async () => {
  try {
    const r = await api.getRunners()
    meta.value = r.meta || null
    applyState(r.protocols || {})
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})

const protocolKeys = computed(() => Object.keys(protocols.value || {}))
const activeCfg = computed(() => protocols.value?.[active.value] || null)

const runnerOptions = computed(() => (activeCfg.value?.runners || []).map((r) => ({ value: r.Name, label: r.Name })))

// 行文本 → 数组（发送前同步回 runner 对象）：空行与无 = 的行丢弃；= 后可空
function parseKvText(text) {
  return String(text || '')
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean)
    .map((l) => {
      const eq = l.indexOf('=')
      return eq <= 0 ? null : { Key: l.slice(0, eq), Value: l.slice(eq + 1) }
    })
    .filter(Boolean)
}

// ---- 选项域（meta 提供；缺 meta（旧后端/请求失败）时下拉为空，值仍可显示与保存）----
const themeOptions = computed(() =>
  (meta.value?.puttyThemes || []).map((x) => ({ value: x.name, label: x.name, theme: x }))
)
const fontOptions = computed(() => (meta.value?.fonts || []).map((f) => ({ value: f, label: f })))
const codePageOptions = computed(() => (meta.value?.codePages || []).map((c) => ({ value: c, label: c })))

function themeColors(name) {
  return (meta.value?.puttyThemes || []).find((x) => x.name === name)?.colors || null
}

// 主题下拉选项：色点行（bg/fg/绿/红 四点，与预览条同键位）——WPF 无对应物，web 简化预览
function renderThemeLabel(option) {
  const c = option.theme?.colors || {}
  const dots = [c.bg || '#000', c.fg || '#bbb', c.green || '#55ff55', c.red || '#ff5555']
  return h('span', { class: 'theme-opt' }, [
    ...dots.map((d) => h('i', { class: 'theme-dot', style: { background: d } })),
    h('span', null, option.label),
  ])
}

// ---- 自动保存：PUT 全量 protocols（发送前同步行文本域），成功静默、失败 toast ----
const {
  saving: autoSaving,
  dispose: disposeAutoSave,
  saveNow,
  saveDebounced,
  hasPending,
} = useAutoSave(
  async () => {
    // 行文本域 → 数组（发送前同步，仅该 runner 有本地文本态时写入，避免覆盖 winscp 自动补全等旁路写入）
    for (const [p, cfg] of Object.entries(protocols.value)) {
      for (const r of cfg.runners || []) {
        if (!isExternal(r)) continue
        const k = p + ':' + r.Name
        if (k in envTexts) r.EnvironmentVariables = parseKvText(envTexts[k])
        if (k in specialTexts) r.SpecialCharacters = parseKvText(specialTexts[k])
      }
    }
    const r = await api.saveRunners(protocols.value)
    // 回读替换本地态保证 GET→PUT→GET 稳定；飞行中又有输入则跳过（防丢字，见文件头）
    if (!hasPending()) {
      meta.value = r.meta || meta.value
      applyState(r.protocols || {})
    }
  },
  {
    onError: (e) => {
      const detail = e?.body?.errors?.join('; ')
      message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
    },
  }
)
onBeforeUnmount(disposeAutoSave)

// ---- 控件 handler（写值 + 触发保存；不用模板内联多语句，prettier 折行会破坏表达式） ----
function onRunnerSelect(name) {
  activeCfg.value.selectedRunnerName = name
  saveNow()
}
function onExePath(r, v) {
  r.ExePath = v
  autoArguments(r) // WPF：ExePath PropertyChanged → AutoArguments（Arguments 为空才填）
  saveDebounced()
}

// ---- 预设自动填充（#10）：WPF ExternalRunnerSettingsViewModel.AutoArguments 前端移植 ----
// 触发条件与 WPF 一致：Arguments 为空才填（用户已填参数不覆盖）；文件名（含目录的完整
// 路径取最后一段）忽略大小写子串命中——WPF 用 FileInfo(path).Name.IndexOf(...)，此处
// split 等价。命中分支按 WPF 顺序互斥（WPF 靠"Arguments 已非空"短路后续分支，此处 return）。
// ArgumentsForPrivateKey 仅 SSH 族 runner 有该属性（hasArgsPrivateKey 探针）。
function autoArguments(r) {
  if (String(r.Arguments || '') !== '') return
  const p = r.OwnerProtocolName || active.value
  const name =
    String(r.ExePath || '')
      .split(/[\\/]/)
      .pop() || ''
  const lower = name.toLowerCase()
  const setPk = (v) => {
    if (hasArgsPrivateKey(r)) r.ArgumentsForPrivateKey = v
  }
  if (lower.includes('winscp')) {
    if (p === 'FTP') r.Arguments = 'ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%'
    else if (p === 'SFTP') {
      r.Arguments = 'sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%'
      setPk('sftp://%1RM_USERNAME%@%1RM_HOSTNAME%:%1RM_PORT% /privatekey=%1RM_PRIVATE_KEY_PATH%')
    }
    r.RunWithHosting = true
    return
  }
  if (lower.includes('filezilla')) {
    if (p === 'FTP') r.Arguments = 'ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%'
    else if (p === 'SFTP') r.Arguments = 'sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%'
    r.RunWithHosting = false
    return
  }
  if (p === 'SSH' && lower.includes('kitty')) {
    r.Arguments =
      '-ssh %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION% -cmd "%STARTUP_AUTO_COMMAND%"'
    setPk('') // WPF：kitty 私钥参数 NOT SUPPORTED
    r.RunWithHosting = true
    return
  }
  if (p === 'SSH' && lower.includes('putty')) {
    r.Arguments = '-ssh %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION%'
    setPk('') // WPF：putty 私钥参数 NOT SUPPORTED
    r.RunWithHosting = true
    return
  }
  if (lower === 'wt.exe' || lower === 'wt') {
    if (p === 'SSH') {
      r.Arguments =
        '-w 1 new-tab --title "%1RM_HOSTNAME%" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -pw %1RM_PASSWORD%'
      if (String(r.ArgumentsForPrivateKey || '') === '')
        setPk(
          '-w 1 new-tab --title "%1RM_HOSTNAME%" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -i %1RM_PRIVATE_KEY_PATH%'
        )
    }
    r.RunWithHosting = false
    return
  }
  if (p === 'VNC' && lower.includes('vpxclient')) {
    r.Arguments = '-s %1RM_HOSTNAME% -u %1RM_USERNAME% -p %1RM_PASSWORD%'
    r.RunWithHosting = true
    return
  }
  if (p === 'VNC' && lower.includes('tvnviewer')) {
    r.Arguments = '%1RM_HOSTNAME%::%1RM_PORT% -password=%1RM_PASSWORD% -scale=auto'
    r.RunWithHosting = true
    return
  }
  if (p === 'VNC' && (lower.includes('vncviewer') || lower.includes('uvnc'))) {
    r.Arguments = '%1RM_HOSTNAME%:%1RM_PORT% -password=%1RM_PASSWORD%'
    r.RunWithHosting = false
  }
}

// ---- exe 路径原生文件选择器（#10）：后端弹 WPF OpenFileDialog（Filter=exe），404=取消 ----
const browsing = ref(false)
async function browseExe(r) {
  if (browsing.value) return
  browsing.value = true
  try {
    const resp = await api.pickExe(r.ExePath)
    if (resp?.path) {
      r.ExePath = resp.path
      autoArguments(r)
      saveNow() // 离散选择：立即保存
    }
  } catch (e) {
    if (e?.status !== 404) message.error(t('settings.r.pickFailed')) // 404=用户取消，静默
  } finally {
    browsing.value = false
  }
}

// 启动参数（Arguments/ArgumentsForPrivateKey 共用）：写值 + debounce 保存
function setRunnerArg(r, field, v) {
  r[field] = v
  saveDebounced()
}

// ---- 宏 chips（#13）：点击插入光标位置（textarea selectionStart 前插、光标移宏尾、聚焦） ----
// n-input 组件实例按 `${proto}:${name}:${field}` 收集（v-for 函数 ref）；$el 是外层 div，
// textarea 在其内。取不到 DOM（理论不可达）时退化为尾部追加。
const argAreaRefs = {}
function setArgRef(comp, key) {
  if (comp) argAreaRefs[key] = comp
  else delete argAreaRefs[key]
}
function insertMacro(r, field, macro) {
  const cur = String(r[field] ?? '')
  const comp = argAreaRefs[active.value + ':' + r.Name + ':' + field]
  const el = comp?.$el?.querySelector?.('textarea')
  if (!el) {
    setRunnerArg(r, field, cur + macro.name)
    return
  }
  const s = el.selectionStart ?? cur.length
  const e = el.selectionEnd ?? s
  setRunnerArg(r, field, cur.slice(0, s) + macro.name + cur.slice(e))
  nextTick(() => {
    el.focus()
    const pos = s + macro.name.length
    el.setSelectionRange(pos, pos)
  })
}

function onEnvText(p, r, v) {
  envTexts[p + ':' + r.Name] = v
  saveDebounced()
}
function onSpecialText(p, r, v) {
  specialTexts[p + ':' + r.Name] = v
  saveDebounced()
}
function onHosting(r, v) {
  r.RunWithHosting = v
  saveNow()
}
// 内置运行器下拉位（主题/字体/字符集）：写值即存
function onSelectField(r, key, v) {
  r[key] = v
  saveNow()
}
// 字号（数字输入）：合法值 debounce 保存；清空/null 跳过（保持原值，不发送非法）
function onFontSize(r, v) {
  const n = Number(v)
  if (!Number.isFinite(n) || n < 1) return
  r.PuttyFontSize = Math.round(n)
  saveDebounced()
}

// ---- 添加运行器（#14 + batch8 #10 对齐 WPF CmdAddRunner）：模态只输入名称 →
// 建默认值运行器进列表 → 选中新卡（下拉指向 + 滚动入视野 + 短暂高亮）→ PUT 全量即存 ----
const adding = ref(false)
const addForm = reactive({ name: '' })
const rootEl = ref(null) // 组件根（滚动新卡入视野的查询域）
const justAdded = ref('') // 新建 runner 名：卡片短暂高亮（3s）
let justAddedTimer = null

// 打开即重置（与 DataSourceGroup.openAdd 同款）：上次未提交的草稿不带入新会话
function openAdd() {
  addForm.name = ''
  adding.value = true
}

// 名称校验 = WPF CmdAddRunner 的 InputBox 规则：非空 + 协议内唯一
const addNameError = computed(() => {
  const n = addForm.name.trim()
  if (!n) return t('settings.r.nameRequired')
  if ((activeCfg.value?.runners || []).some((r) => r.Name === n)) return t('settings.r.nameExists', { name: n })
  return ''
})
const addValid = computed(() => !addNameError.value)
const activeMacros = computed(() => activeCfg.value?.macros || [])

function addSave() {
  if (!addValid.value) return
  const isSshFamily = active.value === 'SSH' || active.value === 'SFTP'
  // 默认值字段集 = WPF new ExternalRunner/ExternalRunnerForSSH 的序列化形态（ExePath/
  // Arguments 留空，在卡片里配置——含 exe 选择器与预设自动填充）
  const runner = {
    $type: isSshFamily ? 'ExternalRunnerForSSH' : 'ExternalRunner',
    Name: addForm.name.trim(),
    OwnerProtocolName: active.value,
    ExePath: '',
    Arguments: '',
    RunWithHosting: false,
    EnvironmentVariables: [],
    SpecialCharacters: [],
  }
  if (isSshFamily) runner.ArgumentsForPrivateKey = ''
  activeCfg.value.runners.push(runner)
  envTexts[active.value + ':' + runner.Name] = ''
  specialTexts[active.value + ':' + runner.Name] = ''
  // 选中新运行器：WPF 里用户在 ListBox 点选新卡后 SelectedRunner 会写回
  // c.SelectedRunnerName——web 无卡片单选态，以默认运行器下拉指向新卡为等效"选中"
  activeCfg.value.selectedRunnerName = runner.Name
  adding.value = false
  justAdded.value = runner.Name
  clearTimeout(justAddedTimer)
  justAddedTimer = setTimeout(() => (justAdded.value = ''), 3000)
  nextTick(() => rootEl.value?.querySelector('.r-card.flash')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' }))
  saveNow()
}

// 添加模态表单回车=保存（与保存按钮同守卫：名称校验未过不动作）：输入框聚焦回车提交；
// textarea（参数）回车=换行、按钮回车=原生 click，均不代提交；IME 组合中的回车不触发
function onFormEnter(e) {
  if (e.key !== 'Enter' || e.isComposing) return
  if (e.target?.closest?.('button, textarea, .n-select')) return
  e.preventDefault()
  addSave()
}

// ---- 删除（#14）：仅外部运行器；确认 → splice + selectedRunnerName 回退首项（WPF 同款）→ 保存 ----
function onDeleteRunner(r) {
  dialog.warning({
    title: t('settings.r.deleteTitle'),
    content: t('settings.r.deleteConfirm', { name: r.Name }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    onPositiveClick: () => {
      const cfg = activeCfg.value
      const idx = cfg.runners.indexOf(r)
      if (idx < 0) return
      cfg.runners.splice(idx, 1)
      if (cfg.selectedRunnerName === r.Name) cfg.selectedRunnerName = cfg.runners[0]?.Name || ''
      delete envTexts[active.value + ':' + r.Name]
      delete specialTexts[active.value + ':' + r.Name]
      saveNow()
    },
  })
}

// ---- Esc 链：添加模态开着时捕获截停（SettingsView 返回导航让位，DataSourceGroup 同款）----
function onEscCapture(e) {
  if (e.key !== 'Escape') return
  if (escShield && escShield.open > 0) return
  if (adding.value) {
    e.stopPropagation()
    adding.value = false
  }
}
onMounted(() => window.addEventListener('keydown', onEscCapture, true))
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onEscCapture, true)
  clearTimeout(justAddedTimer)
})
</script>

<template>
  <div ref="rootEl" class="group">
    <p v-if="loading" class="hint">{{ t('settings.loading') }}</p>
    <p v-else-if="loadError" class="hint err">{{ t('settings.r.loadFailed') }}</p>
    <template v-else-if="activeCfg">
      <!-- 协议页签（键序=后端返回）：协议名为专有名词，不翻译 -->
      <div class="r-tabs">
        <button
          v-for="p in protocolKeys"
          :key="p"
          type="button"
          class="r-tab"
          :class="{ active: p === active }"
          @click="active = p"
        >
          {{ p }}
        </button>
      </div>

      <!-- 默认运行器行（#9）：切换即保存；"+ 添加运行器"并入本行右端（原独立工具条） -->
      <div class="sel-row">
        <label>{{ t('settings.r.selected') }}</label>
        <n-select
          class="sel-select"
          size="small"
          :value="activeCfg.selectedRunnerName"
          :options="runnerOptions"
          @update:show="shield"
          @update:value="onRunnerSelect"
        />
        <span class="sel-add">
          <n-button size="small" type="primary" @click="openAdd">
            {{ t('settings.r.add') }}
          </n-button>
          <!-- 添加行 (?)（#20）：WPF 添加运行器按钮旁 (?) → 运行器文档（url 照抄） -->
          <HelpLink :href="RUNNER_DOC_URL" />
        </span>
      </div>

      <!-- runner 卡片列表（新建卡短暂高亮 flash，3s 后熄灭） -->
      <div class="r-cards">
        <div v-for="r in activeCfg.runners" :key="r.Name || ''" class="r-card" :class="{ flash: r.Name === justAdded }">
          <div class="r-head">
            <span class="r-name" :title="r.Name">{{ r.Name }}</span>
            <span
              class="r-badge"
              :class="{ ext: isExternal(r) }"
              :title="isExternal(r) ? '' : t('settings.r.internalNoDelete')"
            >
              {{ isExternal(r) ? t('settings.r.external') : t('settings.r.internal') }}
            </span>
            <!-- 删除（#14）：仅外部运行器；内置无删除钮（徽标 title 提示不可删） -->
            <button
              v-if="isExternal(r)"
              class="del"
              type="button"
              :title="t('settings.r.deleteTitle')"
              @click="onDeleteRunner(r)"
            >
              ×
            </button>
          </div>

          <!-- 内置运行器：按字段存在性渲染配置位（PuTTY/KiTTY），无可配置位则只读说明 -->
          <template v-if="!isExternal(r)">
            <template v-if="hasInternalConfig(r)">
              <!-- 内置运行器 ExePath 只读（#11）：路径由应用管理（随应用分发/部署），WPF 侧
                   可改是历史遗留，web 端按 owner 验收决定收为只读展示（title 说明悬停可见） -->
              <div v-if="hasExePath(r)" class="f-row exe-readonly" :title="t('settings.r.internalExeManaged')">
                <label>{{ t('editor.f.ExePath') }}</label>
                <n-input size="small" :value="r.ExePath" disabled :input-props="{ spellcheck: false }" />
              </div>
              <div v-if="hasTheme(r)" class="f-row">
                <label>{{ t('settings.r.f.theme') }}</label>
                <div class="theme-wrap">
                  <div class="theme-select-row">
                    <n-select
                      size="small"
                      :value="r.PuttyThemeName"
                      :options="themeOptions"
                      :render-label="renderThemeLabel"
                      @update:show="shield"
                      @update:value="onSelectField(r, 'PuttyThemeName', $event)"
                    />
                    <!-- 主题行 (?)（#20）：WPF PuttyRunnerSettings.xaml:101-105 / KittyRunnerSettings
                         :88 → 主题站（url 照抄） -->
                    <HelpLink :href="PUTTY_THEMES_URL" />
                  </div>
                  <!-- 主题预览：与 WPF 预览同键位的色块文本行（Colour2 底 / Colour11·15·9·0 前景） -->
                  <div
                    v-if="themeColors(r.PuttyThemeName)"
                    class="theme-preview"
                    :style="{ background: themeColors(r.PuttyThemeName).bg || '#000' }"
                  >
                    <span :style="{ color: themeColors(r.PuttyThemeName).green || '#55ff55' }">1Remote</span>
                    <span :style="{ color: themeColors(r.PuttyThemeName).white || '#ffffff' }">version.cpp</span>
                    <span :style="{ color: themeColors(r.PuttyThemeName).red || '#ff5555' }">data.zip</span>
                    <span :style="{ color: themeColors(r.PuttyThemeName).fg || '#bbbbbb' }">root@remote:~$</span>
                  </div>
                </div>
              </div>
              <div v-if="hasFont(r)" class="f-row">
                <label>{{ t('settings.r.f.font') }}</label>
                <n-select
                  size="small"
                  filterable
                  :value="r.PuttyFont"
                  :options="fontOptions"
                  @update:show="shield"
                  @update:value="onSelectField(r, 'PuttyFont', $event)"
                />
              </div>
              <div v-if="hasFontSize(r)" class="f-row">
                <label>{{ t('settings.r.f.fontSize') }}</label>
                <n-input-number
                  class="num-input"
                  size="small"
                  :min="1"
                  :value="r.PuttyFontSize"
                  :show-button="false"
                  @update:value="onFontSize(r, $event)"
                />
              </div>
              <div v-if="hasCharset(r)" class="f-row">
                <label>{{ t('settings.r.f.charset') }}</label>
                <div class="charset-wrap">
                  <n-select
                    size="small"
                    filterable
                    :value="r.LineCodePage"
                    :options="codePageOptions"
                    @update:show="shield"
                    @update:value="onSelectField(r, 'LineCodePage', $event)"
                  />
                  <!-- 字符集行 (?)（#20）：WPF Putty/KittyRunnerSettings Character set 行 (?)
                       ——WPF 该链接同样指向主题站 URL（原样照抄，不代为修正） -->
                  <HelpLink :href="PUTTY_THEMES_URL" />
                </div>
              </div>
            </template>
            <p v-else class="r-internal-hint">{{ t('settings.r.internalHint') }}</p>
          </template>

          <!-- 外部运行器：已知字段编辑（PascalCase 直通） -->
          <template v-else>
            <div class="f-row">
              <label>{{ t('editor.f.ExePath') }}</label>
              <div class="exe-wrap">
                <n-input
                  size="small"
                  :value="r.ExePath"
                  :input-props="{ spellcheck: false }"
                  @update:value="onExePath(r, $event)"
                />
                <!-- 原生文件选择器（#10）：后端 WPF OpenFileDialog（Filter=exe），WPF
                     CmdSelectExePath 同款交互；选中后触发预设自动填充 -->
                <button class="act-btn" type="button" :disabled="browsing" @click="browseExe(r)">
                  {{ t('settings.r.f.browse') }}
                </button>
              </div>
            </div>
            <div class="f-row">
              <label>
                {{ t('settings.r.f.arguments') }}
                <!-- 参数行 (?)（#20）：WPF External(SSH)RunnerSettings Arguments 行 (?) →
                     运行器文档（url 照抄）；WPF 同行的 (i) 宏说明弹窗由宏 chips 的
                     title=描述承载（batch8 #13），不再重复 -->
                <HelpLink :href="RUNNER_DOC_URL" />
              </label>
              <div class="arg-wrap">
                <n-input
                  size="small"
                  type="textarea"
                  :rows="2"
                  :value="r.Arguments"
                  :input-props="{ spellcheck: false }"
                  :ref="(c) => setArgRef(c, active + ':' + r.Name + ':Arguments')"
                  @update:value="setRunnerArg(r, 'Arguments', $event)"
                />
                <!-- 宏 chips（#13）：点击插入光标位置，title=宏描述 -->
                <div v-if="activeMacros.length" class="macro-row">
                  <span class="macro-row-label">{{ t('settings.r.f.macroHint') }}</span>
                  <button
                    v-for="m in activeMacros"
                    :key="m.name"
                    class="macro-pill"
                    type="button"
                    :title="m.description"
                    @click="insertMacro(r, 'Arguments', m)"
                  >
                    {{ m.name }}
                  </button>
                </div>
              </div>
            </div>
            <!-- SSH 族外部运行器：私钥登录参数（WPF ExternalSshRunnerSettings 审计补齐） -->
            <div v-if="hasArgsPrivateKey(r)" class="f-row">
              <label>
                {{ t('settings.r.f.argsPrivateKey') }}
                <!-- 私钥参数行 (?)（#20）：WPF ExternalSshRunnerSettings:216 同款（url 照抄） -->
                <HelpLink :href="RUNNER_DOC_URL" />
              </label>
              <div class="arg-wrap">
                <n-input
                  size="small"
                  type="textarea"
                  :rows="2"
                  :value="r.ArgumentsForPrivateKey"
                  :input-props="{ spellcheck: false }"
                  :ref="(c) => setArgRef(c, active + ':' + r.Name + ':ArgumentsForPrivateKey')"
                  @update:value="setRunnerArg(r, 'ArgumentsForPrivateKey', $event)"
                />
                <div v-if="activeMacros.length" class="macro-row">
                  <span class="macro-row-label">{{ t('settings.r.f.macroHint') }}</span>
                  <button
                    v-for="m in activeMacros"
                    :key="m.name"
                    class="macro-pill"
                    type="button"
                    :title="m.description"
                    @click="insertMacro(r, 'ArgumentsForPrivateKey', m)"
                  >
                    {{ m.name }}
                  </button>
                </div>
              </div>
            </div>
            <div class="f-row">
              <label>{{ t('settings.r.f.env') }}</label>
              <div class="env-wrap">
                <n-input
                  size="small"
                  type="textarea"
                  :rows="2"
                  :value="envTexts[active + ':' + r.Name] ?? ''"
                  :input-props="{ spellcheck: false }"
                  :placeholder="t('settings.r.f.envHint')"
                  @update:value="onEnvText(active, r, $event)"
                />
                <p class="f-hint">{{ t('settings.r.f.envHint') }}</p>
              </div>
            </div>
            <!-- 特殊字符（%XX 转义，WPF ExternalRunnerSettings 审计补齐） -->
            <div class="f-row">
              <label>{{ t('settings.r.f.special') }}</label>
              <div class="env-wrap">
                <n-input
                  size="small"
                  type="textarea"
                  :rows="2"
                  :value="specialTexts[active + ':' + r.Name] ?? ''"
                  :input-props="{ spellcheck: false }"
                  :placeholder="t('settings.r.f.envHint')"
                  @update:value="onSpecialText(active, r, $event)"
                />
                <p class="f-hint">
                  {{ t('settings.r.f.specialHint') }}
                  <!-- 特殊字符行 (?)（#20）：WPF External*RunnerSettings 转义说明旁 (?)
                       → 运行器文档（url 照抄） -->
                  <HelpLink :href="RUNNER_DOC_URL" />
                </p>
              </div>
            </div>
            <!-- 集成到标签页（#12）：开关与输入框同列对齐；解释文本移植 WPF
                 ExternalRunnerSettings.xaml 的 Caution 词条（14 语言），行 ToolTip=WPF 同名词条 -->
            <div class="f-row" :title="t('settings.r.f.hostingTitle')">
              <label>{{ t('editor.f.RunWithHosting') }}</label>
              <div class="hosting-wrap">
                <n-switch
                  size="small"
                  :value="!!r.RunWithHosting"
                  :loading="autoSaving"
                  @update:value="onHosting(r, $event)"
                />
                <span class="hosting-hint">{{ t('settings.r.f.hostingHint') }}</span>
              </div>
            </div>
          </template>
        </div>
      </div>

      <!-- 添加运行器模态（batch8 #10 对齐 WPF CmdAddRunner）：只输入名称（非空 + 协议内唯一），
           确认后建默认值运行器并选中新卡——exe 路径/启动参数等在卡片内继续配置 -->
      <n-modal
        v-model:show="adding"
        preset="card"
        :title="t('settings.r.addTitle')"
        :bordered="false"
        :style="{ width: 'min(520px, 92vw)' }"
        role="dialog"
        aria-modal="true"
      >
        <div class="form" @keydown="onFormEnter">
          <div class="f-row">
            <label>{{ t('editor.f.Name') }}</label>
            <div>
              <n-input
                size="small"
                v-model:value="addForm.name"
                :status="addNameError ? 'error' : undefined"
                :input-props="{ spellcheck: false }"
              />
              <p v-if="addNameError" class="f-err">{{ addNameError }}</p>
            </div>
          </div>
        </div>
        <template #footer>
          <div class="modal-actions">
            <n-button size="small" @click="adding = false">{{ t('editor.cancel') }}</n-button>
            <n-button size="small" type="primary" :disabled="!addValid" @click="addSave">
              {{ t('editor.save') }}
            </n-button>
          </div>
        </template>
      </n-modal>
    </template>
  </div>
</template>

<style scoped>
.group {
  max-width: 720px;
}
.hint {
  font-size: 0.9615rem;
  color: var(--text-3);
}
.hint.err {
  color: var(--danger);
}
.r-tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 12px;
  border-bottom: 1px solid var(--border);
  padding-bottom: 8px;
  flex-wrap: wrap;
}
.r-tab {
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--text-3);
  font-size: 0.9615rem;
  padding: 5px 12px;
  cursor: pointer;
}
.r-tab:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.r-tab.active {
  background: var(--accent-container);
  color: var(--accent-text);
}
.sel-row {
  display: grid;
  /* 第三列（#9）：放"+ 添加运行器"按钮，justify-self:end 推到行右端 */
  grid-template-columns: 100px minmax(0, 300px) 1fr;
  gap: 10px;
  align-items: center;
  margin-bottom: 12px;
}
.sel-row label {
  font-size: 0.9615rem;
  color: var(--text-2);
}
.sel-add {
  /* #9+#20：添加按钮与旁侧 (?) 成组右对齐（grid 第三列端对齐的组形态） */
  justify-self: end;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.r-cards {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.r-card {
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-elevated);
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: border-color 0.4s;
}
/* 新建卡短暂高亮（#10：创建并选中新卡）——justAdded 3s 后熄灭，transition 平滑回落 */
.r-card.flash {
  border-color: var(--accent);
}
.r-head {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.r-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.9615rem;
  color: var(--text-1);
}
.r-badge {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 1px 5px;
  font-size: 0.8077rem;
  color: var(--text-4);
}
.r-badge.ext {
  border-color: var(--accent);
  color: var(--accent-text);
}
.r-head .del {
  margin-left: auto;
  flex: 0 0 auto;
  width: 22px;
  height: 22px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: transparent;
  color: var(--text-3);
  font-size: 0.9615rem;
  line-height: 1;
  cursor: pointer;
}
.r-head .del:hover {
  border-color: var(--danger);
  background: var(--bg-hover);
  color: var(--danger);
}
.r-internal-hint {
  margin: 0;
  font-size: 0.8846rem;
  color: var(--text-4);
}
.f-row {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}
.f-row label {
  font-size: 0.9231rem;
  color: var(--text-2);
}
.f-hint {
  margin: 4px 0 0;
  font-size: 0.8462rem;
  color: var(--text-4);
}
.num-input {
  width: 120px;
}
.env-wrap {
  min-width: 0;
}

/* ---- exe 路径行：输入框 + 浏览按钮（#10）---- */
.exe-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}
.exe-wrap .n-input {
  flex: 1 1 auto;
  min-width: 0;
}
.act-btn {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 0.9231rem;
  line-height: 1;
  padding: 6px 10px;
  cursor: pointer;
}
.act-btn:hover:not(:disabled) {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}
.act-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

/* ---- 启动参数行：textarea + 宏 chips（#13）---- */
.arg-wrap {
  min-width: 0;
}
.macro-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  margin-top: 5px;
}
.macro-row-label {
  font-size: 0.8077rem;
  color: var(--text-4);
  margin-right: 2px;
}
.macro-pill {
  border: 1px solid var(--border);
  border-radius: 999px;
  background: transparent;
  color: var(--text-3);
  font-size: 0.8077rem;
  line-height: 1.5;
  padding: 1px 8px;
  cursor: pointer;
}
.macro-pill:hover {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}

/* ---- 集成到标签页行（#12）：开关与解释文本同列 ---- */
.hosting-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}
.hosting-hint {
  font-size: 0.8462rem;
  color: var(--text-4);
}

/* ---- 主题下拉选项色点 + 预览条 ---- */
.theme-opt {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.theme-opt .theme-dot {
  width: 10px;
  height: 10px;
  border-radius: 3px;
  border: 1px solid var(--border);
  flex: 0 0 auto;
}
.theme-wrap {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
/* 主题下拉 + (?) 帮助并排（#20） */
.theme-select-row {
  display: flex;
  align-items: center;
  gap: 6px;
}
.theme-select-row .n-select {
  flex: 1 1 auto;
  min-width: 0;
}
/* 字符集下拉 + (?) 帮助并排（#20） */
.charset-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}
.charset-wrap .n-select {
  flex: 1 1 auto;
  min-width: 0;
}
.theme-preview {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  padding: 8px 10px;
  border-radius: 6px;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 0.8462rem;
  line-height: 1.5;
}

/* ---- 添加运行器模态 ---- */
.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.form .f-row {
  align-items: center;
}
.f-err {
  margin: 4px 0 0;
  font-size: 0.8462rem;
  color: var(--danger);
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
