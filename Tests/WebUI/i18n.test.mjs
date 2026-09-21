// i18n 完整性用例（spec §7「14 语言、零硬编码文案」；历史缺陷 H16、H28、H30、H31）。
// 五道门：键集平价 / 源码键引用可解析 / zh-CN 基准无英文句残留 / 模板零硬编码 CJK /
// Runner 术语统一。全部离线（读 src/locales 与源码），不依赖 convert-locales 管线。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import {
  loadLocales,
  walkFiles,
  read,
  vueSections,
  stripJsComments,
  stripHtmlComments,
  extractI18nKeys,
} from './helpers.mjs'

const locales = loadLocales()
const CODES = Object.keys(locales)
const EN = locales['en-US']
const ZH = locales['zh-CN']

// ---------- 门 1：键集平价（验收门禁，与 convert-locales --check 独立） ----------
describe('键集平价', () => {
  it('14 个语言键集与 en-US 完全一致', () => {
    assert.ok(CODES.length >= 14, `语言数 ${CODES.length} < 14`)
    const enKeys = Object.keys(EN).sort()
    for (const code of CODES) {
      assert.deepEqual(Object.keys(locales[code]).sort(), enKeys, `${code} 键集与 en-US 不一致`)
    }
  })
})

// ---------- 门 2：源码键引用可解析（防拼写错 / 误删词条） ----------
const NAMESPACE_FIRST_SEGMENTS = new Set(Object.keys(EN).map((k) => k.split('.')[0]))

/** 提取某文件的 i18n 键引用：t('…') 直调全收；键形态字面量按 en-US 命名空间过滤降噪 */
function keysUsedIn(file) {
  const content = read(file)
  const { template, script } = vueSections(content)
  const text = (script ? stripJsComments(script) : '') + '\n' + (template ? stripHtmlComments(template) : '')
  const used = new Set()
  for (const m of text.matchAll(/\bt\(\s*(['"])((?:(?!\1).)*)\1\s*[),]/g)) used.add(m[2])
  if (script) {
    for (const m of stripJsComments(script).matchAll(/(['"])((?:(?!\1).)+)\1/g)) {
      const candidate = m[2]
      const firstSeg = candidate.split('.')[0]
      if (NAMespaceHas(firstSeg) && /^[a-z][a-zA-Z0-9]*(?:\.[a-zA-Z0-9_]+)+$/.test(candidate)) used.add(candidate)
    }
  }
  return used
}
function NAMespaceHas(seg) {
  return NAMESPACE_FIRST_SEGMENTS.has(seg)
}

describe('源码 i18n 键引用完整性', () => {
  it('全部 .vue/.js（不含 locales）引用的键都在 en-US 与 zh-CN 中存在', () => {
    const missing = []
    for (const f of walkFiles('src', ['.vue', '.js'])) {
      if (f.includes('src/locales/')) continue
      for (const key of keysUsedIn(f)) {
        if (!(key in EN)) missing.push(`${f} → ${key} (en-US 缺)`)
        if (!(key in ZH)) missing.push(`${f} → ${key} (zh-CN 缺)`)
      }
    }
    assert.deepEqual(missing, [])
  })
})

// ---------- 门 3：zh-CN 手写基准无英文句残留（H16 类缺陷的唯一可判据：
// 其余 12 语言按 spec §7 允许回退 en-US，回退不是缺陷；zh-CN 缺译才是） ----------
describe('zh-CN 基准翻译完整性', () => {
  // 已知遗留（owner 裁决后移除）：品牌标语暂保留英文原文
  const ALLOWLIST = ['about.tagline']
  it('zh-CN 值不得为三词以上英文句（= 漏翻译，H16 类）', () => {
    const offenders = []
    for (const [k, v] of Object.entries(ZH)) {
      if (ALLOWLIST.includes(k)) continue
      const words = String(v)
        .split(/\s+/)
        .filter((w) => /^[A-Za-z]{2,}[.,!?:;()']*$/.test(w)).length
      if (words >= 3) offenders.push(`${k} = ${v}`)
    }
    assert.deepEqual(offenders, [])
  })
})

// ---------- 门 4：模板零硬编码 CJK（spec §7「界面零硬编码文案」） ----------
describe('零硬编码文案', () => {
  const CJK = /[\u4e00-\u9fff]/
  it('全部 .vue 模板（剥注释后）不含 CJK 字符', () => {
    const hits = []
    for (const f of walkFiles('src', ['.vue'])) {
      const { template } = vueSections(read(f))
      if (template && CJK.test(stripHtmlComments(template))) hits.push(f)
    }
    assert.deepEqual(hits, [])
  })
  it('脚本字符串字面量不含 CJK（console 调试串除外）', () => {
    const hits = []
    for (const f of walkFiles('src', ['.vue'])) {
      const { script } = vueSections(read(f))
      if (!script) continue
      const stripped = stripJsComments(script)
      for (const m of stripped.matchAll(/'[^']*'|"[^"]*"/g)) {
        if (CJK.test(m[0]) && !/console\.\w+\s*\([^)]*$/.test(stripped.slice(0, m.index))) hits.push(`${f}: ${m[0]}`)
      }
    }
    assert.deepEqual(hits, [])
  })
})

// ---------- 门 5：Runner 术语统一（H30 owner 裁决：所有语言不再出现「运行器/執行器」） ----------
describe('术语统一', () => {
  it('全部语言词条不再出现 运行器/執行器', () => {
    const hits = []
    for (const code of CODES) {
      for (const [k, v] of Object.entries(locales[code])) {
        if (/运行器|執行器/.test(String(v))) hits.push(`${code} ${k}`)
      }
    }
    assert.deepEqual(hits, [])
  })
})
