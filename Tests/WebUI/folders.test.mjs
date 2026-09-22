// 文件夹/路径语义纯函数用例（spec §3.2 §3.5；历史缺陷 round4-错误2/H1/H3、round5-BUG1/2/3）。
// 语义单一来源：src/composables/folders.js —— 显示=直接子级（isDirectChildOf，资源管理器
// 模型），勾选=全部子孙（isInSubtreeOf，级联口径）。两者有意不同（H38 一轮反复的根源）。
// 含 scripts/semantics-test.mjs 全部 12 条断言的超集。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import {
  SEP,
  fullKey,
  folderPathsFromKeys,
  parentPath,
  isDescendantPath,
  rewriteServerPath,
  rewriteTreeStateKeys,
  buildTree,
  countHolderServers,
  isDirectChildOf,
  isInSubtreeOf,
  holderAt,
} from '../../webui/src/composables/folders.js'
import { naturalIpCompare } from '../../webui/src/utils/compare.js'

// ---------- 显示语义：直接子级（round5-BUG3 定案） ----------
describe('显示语义 isDirectChildOf（进入文件夹只列直接子级）', () => {
  it('深层服务器不出现在祖先文件夹视图（round5-BUG3：2389 在 323/67 不出现在 323）', () => {
    assert.equal(isDirectChildOf('323/67', '323'), false)
  })
  it('本层服务器出现在本文件夹视图', () => {
    assert.equal(isDirectChildOf('323/67', '323/67'), true)
    assert.equal(isDirectChildOf('323', '323'), true)
  })
  it('根级服务器出现在根视图；子级不出现在根视图', () => {
    assert.equal(isDirectChildOf('', ''), true)
    assert.equal(isDirectChildOf('323', ''), false)
  })
  it('同名前缀不误匹配（3234 不是 323 的子级）', () => {
    assert.equal(isDirectChildOf('3234', '323'), false)
  })
  it('folderPath undefined 归一化为根级（两侧归一）', () => {
    assert.equal(isDirectChildOf(undefined, ''), true)
    assert.equal(isDirectChildOf('', undefined), true)
  })
})

// ---------- 勾选语义：全部子孙（勾选/级联/受影响集共用口径） ----------
describe('勾选语义 isInSubtreeOf（勾文件夹=级联全部子孙）', () => {
  it('勾 323 级联到直接子级与深层子孙', () => {
    assert.equal(isInSubtreeOf('323', '323'), true)
    assert.equal(isInSubtreeOf('323/67', '323'), true)
    assert.equal(isInSubtreeOf('323/67/x', '323/67'), true)
  })
  it('同级与根级不被级联误命中', () => {
    assert.equal(isInSubtreeOf('3234', '323'), false)
    assert.equal(isInSubtreeOf('', '323'), false)
  })
})

// ---------- 路径前缀判定（拖拽禁入自身子树；删除/重命名受影响集） ----------
describe('路径关系 isDescendantPath / parentPath', () => {
  it('相等也算后代（拖到自己身上同拒）', () => {
    assert.equal(isDescendantPath('a', 'a'), true)
  })
  it('真子孙为后代；边界不粘连（a2 不是 a 的后代）', () => {
    assert.equal(isDescendantPath('a', 'a/b'), true)
    assert.equal(isDescendantPath('a', 'a2'), false)
    assert.equal(isDescendantPath('a', 'ab/c'), false)
  })
  it('根级路径不是任何文件夹的后代（isDescendantPath 对空串恒 false——「移到根」恒合法）', () => {
    assert.equal(isDescendantPath('a', ''), false)
    assert.equal(isDescendantPath('', 'a/b'), false)
  })
  it('parentPath：顶层返回空串', () => {
    assert.equal(parentPath('a'), '')
    assert.equal(parentPath('a/b'), 'a')
    assert.equal(parentPath('a/b/c'), 'a/b')
  })
})

// ---------- tree-state 键换算（键格式与 WPF 逐字符一致） ----------
describe('tree-state 键换算 fullKey / folderPathsFromKeys', () => {
  it('键 = 数据源 + SEP + 逐段路径；根键 = 数据源名', () => {
    assert.equal(fullKey('Local', 'a/b'), 'Local' + SEP + 'a' + SEP + 'b')
    assert.equal(fullKey('Local', ''), 'Local')
  })
  it('键 → 路径物化源：按数据源分组；单段键（数据源根）与残键忽略', () => {
    const m = folderPathsFromKeys([fullKey('Local', 'a/b'), 'MySQL', '', '残' + SEP])
    assert.deepEqual([...m.get('Local')], ['a/b'])
    assert.equal(m.size, 1) // 'MySQL' 单段=数据源根键，不是文件夹键
  })
})

