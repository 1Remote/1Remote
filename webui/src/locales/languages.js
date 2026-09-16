// 14 语言静态清单（Plan 3 Task 4）：与 Ui/Resources/Languages/*.xaml 文件名
// （= 后端 WebUiSettingsService.SupportedLanguageCodes）一一对应；web locale 码
// 为 BCP-47 形态（zh-CN），后端 GeneralConfig.CurrentLanguageCode 为小写码（zh-cn）。
// native 名称不做 i18n（各语言用自身名字，与 WPF language_name 词条同语义）。
// 注：Task 7 落地 14 个 locale JSON 前，setLocale 仅接受已装载语言的码，
// 其余语言在 Web 端保持当前界面语言、仅同步桌面端（见 locales/index.js）。
export const LANGUAGES = [
  { code: 'cs-CZ', native: 'Čeština' },
  { code: 'de-DE', native: 'Deutsch' },
  { code: 'en-US', native: 'English' },
  { code: 'es-AR', native: 'Español (Argentina)' },
  { code: 'fr-FR', native: 'Français' },
  { code: 'gl-ES', native: 'Galego' },
  { code: 'it-IT', native: 'Italiano' },
  { code: 'ja-JP', native: '日本語' },
  { code: 'pl-PL', native: 'Polski' },
  { code: 'pt-BR', native: 'Português (Brasil)' },
  { code: 'pt-PT', native: 'Português (Portugal)' },
  { code: 'ru-RU', native: 'Русский' },
  { code: 'zh-CN', native: '简体中文' },
  { code: 'zh-TW', native: '繁體中文' },
]

/** 后端小写码（"zh-cn"）→ web locale 码（"zh-CN"）；未知码回退 en-US */
export function backendToWeb(backendCode) {
  const hit = LANGUAGES.find((l) => l.code.toLowerCase() === String(backendCode || '').toLowerCase())
  return hit ? hit.code : 'en-US'
}

/** web locale 码 → 后端小写码（PUT /api/settings/general.language 的线格式） */
export function webToBackend(webCode) {
  return String(webCode || 'en-US').toLowerCase()
}

/** n-select options：value=web 码，label=原生名 */
export function languageOptions() {
  return LANGUAGES.map((l) => ({ value: l.code, label: l.native }))
}
