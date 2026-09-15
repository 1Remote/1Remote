import { ref, watch } from 'vue'
import { api, subscribeEvents } from '../api'

// 模块级共享状态（spec §9.1：不用 Pinia；多组件调用 useServers() 共享同一份 refs）
const servers = ref([])
const datasources = ref([])
const tags = ref([])
const loading = ref(false)
const connected = ref(false) // 后端可达状态（最近一次拉取/轮询成功为 true，供状态栏指示，Task 20 用）
let unsubscribe = null
let pollTimer = null
let gen = 0 // 乱序完成保护：SSE 事件风暴下多个 loadAll 并发，晚发起的批次可能先返回

async function loadAll() {
  const my = ++gen
  loading.value = true
  try {
    const results = await Promise.all([api.servers(), api.datasources(), api.tags()])
    if (my !== gen) return // 落后批次整体丢弃（乱序完成保护），不覆盖新批次数据
    ;[servers.value, datasources.value, tags.value] = results
    connected.value = true
  } catch (e) {
    // 后端不可达是可预期状态（如开发时代理目标未启动）：保持旧数据、标记断连并记日志，
    // 不向上抛——初始加载与 SSE 回调都是 fire-and-forget 调用，抛出只会变成未处理 rejection
    if (my === gen) {
      connected.value = false
      console.warn('[useServers] reload failed:', e?.message || e)
    }
  } finally {
    if (my === gen) loading.value = false
  }
}

// ---- 搜索（spec §3.1/§8，Task 17）：模块级共享搜索态，App.vue 顶栏输入框与列表过滤共同消费 ----
const searchQuery = ref('')
const searchedIds = ref(null) // Set<serverId> | null；null=未启用搜索过滤（区别于空集=搜了但零命中）
const searching = ref(false)
let searchTimer = null
let searchGen = 0 // 乱序完成保护（与 loadAll 的 gen 同思路）：防抖后连发多请求，晚发的可能先返回

// 输入变化 → 200ms 防抖后才发请求；清空（含纯空白）立即撤销过滤并作废在途请求
watch(searchQuery, q => {
  clearTimeout(searchTimer)
  if (!q || !q.trim()) {
    searchGen++ // 在途请求即使返回也因 gen 落后被丢弃，避免清空后旧结果闪回
    searching.value = false
    searchedIds.value = null
    return
  }
  searchTimer = setTimeout(() => doSearch(q.trim()), 200)
})

async function doSearch(q) {
  const my = ++searchGen
  searching.value = true
  try {
    const results = await api.search(q)
    if (my !== searchGen) return // 落后响应丢弃（乱序完成保护）
    searchedIds.value = new Set(results.map(s => s.id))
  } catch (e) {
    if (my === searchGen) {
      console.warn('[useServers] search failed:', e?.message || e)
      searchedIds.value = new Set() // 失败按零命中呈现：过滤结果与输入框中可见的查询保持一致，不用旧结果误导
    }
  } finally {
    if (my === searchGen) searching.value = false
  }
}

export function useServers() {
  if (!unsubscribe) {
    loadAll()
    // SSE reload 的 data 是每连接计数——只当"数据变了"的信号用，禁止比较版本号。
    // 重载后若搜索过滤仍在生效，旧命中集已过期，需以新数据重跑搜索刷新（searchGen 乱序保护仍生效）
    unsubscribe = subscribeEvents(async () => {
      await loadAll()
      if (searchQuery.value.trim()) doSearch(searchQuery.value.trim())
    })
    // 数据源连接状态（重连倒计时等）不触发 OnReloadAll，低频轮询兜底（顺带刷新边栏状态点）
    pollTimer = setInterval(async () => {
      // 乱序保护（与 loadAll 的 gen 同思路）：loadAll 在途时跳过本拍，防止旧状态点快照
      // 覆盖刚写入的新数据；剩余极小竞态窗口（检查后才发起的 loadAll）由下一拍 30s 自愈
      if (loading.value) return
      const wasDown = !connected.value
      try {
        const ds = await api.datasources()
        if (!loading.value) {
          datasources.value = ds
          connected.value = true
          // 断连恢复：servers/tags 仍是断连前的旧值（初载失败时为空），补一次全量重载，
          // 让离线提示（ServerListView）真正自动切回内容而非停留在空态
          if (wasDown) await loadAll()
        }
      } catch {
        if (!loading.value) connected.value = false
      }
    }, 30000)
  }
  return { servers, datasources, tags, loading, connected, reload: loadAll, searchQuery, searchedIds, searching }
}

/**
 * 扁平列表 → 边栏树结构（纯函数，不做懒计数/排序——Task 15 在此之上扩展）：
 * [{name, type, status, writable, reconnectInfo, serverCount, servers: [], folders: [{name, path, folders, servers}]}]
 * - 根节点 = 数据源（顺序以 /api/datasources 返回为准，展开其全部字段并挂 folders/servers）
 * - root.servers = folderPath 为空串的服务器；其余按 folderPath 以 "/" 逐级下沉（后端约定 "a/b"，根为空串）
 * - 不属于任何已知数据源的服务器（快照错配的孤儿）被丢弃
 */
export function buildTree(servers, datasources) {
  const roots = []
  for (const ds of datasources) {
    const dsServers = servers.filter(s => s.dataSourceName === ds.name)
    const root = { ...ds, folders: [], servers: [] }
    for (const s of dsServers) {
      const parts = s.folderPath ? s.folderPath.split('/') : []
      let node = root
      for (const p of parts) {
        let f = node.folders.find(x => x.name === p)
        if (!f) {
          f = { name: p, path: (node.path ? node.path + '/' : '') + p, folders: [], servers: [] }
          node.folders.push(f)
        }
        node = f
      }
      node.servers.push(s)
    }
    roots.push(root)
  }
  return roots
}

/**
 * 组合应用边栏过滤（spec §3.1/§3.3，Task 17）：基础列表 → 标签过滤 → 搜索命中集逐层收窄。
 * 纯函数（ServerListView 的 computed 与 node 断言共用；调用方负责传入响应式值以维持依赖追踪）：
 * - activeTag 非空 → 仅保留 tags 含该标签的服务器（后端标签名大小写无统一保证，按小写比较）
 * - searchedIds 非 null → 仅保留命中搜索的服务器（null=未启用搜索过滤，全通过）
 * 树选中过滤与排序不在此层——由 ServerTable 在收到收窄后的列表后自行应用，交集自然复合。
 */
export function applyServerFilters(servers, activeTag, searchedIds) {
  let list = servers
  if (activeTag) {
    const t = activeTag.toLowerCase()
    list = list.filter(s => (s.tags || []).some(tag => tag.toLowerCase() === t))
  }
  if (searchedIds) list = list.filter(s => searchedIds.has(s.id))
  return list
}
