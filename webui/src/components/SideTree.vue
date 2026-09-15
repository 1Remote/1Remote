<script setup>
// 边栏树 + 标签区（spec §3.2/§3.3，Task 15）：
// - 数据源=根节点（🗄 名称 · 类型 + 状态点：绿=connected / 灰=disconnected / 红=reconnecting+倒计时 tooltip）
// - 根下递归文件夹树，叶=服务器行（图标+名称；单击=选中、双击=连接[Task 18 接线]）
// - 节点右侧子服务器计数（含全部后代，与根节点 serverCount 同语义）
// - 树下方「标签」chips（置顶在前），点击 emit update:tag；「+ 管理」占位（Plan 3）
// - 底部「« 收起边栏」emit update:collapsed
// - 展开/折叠经 /api/ui-state/tree 持久化（防抖 500ms），与 WPF 共用 .tree_view.json
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { api } from '../api'
import { buildTree, useServers } from '../composables/useServers'

// ---- 持久化键格式（必须与 WPF 逐字符一致，见 Ui/View/ServerView/Tree/ServerTreeViewModel.cs）----
// FullPathSeparator = " ]=+=+=+=>[ "；根节点 FullPath = 数据源名本身（SetParent: IsRootFolder → FullPath = _name），
// 子节点 FullPath = 父 FullPath + SEP + 名。后端 folderPath 约定 "a/b"（DtoMapper: string.Join("/", TreeNodes)），
// 故 a/b @ Local → "Local ]=+=+=+=>[ a ]=+=+=+=>[ b"
const SEP = ' ]=+=+=+=>[ '
const fullKey = (dsName, folderPath) => (folderPath ? dsName + SEP + folderPath.split('/').join(SEP) : dsName)

const props = defineProps({
  selection: { type: Object, default: null }, // { dataSourceName, folderPath, serverId? }（v-model:selection）
  tag: { type: String, default: '' }, // 当前标签过滤（v-model:tag，仅用于 chip 高亮）
})
const emit = defineEmits(['update:selection', 'update:tag', 'connect', 'update:collapsed'])

const { servers, datasources, tags } = useServers()
const tree = computed(() => buildTree(servers.value, datasources.value))

// ---- 展开状态：key → bool（true=展开）。缺失键=展开（对齐 WPF LoadExpansionStates 的 GetValueOrDefault(path, true)）
const expandedMap = ref({})
// 最近一次 GET 的两个字典：PUT 为全量替换，保存时必须以它们为基底——expanded 合并保留 web 树
// 不认识的键（如 WPF 侧空文件夹）；order 原样回传（发 {} 会清掉 WPF 侧拖拽产生的 CustomNodeOrder）
let fetchedExpanded = {}
let fetchedOrder = {}
let hydrated = false // 是否成功 GET 过——未取到基底前绝不 PUT，避免全量替换清空既有持久化

const isExpanded = (key) => expandedMap.value[key] ?? true

async function loadTreeState() {
  try {
    const st = await api.getTreeState()
    fetchedExpanded = st?.expanded || {}
    fetchedOrder = st?.order || {}
    // 水合合并：仅补充内存中没有的键——GET 返回前用户已切换过的展开态（本地意图）优先，
    // 不能被后到的旧快照覆盖
    expandedMap.value = { ...fetchedExpanded, ...expandedMap.value }
    hydrated = true
  } catch {
    // 后端不可达：保持全展开默认；首次保存前会再试一次 GET
  }
}

let saveTimer = null
let savePending = false
function scheduleSave() {
  savePending = true
  clearTimeout(saveTimer)
  saveTimer = setTimeout(() => {
    savePending = false
    flushSave()
  }, 500)
}

async function flushSave() {
  if (!hydrated) {
    await loadTreeState()
    if (!hydrated) return // 仍不可达：跳过保存，本次会话内状态由内存兜底
  }
  const merged = { ...fetchedExpanded }
  // 只覆盖当前树中真实存在的根/文件夹键（WPF SaveExpansionStates 的"当前有效路径"重建语义），
  // 其余键原样保留——PUT 是对两个字典的全量替换，误删会让 WPF 侧空文件夹消失
  for (const row of rows.value) {
    if (row.kind !== 'server') merged[row.key] = isExpanded(row.key)
  }
  try {
    // order 原样回传 GET 到的字典——PUT 对两个字典都是全量替换，web 侧未实现自定义排序
    // （Plan 4），发空字典会永久清掉 WPF 侧拖拽产生的 CustomNodeOrder
    await api.saveTreeState({ expanded: merged, order: { ...fetchedOrder } })
    fetchedExpanded = merged
  } catch (e) {
    console.warn('[SideTree] saveTreeState failed:', e?.message || e)
  }
}

