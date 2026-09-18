<script setup>
/**
 * 运行器分组（Plan 3 Task 6，spec §6；fix batch7 Task D #12 全量自动保存）：
 * GET/PUT /api/settings/runners，改完即存（无保存按钮/dirty 提示）。
 * - 协议页签（6 个：SSH/Telnet/Serial/VNC/SFTP/FTP，键序以 GET 返回为准）× 每协议：
 *   默认运行器下拉（runner 名单）+ runner 卡片列表。
 * - **PascalCase 直通**（有意简化，plan 记录在案）：runners 数组与 GET 原样往返，只字段化编辑
 *   已知属性——外部运行器（$type=ExternalRunner/ExternalRunnerForSSH）：ExePath / Arguments /
 *   EnvironmentVariables（KEY=VALUE 行编辑）/ RunWithHosting；内置运行器（InternalDefaultRunner/
 *   PuttyRunner/KittyRunner/Runner）只读展示说明。Name 不开放改名（重命名牵扯 SelectedRunnerName
 *   与宏引用一致性，归桌面端）。
 * - 环境变量用独立文本域编辑（数组直编输入体验差）：载入时 数组→行文本；PUT 前 行文本→数组
 *   （空行/无 = 的行丢弃）。
 * - PUT 发送整个 protocols 对象（6 协议全量；后端全量预校验，缺失协议=保持，此处全量最稳）；
 * - 自动保存：下拉/开关立即 PUT；文本输入（ExePath/Arguments/环境变量）debounce 500ms 后 PUT；
 *   响应回读带 hasPending 守卫——PUT 飞行中用户又输入时不回填（applyState 会整体替换
 *   protocols/envTexts，防丢字），本地态即真值。
 */
import { computed, inject, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
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
const protocols = ref(null) // GET 状态：{ SSH: { selectedRunnerName, runners: [...] } }
const active = ref('') // 当前页签协议键
const envTexts = reactive({}) // `${proto}:${index}` → 'KEY=VALUE\n…'（外部运行器环境变量行文本）

const isExternal = (r) => !!r && String(r.$type || '').includes('ExternalRunner')

function initEnvTexts() {
  for (const key of Object.keys(envTexts)) delete envTexts[key]
  if (!protocols.value) return
  for (const [p, cfg] of Object.entries(protocols.value)) {
    ;(cfg.runners || []).forEach((r, i) => {
      if (isExternal(r))
        envTexts[p + ':' + i] = (r.EnvironmentVariables || []).map((kv) => `${kv.Key}=${kv.Value}`).join('\n')
    })
  }
}

function applyState(p) {
  protocols.value = p
  const keys = Object.keys(p || {})
  if (!active.value || !keys.includes(active.value)) active.value = keys[0] || ''
  initEnvTexts()
}

onMounted(async () => {
  try {
    const r = await api.getRunners()
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
function parseEnvText(text) {
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

// ---- 自动保存：PUT 全量 protocols（发送前同步环境变量文本域），成功静默、失败 toast ----
const {
  saving: autoSaving,
  dispose: disposeAutoSave,
  saveNow,
  saveDebounced,
  hasPending,
} = useAutoSave(
  async () => {
    // 环境变量文本域 → 数组（发送前同步，仅在文本非空或原有条目时写入）
    for (const [p, cfg] of Object.entries(protocols.value)) {
      ;(cfg.runners || []).forEach((r, i) => {
        if (isExternal(r)) r.EnvironmentVariables = parseEnvText(envTexts[p + ':' + i])
      })
    }
    const r = await api.saveRunners(protocols.value)
    // 回读替换本地态保证 GET→PUT→GET 稳定；飞行中又有输入则跳过（防丢字，见文件头）
    if (!hasPending()) applyState(r.protocols || {})
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
function onEnvText(p, i, v) {
  envTexts[p + ':' + i] = v
  saveDebounced()
}
function onHosting(r, v) {
  r.RunWithHosting = v
  saveNow()
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
        <div v-for="(r, i) in activeCfg.runners" :key="(r.Name || '') + ':' + i" class="r-card">
          <div class="r-head">
            <span class="r-name" :title="r.Name">{{ r.Name }}</span>
            <span class="r-badge" :class="{ ext: isExternal(r) }">
              {{ isExternal(r) ? t('settings.r.external') : t('settings.r.internal') }}
            </span>
          </div>

          <!-- 内置运行器：说明行（无编辑） -->
          <p v-if="!isExternal(r)" class="r-internal-hint">{{ t('settings.r.internalHint') }}</p>

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
            <div class="f-row">
              <label>{{ t('settings.r.f.env') }}</label>
              <div class="env-wrap">
                <n-input
                  size="small"
                  type="textarea"
                  :rows="2"
                  :value="envTexts[active + ':' + i] ?? ''"
                  :input-props="{ spellcheck: false }"
                  :placeholder="t('settings.r.f.envHint')"
                  @update:value="onEnvText(active, i, $event)"
                />
                <p class="f-hint">{{ t('settings.r.f.envHint') }}</p>
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
</style>
