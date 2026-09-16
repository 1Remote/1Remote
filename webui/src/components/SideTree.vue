<script setup>
// 边栏树 + 标签区（spec §3.2/§3.3，Task 15）：
// - 数据源=根节点（🗄 名称 · 类型 + 状态点：绿=connected / 灰=disconnected / 红=reconnecting+倒计时 tooltip）
// - 根下递归文件夹树，叶=服务器行（图标+名称；单击=选中、双击=连接[Task 18 接线]）
// - 节点右侧子服务器计数（含全部后代，与根节点 serverCount 同语义）
// - 树下方「标签」chips（置顶在前），点击 emit update:tag；「+ 管理」占位（Plan 3）
// - 底部「« 收起边栏」emit update:collapsed
// - 展开/折叠经 /api/ui-state/tree 持久化（防抖 500ms），与 WPF 共用 .tree_view.json
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
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
const emit = defineEmits(['update:selection', 'update:tag', 'connect', 'update:collapsed', 'manage-tags'])
const { t } = useI18n()
const message = useMessage()

const { servers, datasources, tags, reload } = useServers()
const tree = computed(() => buildTree(servers.value, datasources.value))

// ---- 自定义顺序（Plan 4 Task 4）：键与 WPF CustomNodeOrder 一致 —— 服务器=其 id、
// 文件夹="%$TreeNode$%:"+名称（见 ServerTreeViewModel.TreeNode.Id；以名称为键、跨层级同名
// 文件夹共用一键属 WPF 既有语义）。orderMap 是 GET 到的字典 + 本地拖拽编辑的工作副本，
// 展示层每级子节点（文件夹+服务器合并，对齐 WPF Custom 排序的交错语义）按序号稳定排序；
// 无序号者排末尾（WPF LoadLocalCaches 的 int.MaxValue 同义；Web 把新增服务器排末尾而
// WPF 默认 0 排最前，是记录在案的有意偏差）
const FOLDER_ID = '%$TreeNode$%:' // 与 ServerTreeViewModel.FolderNodePrefix 逐字符一致
const childId = (c) => (c.kind === 'folder' ? FOLDER_ID + c.folder.name : c.server.id)
const orderMap = ref({})

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
    // 不能被后到的旧快照覆盖。顺序字典同取服务端基底（未 GET 到前绝不 PUT，见 hydrated 守卫）
    expandedMap.value = { ...fetchedExpanded, ...expandedMap.value }
    orderMap.value = { ...fetchedOrder }
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
    // order 发送 orderMap（= GET 基底 + 本地拖拽编辑的完整字典）——PUT 对两个字典都是
    // 全量替换，发空字典会永久清掉 WPF 侧拖拽产生的 CustomNodeOrder
    await api.saveTreeState({ expanded: merged, order: { ...orderMap.value } })
    fetchedExpanded = merged
    fetchedOrder = { ...orderMap.value }
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

// ---- 可见行扁平化（免递归组件；depth 控缩进）。每级子节点 = 文件夹+服务器合并后按自定义
// 序号稳定排序（无序号=保持文件夹在前、其余按入序的自然顺序，见 orderMap 注释）；
// 序号存在时文件夹/服务器按序交错（对齐 WPF SortNodes 的 Custom 分支）
const levelChildren = (holder) => {
  const out = []
  for (const f of holder.folders) out.push({ kind: 'folder', folder: f })
  for (const s of holder.servers) out.push({ kind: 'server', server: s })
  const key = (c) => orderMap.value[childId(c)]
  return out.sort((a, b) => (key(a) ?? Infinity) - (key(b) ?? Infinity))
}
const rows = computed(() => {
  const countServers = (f) => f.servers.length + f.folders.reduce((n, x) => n + countServers(x), 0)
  const out = []
  const pushLevel = (holder, dsName, depth) => {
    for (const c of levelChildren(holder)) {
      if (c.kind === 'folder') {
        const key = fullKey(dsName, c.folder.path)
        out.push({ kind: 'folder', key, folder: c.folder, dsName, depth, count: countServers(c.folder) })
        if (isExpanded(key)) pushLevel(c.folder, dsName, depth + 1)
      } else {
        out.push({ kind: 'server', key: 'srv:' + c.server.id, server: c.server, dsName, depth })
      }
    }
  }
  for (const root of tree.value) {
    out.push({ kind: 'root', key: root.name, ds: root, depth: 0, count: countServers(root) })
    if (isExpanded(root.name)) pushLevel(root, root.name, 1)
  }
  return out
})

