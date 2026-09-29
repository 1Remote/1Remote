// ────────────────────────────────────────────────────────────────────────────
// 新人导览 · kv-pairs.test.mjs · L1 纯逻辑
// ▸ 本文件测什么：键值行编辑器（Runner 环境变量、RDP 附加设置等的行式编辑）的纯逻辑：
//   行文本 ↔ 结构化数组往返、半行识别、外部回读合并。
// ▸ 单独运行：cd webui && node --test ../Tests/WebUI/kv-pairs.test.mjs
// ▸ 全量运行与结果怎么看：Tests/WebUI/README.md §0；断言红了怎么办：README §8
// ────────────────────────────────────────────────────────────────────────────
// 键值行编辑器纯逻辑用例（round11 用户报告 BUG/U6：Runner 环境变量/特殊字符
// "行文本解析丢半行 + 保存回读覆盖文本域"清空机制移除后的行为锁定）。
// 语义单一来源：src/editor/kvPairs.js —— 行态 ↔ [{Key,Value}] PascalCase 数组。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import {
  rowsFromPairs,
  pairsFromRows,
  isFullEmpty,
  rowIsHalf,
  samePairs,
  mergeExternal,
} from '../../webui/src/editor/kvPairs.js'
import { read } from './helpers.mjs'

// ---------- 行态往返 ----------
describe('rowsFromPairs / pairsFromRows 往返', () => {
  it('PascalCase 数组 → 行态 → 数组逐字节往返', () => {
    const pairs = [
      { Key: 'LANG', Value: 'zh_CN' },
      { Key: 'USER', Value: '%1RM_USERNAME%' },
    ]
    assert.deepEqual(pairsFromRows(rowsFromPairs(pairs)), pairs)
  })

  it('null/undefined/非数组按空表处理', () => {
    assert.deepEqual(rowsFromPairs(null), [])
    assert.deepEqual(rowsFromPairs(undefined), [])
    assert.deepEqual(pairsFromRows(rowsFromPairs(undefined)), [])
  })

  it('值保真：Key/Value 原样保留，不做 trim（WPF TextBox 直绑平价）', () => {
    const out = pairsFromRows([{ key: '  PATH ', value: 'C:\\bin;' }])
    assert.deepEqual(out, [{ Key: '  PATH ', Value: 'C:\\bin;' }])
  })
})

// ---------- 行分类 ----------
describe('行分类 isFullEmpty / rowIsHalf', () => {
  it('全空行：key 与 value 均为空白（含纯空格）', () => {
    assert.equal(isFullEmpty({ key: '', value: '' }), true)
    assert.equal(isFullEmpty({ key: '  ', value: ' ' }), true)
    assert.equal(isFullEmpty({ key: 'A', value: '' }), false)
  })

  it('半填行：value 非空但 key 为空（含纯空格 key）', () => {
    assert.equal(rowIsHalf({ key: '', value: 'x' }), true)
    assert.equal(rowIsHalf({ key: '   ', value: 'x' }), true)
    assert.equal(rowIsHalf({ key: 'A', value: 'x' }), false)
    assert.equal(rowIsHalf({ key: '', value: '' }), false)
  })
})

// ---------- 序列化策略 ----------
describe('pairsFromRows 行过滤', () => {
  it('全空行不进输出（加行未输入不产生数据）', () => {
    const out = pairsFromRows([
      { key: 'A', value: '1' },
      { key: '', value: '' },
      { key: ' ', value: ' ' },
    ])
    assert.deepEqual(out, [{ Key: 'A', Value: '1' }])
  })

  it('半填行保留在输出里（用户输入不丢——清空机制回归锁定）', () => {
    const out = pairsFromRows([
      { key: 'A', value: '1' },
      { key: '', value: '正在输入的值' },
    ])
    assert.deepEqual(out, [
      { Key: 'A', Value: '1' },
      { Key: '', Value: '正在输入的值' },
    ])
  })
})

