// 绝对时间 formatter 缓存：按 locale 键缓存 Intl.DateTimeFormat 实例——
// 「最近连接」列每行悬停 title 都会调用，行级重建 formatter 开销可观；
// 键空间 = 用户实际用过的界面语言数（个位数），无需淘汰策略
const absFmtCache = new Map()

/**
 * 绝对时间格式化（「最近连接」列悬停 title；与 formatRelativeTime 同文件同风格）。
 * 精确到秒：dateStyle medium + timeStyle medium（随 locale 本地化，
 * 如 zh-CN「2026/9/17 14:03:22」、en-US「Sep 17, 2026, 2:03:22 PM」）。
 * @param {number} unixSeconds Unix 秒；0/非法 = 从未连接，返回 null（调用方回退「从未连接」文案）
 * @param {string} [locale] 显式 locale（组件按当前 i18n 语言注入渲染）；缺省走 navigator.language
 * @returns {string|null} 完整日期时间字符串
 */
export function formatAbsoluteTime(unixSeconds, locale) {
  const sec = Number(unixSeconds) || 0
  if (sec <= 0) return null
  const lang = locale || (typeof navigator !== 'undefined' && navigator.language) || 'zh'
  try {
    let fmt = absFmtCache.get(lang)
    if (!fmt) {
      fmt = new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeStyle: 'medium' })
      absFmtCache.set(lang, fmt)
    }
    return fmt.format(new Date(sec * 1000))
  } catch {
    return new Date(sec * 1000).toISOString().replace('T', ' ').slice(0, 19) // 极端 locale 异常兜底
  }
}

/**
 * 相对时间格式化（「最近连接」列；相对时间用浏览器 Intl 按当前语言格式化）。
 * 语言取 navigator.language（取不到的环境如单测回退 'zh'）。
 * @param {number} unixSeconds Unix 秒；0/非法 = 从未连接，返回 null（由调用方渲染「从未」）
 * @param {number} nowMs 当前毫秒时间戳（默认 Date.now()；单测可注入）
 * @param {string} [locale] 显式 locale（组件按当前 i18n 语言注入渲染；单测亦可指定）；缺省走 navigator.language
 * @returns {string|null} 如「刚刚 / 3 分钟前 / 2 小时前 / 5 天前 / 2026年8月3日」
 */
export function formatRelativeTime(unixSeconds, nowMs = Date.now(), locale) {
  const sec = Number(unixSeconds) || 0
  if (sec <= 0) return null
  const lang = locale || (typeof navigator !== 'undefined' && navigator.language) || 'zh'
  const diff = Math.min(sec - Math.floor(nowMs / 1000), 0) // 负=过去；未来（时钟偏差）钳到 0
  const abs = Math.abs(diff)
  try {
    const rtf = new Intl.RelativeTimeFormat(lang, { numeric: 'auto' })
    if (abs < 60) return rtf.format(-Math.round(abs), 'second')
    if (abs < 3600) return rtf.format(-Math.round(abs / 60), 'minute')
    if (abs < 86400) return rtf.format(-Math.round(abs / 3600), 'hour')
    if (abs < 30 * 86400) return rtf.format(-Math.round(abs / 86400), 'day')
    return new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(new Date(sec * 1000))
  } catch {
    return new Date(sec * 1000).toISOString().slice(0, 10) // 极端 locale 异常兜底
  }
}