// ---------- 服务器路径前缀重写（重命名/移动/删除共用） ----------
describe('服务器路径重写 rewriteServerPath', () => {
  it('等值与前缀子路径换前缀；深层余量保留', () => {
    assert.equal(rewriteServerPath('a', 'a', 'x'), 'x')
    assert.equal(rewriteServerPath('a/b/c', 'a', 'x/y'), 'x/y/b/c')
  })
  it('删除上移（newPath 空串）：去掉 oldPath 一级', () => {
    assert.equal(rewriteServerPath('a/b', 'a', ''), 'b')
    assert.equal(rewriteServerPath('a/b/c', 'a', ''), 'b/c')
  })
  it('无关路径返回 null（调用方跳过）', () => {
    assert.equal(rewriteServerPath('b', 'a', 'x'), null)
    assert.equal(rewriteServerPath('a2/b', 'a', 'x'), null)
  })
})

// ---------- tree-state 键前缀重写（幽灵文件夹防线，round4-错误2/H1） ----------
describe('tree-state 键重写 rewriteTreeStateKeys', () => {
  const K = fullKey
  it('重命名：旧键移除、新键加入、展开态随键迁移', () => {
    const expanded = {
      [K('L', 'a')]: true,
      [K('L', 'a/sub')]: false,
      [K('L', 'b')]: true,
    }
    const { remove, add } = rewriteTreeStateKeys(expanded, 'L', 'a', 'x')
    assert.deepEqual(remove.sort(), [K('L', 'a'), K('L', 'a/sub')].sort())
    assert.deepEqual(add, { [K('L', 'x')]: true, [K('L', 'x/sub')]: false })
  })
  it('重命名目标键已存在时不覆盖目标侧展开态（H3 合并：存活侧为准）', () => {
    const expanded = { [K('L', 'a')]: true, [K('L', 'x')]: false }
    const { remove, add } = rewriteTreeStateKeys(expanded, 'L', 'a', 'x')
    assert.deepEqual(remove, [K('L', 'a')])
    assert.deepEqual(add, {}) // L/a 的 true 不得覆盖 L/x 的 false
  })
  it('删除（newPath=null）：自身键消失且不向父键迁移展开态；子键上移一级', () => {
    const expanded = {
      [K('L', 'a')]: true,
      [K('L', 'a/sub')]: false,
      [K('L', 'p')]: true,
    }
    const { remove, add } = rewriteTreeStateKeys(expanded, 'L', 'a', null)
    assert.ok(remove.includes(K('L', 'a')))
    assert.ok(remove.includes(K('L', 'a/sub')))
    assert.deepEqual(add, { [K('L', 'sub')]: false }) // 子键上移；父键 p 不新增（不被覆盖）
  })
  it('跨数据源键不受影响', () => {
    const expanded = { [K('M', 'a')]: true }
    const { remove, add } = rewriteTreeStateKeys(expanded, 'L', 'a', 'x')
    assert.deepEqual(remove, [])
    assert.deepEqual(add, {})
  })
})

// ---------- 树模型（虚拟文件夹物化 / 孤儿丢弃 / 递归徽标口径） ----------
describe('树模型 buildTree / countHolderServers / holderAt', () => {
  const DS = [
    { name: 'Local', type: 'sqlite' },
    { name: 'MySQL', type: 'mysql' },
  ]
  const SRV = (id, ds, path) => ({ id, dataSourceName: ds, folderPath: path })
  it('服务器按路径下沉；空键物化虚拟文件夹；孤儿（无属主数据源）丢弃', () => {
    const keys = folderPathsFromKeys([fullKey('Local', 'empty/nest')])
    const roots = buildTree(
      [SRV('1', 'Local', 'a'), SRV('2', 'Local', 'a/b'), SRV('3', 'Local', ''), SRV('9', 'Ghost', 'a')],
      DS,
      keys
    )
    const local = roots.find((r) => r.name === 'Local')
    assert.deepEqual(local.folders.map((f) => f.name).sort(), ['a', 'empty'])
    const fa = local.folders.find((f) => f.name === 'a')
    assert.deepEqual(
      fa.servers.map((s) => s.id),
      ['1']
    )
    assert.deepEqual(
      fa.folders[0].servers.map((s) => s.id),
      ['2']
    )
    assert.deepEqual(
      local.servers.map((s) => s.id),
      ['3']
    )
    assert.equal(
      roots.find((r) => r.name === 'Ghost'),
      undefined
    )
  })
  it('徽标=子树全部服务器（含子文件夹，与列表/面包屑「N 台」同口径 E5-1/H38）', () => {
    const roots = buildTree(
      [SRV('1', 'Local', 'a'), SRV('2', 'Local', 'a/b'), SRV('3', 'Local', 'a/b/c')],
      DS,
      undefined
    )
    const local = roots.find((r) => r.name === 'Local')
    assert.equal(countHolderServers(local), 3)
    assert.equal(countHolderServers(local.folders[0]), 3)
    assert.equal(countHolderServers(local.folders[0].folders[0]), 2)
  })
  it('holderAt 按路径段下钻；不存在返回 null', () => {
    const roots = buildTree([SRV('1', 'Local', 'a/b')], DS, undefined)
    assert.equal(holderAt(roots, 'Local', 'a/b').name, 'b')
    assert.equal(holderAt(roots, 'Local', 'a/x'), null)
    assert.equal(holderAt(roots, 'Ghost', ''), null)
  })
})

