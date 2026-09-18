<script setup>
/**
 * 运行器分组（spec §6）：GET/PUT /api/settings/runners，改完即存（无保存按钮/dirty 提示）。
 * - 协议页签（6 个：SSH/Telnet/Serial/VNC/SFTP/FTP，键序以 GET 返回为准）× 每协议：
 *   默认运行器下拉（runner 名单）+ runner 卡片列表（RunnerCard，内置/外部两种形态）；
 *   「+ 添加运行器」在默认运行器行右端。
 * - **PascalCase 直通**（有意简化，plan 记录在案）：runners 数组与 GET 原样往返，只字段化编辑
 *   已知属性。Name 不开放改名（重命名牵扯 SelectedRunnerName 与宏引用一致性，归桌面端）。
 * - 预设自动填充：ExePath 变化（手输/选择器）时 autoArguments（editor/runnerPresets.js，
 *   WPF ExternalRunnerSettingsViewModel.AutoArguments 前端移植）——仅当 Arguments 为空才填。
 * - 增删：PUT 是全量列表保存，前端构造新行/移除行 + 保存即持久化。添加模态
 *   （RunnerAddModal）只输入名称（WPF CmdAddRunner 的 InputBox 同款：非空 + 协议内唯一），
 *   确认后建默认值运行器并选中新卡（默认运行器下拉指向它 + 滚动入视野 + 短暂高亮——
 *   WPF 里用户在 ListBox 点选新卡后同样会写 SelectedRunnerName）；其余参数（exe 路径/
 *   启动参数）在卡片内继续配置。SSH/SFTP 协议族建 ExternalRunnerForSSH（WPF 同款分支），
 *   其余建 ExternalRunner。删除仅外部运行器（内置无删除钮，徽标 title 提示不可删）；
 *   确认后 splice + selectedRunnerName 回退首项（WPF CmdDeleteRunner 语义）+ 保存。
 * - 环境变量与特殊字符用独立文本域编辑（数组直编输入体验差）：载入时 数组→行文本；
 *   PUT 前 行文本→数组（空行/无 = 的行丢弃）。文本域按 `${proto}:${runner.Name}`
 *   寻址（而非下标）——增删运行器时下标会漂移，Name 在协议内唯一。
 * - PUT 发送整个 protocols 对象（6 协议全量；后端全量预校验，缺失协议=保持，此处全量最稳）。
 * - 自动保存：下拉/开关立即 PUT；文本输入（ExePath/Arguments/行文本域）debounce 500ms 后 PUT；
 *   响应回读带 hasPending 守卫——PUT 飞行中用户又输入时不回填（applyState 会整体替换
 *   protocols/envTexts，防丢字），本地态即真值。增删走 saveNow（离散操作立即保存），
 *   失败 toast 外不做回滚（后端 GET/PUT 语义与 WPF 内存先行一致）。
 */
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { api } from '../../api'
import { useAutoSave } from '../../composables/useAutoSave'
import { useSettingsEsc } from '../../composables/useSettingsEsc'
import { autoArguments, isExternal } from '../../editor/runnerPresets.js'
import HelpLink from '../HelpLink.vue'
import RunnerCard from './RunnerCard.vue'
import RunnerAddModal from './RunnerAddModal.vue'

// 帮助链接：URL 照抄 WPF Hyperlink NavigateUri——运行器文档（External*Settings 各行 (?) 与
// 添加行 (?)，ProtocolRunnerSettingsPageView.xaml:241-247）
const RUNNER_DOC_URL = 'https://1remote.github.io/usage/protocol/runner/'

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()

// 下拉展开计数 + 模态 Esc 截停（Esc 链序见 SettingsView/useSettingsEsc 文件头注释）
const { shield, bindModalEsc } = useSettingsEsc()

const loading = ref(true)
const loadError = ref(false)
const protocols = ref(null) // GET 状态：{ SSH: { selectedRunnerName, runners: [...], macros: [...] } }
const meta = ref(null) // GET meta：{ puttyThemes: [{name, colors}], fonts: [...], codePages: [...] }
const active = ref('') // 当前页签协议键
const envTexts = reactive({}) // `${proto}:${runner.Name}` → 'KEY=VALUE\n…'（环境变量行文本）
const specialTexts = reactive({}) // 同上（SpecialCharacters 行文本）

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
const runnerNames = computed(() => (activeCfg.value?.runners || []).map((r) => r.Name))
const activeMacros = computed(() => activeCfg.value?.macros || [])

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

