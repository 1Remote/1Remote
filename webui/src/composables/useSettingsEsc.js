/**
 * 设置分组组件共用的 Esc 交互（自各分组抽取的同型样板，语义统一描述）：
 *
 * - shield：浮层组件（n-select 等）@update:show 的展开计数维护。计数经
 *   settingsEscShield（provide/inject）上报给 SettingsView 的 Esc 返回链——n-select
 *   展开时首次 Esc 只应关闭下拉（naive 在组件层消化），不能误触「退出设置页」或「关模态」。
 *   链序与计数自愈机制的缺陷分析见 SettingsView 文件头注释。
 * - bindModalEsc：模态开着时在 capture 阶段截停 Esc 只关模态（SettingsView 的返回导航
 *   让位）；浮层计数 >0（下拉开着）时让位——Esc 先由 naive 组件层消化关下拉。
 *   targets 按声明序尝试（同组多模态互斥时第一个开着的赢）。
 */
import { inject, onBeforeUnmount, onMounted } from 'vue'

export function useSettingsEsc() {
  const escShield = inject('settingsEscShield', null)

  function shield(show) {
    if (escShield) escShield.open += show ? 1 : -1
  }

  // @param {Array<{isOpen: () => boolean, close: () => void}>} targets 按序的模态开关对
  function bindModalEsc(targets) {
    const onEscCapture = (e) => {
      if (e.key !== 'Escape') return
      if (escShield && escShield.open > 0) return
      for (const t of targets) {
        if (t.isOpen()) {
          e.stopPropagation()
          t.close()
          return
        }
      }
    }
    onMounted(() => window.addEventListener('keydown', onEscCapture, true))
    onBeforeUnmount(() => window.removeEventListener('keydown', onEscCapture, true))
  }

  return { shield, bindModalEsc }
}
