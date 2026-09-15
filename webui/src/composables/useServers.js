import { ref } from 'vue'
import { api, subscribeEvents } from '../api'

// 模块级共享状态（spec §9.1：不用 Pinia；多组件调用 useServers() 共享同一份 refs）
const servers = ref([])
const datasources = ref([])
const tags = ref([])
const loading = ref(false)
const connected = ref(false) // 后端可达状态（最近一次拉取/轮询成功为 true，供状态栏指示，Task 20 用）
let unsubscribe = null
let pollTimer = null

async function loadAll() {
  loading.value = true
  try {
    [servers.value, datasources.value, tags.value] =
      await Promise.all([api.servers(), api.datasources(), api.tags()])
    connected.value = true
  } catch (e) {
    // 后端不可达是可预期状态（如开发时代理目标未启动）：保持旧数据、标记断连并记日志，
    // 不向上抛——初始加载与 SSE 回调都是 fire-and-forget 调用，抛出只会变成未处理 rejection
    connected.value = false
    console.warn('[useServers] reload failed:', e?.message || e)
  } finally {
    loading.value = false
  }
}

export function useServers() {
  if (!unsubscribe) {
    loadAll()
    // SSE reload 的 data 是每连接计数——只当"数据变了"的信号用，禁止比较版本号
    unsubscribe = subscribeEvents(() => loadAll())
    // 数据源连接状态（重连倒计时等）不触发 OnReloadAll，低频轮询兜底（顺带刷新边栏状态点）
    pollTimer = setInterval(async () => {
      try {
        datasources.value = await api.datasources()
        connected.value = true
      } catch {
        connected.value = false
      }
    }, 30000)
  }
  return { servers, datasources, tags, loading, connected, reload: loadAll }
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
