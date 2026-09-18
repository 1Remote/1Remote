<script setup>
/**
 * 常规分组（Plan 3 Task 4；全量自动保存）：GET/PUT /api/settings/general
 * 白名单字段表单，改完即存（无保存按钮/dirty 提示）。
 * - 每个 switch/select @update:value 立即差量 PUT 该键（useAutoSave 并发合并守卫；
 *   成功不 toast，仅失败 toast）；
 * - language 下拉（14 语言静态清单）选择即 setLocale 让 Web 界面即时切换，同时即存
 *   （映射后端小写码 zh-CN→zh-cn），桌面端同步生效（Task 7 前 Web 仅装配 zh-CN/en-US，
 *   其余语言只同步桌面端，见 locales/languages.js）；
 * - closeButtonBehavior/logLevel 为 int 枚举（WPF 侧语义，见 WebUiSettingsService）。
 * - requireSecondaryVerification 开关例外：点击立即生效（WPF 平价
 *   GeneralSettingView.xaml.cs:31-43 同款）：翻转前先过一次 Windows 凭据/Hello 验证
 *   （POST /api/settings/verify；当前未开启验证时后端直通、无感知），验证通过才 PUT
 *   提交翻转；取消/失败 → 开关回弹不提交（不乐观更新，对齐"验证通过才翻转"）。
 * - 自动保存不做响应回填：差量键本地即真值，回填会在连改多个开关时用旧响应覆盖
 *   刚翻转的控件（失败 toast 已提示用户当前态与服务器的分歧）。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'
import { setLocale } from '../../locales'
import { backendToWeb, languageOptions, webToBackend } from '../../locales/languages'
import { useAutoSave } from '../../composables/useAutoSave'
import { useSettingsEsc } from '../../composables/useSettingsEsc'
import HelpLink from '../HelpLink.vue'

const { t } = useI18n()
const message = useMessage()

// 下拉展开计数（SettingsView 的 Esc 返回链序，见 SettingsView/useSettingsEsc 文件头注释）
const { shield } = useSettingsEsc()

const loading = ref(true)
const loadError = ref(false)
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

// 走自动保存的字段（requireSecondaryVerification 不在列：开关点击立即生效，见文件头）
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
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})

// ---- 自动保存：差量 PUT（仅含变更键），成功静默、失败 toast ----
const {
  saving: autoSaving,
  dispose: disposeAutoSave,
  saveNow,
} = useAutoSave((patch) => api.saveGeneralSettings(patch), {
  onError: (e) => {
    const detail = e?.body?.errors?.join('; ') || (e?.message ? ` (${e.message})` : '')
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  },
})
onBeforeUnmount(disposeAutoSave)

// 离散控件（switch/select）通用：写表单 + 立即差量保存该键
function setField(key, value) {
  form[key] = value
  saveNow({ [key]: value })
}

// 语言切换：Web 界面即时切换（未装配语言静默保持当前界面语言）+ 即存（桌面端同步生效）
const langOptions = languageOptions()
function onLanguageChange(webCode) {
  form.language = webCode
  setLocale(webCode)
  saveNow({ language: webToBackend(webCode) })
}

const closeOptions = [
  { value: 0, label: computed(() => t('settings.o.close.exit')) },
  { value: 1, label: computed(() => t('settings.o.close.minimize')) },
]
// SimpleLogHelper.EnumLogLevel：0=Debug..5=Disabled（zh 术语对齐 zh-cn.xaml Info/Warning/Error 词条）
const logOptions = [0, 1, 2, 3, 4, 5].map((v) => ({ value: v, label: computed(() => t('settings.o.log.' + v)) }))

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
    message.success(t('settings.saved'))
  } catch (e) {
    message.warning(e?.status === 403 ? t('settings.verifyCancelled') : t('settings.saveFailed'))
  } finally {
    verifying.value = false
  }
}

// 开关行清单（模板循环渲染，避免逐个手写重复标记）。requireSecondaryVerification 不在列
//（立即生效行单独渲染，位置保持在原第 3 个开关处），故拆两段循环夹住单独行
const SWITCHES = [{ key: 'confirmBeforeClosingSession' }, { key: 'showSessionIconInSessionWindow' }]
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
          <n-select
            size="small"
            :value="form.language"
            :options="langOptions"
            @update:show="shield"
            @update:value="onLanguageChange"
          />
          <!-- 帮助链接：WPF GeneralSettingView.xaml:41-62 语言行下方的
               "Can't find your language?" → 翻译协作文档。WPF 为字面量英文（14 语言同显英文，
               AboutPageView 硬编码英文同款先例）→ web 同值硬编码，不进 locale -->
          <HelpLink class="lang-help" href="https://1remote.github.io/usage/misc/help-translation/" badge="">
            Can't find your language?
          </HelpLink>
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
            @update:value="setField('closeButtonBehavior', $event)"
          />
        </div>
      </div>

      <div class="row" v-for="s in SWITCHES" :key="s.key">
        <label class="row-label">{{ t('settings.f.' + s.key) }}</label>
        <div class="row-control">
          <n-switch size="small" :value="form[s.key]" :loading="autoSaving" @update:value="setField(s.key, $event)" />
        </div>
      </div>

      <!-- requireSecondaryVerification：立即生效行（WPF 平价翻转验证门，见文件头注释） -->
      <div class="row">
        <label class="row-label">{{ t('settings.f.requireSecondaryVerification') }}</label>
        <div class="row-control">
          <n-switch
            size="small"
            :value="form.requireSecondaryVerification"
            :loading="verifying"
            @update:value="onVerificationToggle"
          />
        </div>
      </div>

      <div class="row" v-for="s in SWITCHES_REST" :key="s.key">
        <label class="row-label">{{ t('settings.f.' + s.key) }}</label>
        <div class="row-control">
          <n-switch size="small" :value="form[s.key]" :loading="autoSaving" @update:value="setField(s.key, $event)" />
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
            @update:value="setField('logLevel', $event)"
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
.row-control.slim {
  max-width: 280px;
}
/* 语言行帮助链接：跟在下拉框下方（WPF 语言行下一行同款位置） */
.lang-help {
  margin-top: 4px;
}
</style>
