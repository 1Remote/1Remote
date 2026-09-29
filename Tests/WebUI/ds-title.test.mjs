// ────────────────────────────────────────────────────────────────────────────
// 新人导览 · ds-title.test.mjs · L1 纯逻辑
// ▸ 本文件测什么：数据源状态点悬停文案的工厂函数：三态词条 + 重连倒计时逐秒走表。
//   树/状态栏/设置卡三处共用这一个实现。
// ▸ 单独运行：cd webui && node --test ../Tests/WebUI/ds-title.test.mjs
// ▸ 全量运行与结果怎么看：Tests/WebUI/README.md §0；断言红了怎么办：README §8
// ────────────────────────────────────────────────────────────────────────────
// 数据源状态点 title 工厂用例（round12 用户报告：状态栏重连倒计时悬停不更新）。
// 语义单一来源：src/utils/dsTitle.js —— 三态词条 + 重连倒计时本地逐秒倒数
// （eta 快照 + useNow 时钟 + fetchedAt 基准）。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { makeDsDotTitle, fmtDuration } from '../../webui/src/utils/dsTitle.js'

// 假 t：回填键名与插值参数，便于断言（不依赖真实词条文案）
const fakeT = (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key)

describe('fmtDuration 秒数 → d/h/m/s 拼接', () => {
  it('对齐后端 GetTime 风格', () => {
    assert.equal(fmtDuration(0), '0s')
    assert.equal(fmtDuration(59), '59s')
    assert.equal(fmtDuration(175), '2m55s')
    assert.equal(fmtDuration(3600), '1h')
    assert.equal(fmtDuration(3661), '1h1m1s')
    assert.equal(fmtDuration(86400 + 7200 + 65), '1d2h1m5s')
  })
  it('负数防御为 0s', () => {
    assert.equal(fmtDuration(-3), '0s')
  })
})

describe('makeDsDotTitle 三态词条', () => {
  const title = makeDsDotTitle(fakeT)
  it('connected / disconnected 走对应词条', () => {
    assert.match(title({ status: 'connected', name: 'dsA' }), /^statusbar.dsConnected /)
    assert.match(title({ status: 'disconnected', name: 'dsA' }), /^statusbar.dsDisconnected /)
  })
  it('reconnecting 无 live 上下文：回落后端串（旧行为）', () => {
    const s = title({ status: 'reconnecting', name: 'dsA', reconnectInfo: 'Next auto reconnect 2m55s' })
    assert.match(s, /^statusbar.dsReconnecting /)
    assert.ok(s.includes('Next auto reconnect 2m55s'))
  })
  it('reconnecting 无 eta 字段（旧后端）：仍回落后端串', () => {
    const live = { now: { value: 100000 }, fetchedAt: { value: 100000 } }
    const title2 = makeDsDotTitle(fakeT, live)
    const s = title2({ status: 'reconnecting', name: 'dsA', reconnectInfo: 'soon' })
    assert.ok(s.includes('soon'))
  })
})

describe('makeDsDotTitle live 倒数（eta 快照 + 本地时钟）', () => {
  const fetchedAt = { value: 1_000_000 }
  const now = { value: 1_000_000 }
  const live = { now, fetchedAt }
  const title = makeDsDotTitle(fakeT, live)
  const ds = { status: 'reconnecting', name: 'dsA', reconnectEtaSeconds: 175 }

  it('快照时刻：显示 2m55s 倒计时（不再滞后于轮询）', () => {
    assert.equal(title(ds), 'statusbar.dsNextRetry {"name":"dsA","time":"2m55s"}')
  })

  it('时钟前进 60s：倒计时同步递减（本地逐秒倒数）', () => {
    now.value = fetchedAt.value + 60_000
    assert.equal(title(ds), 'statusbar.dsNextRetry {"name":"dsA","time":"1m55s"}')
  })

  it('倒数归零：转为"重连中"（已到点）', () => {
    now.value = fetchedAt.value + 175_000
    assert.equal(title(ds), 'statusbar.dsReconnecting {"name":"dsA"}')
    now.value = fetchedAt.value + 999_000
    assert.equal(title(ds), 'statusbar.dsReconnecting {"name":"dsA"}', '归零后不出现负数时长')
  })

  it('eta=0（到点快照）：直接显示"重连中"', () => {
    assert.equal(title({ ...ds, reconnectEtaSeconds: 0 }), 'statusbar.dsReconnecting {"name":"dsA"}')
  })
})
