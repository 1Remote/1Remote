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

// 死键清单（已无任何引用，locale JSON 中保留不删——14 语言 562 键由 convert-locales.mjs
// 统一生成，单侧删键会破坏全语言键集一致；孤儿键随下一次成组清理时一并移除）：
// - tree.deleteFolderConfirm：删除确认按 空文件夹/含服务器 二分（deleteFolderEmpty/HasServers）
// - row.note：列标题统一走 col.* 族（col.note）
// - settings.placeholder：设置占位页改用 placeholder.comingSoon + page.*
// - editor.removeTag：标签词条的移除提示随旧标签 chips 一并移除
// - editor.dataSource / editor.dataSourceLabel：抽屉头部数据源为只读 pill，无文案键
// - cv.nameRequired：名称必填提示统一走 settings.r.nameRequired（凭据库空名直接禁保存不提示）
// - settings.d.saveFirstHint：新建数据源 改保存后测流，不再出现「先保存」提示
// 另有两处孤儿键在产生它们的组件注释里就地说明：statusbar.langEn/langZh（语言切换
// 改显语言自称，见 ServerListView）、cv.secretHint（见 CredentialVaultGroup）

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
