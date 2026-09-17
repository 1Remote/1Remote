/**
 * ColorHex（C# 侧 #AARRGGBB，Newtonsoft 序列化）→ CSS 颜色的归一化纯函数。
 *
 * 两个域的差异：
 *  - C# ColorHex 是 #AARRGGBB（alpha 在前），默认值 '#00000000' = 全透明；
 *  - CSS 的 8 位 hex 是 #RRGGBBAA（alpha 在后）。
 * 因此把 C# 值直接当 CSS 颜色用会得到「非法值（被浏览器丢弃）」或「全透明（暗色下不可见）」。
 *
 * 这里统一口径：alpha=00（无色）与格式非法都返回 null——由调用方回退到中性瓦片样式
 * （--bg-elevated + 边框 + --text-3，EditorDrawer 头瓦片与 ServerRow 回退瓦片共用该语义）。
 */

/**
 * @param {string|undefined|null} colorHex C# ColorHex（#AARRGGBB）或列表 DTO color 原文
 * @returns {string|null} 不透明的 '#RRGGBB'；全透明/格式非法 → null
 */
export function opaqueHex(colorHex) {
  if (typeof colorHex !== 'string' || !/^#[0-9a-fA-F]{8}$/.test(colorHex)) return null
  if (colorHex.slice(1, 3) === '00') return null
  return '#' + colorHex.slice(3)
}
