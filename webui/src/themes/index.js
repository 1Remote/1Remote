import { reactive, computed } from 'vue'
import { darkTheme, lightTheme } from 'naive-ui'
import { api } from '../api'
import './theme.css'

export const ACCENTS = ['blue', 'violet', 'pink', 'red', 'orange', 'green', 'slate']

// 强调色 → Naive UI primaryColor 映射（与 theme.css 中 --accent 保持一致）
export const ACCENT_HEX = {
  blue: '#2c5aff',
  violet: '#8b5cf6',
  pink: '#ec4899',
  red: '#ef4444',
  orange: '#f97316',
  green: '#10b981',
  slate: '#64748b',
}

// 强调色 hover 色（与 theme.css 中 --accent-hover 保持一致）。
// naive-ui 内置主题的 primaryColorPressed/Suppl 派生自绿色基底，overrides 只替换给出的键，
// 故必须一并覆盖，否则按下主按钮/loading 态会闪绿色
export const ACCENT_HOVER_HEX = {
  blue: '#4d73ff',
  violet: '#a78bfa',
  pink: '#f472b6',
  red: '#f87171',
  orange: '#fb923c',
  green: '#34d399',
  slate: '#7c8ba1',
}

// 旧 9 主题 → 预设组合（spec §4）
export const CLASSIC_THEMES = {
  Light: { themeMode: 'light', accent: 'blue' },
  Dark: { themeMode: 'dark', accent: 'blue' },
  Wine: { themeMode: 'light', accent: 'red' },
  Forest: { themeMode: 'dark', accent: 'green' },
  Greystone: { themeMode: 'light', accent: 'slate' },
  Asphalt: { themeMode: 'dark', accent: 'slate' },
  Soil: { themeMode: 'light', accent: 'orange' },
  SecretKey: { themeMode: 'light', accent: 'violet' },
  PRemoteM: { themeMode: 'dark', accent: 'violet' },
}

// reactive 保证切换强调色时 Naive 组件同步刷新（spec §11.3 风险点的处理）
export const themeState = reactive({ themeMode: 'dark', accent: 'blue', fontSize: 'M', font: '', systemDark: true })

/** themeMode === 'system' 时按系统偏好解析出实际生效的 'dark' | 'light' */
function resolvedMode() {
  return themeState.themeMode === 'system'
    ? (themeState.systemDark ? 'dark' : 'light')
    : themeState.themeMode
}

export function applyTheme() {
  document.documentElement.dataset.theme = resolvedMode()
  document.documentElement.dataset.accent = themeState.accent
  const sizes = { S: '12px', M: '13px', L: '14px', XL: '15px' }
  document.documentElement.style.fontSize = sizes[themeState.fontSize] || '13px'
  document.documentElement.style.fontFamily = themeState.font || '' // 空 = 继承/系统字体
}

export function setAppearance(patch) {
  Object.assign(themeState, patch)
  applyTheme()
  // PUT 为全量替换：必须始终携带全部四个字段（含 font），否则遗漏字段会被清空
  api
    .saveAppearance({
      themeMode: themeState.themeMode,
      accent: themeState.accent,
      fontSize: themeState.fontSize,
      font: themeState.font,
    })
    .catch(() => {}) // 桌面后端未运行（纯浏览器预览）时静默
}

export async function initTheme() {
  const mq = matchMedia('(prefers-color-scheme: dark)')
  themeState.systemDark = mq.matches
  mq.addEventListener('change', (e) => {
    themeState.systemDark = e.matches
    applyTheme()
  })
  applyTheme() // await 前先按默认值上色，首帧即有 data-theme（见 main.js 挂载策略）
  try {
    const saved = await api.getAppearance()
    // 白名单校验：手工编辑过的 1Remote.json 不应污染 data-theme 或向 Naive overrides 注入 undefined
    if (['dark', 'light', 'system'].includes(saved.themeMode)) themeState.themeMode = saved.themeMode
    if (ACCENTS.includes(saved.accent)) themeState.accent = saved.accent
    if (['S', 'M', 'L', 'XL'].includes(saved.fontSize)) themeState.fontSize = saved.fontSize
    if (typeof saved.font === 'string') themeState.font = saved.font
  } catch {
    /* 后端不可达时保持默认值 */
  }
  applyTheme()
}

/** Naive UI 主题（含 overrides），供 n-config-provider 绑定 —— computed 保持响应式 */
export function useNaiveTheme() {
  return computed(() => ({
    theme: resolvedMode() === 'dark' ? darkTheme : lightTheme,
    overrides: {
      common: {
        primaryColor: ACCENT_HEX[themeState.accent],
        primaryColorHover: ACCENT_HOVER_HEX[themeState.accent],
        primaryColorPressed: ACCENT_HEX[themeState.accent],
        primaryColorSuppl: ACCENT_HOVER_HEX[themeState.accent],
      },
    },
  }))
}

// 调试钩子（Task 21 验收后移除）：控制台可用
// window.__theme.setAppearance(window.__theme.CLASSIC_THEMES.Wine) 实时切换并持久化。
// 放在 themes 模块内而非 App.vue，避免 Task 13 重写 App.vue 时丢失
if (typeof window !== 'undefined') {
  window.__theme = { setAppearance, themeState, CLASSIC_THEMES, ACCENTS }
}