onMounted(loadTreeState)
onBeforeUnmount(() => {
  clearTimeout(saveTimer)
  if (savePending) flushSave() // 卸载时立即落盘防抖未到的变更（fire-and-forget）
})

function toggleExpand(key) {
  expandedMap.value = { ...expandedMap.value, [key]: !isExpanded(key) }
  scheduleSave()
}

// ---- 可见行扁平化（免递归组件；depth 控缩进）。每层先文件夹后服务器（对齐 WPF SortNodes 的 OrderBy(!IsFolder)）
const rows = computed(() => {
  const countServers = (f) => f.servers.length + f.folders.reduce((n, x) => n + countServers(x), 0)
  const out = []
  const pushLevel = (holder, dsName, depth) => {
    for (const f of holder.folders) {
      const key = fullKey(dsName, f.path)
      out.push({ kind: 'folder', key, folder: f, dsName, depth, count: countServers(f) })
      if (isExpanded(key)) pushLevel(f, dsName, depth + 1)
    }
    for (const s of holder.servers) {
      out.push({ kind: 'server', key: 'srv:' + s.id, server: s, dsName, depth })
    }
  }
  for (const root of tree.value) {
    out.push({ kind: 'root', key: root.name, ds: root, depth: 0, count: countServers(root) })
    if (isExpanded(root.name)) pushLevel(root, root.name, 1)
  }
  return out
})

// ---- 选中模型：根 → {ds,''}；文件夹 → {ds,path}；服务器叶 → {ds,其 folderPath,+serverId}
function isSelected(row) {
  const sel = props.selection
  if (!sel) return false
  if (row.kind === 'server') return sel.serverId === row.server.id
  if (row.kind === 'folder') return sel.serverId == null && sel.dataSourceName === row.dsName && sel.folderPath === row.folder.path
  return sel.serverId == null && !sel.folderPath && sel.dataSourceName === row.ds.name
}

function onRowClick(row) {
  if (row.kind === 'server') {
    emit('update:selection', { dataSourceName: row.dsName, folderPath: row.server.folderPath, serverId: row.server.id })
  } else if (row.kind === 'folder') {
    emit('update:selection', { dataSourceName: row.dsName, folderPath: row.folder.path })
  } else {
    emit('update:selection', { dataSourceName: row.ds.name, folderPath: '' })
  }
}

function onRowDblclick(row) {
  if (row.kind === 'server') emit('connect', row.server.id) // 实际连接动作 Task 18 接线（父级暂可忽略）
}

// ---- 展示辅助
const dotClass = (status) => (status === 'connected' ? 'ok' : status === 'reconnecting' ? 'bad' : 'idle')
const iconSrc = (s) => (s.iconBase64 ? 'data:image/png;base64,' + s.iconBase64 : '')
const protocolInitial = (p) => (p || '?').charAt(0).toUpperCase()

// 置顶标签在前，组内保持 API 顺序（稳定排序；名称排序/管理归 Plan 3）
const sortedTags = computed(() => tags.value.slice().sort((a, b) => Number(b.isPinned) - Number(a.isPinned)))
</script>

