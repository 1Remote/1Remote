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
    Object.assign(themeState, await api.getAppearance())
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
        primaryColorHover: ACCENT_HEX[themeState.accent],
      },
    },
  }))
}
