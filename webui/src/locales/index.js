import { createI18n } from 'vue-i18n'
import { LANGUAGES } from './languages.js'
import zhCN from './zh-CN.json'
import enUS from './en-US.json'
import csCZ from './cs-CZ.json'
import deDE from './de-DE.json'
import esAR from './es-AR.json'
import frFR from './fr-FR.json'
import glES from './gl-ES.json'
import itIT from './it-IT.json'
import jaJP from './ja-JP.json'
import plPL from './pl-PL.json'
import ptBR from './pt-BR.json'
import ptPT from './pt-PT.json'
import ruRU from './ru-RU.json'
import zhTW from './zh-TW.json'

// 14 语言全量打包（Plan 3 Task 7）：12 个 JSON 由 scripts/convert-locales.mjs
// 从 WPF XAML 转换生成（可重跑），zh-CN/en-US 手写维护。全量 ~14×10KB 可接受。
const messages = {
  'zh-CN': zhCN,
  'en-US': enUS,
  'cs-CZ': csCZ,
  'de-DE': deDE,
  'es-AR': esAR,
  'fr-FR': frFR,
  'gl-ES': glES,
  'it-IT': itIT,
  'ja-JP': jaJP,
  'pl-PL': plPL,
  'pt-BR': ptBR,
  'pt-PT': ptPT,
  'ru-RU': ruRU,
  'zh-TW': zhTW,
}
const CODES = LANGUAGES.map((l) => l.code)

// 语言探测：localStorage 优先 → 浏览器语言（先精确匹配文件码，再主子标签前缀
// 匹配，如 'pt' → pt-BR、'zh' → zh-CN，取 LANGUAGES 顺序首个）→ en-US。
function detectLocale() {
  try {
    const stored = localStorage.getItem('1r-lang')
    if (stored && CODES.includes(stored)) return stored
  } catch {
    /* 隐私模式等读取失败可忽略 */
  }
  const nav = String(navigator.language || '').toLowerCase()
  if (!nav) return 'en-US'
  const exact = CODES.find((c) => c.toLowerCase() === nav)
  if (exact) return exact
  const prefix = nav.split('-')[0]
  const hit = CODES.find((c) => c.toLowerCase().split('-')[0] === prefix)
  return hit || 'en-US'
}

// 回退链：该语言 → en-US（未映射的 web 专有键显示英文，脚本已按 en 值填充）
export const i18n = createI18n({
  legacy: false,
  locale: detectLocale(),
  fallbackLocale: 'en-US',
  messages,
})

// 切换语言：legacy:false 下 global.locale 是 ref，赋值即全站响应式生效
// （无需刷新）；'1r-lang' 持久化供下次启动读取；仅接受 14 个已装载语言的码。
export function setLocale(l) {
  if (!CODES.includes(l)) return
  i18n.global.locale.value = l
  try {
    localStorage.setItem('1r-lang', l)
  } catch {
    /* 隐私模式等写入失败可忽略 */
  }
}