<template>
  <div class="side-tree">
    <div class="tree-scroll">
      <div v-if="!rows.length" class="empty-hint">（无数据源）</div>
      <div
        v-for="row in rows"
        :key="row.key"
        class="row"
        :class="{ selected: isSelected(row) }"
        :style="{ paddingLeft: 6 + row.depth * 12 + 'px' }"
        @click="onRowClick(row)"
        @dblclick="onRowDblclick(row)"
      >
        <!-- 根/文件夹：展开箭头；服务器叶：占位对齐（无箭头） -->
        <span
          v-if="row.kind !== 'server'"
          class="chevron"
          :class="{ open: isExpanded(row.key) }"
          @click.stop="toggleExpand(row.key)"
        >▸</span>
        <span v-else class="chevron chevron-leaf"></span>

        <!-- 数据源根：🗄 名称 · 类型 + 状态点 + 计数 -->
        <template v-if="row.kind === 'root'">
          <span class="ds-icon">🗄</span>
          <span class="label" :title="row.ds.name">{{ row.ds.name }}</span>
          <span class="ds-type">{{ row.ds.type }}</span>
          <span
            class="dot"
            :class="dotClass(row.ds.status)"
            :title="row.ds.status === 'reconnecting' ? (row.ds.reconnectInfo || '重连中') : row.ds.status"
          ></span>
          <span class="count">{{ row.count }}</span>
        </template>

        <!-- 文件夹：📁 名称 + 子服务器计数 -->
        <template v-else-if="row.kind === 'folder'">
          <span class="folder-icon">📁</span>
          <span class="label" :title="row.folder.path">{{ row.folder.name }}</span>
          <span class="count">{{ row.count }}</span>
        </template>

        <!-- 服务器叶：小图标（无自定义图标时协议首字母方块，底色=服务器自定义色）+ 名称 -->
        <template v-else>
          <img v-if="row.server.iconBase64" class="srv-icon" :src="iconSrc(row.server)" alt="" />
          <span
            v-else
            class="srv-icon srv-icon-fb"
            :style="row.server.color ? { background: row.server.color } : null"
          >{{ protocolInitial(row.server.protocol) }}</span>
          <span class="label srv-label" :title="row.server.displayName">{{ row.server.displayName }}</span>
        </template>
      </div>
    </div>

    <!-- 标签区（spec §3.3）：chips+计数，置顶在前；点击=过滤条件（Task 17 接线搜索交集） -->
    <div class="tags">
      <div class="tags-head">标签</div>
      <div class="tag-list">
        <button
          v-for="t in sortedTags"
          :key="t.name"
          class="tag-chip"
          :class="{ active: t.name === tag }"
          :title="t.name"
          @click="emit('update:tag', t.name === tag ? '' : t.name)"
        >
          <span v-if="t.isPinned" class="pin">📌</span>{{ t.name }}<span class="tag-count">{{ t.count }}</span>
        </button>
        <!-- 占位：标签管理模态（置顶/重命名/删除等）属 Plan 3 -->
        <button class="tag-chip tag-manage" title="Plan 3">+ 管理</button>
      </div>
    </div>

    <button class="collapse-btn" title="收起边栏" @click="emit('update:collapsed', true)">« 收起边栏</button>
  </div>
</template>

<style scoped>
/* 全部取色走主题 CSS 变量（spec §4），行高紧凑 ~26px，hover --bg-hover */
.side-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}
.tree-scroll {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 6px 6px 10px;
}
.empty-hint {
  color: var(--text-4);
  font-size: 12px;
  padding: 10px 8px;
}

.row {
  display: flex;
  align-items: center;
  gap: 4px;
  height: 26px;
  padding-right: 6px;
  border-radius: 5px;
  white-space: nowrap;
  user-select: none;
}
.row:hover {
  background: var(--bg-hover);
}
.row.selected {
  background: var(--accent-container);
}

.chevron {
  flex: 0 0 14px;
  color: var(--text-3);
  font-size: 10px;
  line-height: 1;
  text-align: center;
  cursor: pointer;
  transition: transform 0.12s ease;
}
.chevron.open {
  transform: rotate(90deg);
}
.chevron-leaf {
  cursor: default;
}

.ds-icon,
.folder-icon {
  flex: 0 0 16px;
  font-size: 12px;
  text-align: center;
}
.label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 13px;
  color: var(--text-1);
}
.srv-label {
  font-size: 12.5px;
  color: var(--text-2);
}
.ds-type {
  flex: 0 0 auto;
  color: var(--text-4);
  font-size: 10px;
}

.dot {
  flex: 0 0 6px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
}
.dot.ok {
  background: var(--success);
}
.dot.idle {
  background: var(--text-4);
}
.dot.bad {
  background: var(--danger);
}

.count {
  margin-left: auto;
  flex: 0 0 auto;
  color: var(--text-4);
  font-size: 11px;
}

.srv-icon {
  flex: 0 0 14px;
  width: 14px;
  height: 14px;
  border-radius: 3px;
  object-fit: cover;
}
.srv-icon-fb {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 9px;
}

.tags {
  flex-shrink: 0;
  max-height: 35%;
  overflow: auto;
  border-top: 1px solid var(--border);
  padding: 8px 10px 6px;
}
.tags-head {
  color: var(--text-4);
  font-size: 11px;
  margin-bottom: 6px;
}
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.tag-chip {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  max-width: 100%;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: 11px;
  line-height: 1;
  padding: 3px 8px;
  cursor: pointer;
}
.tag-chip:hover {
  border-color: var(--border-strong);
}
.tag-chip.active {
  border-color: var(--accent);
  color: var(--accent-text);
}
.tag-chip .pin {
  font-size: 9px;
}
.tag-count {
  color: var(--text-4);
}
.tag-manage {
  color: var(--text-3);
}

.collapse-btn {
  flex-shrink: 0;
  height: 28px;
  border: none;
  border-top: 1px solid var(--border);
  background: transparent;
  color: var(--text-3);
  font-size: 12px;
  cursor: pointer;
}
.collapse-btn:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
</style>
