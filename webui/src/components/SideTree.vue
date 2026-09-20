<script setup>
// 边栏树 + 标签区：
// - 顶部常驻虚拟根「全部数据」（点击=清除数据源过滤，列表显示全部库服务器）
// - 数据源根（🗄 名称 · 类型 + 状态点）→ 递归文件夹树；不再渲染服务器叶（列表承担）
// - 虚拟文件夹：tree-state expansion 键即存在（空文件夹物化，与 WPF BuildView 一致）；
//   右键菜单 新建/重命名/删除（folderOps 统一实现，列表侧共用）
// - 节点右侧子服务器计数与列表同口径：数据源根/文件夹 = 直接子级服务器数
//   （与点击后的列表行数/面包屑「N 台」一致）；「全部数据」= 全库服务器总数
// - 树下方「标签」chips（置顶在前）；底部「« 收起边栏」emit update:collapsed
// - 展开/折叠与拖拽经 /api/ui-state/tree 持久化（防抖 500ms），与 WPF 共用 .tree_view.json；
//   字典状态收在 useTreeState 共享存储（列表文件夹行/新建文件夹也消费，侧栏收起不丢）
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../api'
import { progressToast } from '../utils/progressToast'
import { useServers } from '../composables/useServers'
import { buildTree, countDirectChildServers, fullKey, holderAt, isDescendantPath } from '../composables/folders'
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
  selection: { type: Object, default: null }, // { dataSourceName, folderPath } | null=全部数据（v-model:selection）
  tag: { type: String, default: '' }, // 当前标签过滤（v-model:tag，仅用于 chip 高亮）
})
const emit = defineEmits(['update:selection', 'update:tag', 'update:collapsed', 'manage-tags'])
const { t } = useI18n()
const message = useMessage()

const { servers, datasources, tags, reload } = useServers()
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
  // 列表行拖拽的 dragend 在源元素（列表行）上触发并冒泡到 window——树侧经全局监听兜底
  window.addEventListener('dragend', onListDragEndGlobal)
  // 成功拖放（如拖回列表侧文件夹行放下）也冒泡 drop 到 window：复位跨库标记，
  // 避免 dragend 兜底在成功操作后误报
  window.addEventListener('drop', resetCrossDsHover, true)
})
onBeforeUnmount(() => {
  clearTimeout(saveTimer)
  if (savePending) flushSave() // 卸载时立即落盘防抖未到的变更（fire-and-forget）
  window.removeEventListener('dragend', onListDragEndGlobal)
  window.removeEventListener('drop', resetCrossDsHover, true)
})

function onToggle(key) {
  toggleExpand(key)
  scheduleSave()
}

// 「全部数据」虚拟根的展开态：纯本地（WPF 无此节点，不落 tree-state 字典）
const allOpen = ref(true)

