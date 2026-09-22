// 勾选模型用例（spec §3.2 三态勾选/§3.4 批量；历史缺陷 round5-BUG2/H38）。
// 直接驱动 webui/src/composables/useRowChecks.js（vue 响应式在 node 可运行，零组件挂载）。
// 覆盖：级联勾选、空文件夹勾选通道、表头三态（合并语义）、数据剔除、Ctrl/Shift 选行。
// 每条用例独立构建 useRowChecks 实例——勾选集/锚点是有状态的，实例共享会互相污染。
// vue 经 helpers.loadVue() 解析（本目录在 webui 包外，裸名 import 找不到 node_modules）。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { loadVue } from './helpers.mjs'
import { useRowChecks } from '../../webui/src/composables/useRowChecks.js'

const { computed, nextTick, ref } = loadVue()

const SRV = (id, ds, path) => ({ id, dataSourceName: ds, folderPath: path })

function makeChecks(servers, folders, sorted) {
  return useRowChecks({
    sorted: sorted ?? computed(() => servers.value.filter((s) => s.folderPath === '323')),
    servers: () => servers.value,
    folders: () => folders.value,
  })
}

describe('文件夹勾选（级联=全部子孙，限定同数据源）', () => {
  const SRV323 = () => [
    SRV('root1', 'Local', ''),
    SRV('d1', 'Local', '323'),
    SRV('deep1', 'Local', '323/67'),
    SRV('deep2', 'Local', '323/67/x'),
  ]
  const F323 = () => [{ name: '323', path: '323', dsName: 'Local', count: 3 }]

  it('勾 323 = 直接子级 + 深层子孙进入勾选集（isInSubtreeOf 口径）', async () => {
    const c = makeChecks(ref(SRV323()), ref(F323()))
    c.onFolderToggleCheck(F323()[0])
    assert.deepEqual([...c.checked.value].sort(), ['d1', 'deep1', 'deep2'])
  })
  it('跨数据源同名路径不误勾（「全部数据」视图防串库）', () => {
    const c = makeChecks(
      ref([SRV('a', 'Local', '323'), SRV('b', 'MySQL', '323')]),
      ref([{ name: '323', path: '323', dsName: 'Local', count: 1 }])
    )
    c.onFolderToggleCheck({
      name: '323',
      path: '323',
      dsName: 'Local',
      count: 1,
    })
    assert.deepEqual([...c.checked.value], ['a'])
  })
  it('再点取消全部子孙；三态派生 未选→半选→全选（round5-BUG2 级联口径不变）', async () => {
    const c = makeChecks(ref(SRV323()), ref(F323()))
    c.onFolderToggleCheck(F323()[0])
    assert.equal(c.folderChecks.value.get('f:Local:323').checked, true)
    c.checked.value = new Set(['d1']) // 模拟单独取消一台 → 半选
    assert.equal(c.folderChecks.value.get('f:Local:323').indeterminate, true)
    c.onFolderToggleCheck(F323()[0]) // 半选态点击 = 补全
    assert.equal(c.folderChecks.value.get('f:Local:323').checked, true)
    c.onFolderToggleCheck(F323()[0]) // 全选态再点 = 全部移出
    assert.equal(c.checked.value.size, 0)
  })
})

describe('空文件夹勾选通道（round5-BUG2：勾选=文件夹本身，供批量删除）', () => {
  const F = () => [{ name: 'temp', path: 'temp', dsName: 'Local', count: 0 }]

  it('勾选进入 checkedFolders，值为 {dsName, path}（批量删除直接消费）', async () => {
    const c = makeChecks(ref([SRV('root1', 'Local', '')]), ref(F()))
    c.onFolderToggleCheck(F()[0])
    assert.deepEqual(c.checkedFolders.value.get('f:Local:temp'), {
      dsName: 'Local',
      path: 'temp',
    })
    const st = c.folderChecks.value.get('f:Local:temp')
    assert.equal(st.checked, true)
    assert.equal(st.count, 0)
    await nextTick()
  })
  it('再点取消；clearChecked 两类勾选一起清', () => {
    const c = makeChecks(ref([SRV('root1', 'Local', '')]), ref(F()))
    c.onFolderToggleCheck(F()[0])
    c.onFolderToggleCheck(F()[0])
    assert.equal(c.checkedFolders.value.size, 0)
    c.onFolderToggleCheck(F()[0])
    c.clearChecked()
    assert.equal(c.checkedFolders.value.size, 0)
    assert.equal(c.checked.value.size, 0)
  })
})

