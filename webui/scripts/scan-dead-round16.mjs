// round16 重构侦察：死 export / 死组件 / console 残留扫描（只读报告，不改代码）
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, extname } from 'node:path'

const SRC = 'src'
const files = []
;(function walk(d) {
  for (const f of readdirSync(d)) {
    const p = join(d, f)
    if (statSync(p).isDirectory()) walk(p)
    else if (/\.(js|vue)$/.test(extname(p))) files.push(p)
  }
})(SRC)

const contents = Object.fromEntries(files.map((f) => [f, readFileSync(f, 'utf8')]))
const all = files.map((f) => contents[f]).join('\n')

// 1) 死 export：收集命名导出，查全库引用（定义处除外）
const dead = []
for (const [f, c] of Object.entries(contents)) {
  if (!f.endsWith('.js')) continue
  const re = /^export (?:const|function|let)\s+([A-Za-z_$][\w$]*)/gm
  let m
  while ((m = re.exec(c))) {
    const name = m[1]
    // 引用计数：import {..., name} 或 import name / 动态属性访问兜底（from 路径无关，全库词法匹配）
    const imp = new RegExp(`\\b${name}\\b`, 'g')
    const uses = (all.match(imp) || []).length
    const selfUses = (c.match(imp) || []).length
    if (uses - selfUses <= 0) dead.push(`${f}: export ${name}（库内零外部引用）`)
  }
}
// 2) 死组件：components 下的 .vue 从未被 import
const vueFiles = files.filter((f) => f.endsWith('.vue') && f.includes('/components/'))
for (const vf of vueFiles) {
  const base = vf.split('/').pop().replace('.vue', '')
  const snake = base.replace(/[A-Z]/g, (ch) => '-' + ch.toLowerCase())
  const camel = base
  const re = new RegExp(`(?:${camel}|${snake})`, 'i')
  const refs = files.filter((f) => f !== vf && re.test(contents[f]))
  if (!refs.length) dead.push(`${vf}: 组件零引用`)
}
// 3) console 残留（warn/error 允许——dev 提醒类；log/debug 报告）
for (const [f, c] of Object.entries(contents)) {
  const hits = c.match(/console\.(log|debug)\([^\n]*/g) || []
  for (const h of hits) dead.push(`${f}: ${h.slice(0, 90)}`)
}

console.log(dead.length ? dead.join('\n') : '（无发现）')
console.log(`\nscanned ${files.length} files`)
