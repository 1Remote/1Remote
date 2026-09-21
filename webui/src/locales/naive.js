/**
 * 组件库（naive-ui）界面语言的集中映射（K24，owner 2026-09-21 需求）：
 * 「把 UI 和组件库等等所有的国际化文本放置在同一个地方，方便第三方只用看一个
 * 地方就能完成翻译」——locales/ 目录就是这个唯一的地方：
 * - 自绘界面词条：本目录的 zh-CN.json / en-US.json（手写基准）+ 其余 12 个生成文件
 *   （scripts/convert-locales.mjs 管线，改词条须走基准再生成）；
 * - 组件库内建文案（空下拉「无数据」、对话框按钮等）：本文件集中映射——14 种界面
 *   语言 naive-ui 全部自带语言包（含 glES 加利西亚语），按表取用、无包回落 enUS。
 * 第三方翻译者只需处理 zh-CN.json / en-US.json 两个基准文件（其余语言由管线生成），
 * 组件库文本已随语言包就位、无需翻译。
 */
import {
  zhCN,
  zhTW,
  enUS,
  jaJP,
  ruRU,
  deDE,
  frFR,
  esAR,
  itIT,
  plPL,
  ptBR,
  ptPT,
  csCZ,
  glES,
} from 'naive-ui'

/** Web 语言码（locales/ 文件名同码）→ naive-ui 语言包（内部表：消费方一律经 naiveLocaleOf 取包） */
const NAIVE_LOCALES = {
  'zh-CN': zhCN,
  'zh-TW': zhTW,
  'en-US': enUS,
  'ja-JP': jaJP,
  'ru-RU': ruRU,
  'de-DE': deDE,
  'fr-FR': frFR,
  'es-AR': esAR,
  'it-IT': itIT,
  'pl-PL': plPL,
  'pt-BR': ptBR,
  'pt-PT': ptPT,
  'cs-CZ': csCZ,
  'gl-ES': glES,
}

/** 当前语言对应的 naive-ui 语言包（无映射的语言回落 enUS） */
export function naiveLocaleOf(code) {
  return NAIVE_LOCALES[code] || enUS
}
