import { createI18n } from 'vue-i18n'
import zhCN from './zh-CN.json'
import enUS from './en-US.json'

// 默认跟随浏览器语言（前两位匹配），无匹配回退 zh-CN；切换即时生效（无刷新）
const stored = localStorage.getItem('1r-lang')
const nav = (navigator.language || 'zh').toLowerCase().startsWith('zh') ? 'zh-CN' : 'en-US'
const locale = stored === 'en-US' || stored === 'zh-CN' ? stored : nav

export const i18n = createI18n({
  legacy: false,
  locale,
  fallbackLocale: 'zh-CN',
  messages: { 'zh-CN': zhCN, 'en-US': enUS },
})

// 切换语言（语言切换 UI 归 Task 20）：legacy:false 下 global.locale 是 ref，
// 赋值即全站响应式生效（无需刷新）；'1r-lang' 持久化供下次启动读取
export function setLocale(l) {
  if (l !== 'zh-CN' && l !== 'en-US') return
  i18n.global.locale.value = l
  try {
    localStorage.setItem('1r-lang', l)
  } catch {
    /* 隐私模式等写入失败可忽略 */
  }
}