// ---------- 回环挡板 ----------
describe('samePairs 回环挡板', () => {
  it('emit 后同内容回声不判为外部变化（半输入态不被打断）', () => {
    const emitted = pairsFromRows([
      { key: 'A', value: '1' },
      { key: '', value: '半填' },
    ])
    assert.equal(samePairs(emitted, [...emitted.map((p) => ({ ...p }))]), true)
  })

  it('全空行不影响内容比较（加空行后回声仍视为相同）', () => {
    const a = pairsFromRows([
      { key: 'A', value: '1' },
      { key: '', value: '' },
    ])
    const b = [{ Key: 'A', Value: '1' }]
    assert.equal(samePairs(a, b), true)
  })

  it('真实差异（值变化/增行）判为不同', () => {
    assert.equal(samePairs([{ Key: 'A', Value: '1' }], [{ Key: 'A', Value: '2' }]), false)
    assert.equal(
      samePairs(
        [{ Key: 'A', Value: '1' }],
        [
          { Key: 'A', Value: '1' },
          { Key: 'B', Value: '2' },
        ]
      ),
      false
    )
  })
})

// ---------- 外部合并 ----------
describe('mergeExternal 外部回读合并', () => {
  it('外部值为主整体接收（服务器归一/winscp 旁路注入的行照单全收）', () => {
    const rows = mergeExternal(
      [],
      [
        { Key: '%', Value: '%25' },
        { Key: ';', Value: '%3B' },
      ]
    )
    assert.deepEqual(rows, [
      { key: '%', value: '%25' },
      { key: ';', value: '%3B' },
    ])
  })

  it('本地半填行在合并后保留（保存成功瞬间不丢用户正在输入的行）', () => {
    const local = [
      { key: 'A', value: '1' },
      { key: '', value: '半填' },
    ]
    // 外部回读只含完整行（如服务端曾过滤半填行的旧形态）
    const rows = mergeExternal(local, [{ Key: 'A', Value: '1' }])
    assert.deepEqual(rows, [
      { key: 'A', value: '1' },
      { key: '', value: '半填' },
    ])
  })

  it('回声已含同一半填行时不重复追加（去重键=内容）', () => {
    const local = [{ key: '', value: '半填' }]
    const rows = mergeExternal(local, [{ Key: '', Value: '半填' }])
    assert.deepEqual(rows, [{ key: '', value: '半填' }])
  })

  it('全空行不参与合并（无内容可保留）', () => {
    const rows = mergeExternal([{ key: '', value: '' }], [{ Key: 'A', Value: '1' }])
    assert.deepEqual(rows, [{ key: 'A', value: '1' }])
  })
})

// ---------- 清空机制移除的源码级回归 ----------
describe('清空机制移除（源码回归）', () => {
  it('RunnerGroup 不再存在行文本解析（parseKvText/kvToText/initTexts 已删）', () => {
    const src = read('src/components/settings/RunnerGroup.vue')
    assert.ok(!src.includes('parseKvText'), 'parseKvText 应已删除')
    assert.ok(!src.includes('kvToText'), 'kvToText 应已删除')
    assert.ok(!src.includes('initTexts'), 'initTexts 应已删除')
    assert.ok(!src.includes('envTexts'), 'envTexts 行文本态应已删除')
  })

  it('RunnerCard 环境变量/特殊字符改用 KvPairsField（WPF 行编辑对齐）', () => {
    const src = read('src/components/settings/RunnerCard.vue')
    assert.ok(src.includes('KvPairsField'), '应渲染 KvPairsField')
    assert.ok(src.includes("emit('env-rows'"), '环境变量应上抛行数组')
    assert.ok(src.includes("emit('special-rows'"), '特殊字符应上抛行数组')
    assert.ok(!src.includes('envText'), '不应再有 envText 行文本 props')
    assert.ok(!src.includes('specialText'), '不应再有 specialText 行文本 props')
  })
})
