<script setup>
/**
 * 启动器分组（Plan 3 Task 6，spec §6）：GET/PUT /api/settings/launcher。
 * - 启用开关 + 三个行为开关（showCredentials / allowSaveInfoInQuickConnect）。
 * - 热键录制框：点击进入录制 → window 捕获阶段 keydown（录制中吞掉一切键，防误触浏览器/页面
 *   快捷键与 SettingsView 的 Esc 返回）→ 显示 "Ctrl+Alt+M" 形态。
 *   接受：修饰键（Ctrl/Alt/Shift/Win，至少一个）+ 主键（字母 / 数字 / F1-F12，映射为 Key 枚举
 *   成员名——数字键转 D0..D9 与 WPF Key 枚举一致）；其余按键视为无效（提示后继续录制）。
 * - PUT 线格式：hotKeyModifiers 直接发送显示形态（"Ctrl+Alt"——后端同时接受枚举成员名与显示
 *   形态，见 WebUiSettingsService.TryParseHotKeyModifiers，显示形态即所见即所发最简）；
 *   hotKeyKey 发送 Key 成员名（"M"/"F1"/"D2"）。
 * - 409 = 热键注册冲突：后端语义是"配置已保存但没注册上"——表单按响应回显已保存值并内联
 *   显示冲突提示（不算保存失败，不出错误 toast）。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'

const { t } = useI18n()
const message = useMessage()

const loading = ref(true)
const loadError = ref(false)
const saving = ref(false)
const conflict = ref(false) // 409：热键注册冲突（配置已保存）

// 本地形态：modifiersDisplay="Ctrl+Alt"（显示形态，保存时原样发送）；keyName="M"（Key 成员名）
const form = reactive({
  launcherEnabled: false,
  modifiersDisplay: '',
  keyName: '',
  showCredentials: false,
  allowSaveInfoInQuickConnect: false,
})
let snapshot = null

// 枚举成员名 → 显示形态（"ControlAlt" → "Ctrl+Alt"；成员名组合词逐段探测）
function modifiersToDisplay(member) {
  if (!member) return ''
  const s = String(member).toLowerCase()
  const parts = []
  if (s.includes('control')) parts.push('Ctrl')
  if (s.includes('shift')) parts.push('Shift')
  if (s.includes('alt')) parts.push('Alt')
  if (s.includes('win')) parts.push('Win')
  return parts.join('+')
}

function applyDto(l) {
  if (!l) return
  form.launcherEnabled = !!l.launcherEnabled
  form.modifiersDisplay = modifiersToDisplay(l.hotKeyModifiers)
  form.keyName = l.hotKeyKey || ''
  form.showCredentials = !!l.showCredentials
  form.allowSaveInfoInQuickConnect = !!l.allowSaveInfoInQuickConnect
  snapshot = {
    launcherEnabled: form.launcherEnabled,
    modifiersDisplay: form.modifiersDisplay,
    keyName: form.keyName,
    showCredentials: form.showCredentials,
    allowSaveInfoInQuickConnect: form.allowSaveInfoInQuickConnect,
  }
}

onMounted(async () => {
  try {
    applyDto(await api.getLauncherSettings())
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})

const dirty = computed(
  () =>
    !!snapshot &&
    (form.launcherEnabled !== snapshot.launcherEnabled ||
      form.modifiersDisplay !== snapshot.modifiersDisplay ||
      form.keyName !== snapshot.keyName ||
      form.showCredentials !== snapshot.showCredentials ||
      form.allowSaveInfoInQuickConnect !== snapshot.allowSaveInfoInQuickConnect)
)

// ---- 热键录制 ----
const recording = ref(false)
const recordInvalid = ref(false)
const hotkeyText = computed(() =>
  form.modifiersDisplay && form.keyName ? `${form.modifiersDisplay}+${form.keyName}` : ''
)

// 浏览器 e.key → WPF Key 枚举成员名（仅接受字母/数字/F1-F12，其余返回 ''）
function keyToEnumName(k) {
  if (/^[a-z]$/i.test(k)) return k.toUpperCase()
  if (/^\d$/.test(k)) return 'D' + k
  if (/^F([1-9]|1[0-2])$/.test(k)) return k
  return ''
}

function onRecordKeydown(e) {
  if (!recording.value) return
  // 录制中吞掉一切按键（含 Tab/F5 等浏览器行为与 SettingsView 的 Esc 返回链）
  e.preventDefault()
  e.stopPropagation()
  if (e.key === 'Escape') {
    recording.value = false
    recordInvalid.value = false
    return
  }
  const mods = []
  if (e.ctrlKey) mods.push('Ctrl')
  if (e.altKey) mods.push('Alt')
  if (e.shiftKey) mods.push('Shift')
  if (e.metaKey) mods.push('Win')
  const name = keyToEnumName(e.key)
  if (!name) return // 修饰键本身或未支持按键：继续等待（不退出录制）
  if (!mods.length) {
    recordInvalid.value = true // 缺修饰键：提示无效，保持录制
    return
  }
  form.modifiersDisplay = mods.join('+')
  form.keyName = name
  recording.value = false
  recordInvalid.value = false
}

function toggleRecording() {
  recording.value = !recording.value
  recordInvalid.value = false
  if (recording.value) conflict.value = false
}
onMounted(() => window.addEventListener('keydown', onRecordKeydown, true))
onBeforeUnmount(() => window.removeEventListener('keydown', onRecordKeydown, true))

// ---- 保存 ----
async function save() {
  if (!dirty.value || saving.value) return
  saving.value = true
  conflict.value = false
  try {
    applyDto(
      await api.saveLauncherSettings({
        launcherEnabled: form.launcherEnabled,
        hotKeyModifiers: form.modifiersDisplay, // 显示形态直发（后端两种形态都接受）
        hotKeyKey: form.keyName,
        showCredentials: form.showCredentials,
        allowSaveInfoInQuickConnect: form.allowSaveInfoInQuickConnect,
      })
    )
    message.success(t('settings.saved'))
  } catch (e) {
    if (e?.status === 409) {
      // 后端语义：配置已保存、热键注册失败——按响应回显已保存值，内联提示冲突
      conflict.value = true
      if (e.body?.settings) applyDto(e.body.settings)
    } else {
      const detail = e?.body?.errors?.join('; ')
      message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
    }
  } finally {
    saving.value = false
  }
}

// key = 标签词条后缀（settings.l.*），field = 表单/PUT 载荷字段名（后端域是
// allowSaveInfoInQuickConnect，而词条键简写为 allowSaveInfo——两者不同，分开声明）
const SWITCHES = [
  { key: 'showCredentials', field: 'showCredentials' },
  { key: 'allowSaveInfo', field: 'allowSaveInfoInQuickConnect' },
]
</script>

<template>
  <div class="group">
    <p v-if="loading" class="hint">{{ t('settings.loading') }}</p>
    <p v-else-if="loadError" class="hint err">{{ t('settings.loadFailed') }}</p>
    <template v-else>
      <div class="row">
        <label class="row-label">{{ t('settings.l.enabled') }}</label>
        <div class="row-control">
          <n-switch size="small" :value="form.launcherEnabled" @update:value="form.launcherEnabled = $event" />
        </div>
      </div>

      <div class="row">
        <label class="row-label">{{ t('settings.l.hotkey') }}</label>
        <div class="row-control">
          <div class="hk-wrap">
            <button class="hk-box" type="button" :class="{ recording }" @click="toggleRecording">
              {{ recording ? t('settings.l.hotkeyRecording') : hotkeyText }}
            </button>
            <span class="hk-hint">{{
              recordInvalid ? t('settings.l.hotkeyInvalid') : t('settings.l.hotkeyHint')
            }}</span>
          </div>
          <p v-if="conflict" class="hk-conflict">{{ t('settings.l.conflict') }}</p>
        </div>
      </div>

      <div class="row" v-for="s in SWITCHES" :key="s.key">
        <label class="row-label">{{ t('settings.l.' + s.key) }}</label>
        <div class="row-control">
          <n-switch size="small" :value="form[s.field]" @update:value="form[s.field] = $event" />
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
  max-width: 640px;
}
.hint {
  font-size: 0.9615rem;
  color: var(--text-3);
}
.hint.err {
  color: var(--danger);
}
.row {
  display: grid;
  grid-template-columns: 240px minmax(0, 1fr);
  gap: 6px 12px;
  align-items: center;
  padding: 7px 0;
}
.row-label {
  font-size: 0.9615rem;
  color: var(--text-2);
}
.hk-wrap {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.hk-box {
  min-width: 150px;
  min-height: 28px;
  padding: 4px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-elevated);
  color: var(--text-1);
  font-size: 0.9615rem;
  font-family: inherit;
  text-align: center;
  cursor: pointer;
}
.hk-box:hover {
  border-color: var(--border-strong);
}
.hk-box.recording {
  border-color: var(--accent);
  color: var(--accent-text);
  animation: hk-pulse 1.2s ease-in-out infinite;
}
@keyframes hk-pulse {
  0%,
  100% {
    box-shadow: 0 0 0 0 transparent;
  }
  50% {
    box-shadow: 0 0 0 3px var(--accent-container);
  }
}
@media (prefers-reduced-motion: reduce) {
  .hk-box.recording {
    animation: none;
  }
}
.hk-hint {
  font-size: 0.8846rem;
  color: var(--text-4);
}
.hk-conflict {
  margin: 6px 0 0;
  font-size: 0.8846rem;
  color: var(--warning);
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
  font-size: 0.9231rem;
  color: var(--warning);
}
</style>