// ---- 树拖拽（Plan 4 Task 4）：原生 HTML5 DnD——树需要「前插/后插/移入」三种落区语义，
// vuedraggable 的扁平排序列表模型对层级命中区适配差，故自实现。
// 落区判定：行内上 25% = 插到目标前（上方指示线）、下 25% = 插到目标后（下方指示线）、
// 中部 = 移入（容器高亮；服务器行中部 = 移入其所在文件夹，与 WPF ServerMoveToFolder 的
// targetFolder.ParentNode 回退同义）。计划中的 400ms 悬停切换简化为立即切换（指示线与
// 容器高亮的视觉区分已足够表达两种语义），记录偏差。
// 非法目标（跨数据源 / 拖到自己 / 拖文件夹到自己的后代 / 根行前插后插 / 只读数据源）
// 不显示指示且不 preventDefault → drop 被浏览器拒绝。
const dragRow = ref(null) // 被拖行的 rows 快照（drop 时树可能未变——快照够用）
const dropHint = ref(null) // { key, zone } zone: 'before' | 'after' | 'into'
const ZONE_RATIO = 0.25
const moving = ref(false) // 逐台 PUT 进行中（防重入拖拽）

const isDescendantPath = (ancestor, path) => path === ancestor || path.startsWith(ancestor + '/')
const dsWritable = (dsName) => datasources.value.find(d => d.name === dsName)?.writable !== false
function isDraggable(row) {
  if (row.kind === 'root' || moving.value) return false // 根（数据源）不可拖
  return dsWritable(row.dsName) // 只读数据源（如他人共享的只读库）禁止改结构
}
function zoneFor(row, e) {
  if (row.kind === 'root') return 'into' // 数据源根行只支持移入（顶层没有前后插语义）
  const r = e.currentTarget.getBoundingClientRect()
  const y = (e.clientY - r.top) / r.height
  return y < ZONE_RATIO ? 'before' : y > 1 - ZONE_RATIO ? 'after' : 'into'
}
function canDrop(row, zone) {
  const src = dragRow.value
  if (!src || src.key === row.key) return false // 拖到自己身上
  const srcDs = src.kind === 'root' ? src.ds.name : src.dsName
  const dstDs = row.kind === 'root' ? row.ds.name : row.dsName
  if (srcDs !== dstDs) return false // 跨数据源禁止（WPF GetDataBaseNode 同款）
  if (zone !== 'into' && row.kind === 'root') return false
  // 不能移入自己的后代：目标无论文件夹行还是后代文件夹内的服务器行都拒绝——后者漏判会让
  // 落点路径解析把子级拼回自身（A/B → A/B/B 嵌套重复）；WPF 先把服务器目标归一到其父
  // 再 FindDescendant（ServerTreeViewModel.cs:622-626），拒绝语义一致
  if (src.kind === 'folder') {
    const targetPath = row.kind === 'folder' ? row.folder.path : row.kind === 'server' ? (row.server.folderPath || '') : ''
    if (targetPath && isDescendantPath(src.folder.path, targetPath)) return false
  }
  return true
}
function onRowDragStart(row, e) {
  dragRow.value = row
  e.dataTransfer.effectAllowed = 'move'
  e.dataTransfer.setData('text/plain', row.key) // Firefox 需要非空 data 才会启动拖拽
}
function onRowDragEnd() {
  dragRow.value = null
  dropHint.value = null
}
function onRowDragOver(row, e) {
  if (!dragRow.value || moving.value) return
  const zone = zoneFor(row, e)
  if (!canDrop(row, zone)) {
    dropHint.value = null
    return
  }
  e.preventDefault()
  e.dataTransfer.dropEffect = 'move'
  dropHint.value = { key: row.key, zone }
}
function onRowDrop(row, e) {
  const hint = dropHint.value
  dropHint.value = null
  if (!dragRow.value || moving.value || !hint || hint.key !== row.key) return
  const zone = zoneFor(row, e)
  const src = dragRow.value
  dragRow.value = null
  if (!canDrop(row, zone)) return
  e.preventDefault()
  applyTreeMove(src, row, zone)
}