// ---- 可见行扁平化（免递归组件；depth 控缩进）：全部数据 → 数据源根 → 文件夹（无服务器叶）
const rows = computed(() => {
  // 「全部数据」= 全库服务器总数（与该视图列表同口径；直接用 servers 长度——buildTree
  // 会丢弃数据源快照错配的孤儿服务器，树内求和会把它们漏计）
  const out = [{ kind: 'all', key: 'all', depth: 0, count: servers.value.length }]
  if (!allOpen.value) return out
  const pushLevel = (holder, dsName, depth) => {
    for (const f of levelChildren(holder)) {
      const key = fullKey(dsName, f.path)
      out.push({ kind: 'folder', key, folder: f, dsName, depth, count: countDirectChildServers(f) })
      if (isExpanded(key)) pushLevel(f, dsName, depth + 1)
    }
  }
  for (const root of tree.value) {
    out.push({ kind: 'root', key: root.name, ds: root, depth: 1, count: countDirectChildServers(root) })
    if (isExpanded(root.name)) pushLevel(root, root.name, 2)
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

const dsWritable = (dsName) => datasources.value.find((d) => d.name === dsName)?.writable !== false
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
// 跨库悬停标记移 tableBus 共享（记拖拽种类供 dragend 选文案）：dropEffect='none' 时按
// HTML 规范 drop 事件不会触发，跨库提示改在源侧 dragend 出——dragover 跨库分支记种类
//（树/列表两侧落区都可置位，见 ServerTable.onFolderDragOver），dragend 检查后 toast+复位；
// 回到合法目标或成功 drop 即清标记（不误报）
// 列表行拖拽的 dragend 兜底（树节点上没有列表拖拽的 dragend 事件源）：跨库悬停过则在
// 拖拽结束时提示（drop 在 dropEffect=none 下不触发，见上），按拖拽种类出对应文案
function onListDragEndGlobal() {
  if (crossDsHover.value) {
    const kind = crossDsHover.value
    crossDsHover.value = CROSS_DS_NONE
    message.warning(kind === CROSS_DS_FOLDER ? t('toast.crossDsFolderMove') : t('toast.crossDsMove'))
  }
}
function resetCrossDsHover() {
  crossDsHover.value = CROSS_DS_NONE
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

// 落点执行：算受影响服务器集合的新 TreeNodes → 逐台 GET config → 改 TreeNodes → PUT
//（UpdateServer 不触发 SSE——由前端显式 reload() 刷新）。同级顺序：before/after 重排
// 目标层兄弟文件夹序号（1 起）；into 删除被拖文件夹的序号键（排末尾，与 WPF AddChild 后语义一致）。
// 大子树逐台串行耗时——与批量删除/folderOps 同款 loading 进度 toast（原地更新+终态转换）
async function applyTreeMove(src, row, zone) {
  const parentPath = targetParentPath(row, zone)
  const prefix = src.folder.path + '/'
  const affected = servers.value
    .filter((s) => s.dataSourceName === src.dsName && (s.folderPath ? s.folderPath + '/' : '').startsWith(prefix))
    .map((s) => ({ server: s, rest: s.folderPath.slice(prefix.length).split('/').filter(Boolean) }))

  let moved = 0
  const failed = []
  // 空子树（0 台受影响，纯顺序调整）不弹「0/0」进度，终态直接常规 toast
  const toast = progressToast(message, affected.length, (done) =>
    t('toast.treeWorking', { ok: done, n: affected.length })
  )
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
      toast.step(moved + failed.length)
    }

    // 同级顺序写回（仅 before/after 重排；into 清键排末尾）
    let orderChanged = false
    if (zone === 'before' || zone === 'after') {
      const holder = holderAt(tree.value, src.dsName, parentPath.join('/'))
      if (holder) {
        const siblings = levelChildren(holder).filter((f) => f.path !== src.folder.path)
        const idx = siblings.findIndex((f) => f.path === row.folder.path)
        if (idx >= 0) {
          siblings.splice(zone === 'before' ? idx : idx + 1, 0, src.folder)
          const next = { ...orderMap.value }
          siblings.forEach((f, i) => {
            next[FOLDER_ID + f.name] = i + 1
          })
          orderMap.value = next
          orderChanged = true
        }
      }
    } else {
      const id = FOLDER_ID + src.folder.name
      if (orderMap.value[id] != null) {
        const next = { ...orderMap.value }
        delete next[id]
        orderMap.value = next
        orderChanged = true
      }
    }

    if (moved === 0 && failed.length === 0 && !orderChanged) {
      toast.cancel() // 完全无变化（原位放下）：撤下进度，无终态文案
      return
    }
    if (orderChanged) await flushSave()
    await reload() // UpdateServer 路径不触发 SSE（见上），显式刷新列表/树
    if (failed.length) toast.finish('error', t('toast.treeMoveFailed', { n: failed.length }))
    else toast.finish('success', t('toast.treeMoved', { n: moved }))
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

        <!-- 数据源根：🗄 名称 · 类型 + 状态点 + 计数 -->
        <template v-else-if="row.kind === 'root'">
          <span class="ds-icon">🗄</span>
          <span class="label" :title="row.ds.name">{{ row.ds.name }}</span>
          <span class="ds-type">{{ row.ds.type }}</span>
          <span
            class="dot"
            :class="dotClass(row.ds.status)"
            :title="row.ds.status === 'reconnecting' ? row.ds.reconnectInfo || t('tree.reconnecting') : row.ds.status"
          ></span>
          <span class="count">{{ row.count }}</span>
        </template>

        <!-- 文件夹：📁 名称 + 直接子级服务器计数（与列表同口径；虚拟文件夹可为 0） -->
        <template v-else>
          <span class="folder-icon">📁</span>
          <span class="label" :title="row.folder.path">{{ row.folder.name }}</span>
          <span class="count">{{ row.count }}</span>
        </template>
      </div>
    </div>

    <!-- 标签区：标题行恒定不随列表滚动（.tags 拆 head 固定 + .tag-list 独占滚动）；
         「+ 管理」入口在标题行右端（原为列表区末尾的 chip——混在标签里不显眼）；
         chips+计数，置顶在前；点击=过滤条件；超长名截断（title 恒为全名） -->
    <div class="tags">
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
          :title="tg.name"
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
/* 拖拽：可拖行 grab；指示线/容器高亮用主题强调色，与选中底色区分 */
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
  gap: 3px;
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
