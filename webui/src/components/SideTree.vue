<script setup>
// 边栏树 + 标签区：
// - 顶部常驻虚拟根「全部数据」（点击=清除数据源过滤，列表显示全部库服务器）——
//   仅多数据源时渲染；单数据源（总数 === 1）不显示此根，该源根直接为顶（depth 上移，
//   见 rows），选中协议不变：父层传归一化后的 viewSel（null 已映射为源根）
// - 数据源根（🗄 名称 · 类型 + 状态点）→ 递归文件夹树；不再渲染服务器叶（列表承担）
// - 虚拟文件夹：tree-state expansion 键即存在（空文件夹物化，与 WPF BuildView 一致）；
//   右键菜单 新建/重命名/删除（folderOps 统一实现，列表侧共用）
// - 节点右侧子服务器计数与列表同口径（E5-1/H38 后统一为递归口径）：数据源根/文件夹
//   = 该子树全部服务器数（含子文件夹）——与 H38 后「点进去的列表行数/面包屑 N 台」
//   一致；「全部数据」= 全库服务器总数（各源徽标之和与之相等，H37 矛盾随之消解）
// - 树下方「标签」chips（置顶在前）；底部「« 收起边栏」emit update:collapsed
// - 展开/折叠与拖拽经 /api/ui-state/tree 持久化（防抖 500ms），与 WPF 共用 .tree_view.json；
//   字典状态收在 useTreeState 共享存储（列表文件夹行/新建文件夹也消费，侧栏收起不丢）
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { useServers } from '../composables/useServers'
import { buildTree, countHolderServers, fullKey, holderAt, isDescendantPath } from '../composables/folders'
import { useTreeState } from '../composables/useTreeState'
import { useFolderOps } from '../composables/folderOps'
import {
  CROSS_DS_FOLDER,
  CROSS_DS_NONE,
  CROSS_DS_SERVER,
  crossDsHover,
  listDragFolder,
  listDragServer,
} from '../composables/tableBus'

const props = defineProps({
  selection: { type: Object, default: null }, // { dataSourceName, folderPath } | null=全部数据（父层传归一化 viewSel；update:selection 回写原始值）
  tag: { type: String, default: '' }, // 当前标签过滤（仅用于 chip 高亮，父层 v-model:tag）
})
const emit = defineEmits(['update:selection', 'update:tag', 'update:collapsed', 'manage-tags'])
const { t } = useI18n()
const message = useMessage()

const { servers, datasources, tags, reload, dsWritable } = useServers()
const { orderMap, folderPathsByDs, load, isExpanded, toggleExpand, persist } = useTreeState()
const folderOps = useFolderOps()
const tree = computed(() => buildTree(servers.value, datasources.value, folderPathsByDs.value))

// ---- 自定义顺序：键与 WPF CustomNodeOrder 一致 ——
// 文件夹="%$TreeNode$%:"+名称（见 ServerTreeViewModel.TreeNode.Id；以名称为键、跨层级同名
// 文件夹共用一键属 WPF 既有语义）。无序号者排末尾（WPF LoadLocalCaches 的 int.MaxValue 同义）
const FOLDER_ID = '%$TreeNode$%:' // 与 ServerTreeViewModel.FolderNodePrefix 逐字符一致
const levelChildren = (holder) => {
  const key = (f) => orderMap.value[FOLDER_ID + f.name]
  return holder.folders.slice().sort((a, b) => (key(a) ?? Infinity) - (key(b) ?? Infinity))
}

// ---- 展开保存：防抖 500ms → 基于快照基底合并（只覆盖当前可见的根/文件夹键，
// 其余键原样保留——PUT 全量替换，误删会让 WPF 侧空文件夹消失；详见 useTreeState.persist）
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
  await persist((m) => {
    for (const row of rows.value) {
      if (row.kind === 'folder' || row.kind === 'root') m[row.key] = isExpanded(row.key)
    }
  })
}

