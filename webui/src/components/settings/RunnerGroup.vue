<script setup>
/**
 * 运行器分组（Plan 3 Task 6，spec §6）：GET/PUT /api/settings/runners。
 * - 协议页签（6 个：SSH/Telnet/Serial/VNC/SFTP/FTP，键序以 GET 返回为准）× 每协议：
 *   默认运行器下拉（runner 名单）+ runner 卡片列表。
 * - **PascalCase 直通**（有意简化，plan 记录在案）：runners 数组与 GET 原样往返，只字段化编辑
 *   已知属性——外部运行器（$type=ExternalRunner/ExternalRunnerForSSH）：ExePath / Arguments /
 *   EnvironmentVariables（KEY=VALUE 行编辑）/ RunWithHosting；内置运行器（InternalDefaultRunner/
 *   PuttyRunner/KittyRunner/Runner）只读展示说明。Name 不开放改名（重命名牵扯 SelectedRunnerName
 *   与宏引用一致性，归桌面端）。
 * - 环境变量用独立文本域编辑（数组直编输入体验差）：载入时 数组→行文本，保存时 行文本→数组
 *   （空行/无 = 的行丢弃）；dirty 判定把两份状态一起序列化比较。
 * - PUT 发送整个 protocols 对象（6 协议全量；后端全量预校验，缺失协议=保持，此处全量最稳）；
 *   响应回读替换本地态，保证 GET→PUT→GET 逐字节稳定。
 */
import { computed, inject, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'

const { t } = useI18n()
const message = useMessage()

// 下拉展开计数（SettingsView 的 Esc 返回链序，见 SettingsView 文件头注释；与 GeneralGroup 同款）
const escShield = inject('settingsEscShield', null)
function shield(show) {
  if (escShield) escShield.open += show ? 1 : -1
}

const loading = ref(true)
const loadError = ref(false)
const saving = ref(false)
const protocols = ref(null) // 深拷贝的 GET 状态：{ SSH: { selectedRunnerName, runners: [...] } }
const active = ref('') // 当前页签协议键
const envTexts = reactive({}) // `${proto}:${index}` → 'KEY=VALUE\n…'（外部运行器环境变量行文本）

const stateJson = () => JSON.stringify([protocols.value, envTexts])
let snapshotJson = ''

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
  snapshotJson = stateJson()
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
const dirty = computed(() => !!protocols.value && stateJson() !== snapshotJson)

const runnerOptions = computed(() => (activeCfg.value?.runners || []).map((r) => ({ value: r.Name, label: r.Name })))

// 行文本 → 数组（保存时同步回 runner 对象）：空行与无 = 的行丢弃；= 后可空
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

async function save() {
  if (!dirty.value || saving.value) return
  saving.value = true
  try {
    // 环境变量文本域 → 数组（保存前同步，仅在文本非空或原有条目时写入）
    for (const [p, cfg] of Object.entries(protocols.value)) {
      ;(cfg.runners || []).forEach((r, i) => {
        if (isExternal(r)) r.EnvironmentVariables = parseEnvText(envTexts[p + ':' + i])
      })
    }
    const r = await api.saveRunners(protocols.value)
    applyState(r.protocols || {})
    message.success(t('settings.saved'))
  } catch (e) {
    const detail = e?.body?.errors?.join('; ')
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  } finally {
    saving.value = false
  }
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

      <!-- 默认运行器 -->
      <div class="sel-row">
        <label>{{ t('settings.r.selected') }}</label>
        <n-select
          class="sel-select"
          size="small"
          :value="activeCfg.selectedRunnerName"
          :options="runnerOptions"
          @update:show="shield"
          @update:value="activeCfg.selectedRunnerName = $event"
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
              <n-input size="small" v-model:value="r.ExePath" :input-props="{ spellcheck: false }" />
            </div>
            <div class="f-row">
              <label>{{ t('settings.r.f.arguments') }}</label>
              <n-input
                size="small"
                type="textarea"
                :rows="2"
                v-model:value="r.Arguments"
                :input-props="{ spellcheck: false }"
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
                  @update:value="envTexts[active + ':' + i] = $event"
                />
                <p class="f-hint">{{ t('settings.r.f.envHint') }}</p>
              </div>
            </div>
            <div class="f-row">
              <label>{{ t('editor.f.RunWithHosting') }}</label>
              <n-switch size="small" :value="!!r.RunWithHosting" @update:value="r.RunWithHosting = $event" />
            </div>
          </template>
        </div>
      </div>

      <div class="actions">
        <span v-if="dirty" class="dirty">{{ t('settings.dirtyHint') }}</span>
        <n-button size="small" type="primary" :disabled="!dirty" :loading="saving" @click="save">
          {{ t('settings.save') }}
        </n-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.group {
  max-width: 720px;
}
.hint {
  font-size: 12.5px;
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
  font-size: 12.5px;
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
  font-size: 12.5px;
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
  font-size: 12.5px;
  color: var(--text-1);
}
.r-badge {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 1px 5px;
  font-size: 10.5px;
  color: var(--text-4);
}
.r-badge.ext {
  border-color: var(--accent);
  color: var(--accent-text);
}
.r-internal-hint {
  margin: 0;
  font-size: 11.5px;
  color: var(--text-4);
}
.f-row {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}
.f-row label {
  font-size: 12px;
  color: var(--text-2);
}
.f-hint {
  margin: 4px 0 0;
  font-size: 11px;
  color: var(--text-4);
}
.actions {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 14px;
  padding-top: 12px;
  border-top: 1px solid var(--border);
}
.dirty {
  font-size: 12px;
  color: var(--warning);
}
</style>
