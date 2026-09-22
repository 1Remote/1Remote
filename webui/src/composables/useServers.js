import { ref, watch } from 'vue'
import { api, subscribeEvents } from '../api/index.js'
import { isInSubtreeOf } from './folders.js'

// 批量连接确认阈值（产品决策项）：一次连接超过该台数时前端先弹确认（显示 N 台）
// 再逐台发起；ServerListView 批量条与 TagManagerModal「连接全部」共用，
// 两处阈值必须一致，故收在共享模块导出。
export const BATCH_CONNECT_THRESHOLD = 5

// 模块级共享状态（不引 Pinia；多组件调用 useServers() 共享同一份 refs）
const servers = ref([])
const datasources = ref([])
const tags = ref([])
const loading = ref(false)
const connected = ref(false) // 后端可达状态（最近一次拉取/轮询成功为 true）
const everConnected = ref(false) // 后端曾经可达过（K17：断开警告仅在后端曾可达后出现——exe 冷启动后端未起时不误报）
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
    everConnected.value = true
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

// ---- 搜索：模块级共享搜索态，App.vue 顶栏输入框与列表过滤共同消费 ----
const searchQuery = ref('')
const searchedIds = ref(null) // Set<serverId> | null；null=未启用搜索过滤（区别于空集=搜了但零命中）
const searching = ref(false)
const searchFailedTick = ref(0) // K13：每次搜索请求失败 +1（消费方 watch 弹「搜索失败」，与零命中二分）
let searchTimer = null
let searchGen = 0 // 乱序完成保护（与 loadAll 的 gen 同思路）：防抖后连发多请求，晚发的可能先返回

// 输入变化 → 200ms 防抖后才发请求；清空（含纯空白）立即撤销过滤并作废在途请求
watch(searchQuery, (q) => {
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
    searchedIds.value = new Set(results.map((s) => s.id))
  } catch (e) {
    if (my === searchGen) {
      console.warn('[useServers] search failed:', e?.message || e)
      // K13：失败 ≠ 零命中——不再清成空集（空集会把后端故障伪装成「未找到 xxx」，
      // 误导用户以为服务器不存在甚至去新建重复项）。保留上一版过滤态（首次搜索则
      // 维持未过滤），searchFailedTick +1 由消费方 watch 弹「搜索失败」提示，
      // 列表与输入框查询的差异由该提示解释
      searchFailedTick.value++
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
          everConnected.value = true
          // 断连恢复：servers/tags 仍是断连前的旧值（初载失败时为空），补一次全量重载，
          // 让离线提示（ServerListView）真正自动切回内容而非停留在空态
          if (wasDown) await loadAll()
        }
      } catch {
        if (!loading.value) connected.value = false
      }
    }, 30000)
  }
  // 数据源可写判定（SideTree/folderOps/ServerTable 三处共用同一规则——只读源禁止
  // 改结构/拖拽，J36① 收敛为单实现防漂移）
  const dsWritable = (dsName) => datasources.value.find((d) => d.name === dsName)?.writable !== false
  return {
    servers,
    datasources,
    tags,
    loading,
    connected,
    everConnected,
    reload: loadAll,
    searchQuery,
    searchedIds,
    searching,
    searchFailedTick,
    dsWritable,
  }
}

/**
 * 组合应用边栏过滤：基础列表 → 标签过滤 → 搜索命中集 → 搜索范围收窄。
 * 纯函数（ServerListView 的 computed 与 node 断言共用；调用方负责传入响应式值以维持依赖追踪）：
 * - activeTag 非空 → 仅保留 tags 含该标签的服务器（后端标签名大小写无统一保证，按小写比较）
 * - searchedIds 非 null（搜索激活）→ 仅保留命中搜索的服务器，并按 selection 收窄到
 *   **当前文件夹及其子文件夹**（owner 2026-09-22 设计需求①，资源管理器语义）：此前
 *   "搜索=全库递归"在文件夹内搜索会命中其他文件夹/其他库，owner 判为 BUG。层级语义：
 *   文件夹=该子树；数据源根=整个数据源；「全部数据」根（selection 无数据源）=全库。
 *   搜索未激活时 selection 不参与——树选中过滤归 ServerTable 的显示语义 isDirectChildOf，
 *   「过滤域收窄」与「显示域过滤」两层职责不同，勿合并
 */
export function applyServerFilters(servers, activeTag, searchedIds, selection) {
  let list = servers
  if (activeTag) {
    const t = activeTag.toLowerCase()
    list = list.filter((s) => (s.tags || []).some((tag) => tag.toLowerCase() === t))
  }
  if (searchedIds) {
    list = list.filter((s) => searchedIds.has(s.id))
    if (selection?.dataSourceName) {
      const target = selection.folderPath || ''
      // 数据源根（target=''）= 整源可见：isInSubtreeOf(x, '') 只会命中根级（fp==='')，
      // 故根视图单独放行同数据源全部路径；文件夹视图走子树语义
      list = list.filter(
        (s) => s.dataSourceName === selection.dataSourceName && (target === '' || isInSubtreeOf(s.folderPath, target))
      )
    }
  }
  return list
}