// 目标父路径（文件夹路径段数组）：into 文件夹=其路径；into 根=空；into 服务器行=其所在文件夹；
// before/after = 与目标同级（目标的父路径）
function targetParentPath(row, zone) {
  if (zone === 'into') {
    if (row.kind === 'folder') return row.folder.path ? row.folder.path.split('/') : []
    if (row.kind === 'root') return []
    return row.server.folderPath ? row.server.folderPath.split('/') : []
  }
  if (row.kind === 'folder') {
    const p = row.folder.path.split('/')
    p.pop()
    return p
  }
  return row.server.folderPath ? row.server.folderPath.split('/') : []
}

function holderAt(dsName, parentPath) {
  let node = tree.value.find(r => r.name === dsName)
  if (!node) return null
  for (const seg of parentPath) {
    node = node.folders.find(f => f.name === seg)
    if (!node) return null
  }
  return node
}

// 落点执行：算受影响服务器集合的新 TreeNodes → 逐台 GET config → 改 TreeNodes → PUT
//（config 往返每台一次即可，N 通常小；UpdateServer 路径不触发 ReloadAll/SSE——needRead
// 在写库前判定为否，故由前端显式 reload() 刷新）。同级顺序：before/after 重排目标层
// 兄弟节点的序号（1 起，键=node Id，含文件夹）；into 删除被拖节点的序号键（排末尾，
// 与 WPF AddChild 后无序号即最后的语义一致）。写回经 PUT /api/ui-state/tree 全量替换。
async function applyTreeMove(src, row, zone) {
  const parentPath = targetParentPath(row, zone)
  const prefix = src.kind === 'folder' ? src.folder.path + '/' : ''
  const affected = src.kind === 'server'
    ? [{ server: src.server, rest: [] }]
    : servers.value
        .filter(s => s.dataSourceName === src.dsName && (s.folderPath ? s.folderPath + '/' : '').startsWith(prefix))
        .map(s => ({ server: s, rest: s.folderPath.slice(prefix.length).split('/').filter(Boolean) }))

  let moved = 0
  const failed = []
  moving.value = true
  try {
    for (const { server, rest } of affected) {
      const newPath = [...parentPath, ...rest]
      if ((server.folderPath || '') === newPath.join('/')) continue // 位置未变（仅顺序调整）
      try {
        const cfg = await api.getServerConfig(server.id, src.dsName)
        cfg.json.TreeNodes = newPath // 编辑器配置域 PascalCase 直通（勿做命名转换）
        await api.updateServer(server.id, cfg.json, src.dsName)
        moved++
      } catch (err) {
        console.warn('[SideTree] move failed:', server.id, err?.message || err)
        failed.push(server.displayName)
      }
    }

    // 同级顺序写回（仅 before/after 重排；into 清键排末尾）
    let orderChanged = false
    if (zone === 'before' || zone === 'after') {
      const holder = holderAt(src.dsName, parentPath)
      if (holder) {
        const siblings = levelChildren(holder).filter(c =>
          !(src.kind === 'server' && c.kind === 'server' && c.server.id === src.server.id)
          && !(src.kind === 'folder' && c.kind === 'folder' && c.folder.path === src.folder.path))
        const idx = siblings.findIndex(c =>
          (row.kind === 'folder' && c.kind === 'folder' && c.folder.path === row.folder.path)
          || (row.kind === 'server' && c.kind === 'server' && c.server.id === row.server.id))
        if (idx >= 0) {
          siblings.splice(zone === 'before' ? idx : idx + 1, 0,
            src.kind === 'folder' ? { kind: 'folder', folder: src.folder } : { kind: 'server', server: src.server })
          const next = { ...orderMap.value }
          siblings.forEach((c, i) => { next[childId(c)] = i + 1 })
          orderMap.value = next
          orderChanged = true
        }
      }
    } else {
      const id = src.kind === 'folder' ? FOLDER_ID + src.folder.name : src.server.id
      if (orderMap.value[id] != null) {
        const next = { ...orderMap.value }
        delete next[id]
        orderMap.value = next
        orderChanged = true
      }
    }

    if (moved === 0 && failed.length === 0 && !orderChanged) return // 完全无变化（原位放下）
    if (orderChanged) await flushSave()
    await reload() // UpdateServer 路径不触发 SSE（见方法头注释），显式刷新列表/树
    if (failed.length) message.error(t('toast.treeMoveFailed', { n: failed.length }))
    else if (moved > 0) message.success(t('toast.treeMoved', { n: moved }))
  } finally {
    moving.value = false
  }
}

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
  // 双击服务器叶 = 连接：emit 到父级（ServerListView）经 api.connect 发起，桌面端接管会话（Task 18 已接线）
  if (row.kind === 'server') emit('connect', row.server.id)
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
      <div v-if="!rows.length" class="empty-hint">{{ t('tree.noDatasources') }}</div>
      <div
        v-for="row in rows"
        :key="row.key"
        class="row"
        :class="{
          selected: isSelected(row),
          'drop-before': dropHint && dropHint.key === row.key && dropHint.zone === 'before',
          'drop-after': dropHint && dropHint.key === row.key && dropHint.zone === 'after',
          'drop-into': dropHint && dropHint.key === row.key && dropHint.zone === 'into',
        }"
        :style="{ paddingLeft: 6 + row.depth * 12 + 'px' }"
        :draggable="isDraggable(row)"
        @click="onRowClick(row)"
        @dblclick="onRowDblclick(row)"
        @dragstart="onRowDragStart(row, $event)"
        @dragend="onRowDragEnd"
        @dragover="onRowDragOver(row, $event)"
        @drop="onRowDrop(row, $event)"
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
            :title="row.ds.status === 'reconnecting' ? (row.ds.reconnectInfo || t('tree.reconnecting')) : row.ds.status"
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
      <div class="tags-head">{{ t('tree.tags') }}</div>
      <div class="tag-list">
        <!-- 循环变量命名 tg：避免遮蔽 script setup 暴露的 i18n 翻译函数 t -->
        <button
          v-for="tg in sortedTags"
          :key="tg.name"
          class="tag-chip"
          :class="{ active: tg.name === tag }"
          :title="tg.name"
          @click="emit('update:tag', tg.name === tag ? '' : tg.name)"
        >
          <span v-if="tg.isPinned" class="pin">📌</span>{{ tg.name }}<span class="tag-count">{{ tg.count }}</span>
        </button>
        <!-- 标签管理：打开模态（TagManagerModal 由 ServerListView 挂载）——ds 取当前树选中 -->
        <button class="tag-chip tag-manage" :title="t('tagm.title')" @click="emit('manage-tags')">{{ t('tree.manageTags') }}</button>
      </div>
    </div>

    <button class="collapse-btn" :title="t('tree.collapseTitle')" @click="emit('update:collapsed', true)">« {{ t('tree.collapse') }}</button>
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
/* 拖拽（Plan 4 Task 4）：可拖行 grab；指示线/容器高亮用主题强调色，与选中底色区分 */
.row[draggable='true'] {
  cursor: grab;
}
.row.drop-before {
  box-shadow: inset 0 2px 0 var(--accent);
}
.row.drop-after {
  box-shadow: inset 0 -2px 0 var(--accent);
}
.row.drop-into {
  background: var(--accent-container);
  outline: 1px dashed var(--accent);
  outline-offset: -1px;
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