onMounted(() => {
  load() // 共享存储幂等（ServerListView 亦会触发首载）
  // 跨库拖拽的 dragend/drop 兜底监听已迁至 ServerTable（H18：随 SideTree 卸载会在
  // 边栏收起时失效，窄窗下跨库拖拽零反馈——见 ServerTable.onListDragEndGlobal 注释）
})
onBeforeUnmount(() => {
  clearTimeout(saveTimer)
  if (savePending) flushSave() // 卸载时立即落盘防抖未到的变更（fire-and-forget）
})

function onToggle(key) {
  toggleExpand(key)
  scheduleSave()
}

// 「全部数据」虚拟根的展开态：纯本地（WPF 无此节点，不落 tree-state 字典）
const allOpen = ref(true)

// ---- 可见行扁平化（免递归组件；depth 控缩进）：全部数据 → 数据源根 → 文件夹（无服务器叶）
// 单数据源（datasources 总数 === 1，通用判定不认死名字）：不渲染「全部数据」虚拟根，
// 该数据源根上移到 depth 0（文件夹随之 1 起）——只有一个源时"全部数据"与"源根"语义合一，
// 中间层只添一次无意义点击（owner 需求）；多数据源行为不变。「全部数据」的展开态 allOpen
// 纯本地（WPF 无此节点，不落 tree-state 字典），单源模式不参与渲染
const singleDs = computed(() => datasources.value.length === 1)
const rows = computed(() => {
  const out = []
  if (!singleDs.value) {
    // 「全部数据」= 全库服务器总数（与该视图列表同口径；直接用 servers 长度——buildTree
    // 会丢弃数据源快照错配的孤儿服务器，树内求和会把它们漏计）
    out.push({ kind: 'all', key: 'all', depth: 0, count: servers.value.length })
    if (!allOpen.value) return out
  }
  const rootDepth = singleDs.value ? 0 : 1
  const pushLevel = (holder, dsName, depth) => {
    for (const f of levelChildren(holder)) {
      const key = fullKey(dsName, f.path)
      out.push({ kind: 'folder', key, folder: f, dsName, depth, count: countHolderServers(f) })
      if (isExpanded(key)) pushLevel(f, dsName, depth + 1)
    }
  }
  for (const root of tree.value) {
    out.push({ kind: 'root', key: root.name, ds: root, depth: rootDepth, count: countHolderServers(root) })
    if (isExpanded(root.name)) pushLevel(root, root.name, rootDepth + 1)
  }
  return out
})

// ---- 树拖拽（仅文件夹可拖）：原生 HTML5 DnD。
// 三条互斥链路，以拖拽来源分发（快照互斥，同时至多一条在拖）：
// - 列表服务器行拖入（listDragServer 非空，见 tableBus）与 列表文件夹行拖入
//   （listDragFolder 非空）：同款「移入」语义——数据源根=移到根 / 文件夹=移入其中，
//   无前后插语义（树内重排请直接在树内拖）；文件夹为整子树迁移（folderOps.moveFolder）。
//   两条链路的落区规则共用 listDropAdmit（跨库 / 移入自身子树 / 「全部数据」根均不收）；
// - 树内文件夹拖拽（dragRow 非空）：行内上 25% = 插到目标前、下 25% = 插到目标后、
//   中部 = 移入。非法目标（跨数据源 / 拖到自己 / 拖文件夹到自己的后代 / 根行前插后插 /
//   只读数据源）不显示指示且不 preventDefault → drop 被浏览器拒绝。
const dragRow = ref(null) // 被拖行的 rows 快照（drop 时树可能未变——快照够用）
const dropHint = ref(null) // { key, zone } zone: 'before' | 'after' | 'into'
const ZONE_RATIO = 0.25
const moving = ref(false) // 逐台 PUT 进行中（防重入拖拽）

