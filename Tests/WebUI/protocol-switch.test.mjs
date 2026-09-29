// ────────────────────────────────────────────────────────────────────────────
// 新人导览 · protocol-switch.test.mjs · L1/L2
// ▸ 本文件测什么：编辑器里切换协议再切回时，协议专属设置不丢（每协议一份快照）。
//   最后一段是"源码接线"锚点：确认 EditorDrawer 真的接了这个模块。
// ▸ 单独运行：cd webui && node --test ../Tests/WebUI/protocol-switch.test.mjs
// ▸ 全量运行与结果怎么看：Tests/WebUI/README.md §0；断言红了怎么办：README §8
// ────────────────────────────────────────────────────────────────────────────
// 协议切换会话快照用例（round11 U1：编辑器切换协议后再切回，协议专属设置被清空）。
// 语义单一来源：src/editor/protocolSwitch.js —— 会话内每协议保留最后一份表单快照，
// 切回恢复（对齐桌面版每协议一份实例）；首次到访协议仍走交集携带改造。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { PROTOCOLS } from '../../webui/src/editor/schemas.js'
import { switchProtocolWithSnapshots, switchProtocol } from '../../webui/src/editor/protocolSwitch.js'

// SSH 表单：defaults + 协议专属自定义值（U1 场景里的私钥路径/启动命令）
const sshJson = () => ({
  ...JSON.parse(JSON.stringify(PROTOCOLS.SSH.defaults)),
  Protocol: PROTOCOLS.SSH.protocol,
  ClassVersion: PROTOCOLS.SSH.classVersion,
  DisplayName: 'srv-1',
  Address: '10.0.0.8',
  PrivateKeyPath: 'C:/keys/id_rsa', // SSH 专属
  SshStartCommand: 'cd /srv; ./run.sh', // SSH 专属
})

describe('switchProtocol 基线（首次到访：交集携带，专属字段丢弃）', () => {
  it('SSH→RDP：共有字段携带，SSH 专属键不在结果中', () => {
    const out = switchProtocol(sshJson(), PROTOCOLS.SSH, PROTOCOLS.RDP)
    assert.equal(out.DisplayName, 'srv-1')
    assert.equal(out.Address, '10.0.0.8')
    assert.ok(!('PrivateKeyPath' in out), 'SSH 专属字段应被丢弃（基线行为）')
    assert.equal(out.Protocol, PROTOCOLS.RDP.protocol)
  })
})

describe('switchProtocolWithSnapshots 往返恢复（U1）', () => {
  it('SSH→RDP→SSH：恢复 SSH 会话快照，协议专属设置不丢', () => {
    const snapshots = {}
    const orig = sshJson()
    const r1 = switchProtocolWithSnapshots(orig, 'SSH', 'RDP', PROTOCOLS.SSH, PROTOCOLS.RDP, snapshots)
    assert.equal(r1.restored, false, '首次到访 RDP 走携带改造')
    // RDP 下改共有字段（用户继续编辑）
    r1.json.Address = '10.0.0.9'
    const r2 = switchProtocolWithSnapshots(r1.json, 'RDP', 'SSH', PROTOCOLS.RDP, PROTOCOLS.SSH, snapshots)
    assert.equal(r2.restored, true, '切回 SSH 恢复快照')
    assert.equal(r2.json.PrivateKeyPath, 'C:/keys/id_rsa', '私钥路径不丢（U1 回归锁定）')
    assert.equal(r2.json.SshStartCommand, 'cd /srv; ./run.sh', '启动命令不丢')
    assert.equal(r2.json.Address, '10.0.0.8', '共有字段恢复为 SSH 侧快照值（每协议独立，对齐桌面版）')
    assert.equal(r2.json.Protocol, PROTOCOLS.SSH.protocol)
  })

  it('快照随最新编辑更新：SSH→RDP→SSH→（改 SSH）→RDP→SSH 取到最新一份', () => {
    const snapshots = {}
    const a = switchProtocolWithSnapshots(sshJson(), 'SSH', 'RDP', PROTOCOLS.SSH, PROTOCOLS.RDP, snapshots)
    const b = switchProtocolWithSnapshots(a.json, 'RDP', 'SSH', PROTOCOLS.RDP, PROTOCOLS.SSH, snapshots)
    b.json.PrivateKeyPath = 'C:/keys/new_key'
    const c = switchProtocolWithSnapshots(b.json, 'SSH', 'RDP', PROTOCOLS.SSH, PROTOCOLS.RDP, snapshots)
    const d = switchProtocolWithSnapshots(c.json, 'RDP', 'SSH', PROTOCOLS.RDP, PROTOCOLS.SSH, snapshots)
    assert.equal(d.json.PrivateKeyPath, 'C:/keys/new_key', '快照应是离开时的最新表单')
  })

  it('恢复结果是克隆：改动结果不影响快照表', () => {
    const snapshots = {}
    switchProtocolWithSnapshots(sshJson(), 'SSH', 'RDP', PROTOCOLS.SSH, PROTOCOLS.RDP, snapshots)
    const back = switchProtocolWithSnapshots(
      { ...sshJson(), Address: 'x' },
      'RDP',
      'SSH',
      PROTOCOLS.RDP,
      PROTOCOLS.SSH,
      snapshots
    )
    back.json.DisplayName = 'mutated'
    const again = switchProtocolWithSnapshots(
      { Protocol: 'RDP' },
      'SSH',
      'RDP',
      PROTOCOLS.SSH,
      PROTOCOLS.RDP,
      snapshots
    )
    const back2 = switchProtocolWithSnapshots(again.json, 'RDP', 'SSH', PROTOCOLS.RDP, PROTOCOLS.SSH, snapshots)
    assert.notEqual(back2.json.DisplayName, 'mutated', '快照未被恢复结果的改动污染')
  })
})

describe('EditorDrawer 接线（源码回归）', () => {
  it('编辑器使用快照版切换，且加载时清空快照表', async () => {
    const { read } = await import('./helpers.mjs')
    const src = read('src/components/editor/EditorDrawer.vue')
    assert.ok(src.includes('switchProtocolWithSnapshots'), '应改用快照版切换')
    assert.ok(src.includes('protocolSnapshots = {}'), 'load() 应清空会话快照（防跨服务器泄漏）')
  })
})
