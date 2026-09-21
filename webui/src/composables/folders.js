// 虚拟文件夹纯函数模块：
// 无 Vue/浏览器依赖（node 断言直接 import 本文件）。两类职责：
// 1. tree-state 键 ↔ 数据源/路径 的换算与重写（键格式与 WPF 逐字符一致）；
// 2. 树/文件夹模型（buildTree 自 useServers 迁入——物化空文件夹需在此做）。
//
// 虚拟文件夹语义（与 WPF BuildView 对齐，ServerTreeViewModel.cs:873-889）：
// 一个文件夹「存在」当且仅当 (a) 任一服务器的 TreeNodes 路径经过它，或
// (b) 它的完整路径键出现在 tree-state expansion 字典中（WPF 用该字典物化
// 空文件夹——键存在即存在，与展开布尔值无关）。新建文件夹=向字典增键；
// 重命名/删除=重写服务器 TreeNodes 前缀 + 重写字典键。

// 与 WPF ServerTreeViewModel.FullPathSeparator 逐字符一致（" ]=+=+=+=>[ "）。
// 根节点 FullPath = 数据源名本身；子节点 = 父 FullPath + SEP + 名。
// 后端 folderPath 约定 "a/b"（DtoMapper: string.Join("/", TreeNodes)）。
export const SEP = ' ]=+=+=+=>[ '

export const fullKey = (dsName, folderPath) => (folderPath ? dsName + SEP + folderPath.split('/').join(SEP) : dsName)

/**
 * tree-state 键 → { ds, path }。单段键（数据源根）或无法解析（空）返回 null。
 * 注意 path 各段不允许包含 '/'，故 split('/').join(SEP) 往返无损。
 */
function parseKey(key) {
  if (!key) return null
  const parts = key.split(SEP)
  if (parts.length < 2 || parts.some((p) => !p)) return null
  return { ds: parts[0], path: parts.slice(1).join('/') }
}

/**
 * 全部已知键 → Map<dsName, Set<'a/b'>>（虚拟文件夹物化源）。
 * 键即存在（不看值）——与 WPF 物化循环一致；父路径由各 path 隐含（物化时逐级创建）。
 */
export function folderPathsFromKeys(keys) {
  const out = new Map()
  for (const key of keys) {
    const parsed = parseKey(key)
    if (!parsed) continue
    if (!out.has(parsed.ds)) out.set(parsed.ds, new Set())
    out.get(parsed.ds).add(parsed.path)
  }
  return out
}

/** path 的父路径（顶层返回 ''） */
export const parentPath = (p) => {
  const i = p.lastIndexOf('/')
  return i < 0 ? '' : p.slice(0, i)
}

/** path 是否在 ancestor 之内（等于或为其后代）——文件夹拖拽的「移入自身/后代」
 *  禁止判定（SideTree / ServerTable / folderOps 三处共用；含相等 → 拖到自己身上同拒） */
export const isDescendantPath = (ancestor, path) => path === ancestor || path.startsWith(ancestor + '/')

/**
 * 服务器 TreeNodes 路径前缀重写（重命名/删除文件夹共用）：
 * folderPath 在 oldPath 之下（等于或以 oldPath+'/' 开头）→ 换前缀为 newPath；
 * 不受影响返回 null。delete 场景传 newPath = parentPath(oldPath)（子项上移一级）。
 */
export function rewriteServerPath(folderPath, oldPath, newPath) {
  const cur = folderPath || ''
  if (cur === oldPath) return newPath
  const prefix = oldPath + '/'
  if (cur.startsWith(prefix)) return newPath ? newPath + '/' + cur.slice(prefix.length) : cur.slice(prefix.length)
  return null
}

/**
 * tree-state expansion 键前缀重写：返回 { remove: [旧键], add: {新键: 值} }。
 * 只动 ds 数据源下 oldPath 及其子孙的键；其余键不动（由调用方在合并基底上应用）。
 * newPath=null = 删除（子键上移一级）；否则 = 重命名（前缀替换）。
 * 目标键已存在时不覆盖其值（保留目标侧展开态）——同名文件夹合并（H3）时两侧键归一，
 * 存活文件夹的展开状态应以目标侧为准（WPF 节点移接、展开态随子项走；第五轮 E5-4：
 * 此前 Object.assign 会用源值覆盖，合出来的文件夹展开态跟着源走）。
 */
