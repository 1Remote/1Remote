/**
 * 右键菜单浮层定位（round17 收敛，三处消费：ServerTable 行菜单 / 文件夹空白菜单、
 * SideTree 树菜单）。此前三处各自内联「相对容器坐标 + Math.max/Math.min 内收」同构块，
 * 仅预算常量不同。
 *
 * 坐标系：传入视口坐标（clientX/clientY），返回相对 rootEl 左上角的坐标；rootEl 缺失
 * （根 ref 未挂/测试环境）时原样返回视口坐标。
 * 防溢出内收：x 上限 = 容器宽 - menuW（菜单预算宽）；y 上限 = 容器高 - menuH，下限 0
 *（极端矮窗回落贴顶）。menuH 传 null = y 不设上限（文件夹空白菜单沿用的旧行为）。
 * 预算值是各菜单项数 × 行高的经验值，非精确测量——菜单实际更矮时菜单整体偏上一点点，
 * 可接受（J2 起的既定口径）。
 *
 * @param {number} clientX 视口 x（contextmenu 事件的 e.clientX）
 * @param {number} clientY 视口 y
 * @param {HTMLElement|null} rootEl 锚定容器（菜单的定位祖先）
 * @param {number} menuW 菜单预算宽（px）
 * @param {number|null} menuH 菜单预算高（px）；null = 不限 y 上限
 * @returns {{x: number, y: number}} 相对容器坐标
 */
export function clampMenuPos(clientX, clientY, rootEl, menuW, menuH = null) {
  const r = rootEl?.getBoundingClientRect()
  if (!r) return { x: clientX, y: clientY }
  const x = Math.max(0, Math.min(clientX - r.left, r.width - menuW))
  const y = menuH == null ? Math.max(0, clientY - r.top) : Math.max(0, Math.min(clientY - r.top, r.height - menuH))
  return { x, y }
}
