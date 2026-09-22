/**
 * 数据源状态点悬停 title 的单一实现（round8 重构收敛）。
 * 三处消费（侧树根行 / 底部状态栏 / 设置页数据源卡片）此前各持一份逐行同构的拷贝
 * （K16 修复时又复制了一份）——同一颗点三处语言必须一致，收敛后任何一侧的调整自然
 * 三侧生效。纯函数工厂（t 由调用方注入，保持模块无 i18n 依赖）：
 * - connected / reconnecting（附重连倒计时）/ disconnected 三态走状态栏词条（含数据源名）；
 * - H31 定案：不直出后端英文裸枚举。
 */
export function makeDsDotTitle(t) {
  return (d) => {
    if (d.status === 'connected') return t('statusbar.dsConnected', { name: d.name })
    if (d.status === 'reconnecting')
      return t('statusbar.dsReconnecting', { name: d.name }) + (d.reconnectInfo ? ' · ' + d.reconnectInfo : '')
    return t('statusbar.dsDisconnected', { name: d.name })
  }
}

/**
 * 数据源状态点 → CSS 类的单一实现（round11 重构收敛，与 makeDsDotTitle 同居：
 * 状态点的"颜色"与"悬停说明"是同一语义的两半）。三处消费（侧树根行 / 底部状态栏 /
 * 设置页数据源卡片）此前各持一份同构拷贝。
 */
export const dsDotClass = (status) => (status === 'connected' ? 'ok' : status === 'reconnecting' ? 'bad' : 'idle')