export function rewriteTreeStateKeys(expanded, dsName, oldPath, newPath) {
  const remove = []
  const add = {}
  const exact = fullKey(dsName, oldPath)
  for (const key of Object.keys(expanded || {})) {
    let rest = null
    if (key === exact) rest = ''
    else if (key.startsWith(exact + SEP)) rest = key.slice((exact + SEP).length)
    if (rest === null) continue
    remove.push(key)
    // 键值（展开态）随键迁移。删除场景（newPath=null）键本身（rest=''）随文件夹消失，
    // 不向父键迁移展开态（避免覆盖父级既有状态）；重命名场景文件夹仍存在，展开态跟新键走
    const target = newPath == null ? parentPath(oldPath) : newPath
    let newKey = null
    if (rest) newKey = fullKey(dsName, target ? target + '/' + rest : rest)
    else if (newPath != null && target) newKey = fullKey(dsName, target)
    if (newKey && newKey !== key && !Object.prototype.hasOwnProperty.call(expanded, newKey))
      add[newKey] = expanded[key] ?? true
  }
  return { remove, add }
}

// ---------------------------------------------------------------------------
// 树/文件夹模型（自 useServers.js 迁入，保持导出兼容：useServers 转发 buildTree）
// ---------------------------------------------------------------------------

const ensureFolder = (holder, name) => {
  let f = holder.folders.find((x) => x.name === name)
  if (!f) {
    f = { name, path: (holder.path ? holder.path + '/' : '') + name, folders: [], servers: [] }
    holder.folders.push(f)
  }
  return f
}

/**
 * 扁平列表 → 树结构（纯函数）：
 * [{name, type, status, writable, reconnectInfo, serverCount, servers: [], folders: [...]}]
 * - 根节点 = 数据源；root.servers = folderPath 为空串的服务器，其余按 "/" 逐级下沉
 * - folderPathsByDs（可选，Map<ds, Set<'a/b'>>，来自 tree-state 键）先行物化空文件夹
 *   （虚拟文件夹：键即存在，与 WPF BuildView 物化循环一致）
 * - 不属于任何已知数据源的服务器（快照错配的孤儿）被丢弃
 */
export function buildTree(servers, datasources, folderPathsByDs) {
  const roots = []
  for (const ds of datasources) {
    const root = { ...ds, folders: [], servers: [] }
    const extra = folderPathsByDs?.get?.(ds.name)
    if (extra)
      for (const p of extra) {
        let node = root
        for (const seg of p.split('/')) node = ensureFolder(node, seg)
      }
    for (const s of servers.filter((s) => s.dataSourceName === ds.name)) {
      let node = root
      for (const p of s.folderPath ? s.folderPath.split('/') : []) node = ensureFolder(node, p)
      node.servers.push(s)
    }
    roots.push(root)
  }
  return roots
}

/** holder（根/文件夹）下的服务器总数（含全部后代）——树徽标/文件夹行计数/删除确认
 *  （影响整个子树）与「全部数据」根徽标（全库总览）共用口径；H38 后「选中文件夹 =
 *  列表显示含子文件夹的全部服务器」，徽标与列表/面包屑「N 台」同口径才自洽
 *  （第五轮 E5-1：直接子级口径曾在同屏呈现两个矛盾数字）。 */
export function countHolderServers(holder) {
  return holder.servers.length + holder.folders.reduce((n, f) => n + countHolderServers(f), 0)
}

/**
 * 文件夹视图的显示语义（资源管理器模型，owner 2026-09-21 第三轮反馈定案）——单一来源：
 * 进入文件夹只列**直接子级**（folderPath 全等），深层由各层文件夹行/徽标承载；
 * 一台服务器不会同时出现在父与子两级视图（「323/67 的 2389 同时出现在 323 与
 * 323/67」曾被判 BUG）。ServerTable.filtered 与 scripts/semantics-test.mjs 共用。
 */
export const isDirectChildOf = (folderPath, target) => (folderPath || '') === (target || '')

/**
 * 「包含子孙」语义（勾选/级联/受影响集共用）——等值或前缀（含子文件夹全部）。
 * useRowChecks.folderDescendantIds、folderOps.affectedServers、semantics-test 共用；
 * 与 isDirectChildOf 是**两个不同语义**（显示=直接子级 / 勾选=全部子孙），
 * 防止未来再混用（H38 一轮反复的根源即此二义）。
 */
export const isInSubtreeOf = (folderPath, target) => {
  const fp = folderPath || ''
  const t = target || ''
  return fp === t || fp.startsWith(t + '/')
}

/** 在树模型中按数据源 + 路径段下钻取 holder；不存在返回 null */
export function holderAt(roots, dsName, folderPath) {
  let node = roots.find((r) => r.name === dsName)
  for (const seg of folderPath ? folderPath.split('/') : []) {
    if (!node) return null
    node = node.folders.find((f) => f.name === seg)
  }
  return node || null
}
