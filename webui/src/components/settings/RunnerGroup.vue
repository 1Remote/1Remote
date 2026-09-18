<script setup>
/**
 * 运行器分组（Plan 3 Task 6，spec §6；fix batch7 Task D #12 全量自动保存；
 * fix batch7 Task E #13 配置对齐 + 逐运行器字段审计）：
 * GET/PUT /api/settings/runners，改完即存（无保存按钮/dirty 提示）。
 * - 协议页签（6 个：SSH/Telnet/Serial/VNC/SFTP/FTP，键序以 GET 返回为准）× 每协议：
 *   默认运行器下拉（runner 名单）+ runner 卡片列表。
 * - **PascalCase 直通**（有意简化，plan 记录在案）：runners 数组与 GET 原样往返，只字段化编辑
 *   已知属性。Name 不开放改名（重命名牵扯 SelectedRunnerName 与宏引用一致性，归桌面端）。
 * - 内置运行器（#13）：按"属性存在性"渲染配置位，不硬编码 $type 名单——
 *   PuttyRunner：ExePath / 主题下拉（含色块预览）/ 字体 / 字号 / 字符集（对照 WPF
 *   PuttyRunnerSettings.xaml 全量字段）；KittyRunner[Obsolete]：同上但无字体位；
 *   InternalDefaultRunner（VNC/SFTP/FTP）：无可配置字段 → 只读说明。
 *   主题/字体/字符集选项域来自 GET 的 meta（后端同源 PuttyThemes.Themes /
 *   Fonts.SystemFontFamilies / PuttyRunner.CodePages），前端不硬编码。
 * - 外部运行器（$type=ExternalRunner/ExternalRunnerForSSH）字段化编辑（对照 WPF
 *   ExternalRunnerSettings.xaml / ExternalSshRunnerSettings.xaml 审计补齐）：
 *   ExePath / Arguments / ArgumentsForPrivateKey（仅 SSH 族，属性存在性判断）/
 *   EnvironmentVariables / SpecialCharacters（KEY=VALUE 行编辑）/ RunWithHosting。
 * - 环境变量与特殊字符用独立文本域编辑（数组直编输入体验差）：载入时 数组→行文本；
 *   PUT 前 行文本→数组（空行/无 = 的行丢弃）。文本域按 `${proto}:${runner.Name}`
 *   寻址（而非下标）——增删运行器时下标会漂移，Name 在协议内唯一。
 * - PUT 发送整个 protocols 对象（6 协议全量；后端全量预校验，缺失协议=保持，此处全量最稳）；
 * - 自动保存：下拉/开关立即 PUT；文本输入（ExePath/Arguments/行文本域）debounce 500ms 后 PUT；
 *   响应回读带 hasPending 守卫——PUT 飞行中用户又输入时不回填（applyState 会整体替换
 *   protocols/envTexts，防丢字），本地态即真值。
 */
import { computed, h, inject, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'
import { useAutoSave } from '../../composables/useAutoSave'

const { t } = useI18n()
const message = useMessage()

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
  saveDebounced()
}
function onArguments(r, v) {
  r.Arguments = v
  saveDebounced()
}
function onArgsPrivateKey(r, v) {
  r.ArgumentsForPrivateKey = v
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
// 字号（数字输入）：合法值 debounce 保存；清空/null 跳过（保持原值，不发送非法）
function onFontSize(r, v) {
  const n = Number(v)
  if (!Number.isFinite(n) || n < 1) return
  r.PuttyFontSize = Math.round(n)
  saveDebounced()
}
</script>

<template>
  <div class="group">
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

      <!-- 默认运行器：切换即保存 -->
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
      </div>

      <!-- runner 卡片列表 -->
      <div class="r-cards">
        <div v-for="r in activeCfg.runners" :key="r.Name || ''" class="r-card">
          <div class="r-head">
            <span class="r-name" :title="r.Name">{{ r.Name }}</span>
            <span class="r-badge" :class="{ ext: isExternal(r) }">
              {{ isExternal(r) ? t('settings.r.external') : t('settings.r.internal') }}
            </span>
          </div>

          <!-- 内置运行器：按字段存在性渲染配置位（PuTTY/KiTTY），无可配置位则只读说明 -->
          <template v-if="!isExternal(r)">
            <template v-if="hasInternalConfig(r)">
              <div v-if="hasExePath(r)" class="f-row">
                <label>{{ t('editor.f.ExePath') }}</label>
                <n-input
                  size="small"
                  :value="r.ExePath"
                  :input-props="{ spellcheck: false }"
                  @update:value="onExePath(r, $event)"
                />
              </div>
              <div v-if="hasTheme(r)" class="f-row">
                <label>{{ t('settings.r.f.theme') }}</label>
                <div class="theme-wrap">
                  <n-select
                    size="small"
                    :value="r.PuttyThemeName"
                    :options="themeOptions"
                    :render-label="renderThemeLabel"
                    @update:show="shield"
                    @update:value="onSelectField(r, 'PuttyThemeName', $event)"
                  />
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
                <n-select
                  size="small"
                  filterable
                  :value="r.LineCodePage"
                  :options="codePageOptions"
                  @update:show="shield"
                  @update:value="onSelectField(r, 'LineCodePage', $event)"
                />
              </div>
            </template>
            <p v-else class="r-internal-hint">{{ t('settings.r.internalHint') }}</p>
          </template>

          <!-- 外部运行器：已知字段编辑（PascalCase 直通） -->
          <template v-else>
            <div class="f-row">
              <label>{{ t('editor.f.ExePath') }}</label>
              <n-input
                size="small"
                :value="r.ExePath"
                :input-props="{ spellcheck: false }"
                @update:value="onExePath(r, $event)"
              />
            </div>
            <div class="f-row">
              <label>{{ t('settings.r.f.arguments') }}</label>
              <n-input
                size="small"
                type="textarea"
                :rows="2"
                :value="r.Arguments"
                :input-props="{ spellcheck: false }"
                @update:value="onArguments(r, $event)"
              />
            </div>
            <!-- SSH 族外部运行器：私钥登录参数（WPF ExternalSshRunnerSettings 审计补齐） -->
            <div v-if="hasArgsPrivateKey(r)" class="f-row">
              <label>{{ t('settings.r.f.argsPrivateKey') }}</label>
              <n-input
                size="small"
                type="textarea"
                :rows="2"
                :value="r.ArgumentsForPrivateKey"
                :input-props="{ spellcheck: false }"
                @update:value="onArgsPrivateKey(r, $event)"
              />
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
                <p class="f-hint">{{ t('settings.r.f.specialHint') }}</p>
              </div>
            </div>
            <div class="f-row">
              <label>{{ t('editor.f.RunWithHosting') }}</label>
              <n-switch
                size="small"
                :value="!!r.RunWithHosting"
                :loading="autoSaving"
                @update:value="onHosting(r, $event)"
              />
            </div>
          </template>
        </div>
      </div>
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
  grid-template-columns: 100px minmax(0, 300px);
  gap: 10px;
  align-items: center;
  margin-bottom: 12px;
}
.sel-row label {
  font-size: 0.9615rem;
  color: var(--text-2);
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
</style>
