<script setup>
/**
 * 常规分组（Plan 3 Task 4）：GET/PUT /api/settings/general 白名单字段表单。
 * - language 下拉（14 语言静态清单）选择即 setLocale 让 Web 界面即时切换；
 *   保存时映射为后端小写码（zh-CN→zh-cn）随 PUT 提交，桌面端同步生效
 *   （Task 7 前 Web 仅装配 zh-CN/en-US，其余语言只同步桌面端，见 locales/languages.js）。
 * - closeButtonBehavior/logLevel 为 int 枚举（WPF 侧语义，见 WebUiSettingsService）。
 * - PUT 只提交相对加载快照变化过的键（白名单部分更新）。
 * - requireSecondaryVerification 开关例外：不走保存表单，点击立即生效（fix batch3 #6，
 *   owner 三次反馈"无效"的根因 = 该开关被混进"点保存才提交"的表单）。WPF 平价
 *   （GeneralSettingView.xaml.cs:31-43 同款）：翻转前先过一次 Windows 凭据/Hello 验证
 *   （POST /api/settings/verify；当前未开启验证时后端直通、无感知），验证通过才 PUT
 *   提交翻转；取消/失败 → 开关回弹不提交（不乐观更新，对齐"验证通过才翻转"）。
 */
import { computed, inject, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'
import { setLocale } from '../../locales'
import { backendToWeb, languageOptions, webToBackend } from '../../locales/languages'

const { t } = useI18n()
const message = useMessage()

// 下拉展开计数（SettingsView 的 Esc 返回链序，见 SettingsView 文件头注释）
const escShield = inject('settingsEscShield', null)
function shield(show) {
  if (escShield) escShield.open += show ? 1 : -1
}

const loading = ref(true)
const loadError = ref(false)
const saving = ref(false)
// 表单态：language 存 web 码（下拉直用），其余与后端 DTO camelCase 一致
const form = reactive({
  language: 'en-US',
  closeButtonBehavior: 0,
  confirmBeforeClosingSession: false,
  showSessionIconInSessionWindow: false,
  requireSecondaryVerification: false,
  logLevel: 0,
  tabWindowCloseButtonOnLeft: false,
  tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow: false,
  copyPortWhenCopyAddress: false,
  doNotCheckNewVersion: false,
})
// 加载快照（dirty 基准 + PUT 差量基底）
let snapshot = null

// 走保存表单的字段（requireSecondaryVerification 不在列：开关点击立即生效，见文件头）
const FIELD_KEYS = [
  'closeButtonBehavior',
  'confirmBeforeClosingSession',
  'showSessionIconInSessionWindow',
  'logLevel',
  'tabWindowCloseButtonOnLeft',
  'tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow',
  'copyPortWhenCopyAddress',
  'doNotCheckNewVersion',
]

onMounted(async () => {
  try {
    const g = await api.getGeneralSettings()
    form.language = backendToWeb(g.language)
    for (const k of FIELD_KEYS) form[k] = g[k]
    form.requireSecondaryVerification = g.requireSecondaryVerification
    snapshot = {
      language: form.language,
      ...Object.fromEntries(FIELD_KEYS.map((k) => [k, form[k]])),
      requireSecondaryVerification: form.requireSecondaryVerification,
    }
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})

const dirty = computed(() => !!snapshot && (form.language !== snapshot.language || FIELD_KEYS.some((k) => form[k] !== snapshot[k])))

// 语言切换：选择即生效（Web 端无刷新；未装配语言静默保持当前界面语言）
const langOptions = languageOptions()
function onLanguageChange(webCode) {
  form.language = webCode
  setLocale(webCode)
}

const closeOptions = [
  { value: 0, label: computed(() => t('settings.o.close.exit')) },
  { value: 1, label: computed(() => t('settings.o.close.minimize')) },
]
// SimpleLogHelper.EnumLogLevel：0=Debug..5=Disabled（zh 术语对齐 zh-cn.xaml Info/Warning/Error 词条）
const logOptions = [0, 1, 2, 3, 4, 5].map((v) => ({ value: v, label: computed(() => t('settings.o.log.' + v)) }))

async function save() {
  if (!dirty.value || saving.value) return
  saving.value = true
  try {
    // 差量提交：只发送变化的键；language 转后端小写码
    const patch = {}
    if (form.language !== snapshot.language) patch.language = webToBackend(form.language)
    for (const k of FIELD_KEYS) if (form[k] !== snapshot[k]) patch[k] = form[k]
    const g = await api.saveGeneralSettings(patch)
    form.language = backendToWeb(g.language)
    for (const k of FIELD_KEYS) form[k] = g[k]
    // 响应含全量 DTO：同步回立即生效开关的当前真值（保存表单不动它，但后端状态可能已被
    // 立即生效路径改过——以响应为准，避免 snapshot 漂移）
    form.requireSecondaryVerification = g.requireSecondaryVerification
    snapshot = {
      language: form.language,
      ...Object.fromEntries(FIELD_KEYS.map((k) => [k, form[k]])),
      requireSecondaryVerification: form.requireSecondaryVerification,
    }
    message.success(t('settings.saved'))
  } catch (e) {
    const detail = e?.body?.errors?.join('; ') || (e?.message ? ` (${e.message})` : '')
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  } finally {
    saving.value = false
  }
}

// requireSecondaryVerification 开关：点击立即生效（WPF 平价，见文件头注释）——
// 先验证（POST /api/settings/verify；未开启验证时后端直通 200）、通过才 PUT 提交翻转，
// 取消/失败则开关回弹（不乐观更新：验证/提交成功前 :value 绑定保持旧值）
const verifying = ref(false)
async function onVerificationToggle(next) {
  if (verifying.value) return
  verifying.value = true
  try {
    await api.verifySettings() // 200=验证通过；403=取消/失败 → 抛错走 catch，开关回弹
    const g = await api.saveGeneralSettings({ requireSecondaryVerification: next })
    form.requireSecondaryVerification = g.requireSecondaryVerification
    snapshot.requireSecondaryVerification = g.requireSecondaryVerification
    message.success(t('settings.saved'))
  } catch (e) {
    message.warning(e?.status === 403 ? t('settings.verifyCancelled') : t('settings.saveFailed'))
  } finally {
    verifying.value = false
  }
}

// 开关行清单（模板循环渲染，避免逐个手写重复标记）。requireSecondaryVerification 不在列
//（立即生效行单独渲染，位置保持在原第 3 个开关处），故拆两段循环夹住单独行
const SWITCHES = [
  { key: 'confirmBeforeClosingSession' },
  { key: 'showSessionIconInSessionWindow' },
]
const SWITCHES_REST = [
  { key: 'tabWindowCloseButtonOnLeft' },
  { key: 'tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow' },
  { key: 'copyPortWhenCopyAddress' },
  { key: 'doNotCheckNewVersion' },
]
</script>

<template>
  <div class="group">
    <p v-if="loading" class="hint">{{ t('settings.loading') }}</p>
    <p v-else-if="loadError" class="hint err">{{ t('settings.loadFailed') }}</p>
    <template v-else>
      <div class="row">
        <label class="row-label">{{ t('settings.f.language') }}</label>
        <div class="row-control slim">
          <n-select size="small" :value="form.language" :options="langOptions" @update:show="shield" @update:value="onLanguageChange" />
        </div>
      </div>

      <div class="row">
        <label class="row-label">{{ t('settings.f.closeButtonBehavior') }}</label>
        <div class="row-control slim">
          <n-select
            size="small"
            :value="form.closeButtonBehavior"
            :options="closeOptions.map((o) => ({ value: o.value, label: o.label.value }))"
            @update:show="shield"
            @update:value="form.closeButtonBehavior = $event"
          />
        </div>
      </div>

      <div class="row" v-for="s in SWITCHES" :key="s.key">
        <label class="row-label">{{ t('settings.f.' + s.key) }}</label>
        <div class="row-control">
          <n-switch size="small" :value="form[s.key]" @update:value="form[s.key] = $event" />
        </div>
      </div>

      <!-- requireSecondaryVerification：立即生效行（WPF 平价翻转验证门，见文件头注释） -->
      <div class="row">
        <label class="row-label">{{ t('settings.f.requireSecondaryVerification') }}</label>
        <div class="row-control">
          <n-switch size="small" :value="form.requireSecondaryVerification" :loading="verifying" @update:value="onVerificationToggle" />
        </div>
      </div>

      <div class="row" v-for="s in SWITCHES_REST" :key="s.key">
        <label class="row-label">{{ t('settings.f.' + s.key) }}</label>
        <div class="row-control">
          <n-switch size="small" :value="form[s.key]" @update:value="form[s.key] = $event" />
        </div>
      </div>

      <div class="row">
        <label class="row-label">{{ t('settings.f.logLevel') }}</label>
        <div class="row-control slim">
          <n-select
            size="small"
            :value="form.logLevel"
            :options="logOptions.map((o) => ({ value: o.value, label: o.label.value }))"
            @update:show="shield"
            @update:value="form.logLevel = $event"
          />
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
  font-size: 12.5px;
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
  font-size: 12.5px;
  color: var(--text-2);
}
.row-control.slim {
  max-width: 280px;
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
