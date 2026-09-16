<script setup>
/**
 * 语言与关于分组（Plan 3 Task 4，spec §6）：语言下拉 + 版本 + 链接占位 + 开源许可。
 * - 语言选择 = 即时三重生效：setLocale（Web 界面无刷新切换）+ PUT general.language
 *   （小写码落库并同步桌面端 LanguageService）。与常规组共用同一后端字段，
 *   变更后双方重新拉取即可对齐（此处选择即保存，无需保存按钮）。
 * - 版本来自 GET /api/version（WPF AppVersion.Version）。
 * - 链接为占位（docs 站待建）；开源许可见仓库 LICENSE。
 */
import { computed, inject, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'
import { setLocale } from '../../locales'
import { backendToWeb, languageOptions, webToBackend } from '../../locales/languages'

const { t } = useI18n()
const message = useMessage()
const escShield = inject('settingsEscShield', null)
function shield(show) {
  if (escShield) escShield.open += show ? 1 : -1
}

const langOptions = languageOptions()
const lang = ref('en-US')
const version = ref('')
const saving = ref(false)

onMounted(async () => {
  try {
    const g = await api.getGeneralSettings()
    lang.value = backendToWeb(g.language)
  } catch {
    /* 后端不可达时保持当前界面语言 */
  }
  try {
    const v = await api.version()
    version.value = v?.version || ''
  } catch {
    /* 版本不可达时留空 */
  }
})

async function onLanguageChange(webCode) {
  if (webCode === lang.value || saving.value) return
  const prev = lang.value
  lang.value = webCode
  setLocale(webCode) // Web 端即时切换（14 语言全量装配，见 locales/index.js）
  saving.value = true
  try {
    await api.saveGeneralSettings({ language: webToBackend(webCode) })
    message.success(t('settings.saved'))
  } catch (e) {
    lang.value = prev
    setLocale(prev)
    message.error(t('settings.saveFailed') + (e?.message ? ` (${e.message})` : ''))
  } finally {
    saving.value = false
  }
}

// computed：本页语言切换即时生效，docs 词条需保持响应式
const LINKS = computed(() => [
  { key: 'github', url: 'https://github.com/1Remote/1Remote', label: 'GitHub' },
  { key: 'docs', url: 'https://github.com/1Remote/1Remote#readme', label: t('settings.link.docs') },
])
</script>

<template>
  <div class="group">
    <div class="row">
      <label class="row-label">{{ t('settings.langAbout.language') }}</label>
      <div class="row-control">
        <n-select
          class="sel"
          size="small"
          :value="lang"
          :options="langOptions"
          :loading="saving"
          @update:show="shield"
          @update:value="onLanguageChange"
        />
        <span class="hint">{{ t('settings.langAbout.languageHint') }}</span>
      </div>
    </div>

    <div class="row">
      <label class="row-label">{{ t('settings.langAbout.version') }}</label>
      <div class="row-control">
        <span class="value">{{ version ? 'v' + version : t('common.comingSoon') }}</span>
      </div>
    </div>

    <div class="row">
      <label class="row-label">{{ t('settings.langAbout.links') }}</label>
      <div class="row-control links">
        <a v-for="l in LINKS" :key="l.key" :href="l.url" target="_blank" rel="noreferrer noopener">{{ l.label }}</a>
      </div>
    </div>

    <div class="row">
      <label class="row-label">{{ t('settings.langAbout.license') }}</label>
      <div class="row-control">
        <span class="hint">{{ t('settings.langAbout.licenseText') }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.group {
  max-width: 640px;
}
.row {
  display: grid;
  grid-template-columns: 160px minmax(0, 1fr);
  gap: 6px 12px;
  align-items: center;
  padding: 7px 0;
}
.row-label {
  font-size: 12.5px;
  color: var(--text-2);
}
.row-control {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 10px;
}
.sel {
  flex: 0 1 280px;
}
.value {
  font-size: 12.5px;
  color: var(--text-1);
}
.hint {
  font-size: 12px;
  color: var(--text-4);
}
.links a {
  font-size: 12.5px;
  color: var(--accent-text);
  text-decoration: none;
  margin-right: 14px;
}
.links a:hover {
  text-decoration: underline;
}
</style>
