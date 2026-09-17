/**
 * 公共 Markdown 渲染：marked.parse（gfm + breaks）+ 轻量净化，
 * 返回可直接交给 v-html 的 HTML 字符串。两处消费：编辑器备注字段预览
 * （MarkdownField.vue）、服务器列表行备注悬停弹层（ServerRow.vue）
 * ——两处输入同源（用户自己的服务器 Note 字段）。
 *
 * XSS 处理（有意轻量，注意局限）：渲染后做一次正则清洗——剥 <script> 块、危险嵌资源
 * 标签（iframe/object/embed/...）、on* 事件属性、javascript: URL。这不是完整净化器
 * （如 <img src=x onerror=… 的属性拆分混淆、data: URL、CSS 注入等极端构造不保证覆盖）；
 * 内容来自用户自己的服务器配置（单机自托管场景，无多租户互攻击面），后端保存的也是
 * 这份纯文本，故轻量清洗已覆盖常见意外注入面。若未来引入多用户共享库，应换 DOMPurify
 * 等完整净化器（v-html + 本函数 sanitize 的替换点集中在此）。
 */
import { marked } from 'marked'

marked.setOptions({ breaks: true, gfm: true })

/** 轻量净化（局限见文件头注释）：剥 script 块 / 危险标签 / on* 属性 / javascript: URL */
function sanitize(html) {
  return String(html)
    .replace(/<script[\s\S]*?<\/script\s*>/gi, '')
    .replace(/<\/?(iframe|object|embed|style|link|meta|base|form)\b[^>]*>/gi, '')
    .replace(/\son\w+\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+)/gi, '')
    .replace(/((?:href|src|xlink:href)\s*=\s*)(?:"\s*javascript:[^"]*"|'\s*javascript:[^']*'|javascript:\S+)/gi, '$1""')
}

/** Markdown → HTML（gfm+breaks，已净化）；null/undefined 容忍按空串处理 */
export function renderMarkdown(text) {
  return sanitize(marked.parse(text ?? ''))
}
