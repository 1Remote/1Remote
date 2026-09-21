// 测试共享工具：源码读取 / 注释剥离 / SFC 分段 / i18n 键提取 / webui 依赖解析。
// 全部为纯字符串处理（node --test 直接跑，无构建器参与）。
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { createRequire } from 'node:module'

// webui/ 根（本文件位于仓库根下 Tests/WebUI/）
export const WEBUI_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../webui')
export const SRC = path.join(WEBUI_ROOT, 'src')

// 本目录在 webui 包之外，裸包名 import（如 'vue'）按文件位置向上找不到
// webui/node_modules——统一从 webui/package.json 出发 createRequire 解析。
// require 拿到的是 CJS 具名导出，与 ESM 版 API 一致（测试只用响应式 API）。
const requireFromWebui = createRequire(new URL('../../webui/package.json', import.meta.url))
export const loadVue = () => requireFromWebui('vue')

/** 读仓库内文本文件（相对 webui/ 或绝对路径） */
export function read(rel) {
  return readFileSync(path.isAbsolute(rel) ? rel : path.join(WEBUI_ROOT, rel), 'utf8')
}

/**
 * 剥离 JS 注释（// 与 /* *\/），感知引号字符串——字符串内的 "//"（如 URL）与
 * 注释里提及的 i18n 键（如历史注释里的 tree.noDatasources）都不会污染后续提取。
 */
export function stripJsComments(code) {
  let out = ''
  let i = 0
  let quote = null // 当前所处字符串的引号字符
  while (i < code.length) {
    const c = code[i]
    const next = code[i + 1]
    if (quote) {
      out += c
      if (c === '\\') {
        out += next ?? ''
        i += 2
        continue
      }
      if (c === quote) quote = null
      i++
      continue
    }
    if (c === '"' || c === "'" || c === '`') {
      quote = c
      out += c
      i++
      continue
    }
    if (c === '/' && next === '/') {
      while (i < code.length && code[i] !== '\n') i++
      continue
    }
    if (c === '/' && next === '*') {
      i += 2
      while (i < code.length && !(code[i] === '*' && code[i + 1] === '/')) i++
      i += 2
      continue
    }
    out += c
    i++
  }
  return out
}

/** Vue SFC 分段：{ template, script, style }（无该段为 null）。template 取首个 <template> 到最后一个 </template>。 */
export function vueSections(content) {
  const tplStart = content.indexOf('<template>')
  const tplEnd = content.lastIndexOf('</template>')
  const template = tplStart >= 0 && tplEnd > tplStart ? content.slice(tplStart + '<template>'.length, tplEnd) : null
  const scriptMatch = /<script[^>]*>([\s\S]*?)<\/script>/.exec(content)
  const script = scriptMatch ? scriptMatch[1] : null
  const styleMatch = /<style[^>]*>([\s\S]*?)<\/style>/.exec(content)
  const style = styleMatch ? styleMatch[1] : null
  return { template, script, style }
}

/** 剥离 HTML 注释（模板段用） */
export function stripHtmlComments(html) {
  return html.replace(/<!--[\s\S]*?-->/g, '')
}

// i18n 键形态：点分小驼峰段（不含连字符/冒号/斜杠——排除 '1r-sort'、'x-1r-server-row' 等）
const KEY_SHAPE = /^[a-z][a-zA-Z0-9]*(?:\.[a-zA-Z0-9_]+)+$/

/**
 * 从（已剥离注释的）源码中提取 i18n 键候选：
 * - t('a.b.c') 直接调用；
 * - 任何键形态的字符串字面量（HINT_KEYS 数组、schema labelKey/placeholderKey 等间接消费）。
 */
export function extractI18nKeys(code) {
  const keys = new Set()
  for (const m of code.matchAll(/\bt\(\s*(['"])((?:(?!\1).)*)\1\s*[),]/g)) keys.add(m[2])
  for (const m of code.matchAll(/(['"])((?:(?!\1).)+)\1/g)) {
    if (KEY_SHAPE.test(m[2])) keys.add(m[2])
  }
  return keys
}

/** 递归收集目录下指定扩展名文件（相对路径，POSIX 分隔符） */
export function walkFiles(dirRel, exts) {
  const out = []
  const abs = path.isAbsolute(dirRel) ? dirRel : path.join(WEBUI_ROOT, dirRel)
  for (const name of readdirSync(abs)) {
    const p = path.join(abs, name)
    if (statSync(p).isDirectory()) out.push(...walkFiles(p, exts))
    else if (exts.some((e) => name.endsWith(e))) out.push(path.relative(WEBUI_ROOT, p).split(path.sep).join('/'))
  }
  return out.sort()
}

/** 读取 14 个语言 JSON：{ code: flatKeySet } */
export function loadLocales() {
  const dir = path.join(SRC, 'locales')
  const locales = {}
  for (const f of walkFiles('src/locales', ['.json'])) {
    const code = path.basename(f, '.json')
    locales[code] = JSON.parse(read(f))
  }
  return locales
}