// ---- RunnerCard 事件接线（写值 + 触发保存；不用模板内联多语句，prettier 折行会破坏表达式） ----
function onRunnerSelect(name) {
  activeCfg.value.selectedRunnerName = name
  saveNow()
}
function onExePath(r, v) {
  r.ExePath = v
  autoArguments(r, active.value) // WPF：ExePath PropertyChanged → AutoArguments（Arguments 为空才填）
  saveDebounced()
}

// ---- exe 路径原生文件选择器：后端弹 WPF OpenFileDialog（filter=exe），404=取消 ----
const browsing = ref(false)
async function browseExe(r) {
  if (browsing.value) return
  browsing.value = true
  try {
    const resp = await api.pickFile('exe|*.exe', { path: r.ExePath })
    if (resp?.path) {
      r.ExePath = resp.path
      autoArguments(r, active.value)
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
// 字号（数字输入，卡片侧已校验 ≥1 整数）：写值 + debounce 保存
function onFontSize(r, n) {
  r.PuttyFontSize = n
  saveDebounced()
}

// ---- 添加运行器（对齐 WPF CmdAddRunner）：模态确认名称 → 建默认值运行器进列表 →
// 选中新卡（下拉指向 + 滚动入视野 + 短暂高亮）→ PUT 全量即存 ----
const adding = ref(false)
const rootEl = ref(null) // 组件根（滚动新卡入视野的查询域）
const justAdded = ref('') // 新建 runner 名：卡片短暂高亮（3s）
let justAddedTimer = null

function onAddSave(name) {
  if (!name) return
  const isSshFamily = active.value === 'SSH' || active.value === 'SFTP'
  // 默认值字段集 = WPF new ExternalRunner/ExternalRunnerForSSH 的序列化形态（ExePath/
  // Arguments 留空，在卡片里配置——含 exe 选择器与预设自动填充）
  const runner = {
    $type: isSshFamily ? 'ExternalRunnerForSSH' : 'ExternalRunner',
    Name: name,
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

// ---- 删除：仅外部运行器；确认 → splice + selectedRunnerName 回退首项（WPF 同款）→ 保存 ----
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

// ---- Esc 链：添加模态开着时捕获截停（SettingsView 返回导航让位）----
bindModalEsc([{ isOpen: () => adding.value, close: () => (adding.value = false) }])
onBeforeUnmount(() => {
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

      <!-- 默认运行器行：切换即保存；「+ 添加运行器」并入本行右端 -->
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
          <n-button size="small" type="primary" @click="adding = true">
            {{ t('settings.r.add') }}
          </n-button>
          <!-- 添加行 (?)：WPF 添加运行器按钮旁 (?) → 运行器文档（url 照抄） -->
          <HelpLink :href="RUNNER_DOC_URL" />
        </span>
      </div>

      <!-- runner 卡片列表（新建卡短暂高亮 flash，3s 后熄灭） -->
      <div class="r-cards">
        <RunnerCard
          v-for="r in activeCfg.runners"
          :key="r.Name || ''"
          :class="{ flash: r.Name === justAdded }"
          :runner="r"
          :macros="activeMacros"
          :meta="meta"
          :saving="autoSaving"
          :browsing="browsing"
          :env-text="envTexts[active + ':' + r.Name] ?? ''"
          :special-text="specialTexts[active + ':' + r.Name] ?? ''"
          @exe-path="onExePath(r, $event)"
          @browse="browseExe(r)"
          @arg="(f, v) => setRunnerArg(r, f, v)"
          @env-text="onEnvText(active, r, $event)"
          @special-text="onSpecialText(active, r, $event)"
          @hosting="onHosting(r, $event)"
          @select-field="(k, v) => onSelectField(r, k, v)"
          @font-size="onFontSize(r, $event)"
          @delete="onDeleteRunner(r)"
        />
      </div>

      <!-- 添加运行器模态：只输入名称（非空 + 协议内唯一），确认后建默认值运行器并选中新卡 -->
      <RunnerAddModal v-model:show="adding" :existing-names="runnerNames" @save="onAddSave" />
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
  /* 第三列：放「+ 添加运行器」按钮与旁侧 (?)，justify-self:end 推到行右端 */
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
  /* 添加按钮与旁侧 (?) 成组右对齐（grid 第三列端对齐的组形态） */
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
</style>
