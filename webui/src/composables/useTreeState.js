// tree-state 共享存储（fix-batch1 Task 2 虚拟文件夹）：
// 原先展开/顺序字典只在 SideTree 内部持有；虚拟文件夹（空文件夹物化）后，
// 列表（ServerTable 的文件夹行）与 ServerListView（新建文件夹）也需要这些键，
// 且侧栏收起（SideTree 卸载）时键不能丢——故提为模块级共享 composable（同 useServers 模式）。
// PUT /api/ui-state/tree 对两个字典都是全量替换：保存必须以最近一次 GET/PUT 快照
// （persistedExpanded）为基底合并，未取到基底前绝不 PUT（清空 WPF 侧空文件夹/顺序）。
import { computed, ref } from 'vue'
import { api } from '../api'
import { folderPathsFromKeys } from './folders'

const expandedMap = ref({}) // 工作副本：键 → bool（本地切换意图；GET 到达前的切换优先）
const orderMap = ref({}) // 自定义顺序工作副本（GET 基底 + 本地拖拽编辑）
const persistedExpanded = ref({}) // 最近一次 GET/PUT 快照（合并基底）
const persistedOrder = ref({})
const loaded = ref(false) // 是否成功 GET 过——未取到基底前绝不 PUT

async function load() {
  if (loaded.value) return
  try {
    const st = await api.getTreeState()
    persistedExpanded.value = st?.expanded || {}
    persistedOrder.value = st?.order || {}
    // 水合合并：仅补充内存中没有的键——GET 返回前用户已切换过的展开态（本地意图）优先
    expandedMap.value = { ...persistedExpanded.value, ...expandedMap.value }
    orderMap.value = { ...persistedOrder.value }
    loaded.value = true
  } catch {
    // 后端不可达：保持全展开默认；首次保存前会再试一次 GET（见 persist 守卫）
  }
}

const isExpanded = (key) => expandedMap.value[key] ?? true // 缺失键=展开（WPF GetValueOrDefault(path, true) 同义）

function toggleExpand(key) {
  expandedMap.value = { ...expandedMap.value, [key]: !isExpanded(key) }
  return expandedMap.value[key]
}

// 本地意图直写（虚拟文件夹操作用：新建/重命名把新键置 true → 树/列表即时物化，不等 PUT 往返）
function setLocalKeys(add, removeKeys = []) {
  const next = { ...expandedMap.value }
  for (const k of removeKeys) delete next[k]
  Object.assign(next, add)
  expandedMap.value = next
}

// 已知全部键（快照 ∪ 本地意图）→ 虚拟文件夹物化源 + 键重写操作的输入。
// 删除/重命名文件夹时键从两处都移除（persist 落盘后快照同步），故此合并即
// 「当前应物化的文件夹路径」全集；PUT 失败时快照未变 → 键保留（回滚语义）。
const knownExpanded = computed(() => ({ ...persistedExpanded.value, ...expandedMap.value }))
const folderPathsByDs = computed(() => folderPathsFromKeys(Object.keys(knownExpanded.value)))

/**
 * 基于快照基底合并后全量 PUT。mutate(merged) 就地修改合并结果（键 → bool）；
 * order 字典发 orderMap 工作副本（GET 基底 + 本地拖拽编辑的完整字典）。
 * 返回是否落盘成功（后端不可达 / 未水合时 false，调用方提示失败）。
 */
async function persist(mutate) {
  if (!loaded.value) {
    await load()
    if (!loaded.value) return false
  }
  const merged = { ...persistedExpanded.value }
  mutate(merged)
  try {
    await api.saveTreeState({ expanded: merged, order: { ...orderMap.value } })
  } catch (e) {
    console.warn('[useTreeState] saveTreeState failed:', e?.message || e)
    return false
  }
  persistedExpanded.value = merged
  persistedOrder.value = { ...orderMap.value }
  return true
}

export function useTreeState() {
  return {
    expandedMap, orderMap, loaded, load,
    isExpanded, toggleExpand, setLocalKeys,
    knownExpanded, folderPathsByDs, persist,
  }
}
