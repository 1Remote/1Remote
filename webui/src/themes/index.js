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
const ACCENT_HOVER_HEX = {
  blue: '#4d73ff',
  violet: '#a78bfa',
  pink: '#f472b6',
  red: '#f87171',
  orange: '#fb923c',
  green: '#34d399',
  slate: '#7c8ba1',
}

// 实底强调变体（与 theme.css 中 --accent-solid 保持一致，双基底同值）：白字在亮 --accent
// 上 orange/green 仅 2.80/2.54（WCAG FAIL），在 solid 上全 7 色 ≥5.18 AA。hover/pressed
// 再加深一档（白字对比只增不减）
const ACCENT_SOLID_HEX = {
  blue: '#2c5aff',
  violet: '#6d28d9',
  pink: '#be185d',
  red: '#b91c1c',
  orange: '#c2410c',
  green: '#047857',
  slate: '#475569',
}
const ACCENT_SOLID_HOVER_HEX = {
  blue: '#274fe0',
  violet: '#6023bf',
  pink: '#a71552',
  red: '#a31919',
  orange: '#ab390b',
  green: '#046a4c',
  slate: '#3e4b5c',
}
const ACCENT_SOLID_PRESSED_HEX = {
  blue: '#2144bf',
  violet: '#521ea3',
  pink: '#8f1246',
  red: '#8b1515',
  orange: '#923109',
  green: '#035a41',
  slate: '#35404f',
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
  return themeState.themeMode === 'system' ? (themeState.systemDark ? 'dark' : 'light') : themeState.themeMode
}

