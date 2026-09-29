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

// 死键清理（round15 重构成组移除，14 语言同步删除后经 parity 校验 576 键一致）。
// 历史死键及其成因存档（再遇到同类键，确认无引用后可直接成组删）：
// - tree.deleteFolderConfirm：删除确认按 空文件夹/含服务器 二分（deleteFolderEmpty/HasServers）
// - row.note：列标题统一走 col.* 族（col.note）
// - settings.placeholder：设置占位页改用 placeholder.comingSoon + page.*
// - editor.removeTag：标签词条的移除提示随旧标签 chips 一并移除
// - editor.dataSource / editor.dataSourceLabel：抽屉头部数据源为只读 pill，无文案键
// - cv.nameRequired：名称必填提示统一走 settings.r.nameRequired
// - settings.d.saveFirstHint：新建数据源 改保存后测流，不再出现「先保存」提示
// - modal.dirtyHint：遮罩点击改与 ✕/Esc 同弹丢弃确认（round14），引导文案不再使用
// - cv.revealHide / cv.revealWaiting / cv.autoHide：凭据库列表行内 reveal 已删（round14
//   需求：明文只在编辑模态内查看），cv.reveal / cv.revealFailed 编辑模态仍在用
// - editor.missingRequired：编辑器/批量的必填缺失横幅已删（round15：统一走必填守卫的
//   假禁用+强点聚焦高亮），toast 侧用 editor.fixBeforeSave
// - editor.kvIncompleteRow：半行文案拼接随必填横幅删除移除（round15 重构；半行仍有
//   行内红框与保存拦截）
// - statusbar.langEn/langZh：语言切换改显语言自称（round8 P13）
// - cv.secretHint：随即时查重的长提示行移除

// 语言探测（round14 R5：语言唯一持久化在后端配置，不再读写 localStorage）：
// 浏览器语言（先精确匹配文件码，再主子标签前缀匹配，如 'pt' → pt-BR、'zh' → zh-CN，
// 取 LANGUAGES 顺序首个）→ en-US。启动时 App.vue 会拉后端语言覆盖探测值（后端未起
// 或请求失败时保持探测语言），旧版遗留的 '1r-lang' 键由 App.vue 启动清理。
function detectLocale() {
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
// （无需刷新）；仅接受 14 个已装载语言的码。纯内存切换，不落任何本地存储（round14
// R5 定案：语言唯一持久化在后端 1Remote.json——调用方在用户显式切换时自行 PUT
// /api/settings/general 的 language 字段；启动同步只读不写）。
export function setLocale(l) {
  if (!CODES.includes(l)) return
  i18n.global.locale.value = l
}
