<script setup>
/**
 * 启动器分组（Plan 3 Task 6，spec §6；fix batch7 Task D #12 全量自动保存）：
 * GET/PUT /api/settings/launcher，改完即存（无保存按钮/dirty 提示）。
 * - 启用开关 + 两个行为开关（showCredentials / allowSaveInfoInQuickConnect）翻转即 PUT 全量表单。
 * - 热键录制框：点击进入录制 → window 捕获阶段 keydown（录制中吞掉一切键，防误触浏览器/页面
 *   快捷键与 SettingsView 的 Esc 返回）→ 显示 "Ctrl+Alt+M" 形态，录制成功即保存。
 *   接受：修饰键（Ctrl/Alt/Shift/Win，至少一个）+ 主键（字母 / 数字 / F1-F12，映射为 Key 枚举
 *   成员名——数字键转 D0..D9 与 WPF Key 枚举一致）；其余按键视为无效（提示后继续录制）。
 * - PUT 线格式：hotKeyModifiers 直接发送显示形态（"Ctrl+Alt"——后端同时接受枚举成员名与显示
 *   形态，见 WebUiSettingsService.TryParseHotkeyModifiers，显示形态即所见即所发最简）；
 *   hotKeyKey 发送 Key 成员名（"M"/"F1"/"D2"）。
 * - 409 = 热键注册冲突：后端语义是"配置已保存但没注册上"——按响应回显已保存值并内联
 *   显示冲突提示（不算保存失败，不出错误 toast）。
 * - 响应回填带 hasPending 守卫：PUT 飞行中用户又翻转了开关时跳过回填，防旧响应把
 *   刚翻转的控件弹回（pending 里的全量载荷随后会带上最新态再发）。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'
import { useAutoSave } from '../../composables/useAutoSave'

const { t } = useI18n()
const message = useMessage()

const loading = ref(true)
const loadError = ref(false)
const conflict = ref(false) // 409：热键注册冲突（配置已保存）

// 本地形态：modifiersDisplay="Ctrl+Alt"（显示形态，保存时原样发送）；keyName="M"（Key 成员名）
const form = reactive({
  launcherEnabled: false,
  modifiersDisplay: '',
  keyName: '',
  showCredentials: false,
  allowSaveInfoInQuickConnect: false,
})

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

// ---- 自动保存：全量表单 PUT（每次发送时读 form 构造，天然最新），成功静默、失败 toast ----
const {
  saving: autoSaving,
  dispose: disposeAutoSave,
  saveNow,
  hasPending,
} = useAutoSave(
  async () => {
    const l = await api.saveLauncherSettings({
      launcherEnabled: form.launcherEnabled,
      hotKeyModifiers: form.modifiersDisplay, // 显示形态直发（后端两种形态都接受）
      hotKeyKey: form.keyName,
      showCredentials: form.showCredentials,
      allowSaveInfoInQuickConnect: form.allowSaveInfoInQuickConnect,
    })
    if (!hasPending()) applyDto(l) // 归一化回显；飞行中又有变更则跳过（防回弹，见文件头）
  },
  {
    onError: (e) => {
      if (e?.status === 409) {
        // 后端语义：配置已保存、热键注册失败——按响应回显已保存值，内联提示冲突
        conflict.value = true
        if (e.body?.settings) applyDto(e.body.settings)
      } else {
        const detail = e?.body?.errors?.join('; ')
        message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
      }
    },
  }
)
onBeforeUnmount(disposeAutoSave)

// 开关翻转：写表单 + 立即保存全量
function onSwitch(field, value) {
  form[field] = value
  saveNow()
}

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
  conflict.value = false
  saveNow() // 录制成功即保存（写后重注册热键）
}

function toggleRecording() {
  recording.value = !recording.value
  recordInvalid.value = false
  if (recording.value) conflict.value = false
}
onMounted(() => window.addEventListener('keydown', onRecordKeydown, true))
onBeforeUnmount(() => window.removeEventListener('keydown', onRecordKeydown, true))

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
          <n-switch
            size="small"
            :value="form.launcherEnabled"
            :loading="autoSaving"
            @update:value="onSwitch('launcherEnabled', $event)"
          />
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
          <n-switch
            size="small"
            :value="form[s.field]"
            :loading="autoSaving"
            @update:value="onSwitch(s.field, $event)"
          />
        </div>
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
</style>