// ---------- 地址列自然 IP 排序（对齐 WPF SubTitleSortByNaturalIp） ----------
describe('自然 IP 比较 naturalIpCompare', () => {
  it('完整 IPv4 按段数值比较（2 < 10，不看字符串序）', () => {
    assert.ok(naturalIpCompare('10.0.0.2', '10.0.0.10') < 0)
    assert.ok(naturalIpCompare('9.0.0.1', '10.0.0.1') < 0)
    assert.equal(naturalIpCompare('10.0.0.1', '10.0.0.1'), 0)
  })
  it('主机名回退码点序；混合不再按 IP 段比较', () => {
    assert.ok(naturalIpCompare('web1', 'web2') < 0)
    assert.ok(naturalIpCompare('10.0.0.2', 'web1') !== 0)
  })
})

// ---------- 边栏过滤交集（标签 → 搜索，useServers 纯函数导出） ----------
// api/index.js 顶层读取 location/sessionStorage——node 无此对象，先垫最小假体再动态 import。
globalThis.location = new URL('http://127.0.0.1/')
globalThis.sessionStorage = {
  getItem: () => null,
  setItem() {},
  removeItem() {},
}
globalThis.history = { replaceState() {} }
const { applyServerFilters } = await import('../../webui/src/composables/useServers.js')

describe('边栏过滤 applyServerFilters（标签 ∩ 搜索）', () => {
  const list = [
    { id: '1', tags: ['prod', 'web'] },
    { id: '2', tags: ['PROD'] },
    { id: '3', tags: [] },
  ]
  it('无过滤全通过（searchedIds=null=未启用，区别于空集）', () => {
    assert.equal(applyServerFilters(list, '', null).length, 3)
  })
  it('标签过滤大小写不敏感', () => {
    assert.deepEqual(
      applyServerFilters(list, 'prod', null).map((s) => s.id),
      ['1', '2']
    )
  })
  it('搜索命中集过滤；空集=零命中（不是全通过）', () => {
    assert.deepEqual(
      applyServerFilters(list, '', new Set(['3'])).map((s) => s.id),
      ['3']
    )
    assert.deepEqual(
      applyServerFilters(list, '', new Set()).map((s) => s.id),
      []
    )
  })
  it('标签与搜索取交集', () => {
    assert.deepEqual(
      applyServerFilters(list, 'prod', new Set(['2'])).map((s) => s.id),
      ['2']
    )
  })
})

describe('搜索范围收窄（owner 2026-09-22 需求①：资源管理器语义）', () => {
  // 树形测试数据：ds-A 下 folder a（含子 a/b），ds-B 独库
  const list = [
    { id: 'a1', dataSourceName: 'ds-A', folderPath: 'a' },
    { id: 'a1x', dataSourceName: 'ds-A', folderPath: 'a/b' }, // a 的子文件夹内
    { id: 'a2', dataSourceName: 'ds-A', folderPath: 'a2' },
    { id: 'root', dataSourceName: 'ds-A', folderPath: '' }, // 数据源根级
    { id: 'b1', dataSourceName: 'ds-B', folderPath: 'a' }, // 跨库同路径
  ]
  const selFolder = { dataSourceName: 'ds-A', folderPath: 'a' }
  const selDsRoot = { dataSourceName: 'ds-A', folderPath: '' }
  const hit = (sel) => applyServerFilters(list, '', new Set(['a1', 'a1x', 'a2', 'root', 'b1']), sel).map((s) => s.id)

  it('文件夹视图：仅命中当前文件夹及其子文件夹', () => {
    assert.deepEqual(hit(selFolder), ['a1', 'a1x'])
  })
  it('数据源根视图：整个数据源命中可见（含全部子文件夹）', () => {
    assert.deepEqual(hit(selDsRoot), ['a1', 'a1x', 'a2', 'root'])
  })
  it('「全部数据」根（无数据源）：全库命中可见（跨库）', () => {
    assert.deepEqual(hit(null), ['a1', 'a1x', 'a2', 'root', 'b1'])
  })
  it('搜索未激活（searchedIds=null）时 selection 不参与过滤', () => {
    assert.equal(applyServerFilters(list, '', null, selFolder).length, 5)
  })
})