describe('表头三态全选（合并语义 + 对称清空）', () => {
  it('全选=当前视图可见行；隐藏子孙保留（合并而非替换）', async () => {
    const c = makeChecks(
      ref([SRV('d1', 'Local', '323'), SRV('deep1', 'Local', '323/67')]),
      ref([]),
      ref([SRV('d1', 'Local', '323')]) // 视图（直接子级）只有 d1；deep1 是隐藏子孙
    )
    c.checked.value = new Set(['deep1']) // 先勾文件夹带来的隐藏子孙
    c.addAllVisible()
    assert.deepEqual([...c.checked.value].sort(), ['d1', 'deep1'])
    await nextTick()
  })
  it('已全选时再按 = 清空（键盘/鼠标通道对称）', () => {
    const c = makeChecks(ref([SRV('d1', 'Local', '323')]), ref([]), ref([SRV('d1', 'Local', '323')]))
    c.toggleAll() // 全选
    assert.equal(c.allChecked.value, true)
    c.toggleAll() // 清空
    assert.equal(c.checked.value.size, 0)
  })
})

describe('数据变化剔除（按过滤后列表域；视图切换清空归 ServerTable 的 selection watch）', () => {
  it('被删除/过滤掉的勾选即时剔除；仍在过滤列表中的保留', async () => {
    const servers = ref([SRV('keep', 'Local', '323'), SRV('gone', 'Local', '323')])
    const c = makeChecks(servers, ref([]))
    c.checked.value = new Set(['keep', 'gone'])
    await nextTick()
    servers.value = [SRV('keep', 'Local', '323')] // gone 消失
    await nextTick()
    assert.deepEqual([...c.checked.value], ['keep'])
  })
})

describe('空文件夹勾选剔除与显示同谓词（round8 P4/M20：消失或非空即剔）', () => {
  const F0 = () => [{ name: 'temp', path: 'temp', dsName: 'Local', count: 0 }]

  it('文件夹被填入服务器（count>0）后从 checkedFolders 剔除', async () => {
    const servers = ref([SRV('root1', 'Local', '')])
    const folders = ref(F0())
    const c = makeChecks(servers, folders)
    c.onFolderToggleCheck(folders.value[0])
    assert.equal(c.checkedFolders.value.size, 1)
    servers.value = [SRV('root1', 'Local', ''), SRV('new1', 'Local', 'temp')] // 被拖入服务器
    folders.value = [{ name: 'temp', path: 'temp', dsName: 'Local', count: 1 }]
    await nextTick()
    assert.equal(c.checkedFolders.value.size, 0, '不再为空的文件夹勾选应被剔除（防静默搬迁内容）')
  })
  it('文件夹行消失（被删除/切走视图）后从 checkedFolders 剔除', async () => {
    const folders = ref(F0())
    const c = makeChecks(ref([SRV('root1', 'Local', '')]), folders)
    c.onFolderToggleCheck(folders.value[0])
    assert.equal(c.checkedFolders.value.size, 1)
    folders.value = []
    await nextTick()
    assert.equal(c.checkedFolders.value.size, 0)
  })
  it('仍为空且仍在当前层级的勾选不受影响', async () => {
    const folders = ref(F0())
    const c = makeChecks(ref([SRV('root1', 'Local', '')]), folders)
    c.onFolderToggleCheck(folders.value[0])
    await nextTick()
    folders.value = F0() // 同形状刷新（SSE 重载等）
    await nextTick()
    assert.equal(c.checkedFolders.value.size, 1)
  })
})

describe('行点击选行（Ctrl 切换 / Shift 范围 / 裸点击=纯光标）', () => {
  it('Ctrl+点击切换勾选；裸点击不改勾选集', () => {
    const c = makeChecks(ref([SRV('a', 'Local', '323'), SRV('b', 'Local', '323')]), ref([]))
    c.rowClickSelect({ ctrlKey: true }, 0, 'a')
    assert.deepEqual([...c.checked.value], ['a'])
    c.rowClickSelect({}, 1, 'b') // 裸点击=纯光标，不改勾选
    assert.deepEqual([...c.checked.value], ['a'])
  })
  it('Shift+点击 范围选择（锚点=上次点击行）', () => {
    const c = makeChecks(ref([SRV('a', 'Local', '323'), SRV('b', 'Local', '323'), SRV('c', 'Local', '323')]), ref([]))
    c.rowClickSelect({}, 0, 'a') // 裸点击落锚点
    c.rowClickSelect({ shiftKey: true }, 2, 'c')
    assert.deepEqual([...c.checked.value].sort(), ['a', 'b', 'c'])
  })
  it('视图变化（sorted 更新）作废 Shift 锚点——范围选择不跨视图残留', async () => {
    const sorted = ref([SRV('a', 'Local', '323'), SRV('b', 'Local', '323')])
    const c = makeChecks(ref([SRV('a', 'Local', '323'), SRV('b', 'Local', '323')]), ref([]), sorted)
    c.rowClickSelect({}, 0, 'a') // 锚点落 0
    sorted.value = [SRV('c', 'Local', '323')] // 视图变了
    await nextTick()
    c.rowClickSelect({ shiftKey: true }, 0, 'c') // 锚点已作废 → 无范围勾选
    assert.equal(c.checked.value.size, 0)
  })
})
