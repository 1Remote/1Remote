// 连接编辑器 schema 结构用例（spec §5；历史缺陷 round4-设计改动4）。
// schemas.js 是纯数据模块（无 vue 依赖），直接断言 9 协议分组结构——
// 「地址/端口入凭据组」的行序是 owner 2026-09-21 拍板的设计，回归中不得漂移。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { PROTOCOLS } from '../../webui/src/editor/schemas.js'
import { diffPatch } from '../../webui/src/editor/patch.js'
import { loadLocales } from './helpers.mjs'

// ---------- 批量编辑补丁（spec 风险 2「批量合并逻辑移植必须带单元测试」） ----------
describe('批量编辑补丁 diffPatch', () => {
  it('无变化返回空补丁（后端契约：缺失键=保持不变）', () => {
    assert.deepEqual(diffPatch({ a: 1, b: 'x' }, { a: 1, b: 'x' }), {})
  })
  it('仅变化键进入补丁；数组整体覆盖语义（tags 显式覆盖）', () => {
    assert.deepEqual(diffPatch({ tags: ['a'] }, { tags: ['a', 'b'] }), {
      tags: ['a', 'b'],
    })
    assert.deepEqual(diffPatch({ port: 23 }, { port: 23 }), {})
  })
})
describe('diffPatch 补充边界', () => {
  it('current 缺键不进补丁（不表达「删除」=保持不变）', () => {
    assert.deepEqual(diffPatch({ a: 1 }, {}), {})
  })
  it('null 是可写入的明确值（与 undefined 不相等）', () => {
    assert.deepEqual(diffPatch({ a: 1 }, { a: null }), { a: null })
  })
  it('相等判定=JSON 序列化比较（对象键序敏感——保守覆盖）', () => {
    assert.deepEqual(diffPatch({ o: { b: 1, a: 2 } }, { o: { a: 2, b: 1 } }), {
      o: { a: 2, b: 1 },
    })
  })
})

// ---------- 9 协议 schema 结构（地址/端口入凭据组 + 结构完整性） ----------
const HAS_CRED = ['RDP', 'SSH', 'SFTP', 'FTP', 'VNC', 'RemoteApp'] // round4-设计改动4：六协议
const NO_CRED = ['Telnet', 'Serial', 'APP']
const ADDR_TRIO = ['Address', 'Port', 'IsPingBeforeConnect']

function findGroup(schema, id) {
  return schema.groups.find((g) => g.id === id)
}
function schemaHasField(schema, key) {
  return schema.groups.some((g) => g.fields.some((f) => f.key === key))
}

describe('编辑器 schema：地址/端口入「凭据」组（round4-设计改动4）', () => {
  it('六协议凭据组 pre 段前三位 = 地址/端口/可用性检测，且位于用户名之前', () => {
    for (const key of HAS_CRED) {
      const cred = findGroup(PROTOCOLS[key], 'credential')
      assert.ok(cred, `${key} 应有凭据组`)
      const pre = cred.fields.filter((f) => f.credRole === 'pre')
      assert.deepEqual(
        pre.slice(0, 3).map((f) => f.key),
        ADDR_TRIO,
        `${key} 凭据组 pre 段前三 = ${ADDR_TRIO.join(',')}`
      )
      const idxAddr = cred.fields.findIndex((f) => f.key === 'Address')
      const idxUser = cred.fields.findIndex((f) => f.key === 'UserName')
      assert.ok(idxAddr < idxUser, `${key} 地址必须在用户名之前（WPF 目标列行序）`)
    }
  })
  it('无凭据组的协议（Telnet/Serial/APP）地址不挂 credRole', () => {
    for (const key of NO_CRED) {
      assert.equal(findGroup(PROTOCOLS[key], 'credential'), undefined, `${key} 不应有凭据组`)
      for (const g of PROTOCOLS[key].groups) {
        for (const f of g.fields) {
          if (f.key === 'Address') assert.equal(f.credRole, undefined, `${key} 的 Address 不应挂 credRole`)
        }
      }
    }
  })
  it('Serial 无地址概念（无 Address/Port 字段）', () => {
    assert.equal(schemaHasField(PROTOCOLS.Serial, 'Address'), false)
    assert.equal(schemaHasField(PROTOCOLS.Serial, 'Port'), false)
  })
  it('RDP 新建默认值：3389 / Administrator / 检测开启（round4-设计改动4 回归口径）', () => {
    assert.equal(PROTOCOLS.RDP.defaults.Port, '3389')
    assert.equal(PROTOCOLS.RDP.defaults.UserName, 'Administrator')
    assert.equal(PROTOCOLS.RDP.defaults.IsPingBeforeConnect, true)
  })
})

describe('编辑器 schema 结构完整性（键唯一 / visibleWhen 可解析 / 词条存在）', () => {
  const locales = loadLocales()

  it('每协议字段键唯一', () => {
    for (const schema of Object.values(PROTOCOLS)) {
      const seen = new Set()
      for (const g of schema.groups) {
        for (const f of g.fields) {
          assert.ok(!seen.has(f.key), `字段键重复: ${f.key}`)
          seen.add(f.key)
        }
      }
    }
  })
  it('visibleWhen 引用的字段在本协议 schema 内存在（含数组多条件形态）', () => {
    for (const [proto, schema] of Object.entries(PROTOCOLS)) {
      for (const g of schema.groups) {
        for (const f of g.fields) {
          if (!f.visibleWhen) continue
          const conds = Array.isArray(f.visibleWhen) ? f.visibleWhen : [f.visibleWhen]
          for (const cond of conds) {
            assert.ok(schemaHasField(schema, cond.field), `${proto} visibleWhen 引用不存在的字段 ${cond.field}`)
          }
        }
      }
    }
  })
  it('schema 内全部 labelKey/placeholderKey 在 en-US 与 zh-CN 基准中存在', () => {
    const collected = {}
    for (const [proto, schema] of Object.entries(PROTOCOLS)) {
      for (const g of schema.groups) {
        assert.ok(g.labelKey, `${proto}/${g.id} 组缺 labelKey`)
        collected[g.labelKey] = 1
        for (const f of g.fields) {
          if (f.placeholderKey) collected[f.placeholderKey] = 1
          for (const opt of f.options || []) if (opt.labelKey) collected[opt.labelKey] = 1
        }
      }
    }
    for (const key of Object.keys(collected)) {
      assert.ok(key in locales['en-US'], `en-US 缺词条: ${key}`)
      assert.ok(key in locales['zh-CN'], `zh-CN 缺词条: ${key}`)
    }
  })
})
