/**
 * 自然 IP 排序比较器（地址列表头排序，对齐 WPF `SubTitleSortByNaturalIp` 语义）：
 * 双方均为完整 IPv4（四段、纯数字）时按 '.' 分段数值比较（'10.0.0.2' < '10.0.0.10'）；
 * 否则（主机名/混合）回退码点序字符串比较——不用 localeCompare，保证与单测/后端语义一致。
 * @returns {number} <0 a 在前；0 相等；>0 b 在前
 */
function isIPv4(s) {
  return typeof s === 'string' && /^\d{1,3}(\.\d{1,3}){3}$/.test(s)
}

export function naturalIpCompare(a, b) {
  if (isIPv4(a) && isIPv4(b)) {
    const pa = a.split('.')
    const pb = b.split('.')
    for (let i = 0; i < 4; i++) {
      const d = Number(pa[i]) - Number(pb[i])
      if (d) return d
    }
    return 0
  }
  const sa = String(a ?? '')
  const sb = String(b ?? '')
  return sa < sb ? -1 : sa > sb ? 1 : 0
}
