// round16 重构侦察 2：CSS 死类扫描（style 块中定义的类 vs 模板/JS 中的使用）
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, extname } from 'node:path'

const files = []
;(function walk(d) {
  for (const f of readdirSync(d)) {
    const p = join(d, f)
    if (statSync(p).isDirectory()) walk(p)
    else if (/\.(js|vue|css)$/.test(extname(p))) files.push(p)
  }
})('src')

const report = []
for (const f of files.filter((x) => x.endsWith('.vue') || x.endsWith('.css'))) {
  const c = readFileSync(f, 'utf8')
  // 收集本文件 style 段定义的类
  const styleMatch = c.match(/<style[^>]*>([\s\S]*?)<\/style>/)
  if (!styleMatch) continue
  const style = styleMatch[1]
  const defined = new Set()
  let m
  const defRe = /\.([a-z][a-z0-9-]*)\s*[, {\n:]/gi
  while ((m = defRe.exec(style))) defined.add(m[1])
  // 使用面：模板段 + script 段 + 全库（全局类如 .act/.row 可能被别的文件用）
  const useScope = c.replace(styleMatch[1], '') // 本文件非 style 部分
  const others = files
    .filter((x) => x !== f)
    .map((x) => readFileSync(x, 'utf8'))
    .join('\n')
  for (const cls of defined) {
    const re = new RegExp(`['"\` .]${cls}['"\` .]|class="[^"]*\\b${cls}\\b`, 'm')
    if (!re.test(useScope) && !re.test(others)) report.push(`${f}: .${cls} 零使用`)
  }
}
console.log(report.length ? report.join('\n') : '（无发现）')
