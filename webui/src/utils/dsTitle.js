/**
 * 数据源状态点悬停 title 的单一实现（round8 重构收敛）。
 * 三处消费（侧树根行 / 底部状态栏 / 设置页数据源卡片）此前各持一份逐行同构的拷贝
 * （K16 修复时又复制了一份）——同一颗点三处语言必须一致，收敛后任何一侧的调整自然
 * 三侧生效。纯函数工厂（t 由调用方注入，保持模块无 i18n 依赖）：
 * - connected / reconnecting / disconnected 三态走状态栏词条（含数据源名）；
 * - H31 定案：不直出后端英文裸枚举。
 *
 * 重连倒计时（2026-09-22 用户报告修复）：此前直出后端 ReconnectInfo 格式化串，
 * 仅随 30s 轮询刷新——悬停值最多滞后 30s。现后端附带 reconnectEtaSeconds
 * （剩余秒数快照），live 上下文注入 { now, fetchedAt }（useNow 共享秒级时钟 +
 * useServers 的 datasourcesFetchedAt）本地逐秒倒数：remaining = eta - 经过秒数，
 * 归零后显示"重连中"。无 live 上下文或旧后端无 eta 字段 → 回落旧行为（后端串）。
 */
export function makeDsDotTitle(t, live = null) {
  return (d) => {
    if (d.status === 'connected') return t('statusbar.dsConnected', { name: d.name })
    if (d.status === 'reconnecting') {
      if (live && Number.isFinite(d.reconnectEtaSeconds)) {
        const elapsed = Math.floor((live.now.value - live.fetchedAt.value) / 1000)
        const left = d.reconnectEtaSeconds - elapsed
        if (left > 0) return t('statusbar.dsNextRetry', { name: d.name, time: fmtDuration(left) })
        return t('statusbar.dsReconnecting', { name: d.name }) // 已到点：正在重连
      }
      return t('statusbar.dsReconnecting', { name: d.name }) + (d.reconnectInfo ? ' · ' + d.reconnectInfo : '')
    }
    return t('statusbar.dsDisconnected', { name: d.name })
  }
}

/** 秒数 → "2m55s" 风格时长（对齐后端 GetTime 的 d/h/m/s 拼接）。 */
export function fmtDuration(totalSeconds) {
  let s = Math.max(0, Math.floor(totalSeconds))
  const parts = []
  const units = [
    ['d', 86400],
    ['h', 3600],
    ['m', 60],
  ]
  for (const [suffix, size] of units) {
    if (s >= size) {
      parts.push(Math.floor(s / size) + suffix)
      s %= size
    }
  }
  if (s > 0 || parts.length === 0) parts.push(s + 's')
  return parts.join('')
}

/**
 * 数据源状态点 → CSS 类的单一实现（round11 重构收敛，与 makeDsDotTitle 同居：
 * 状态点的"颜色"与"悬停说明"是同一语义的两半）。三处消费（侧树根行 / 底部状态栏 /
 * 设置页数据源卡片）此前各持一份同构拷贝。
 */
export const dsDotClass = (status) => (status === 'connected' ? 'ok' : status === 'reconnecting' ? 'bad' : 'idle')