function isDraggable(row) {
  if (row.kind !== 'folder' || moving.value) return false // 仅文件夹可拖（根/全部数据源不可）
  return dsWritable(row.dsName) // 只读数据源禁止改结构
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
  // 不能移入自己的后代（WPF FindDescendant 同语义）
  if (row.kind === 'folder' && isDescendantPath(src.folder.path, row.folder.path)) return false
  return true
}
function onRowDragStart(row, e) {
  dragRow.value = row
  e.dataTransfer.effectAllowed = 'move'
  e.dataTransfer.setData('text/plain', row.key) // Firefox 需要非空 data 才会启动拖拽
  // 与列表行拖拽（application/x-1r-server-row）以类型区分——列表 drop 分发依据
  e.dataTransfer.setData('application/x-1r-tree-node', row.key)
}
function onRowDragEnd() {
  dragRow.value = null
  dropHint.value = null
}
// 行 dsName（根行持 ds 对象、文件夹行持 dsName 字符串；列表拖入与树内拖共用）
const rowDsName = (row) => (row.kind === 'root' ? row.ds.name : row.dsName)
// 列表行（服务器/文件夹同款「移入」语义）拖入树的落区判定：
// null=不收（重入锁进行中 /「全部数据」虚拟根无数据源归属）；'cross-ds'=跨库；
// 'subtree'=文件夹拖入自身或自身后代（isDescendantPath 含相等，仅文件夹链路可达）；
// 'ok'=合法移入目标
function listDropAdmit(row, srcDsName, draggedFolder) {
  if (moving.value || row.kind === 'all') return null
  if (srcDsName !== rowDsName(row)) return 'cross-ds'
  if (draggedFolder && row.kind === 'folder' && isDescendantPath(draggedFolder.path, row.folder.path)) return 'subtree'
  return 'ok'
}
function onRowDragOver(row, e) {
  const dragSrv = listDragServer.value
  const dragFld = listDragFolder.value
  if (dragSrv || dragFld) {
    // 列表行拖入树：合法目标高亮「移入」；跨库禁光标（drop 不触发，提示在 dragend 出）；
    // 文件夹拖入自身子树同禁（不出跨库提示）
    const admit = listDropAdmit(row, dragSrv ? dragSrv.dataSourceName : dragFld.dsName, dragFld)
    if (!admit) return
    e.preventDefault()
    if (admit === 'ok') {
      e.dataTransfer.dropEffect = 'move'
      dropHint.value = { key: row.key, zone: 'into' }
      crossDsHover.value = CROSS_DS_NONE
    } else {
      e.dataTransfer.dropEffect = 'none'
      dropHint.value = null
      if (admit === 'cross-ds') crossDsHover.value = dragSrv ? CROSS_DS_SERVER : CROSS_DS_FOLDER
    }
    return
  }
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
function onRowDragLeave(row) {
  // 离开行即清该行指示（列表拖入的 drop 不会有树内 dragend 兜底，dragleave 必须自理）
  if (dropHint.value?.key === row.key) dropHint.value = null
}
function onRowDrop(row, e) {
  const dragSrv = listDragServer.value
  const dragFld = listDragFolder.value
  if (dragSrv || dragFld) {
    // 列表行落下：落区判定与 dragover 同源（listDropAdmit），合法目标执行对应移入
    e.preventDefault()
    dropHint.value = null
    const admit = listDropAdmit(row, dragSrv ? dragSrv.dataSourceName : dragFld.dsName, dragFld)
    if (admit === 'cross-ds') {
      crossDsHover.value = CROSS_DS_NONE // drop 不会触发（dropEffect=none），此处仅防御性复位
      return
    }
    if (admit !== 'ok') return
    const path = row.kind === 'folder' ? row.folder.path : ''
    if (dragSrv) folderOps.moveServersToFolder([dragSrv], rowDsName(row), path)
    else folderOps.moveFolder(dragFld.dsName, dragFld.path, path)
    return
  }
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

// 目标父路径（文件夹路径段数组）：into 文件夹=其路径；into 根=空；
// before/after = 与目标同级（目标的父路径）
function targetParentPath(row, zone) {
  if (zone === 'into') {
    if (row.kind === 'folder') return row.folder.path ? row.folder.path.split('/') : []
    return []
  }
  const p = row.folder.path.split('/')
  p.pop()
  return p
}

// 落点执行（owner 2026-09-21 第二轮反馈：树内拖文件夹仍不动——列表→树路径可用而
// 树内不可用，两者的差异只在执行体；据此把跨层移动整体委托给 folderOps.moveFolder
// ——与列表→树完全同一条已验证路径（H3 合并确认 + 前缀重写 + 键迁移 + 进度 toast
// 都在其内），树内不再自持执行体，任何一侧的修复自然双侧生效）：
// - into（文件夹/数据源根行中部）→ moveFolder 到目标文件夹/根；
// - before/after 且跨层（目标在同层的其它父层）→ moveFolder 到目标的父层（落到该层
//   末尾，与 WPF 保留旧序号同语义）；
// - before/after 且同层 → 纯重排（仅写同级序号键，不动服务器不动键，原逻辑保留）。
async function applyTreeMove(src, row, zone) {
  const parentPath = targetParentPath(row, zone)
  const newPath = [...parentPath, src.folder.name].join('/')
  if (newPath !== src.folder.path) {
    // 跨层移动：与列表文件夹行拖拽同款（同名校验/合并确认/整子树前缀重写全在 moveFolder）
    await folderOps.moveFolder(src.dsName, src.folder.path, parentPath.join('/'))
    return
  }

  // 同层 before/after 纯重排：目标层兄弟文件夹序号重写（1 起），不动服务器与键
  moving.value = true
  try {
    const holder = holderAt(tree.value, src.dsName, parentPath.join('/'))
    if (!holder) return
    const siblings = levelChildren(holder).filter((f) => f.path !== src.folder.path)
    const idx = siblings.findIndex((f) => f.path === row.folder.path)
    if (idx < 0) return
    siblings.splice(zone === 'before' ? idx : idx + 1, 0, src.folder)
    const next = { ...orderMap.value }
    siblings.forEach((f, i) => {
      next[FOLDER_ID + f.name] = i + 1
    })
    orderMap.value = next
    await flushSave() // orderMap 随合并基底 PUT 落盘
    await reload()
    message.success(t('tree.folderReordered'))
  } finally {
    moving.value = false
  }
}

// ---- 选中模型：全部数据=null；根 → {ds,''}；文件夹 → {ds,path}
function isSelected(row) {
  const sel = props.selection
  if (row.kind === 'all') return !sel || !sel.dataSourceName
  if (!sel) return false
  if (row.kind === 'folder') return sel.dataSourceName === row.dsName && sel.folderPath === row.folder.path
  return !sel.folderPath && sel.dataSourceName === row.ds.name
}

function onRowClick(row) {
  if (row.kind === 'all') emit('update:selection', null)
  else if (row.kind === 'folder') emit('update:selection', { dataSourceName: row.dsName, folderPath: row.folder.path })
  else emit('update:selection', { dataSourceName: row.ds.name, folderPath: '' })
}

// ---- 右键菜单：新建文件夹（根/文件夹行）/ 重命名 / 删除（文件夹行）
const ctx = ref(null) // { x, y, dsName, parentPath, folderPath? } —— folderPath 空=在根下新建
const rootEl = ref(null)
function onRowContext(row, e) {
  if (row.kind === 'all') return
  e.preventDefault()
  const r = rootEl.value?.getBoundingClientRect()
  const px = r ? e.clientX - r.left : e.clientX
  const py = r ? e.clientY - r.top : e.clientY
  ctx.value = {
    x: r ? Math.max(0, Math.min(px, r.width - 150)) : px, // 防溢出内收（菜单约 140px 宽）
    y: r ? Math.max(0, py) : py,
    dsName: row.kind === 'root' ? row.ds.name : row.dsName,
    parentPath: row.kind === 'folder' ? row.folder.path : '',
    folderPath: row.kind === 'folder' ? row.folder.path : null,
  }
}
const ctxWritable = computed(() => ctx.value && dsWritable(ctx.value.dsName))
function closeCtx() {
  ctx.value = null
}
function onGlobalDownCloseCtx(e) {
  if (ctx.value && !e.target.closest?.('.tree-ctx')) ctx.value = null
}
// 供 ServerListView 全局 Esc 链调用：树右键菜单开着时 Esc 只关菜单（返回 true = 消费）。
// 此前只响应外部 mousedown 关闭，不在链内——开着菜单按 Esc 会击穿去清勾选/搜索
function closeCtxIfOpen() {
  if (ctx.value) {
    ctx.value = null
    return true
  }
  return false
}
defineExpose({ closeCtxIfOpen })
onMounted(() => window.addEventListener('mousedown', onGlobalDownCloseCtx))
onBeforeUnmount(() => window.removeEventListener('mousedown', onGlobalDownCloseCtx))

function ctxCreate() {
  const c = ctx.value
  closeCtx()
  if (c) folderOps.createFolder(c.dsName, c.parentPath)
}
function ctxRename() {
  const c = ctx.value
  closeCtx()
  if (c?.folderPath) folderOps.renameFolder(c.dsName, c.folderPath)
}
function ctxDelete() {
  const c = ctx.value
  closeCtx()
  if (c?.folderPath) folderOps.deleteFolder(c.dsName, c.folderPath)
}

// ---- 展示辅助
const dotClass = (status) => (status === 'connected' ? 'ok' : status === 'reconnecting' ? 'bad' : 'idle')
// H31：树根状态点悬停 title 走 i18n（复用底部状态栏三词条，含数据源名）——此前
// connected/disconnected 直出英文裸枚举（重连分支倒是配了翻译），同一颗点两套口径
const dsDotTitle = (ds) => {
  if (ds.status === 'connected') return t('statusbar.dsConnected', { name: ds.name })
  if (ds.status === 'reconnecting')
    return t('statusbar.dsReconnecting', { name: ds.name }) + (ds.reconnectInfo ? ' · ' + ds.reconnectInfo : '')
  return t('statusbar.dsDisconnected', { name: ds.name })
}

// 置顶标签在前，组内保持 API 顺序（稳定排序；重命名/删除等管理操作走标签管理模态）
const sortedTags = computed(() => tags.value.slice().sort((a, b) => Number(b.isPinned) - Number(a.isPinned)))

// 超长标签名显示截断阈值（字符数，阈值可调）：超过截断加 …；title 恒为全名。
// JS 截断之外 CSS ellipsis 再兜一层视觉宽度（侧栏窄于 50 字符，截断后仍可能放不下）
const TAG_MAX_LEN = 50
const tagName = (name) => (name.length > TAG_MAX_LEN ? name.slice(0, TAG_MAX_LEN) + '…' : name)
</script>

<template>
  <div ref="rootEl" class="side-tree">
    <div class="tree-scroll">
      <!-- H32：真无数据源（rows 恒含「全部数据」虚拟根或源根，原 rows.length 判空是
           永不触发的死代码）——给去设置的引导，而不是一行无人认领的「（无数据源）」 -->
      <div v-if="!datasources.length" class="empty-hint">{{ t('tree.noDsHint') }}</div>
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
        @contextmenu="onRowContext(row, $event)"
        @dragstart="onRowDragStart(row, $event)"
        @dragend="onRowDragEnd"
        @dragover="onRowDragOver(row, $event)"
        @dragleave="onRowDragLeave(row)"
        @drop="onRowDrop(row, $event)"
      >
        <!-- 全部数据源 / 根 / 文件夹：展开箭头 -->
        <span
          class="chevron"
          :class="{ open: row.kind === 'all' ? allOpen : isExpanded(row.key) }"
          @click.stop="row.kind === 'all' ? (allOpen = !allOpen) : onToggle(row.key)"
          >▸</span
        >

        <!-- 全部数据虚拟根：常驻顶部，点击=清除数据源过滤 -->
        <template v-if="row.kind === 'all'">
          <span class="ds-icon">🗂</span>
          <span class="label">{{ t('crumb.allDataSources') }}</span>
          <span class="count">{{ row.count }}</span>
        </template>

        <!-- 数据源根：🗄 名称 · 类型 + 状态点 + 计数（状态点 title 见 dsDotTitle，H31） -->
        <template v-else-if="row.kind === 'root'">
          <span class="ds-icon">🗄</span>
          <span class="label" :title="row.ds.name">{{ row.ds.name }}</span>
          <span class="ds-type">{{ row.ds.type }}</span>
          <span class="dot" :class="dotClass(row.ds.status)" :title="dsDotTitle(row.ds)"></span>
          <span class="count">{{ row.count }}</span>
        </template>

        <!-- 文件夹：📁 名称 + 子树服务器计数（含子文件夹，与列表/面包屑同口径；虚拟文件夹可为 0） -->
        <template v-else>
          <span class="folder-icon">📁</span>
          <span class="label" :title="row.folder.path">{{ row.folder.name }}</span>
          <span class="count">{{ row.count }}</span>
        </template>
      </div>
    </div>

    <!-- 标签区：标题行恒定不随列表滚动（.tags 拆 head 固定 + .tag-list 独占滚动）；
         「+ 管理」入口在标题行右端（原为列表区末尾的 chip——混在标签里不显眼）；
         chips+计数，置顶在前；点击=过滤条件；超长名截断（title 含全名）。
         chip 计数来自 /api/tags 全库聚合（G8：点击过滤的是当前视图，两口径不同——
         title 注明「全库 {n} 台」，消除「chip 显示 5、界面说没有」的自相矛盾）。
         H32：无任何标签时整区隐藏（owner 2026-09-21 决策）——空态下只剩「标签/管理」
         两行孤字无信息量，且标签唯一创建入口在服务器编辑器，留着空白区反而暗示
         「这里该有什么东西」；有标签即恢复（管理入口随之回来） -->
    <div v-if="tags.length" class="tags">
      <div class="tags-head">
        <span>{{ t('tree.tags') }}</span>
        <button class="tags-manage" :title="t('tagm.title')" @click="emit('manage-tags')">
          {{ t('tree.manageTags') }}
        </button>
      </div>
      <div class="tag-list">
        <!-- 循环变量命名 tg：避免遮蔽 script setup 暴露的 i18n 翻译函数 t -->
        <button
          v-for="tg in sortedTags"
          :key="tg.name"
          class="tag-chip"
          :class="{ active: tg.name === tag }"
          :title="t('tree.tagChipTitle', { name: tg.name, n: tg.count })"
          @click="emit('update:tag', tg.name === tag ? '' : tg.name)"
        >
          <span v-if="tg.isPinned" class="pin">📌</span><span class="tag-name">{{ tagName(tg.name) }}</span
          ><span class="tag-count">{{ tg.count }}</span>
        </button>
      </div>
    </div>

    <button class="collapse-btn" :title="t('tree.collapseTitle')" @click="emit('update:collapsed', true)">
      « {{ t('tree.collapse') }}
    </button>

    <!-- 文件夹操作右键菜单（新建/重命名/删除；只读数据源禁用） -->
    <div v-if="ctx" class="tree-ctx" :style="{ left: ctx.x + 'px', top: ctx.y + 'px' }">
      <button class="ctx-item" :disabled="!ctxWritable || folderOps.busy.value" @click="ctxCreate">
        <span>{{ t('tree.newFolder') }}</span>
      </button>
      <template v-if="ctx.folderPath">
        <button class="ctx-item" :disabled="!ctxWritable || folderOps.busy.value" @click="ctxRename">
          <span>{{ t('tree.renameFolder') }}</span>
        </button>
        <button class="ctx-item" :disabled="!ctxWritable || folderOps.busy.value" @click="ctxDelete">
          <span>{{ t('tree.deleteFolder') }}</span>
        </button>
      </template>
    </div>
  </div>
</template>

<style scoped>
/* 全部取色走主题 CSS 变量，行高紧凑 24px（行内档），hover --bg-hover */
.side-tree {
  position: relative; /* 右键菜单浮层定位基准 */
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
  font-size: var(--fs-body);
  padding: 10px 8px;
}

.row {
  display: flex;
  align-items: center;
  gap: 4px;
  height: var(--ctrl-h-s); /* 行内紧凑档 */
  padding-right: 6px;
  border-radius: var(--radius-ctrl);
  white-space: nowrap;
  user-select: none;
}
.row:hover {
  background: var(--bg-hover);
}
.row.selected {
  background: var(--accent-container);
}
/* 拖拽：可拖行 grab；指示线/容器高亮用主题强调色，与选中底色区分。
   落点线/虚线轮廓为状态指示（G6）：走 --accent-focus（light 橙/绿亮 accent ×bg <3:1，
   深变体 4.96/5.25；dark 基该变量===--accent 观感不变） */
.row[draggable='true'] {
  cursor: grab;
}
.row.drop-before {
  box-shadow: inset 0 2px 0 var(--accent-focus);
}
.row.drop-after {
  box-shadow: inset 0 -2px 0 var(--accent-focus);
}
.row.drop-into {
  background: var(--accent-container);
  outline: 1px dashed var(--accent-focus);
  outline-offset: -1px;
}

.chevron {
  flex: 0 0 14px;
  color: var(--text-3);
  font-size: var(--fs-micro);
  line-height: 1;
  text-align: center;
  cursor: pointer;
  transition: transform var(--dur-fast) ease;
}
.chevron.open {
  transform: rotate(90deg);
}

.ds-icon,
.folder-icon {
  flex: 0 0 16px;
  font-size: var(--fs-body);
  text-align: center;
}
.label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: var(--fs-body);
  color: var(--text-1);
}
.ds-type {
  flex: 0 0 auto;
  color: var(--text-4);
  font-size: var(--fs-micro);
}

.dot {
  /* 数据源状态点 6px 紧凑档（G19）：与设置页数据源卡/底部状态栏同值同义；
     服务器级状态点 8px（StatusDot），按宿主行高一档区分 */
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
  font-size: var(--fs-caption);
}

/* 标签区容器：标题行 + 滚动区拆分——「标签」标题恒定可见，
   只有 .tag-list 滚动；max-height 兜底防超多标签挤压树区 */
.tags {
  flex-shrink: 0;
  max-height: 35%;
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--border);
  padding: 8px 10px 6px;
}
.tags-head {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between; /* 「+ 管理」贴标题行右端 */
  color: var(--text-4);
  font-size: var(--fs-caption);
  margin-bottom: 6px;
}
/* 标签管理入口：标题行右端的轻量文字按钮（不再混入 chips 列表） */
.tags-manage {
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1;
  padding: 2px 0;
  cursor: pointer;
}
.tags-manage:hover {
  color: var(--text-1);
  background: transparent;
}
.tag-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-content: flex-start; /* 高度受限时行簇顶对齐，不被 flex 行均分拉伸 */
}
.tag-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px; /* 归偶（G22） */
  max-width: 100%;
  height: var(--ctrl-h-s);
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1;
  padding: 0 8px;
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
  font-size: 0.6923rem;
}
/* 标签名：单行 + CSS ellipsis 兜底（配合 JS 截断 TAG_MAX_LEN，见 script 注释）；
   min-width:0 放行 flex 收缩，pin/计数不参与压缩 */
.tag-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.tag-count {
  flex: 0 0 auto;
  color: var(--text-4);
}

.collapse-btn {
  flex-shrink: 0;
  height: var(--ctrl-h-m);
  border: none;
  border-top: 1px solid var(--border);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  cursor: pointer;
}
.collapse-btn:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}

/* 文件夹操作右键菜单浮层（对齐 ServerTable 的 .ctx-menu 视觉） */
.tree-ctx {
  position: absolute;
  z-index: 30;
  display: flex;
  flex-direction: column;
  min-width: 130px;
  padding: 4px;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-box);
  background: var(--bg-elevated);
  box-shadow: var(--shadow-menu);
}
.ctx-item {
  /* 菜单项内距档 7px 10px 与 ServerTable .ctx-item / TableToolbar .col-item 同源（G16 锚点） */
  display: flex;
  align-items: center;
  gap: 18px;
  border: none;
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-2);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 7px 10px;
  cursor: pointer;
  text-align: left;
}
.ctx-item:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-1);
}
.ctx-item:disabled {
  color: var(--text-4);
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
</style>
