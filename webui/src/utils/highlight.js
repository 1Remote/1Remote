/**
 * 搜索命中高亮：对查询串按空白分词，逐 token 做「大小写不敏感的子串」
 * 标记，把文本切成 [{text, hit}] 分段供模板渲染（hit=true 包 .hl 强调样式）。
 * 纯函数（node 断言与 ServerRow 共用）：
 * - 查询为空/纯空白或文本为空 → 单段整体不命中（不高亮）
 * - 多 token 各自独立标记，重叠命中自然合并（字符级命中掩码）
 * - "#tag" 过滤词对名称/地址做子串匹配不会命中 → 不高亮（tag 过滤本就不落在文本上）
 * - 拼音/首字母命中（后端 KeywordMatchService 能力）无法定位原文位置 → 不高亮，
 *   与 WPF Launcher 只高亮可定位命中的行为一致
 */
export function splitHighlight(text, query) {
  const s = text == null ? '' : String(text)
  const tokens = String(query || '')
    .split(/\s+/)
    .filter(Boolean)
  if (!tokens.length || !s) return [{ text: s, hit: false }]

  // 字符级命中掩码：所有 token 的所有出现（含跨 token 重叠）统一标记后切段
  const hits = new Array(s.length).fill(false)
  const lower = s.toLowerCase()
  for (const token of tokens) {
    const tl = token.toLowerCase()
    let from = 0
    for (;;) {
      const i = lower.indexOf(tl, from)
      if (i < 0) break
      for (let k = i; k < i + tl.length; k++) hits[k] = true
      from = i + tl.length
    }
  }

  const segments = []
  let start = 0
  for (let i = 1; i <= s.length; i++) {
    if (i === s.length || hits[i] !== hits[start]) {
      segments.push({ text: s.slice(start, i), hit: hits[start] })
      start = i
    }
  }
  return segments
}