/** 当前生效基底是否为 dark（themeMode==='system' 时随系统偏好；读 themeState 保持响应式） */
export function isDarkMode() {
  return resolvedMode() === 'dark'
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

/** px → rem（基准 13px = M 档根字号），保留 4 位小数 */
function px2rem(px) {
  return `${Math.round((px / 13) * 10000) / 10000}rem`
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
        // 组件字号改用 rem：applyTheme 改 documentElement.fontSize 时 naive 组件文字
        // 同步缩放。默认值取自 naive common（_common.mjs），仅换单位。
        // fontSizeSmall 对齐 CSS --fs-body（12.5px，第三轮 G27）：naive 默认 14px 与同屏的
        // 自绘表单控件（kvl/kvm/ff 系、bb-btn/act 系均 fs-body）字号差一档——编辑抽屉里
        // naive small 输入与自绘输入混排时文字大小不一致；small 是 naive 组件在表单里的
        // 主力档位，对齐后 12.5 < 14(medium) 档序不变
        fontSize: px2rem(14),
        fontSizeMini: px2rem(12),
        fontSizeTiny: px2rem(12),
        fontSizeSmall: px2rem(12.5),
        fontSizeMedium: px2rem(14),
        fontSizeLarge: px2rem(15),
        fontSizeHuge: px2rem(16),
        // 圆角对齐 theme.css 三档：Naive 默认 3px 与自定义控件 6/5px 会同屏混用（工具栏
        // n-button 3px + bb-btn 6px + tt-btn 5px）；控件档对齐 --radius-ctrl=6px。
        // 小档同样 6px（第三轮 G7）：4px 不在 CSS 圆角档（3/6/8）内——naive 侧唯一消费面
        // 是 small/tiny 按钮（对话框按钮、模态页脚钮），与同屏自绘按钮（bb-btn/ed-btn 6px）
        // 并排差 2px；n-input 无 small 圆角变体（单一 borderRadius），不受影响
        borderRadius: '6px',
        borderRadiusSmall: '6px',
      },
      // primary 实心按钮底色改喂深变体 solid（--accent-solid 同值）：白字在原亮 accent 上
      // orange/green 仅 2.80/2.54 FAIL，在 solid/hover/pressed 上全组合 ≥5.18 AA。仅覆盖
      // Button 组件级 colorPrimary 系——common.primaryColor 仍为原 accent，开关/复选框/
      // 下拉选中/焦点边框等非文字强调面维持原观感（solid 在暗底上 <3:1 不宜作大面积状态色）
      Button: {
        colorPrimary: ACCENT_SOLID_HEX[themeState.accent],
        colorHoverPrimary: ACCENT_SOLID_HOVER_HEX[themeState.accent],
        colorPressedPrimary: ACCENT_SOLID_PRESSED_HEX[themeState.accent],
        colorFocusPrimary: ACCENT_SOLID_HOVER_HEX[themeState.accent],
        colorDisabledPrimary: ACCENT_SOLID_HEX[themeState.accent],
        // primary 实心/secondary 按钮文字固定白色 = theme.css --text-on-accent 的 JS 镜像
        //（键名以 naive Button self 变量为准，见 node_modules/naive-ui/es/button/styles/light.mjs）：
        // 暗色基底的 baseColor=#000 使 textColorPrimary 系派生为黑字（naive 暗色以 baseColor
        // 反差取字色），在蓝/紫等强调色上不可读；亮色基底本就是 #FFF，覆盖后行为不变。
        // ghost/text 型保持强调色文字（透明底白字不可读），不在覆盖范围
        textColorPrimary: '#FFFFFF',
        textColorHoverPrimary: '#FFFFFF',
        textColorPressedPrimary: '#FFFFFF',
        textColorFocusPrimary: '#FFFFFF',
        textColorDisabledPrimary: '#FFFFFF',
        // error 实心按钮（对话框删除确认的 positive）同规则：naive 派生 errorColor 系在
        // 暗色基底偏亮（dark errorHover 比 errorDefault 更亮，白字 <4.5），亮色基底白字虽过
        // 但双色不一致——统一收敛到 light --danger 同值 #dc2626 阶梯（白字 4.83/5.74/6.47
        // 全 AA；hover/pressed 加深与 accent-solid 族同向，白字对比只增不减）。
        // 文字固定白：dark 基底 baseColor=#000 会派生黑字（同 textColorPrimary 的理由）
        colorError: '#dc2626',
        colorHoverError: '#c81e1e',
        colorPressedError: '#b91c1c',
        colorFocusError: '#c81e1e',
        colorDisabledError: '#dc2626',
        textColorError: '#FFFFFF',
        textColorHoverError: '#FFFFFF',
        textColorPressedError: '#FFFFFF',
        textColorFocusError: '#FFFFFF',
        textColorDisabledError: '#FFFFFF',
      },
      // 下拉选中项文字（第三轮 G1，本轮唯一 P1）：common.primaryColor 仍喂亮 accent，而
      // naive select-menu 的 optionTextColorActive/optionCheckColor 直接取 primaryColor
      //（node_modules/_internal/select-menu/styles/light.mjs 派生关系），亮 accent 作弹层
      // 小文字在 14 组合里 12 个 <4.5（默认 dark+blue 仅 1.74，light orange/green 2.80/2.54）。
      // 覆盖为：light 基 = ACCENT_SOLID 深变体（×白弹层底 5.18-7.58 全 AA，与 F1 高亮族
      // 同一深变体来源）；dark 基 = #e8e9ea（×弹层底 #48484e = 7.47）。optionTextColorPressed
      // 同源（naive 取 primaryColorPressed=亮 accent，同为选中项按下瞬态文字）
      InternalSelectMenu: {
        optionTextColorActive: resolvedMode() === 'dark' ? '#e8e9ea' : ACCENT_SOLID_HEX[themeState.accent],
        optionCheckColor: resolvedMode() === 'dark' ? '#e8e9ea' : ACCENT_SOLID_HEX[themeState.accent],
        optionTextColorPressed: resolvedMode() === 'dark' ? '#e8e9ea' : ACCENT_SOLID_HEX[themeState.accent],
      },
    },
  }))
}
