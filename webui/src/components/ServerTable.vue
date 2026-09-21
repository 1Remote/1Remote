<script setup>
// 行列表：div+flex 自写轻量表格——列配置以 CSS 变量（--c-*）在根节点下发（colVars），
// 表头与 ServerRow / FolderRow 行严格对齐。
// - 过滤：搜索/标签过滤已由 ServerListView（applyServerFilters）收窄后经 servers prop 传入，
//   此处仅剩树选中过滤，两层交集自然复合；搜索激活时树过滤整体让位（搜索本就是全库递归语义）
// - 视图语义：「全部数据」虚拟根 = 跨库递归总览且不显示文件夹行（来源上下文由行内 folder 列
//   承担）；数据源根/文件夹内列出其下全部服务器（含子文件夹，H38 按 spec §3.2），子文件夹
//   另以文件夹行呈现作导航捷径
// - 排序：名称/地址（自然 IP）/协议/最近连接，点表头 升→降→无 三态循环（第三态清排序
//   恢复默认树序）；localStorage '1r-sort' 持久化（清除态落 {key:''}）
// - 多选：勾选只由 复选框点击 / Ctrl+点击 / Shift+点击 / Ctrl+A / 文件夹与表头复选框
//   触发——裸点击行仅设光标（详见 composables/useRowChecks.js 文件头）；表头三态全选
//   （=当前视图可见服务器行，不含文件夹的隐藏子孙——要含子孙勾文件夹行复选框）；
//   勾选集合始终是服务器 id 集，「已选 N 台」与批量编辑/导出/删除自然作用于全集。
//   勾选/文件夹勾选/剔除的完整语义与口径见 composables/useRowChecks.js 文件头
// - 键盘：↑↓ 移动光标行（sorted 可见列表内）、Ctrl+A 全选可见（已全选时再按 = 清空，
//   与表头三态复选框同款对称）、Menu/Shift+F10 呼出右键菜单、E 编辑 / Ctrl+D 复制
//   （目标行 = 恰好单选该台，否则光标行）；Enter 连接 / Del 删除在多选（>1）时整体
//   禁用（owner 2026-09-21 决策：批量语境下目标歧义无法自解释，误触代价高——批量
//   操作走批量条按钮；Ctrl+D 在多选（>1）时忽略——批量复制无对应后端动作，防误触）；
//   Esc 不在此处理——全局 Esc 链（菜单→列菜单→勾选→搜索→标签→光标）由 ServerListView
//   统一调度（见其 onGlobalEsc）
// - 批量条 + ≡ 自定义顺序 / ▦ 列菜单工具簇：TableToolbar 组件承载，Teleport 至面包屑行右侧
//   （#crumb-actions）；勾选/排序模式/列状态仍归本组件，不上提
// - 行拖拽：重排仅自定义顺序模式生效（上/下半行 = 插到目标前/后）；任意模式拖到
//   「文件夹行」= 移入该文件夹；文件夹行自身可拖（可写源）= 整个子树移动——拖到
//   别的文件夹行/「..」上级行 = 移入或上移一级，文件夹行不支持拖拽重排序
//   （hover 服务器行一次性 toast 说明，重排序归树内拖拽）
// - 右键菜单：连接/编辑/复制/复制地址/复制用户名/复制密码(H4，后端验证门+前端写剪贴板)/
//   删除可用，其余占位禁用（title 提示）；服务器行与文件夹/空白两类菜单（menu/nfMenu）
//   均点击外部关闭，Esc 经 ServerListView 全局 Esc 链关闭（closeMenuIfOpen/closeNfMenuIfOpen
//   两级，链序见 ServerListView）
// - 空态：默认居中提示按「传入列表空=空库 / 非空但过滤后无行=无匹配」二分；
//   具名插槽 empty 供父级覆写（ServerListView 的引导卡片/无匹配态在表格外层接管）
import { computed, onBeforeUnmount, onMounted, ref, watch, watchEffect } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { useVirtualList } from '@vueuse/core'
import ServerRow from './ServerRow.vue'
import FolderRow from './FolderRow.vue'
import TableToolbar from './TableToolbar.vue'
import { api } from '../api'
import { useColumns } from '../composables/useColumns'
import { useRowChecks } from '../composables/useRowChecks'
import { useServers } from '../composables/useServers'
import {
  CROSS_DS_FOLDER,
  CROSS_DS_NONE,
  CROSS_DS_SERVER,
  crossDsHover,
  focusHandoff,
  listDragServer,
  listDragFolder,
} from '../composables/tableBus'
import { fullKey, isDescendantPath } from '../composables/folders'
import { naturalIpCompare } from '../utils/compare'

const props = defineProps({
  servers: { type: Array, default: () => [] },
  selection: { type: Object, default: null }, // { dataSourceName, folderPath } | null=全部数据源
  query: { type: String, default: '' }, // 搜索过滤词：透传给行做命中高亮
  folders: { type: Array, default: () => [] }, // 当前层级文件夹行：{name, path, dsName, count}
  opsBusy: { type: Boolean, default: false }, // 父级 folderOps 长操作进行中（nfMenu 禁用，与树右键同口径）
})
const emit = defineEmits([
  'connect',
  'batch-connect',
  'batch-delete',
  'bulk-edit',
  'export',
  'edit',
  'duplicate',
  'delete',
  'counted',
  'open-folder',
  'create-folder',
  'rename-folder',
  'delete-folder',
  'move-to-folder',
  'move-folder',
  'new-server',
  'import-servers',
])
const { t } = useI18n()
const message = useMessage()
const { datasources, searchedIds } = useServers() // 文件夹右键菜单只读判定 + 搜索激活判定（共享模块单例，无额外请求）

// ---- 过滤 ----
const filtered = computed(() => {
  // 搜索激活：列表已由 searchedIds 收窄，且搜索本就是全库递归语义（后端跨数据源/子文件夹
  // 匹配）——树选中过滤整体让位，子文件夹深处的命中一律可见
  if (searchedIds.value != null) return props.servers
  const sel = props.selection
  if (!sel || !sel.dataSourceName) return props.servers // 「全部数据」虚拟根 = 跨库总览（递归，无文件夹行）
  // H38（spec §3.2「选中文件夹节点：列表显示该文件夹下（含子文件夹）全部服务器」）：
  // 前缀匹配含全部子孙服务器，不再只列直接子级；子文件夹仍以文件夹行呈现导航入口
  //（资源管理器式的浏览捷径保留），文件夹行复选框级联勾选子孙的语义与列表内容对齐。
  // folderPath 两侧归一化（根选中/根级服务器的 folderPath 可能是 '' 或 undefined）
  const target = sel.folderPath || ''
  const prefix = target ? target + '/' : ''
  return props.servers.filter((s) => s.dataSourceName === sel.dataSourceName && (s.folderPath || '').startsWith(prefix))
})
// H9：搜索激活时强制显示文件夹列——搜索是全库递归语义（后端跨数据源/跨文件夹匹配），
// 子文件夹视图内搜索的结果可能来自任意位置，文件夹列（数据源/路径）是唯一来处标注，
// 隐藏它会让用户把全库命中误读成本文件夹命中（面包屑「N 台」仍在撒谎的另一半由
// ServerListView 的搜索范围提示补足）
const showFolder = computed(() => searchedIds.value != null || !props.selection || !props.selection.folderPath) // 仅根视图显示文件夹列（搜索态强制显示，见上）；列菜单在非根视图禁用该项（H9）
const showDs = computed(() => !props.selection || !props.selection.dataSourceName) // 「全部数据」根：文件夹列前缀数据源名

// ---- 排序 ----
const SORTABLE = ['displayName', 'address', 'protocol', 'lastConnectTime']
const comparators = {
  displayName: (a, b) => a.displayName.localeCompare(b.displayName, undefined, { numeric: true }),
  address: (a, b) => naturalIpCompare(a.address, b.address),
  protocol: (a, b) => (a.protocol || '').localeCompare(b.protocol || ''),
  lastConnectTime: (a, b) => (a.lastConnectTime || 0) - (b.lastConnectTime || 0),
}
function readSort() {
  try {
    const s = JSON.parse(localStorage.getItem('1r-sort') || 'null')
    if (s && (!s.key || s.key === 'custom' || SORTABLE.includes(s.key)))
      return { key: s.key || '', dir: s.dir === -1 ? -1 : 1 }
  } catch {
    /* 损坏数据当未排序 */
  }
  return { key: '', dir: 1 }
}
const sort = ref(readSort())
function toggleSort(key) {
  // 三态循环：升 → 降 → 无（第三态清排序恢复默认树序，随清除一并持久化）
  if (sort.value.key !== key) sort.value = { key, dir: 1 }
  else if (sort.value.dir > 0) sort.value = { key, dir: -1 }
  else sort.value = { key: '', dir: 1 }
  try {
    localStorage.setItem('1r-sort', JSON.stringify(sort.value))
  } catch {
    /* 隐私模式等写入失败可忽略 */
  }
}
const arrow = (k) => (sort.value.key === k ? (sort.value.dir > 0 ? '▲' : '▼') : '↕')

// ---- 自定义顺序：sort.key==='custom'（工具条 ≡ 切换）时行可拖拽重排，顺序 =
// /api/ui-state/list-order 的 ServerCustomOrder（与 WPF 列表视图共用同一字典；
// 未知 id 排末尾、稳定保持自然序——WPF 默认 0 排最前，记录偏差）----
const isCustom = computed(() => sort.value.key === 'custom')
const customOrder = ref(null) // Map<serverId, int> | null（null=尚未加载）
const customKey = (id) => (customOrder.value ? customOrder.value.get(id) : undefined)
async function loadCustomOrder() {
  try {
    const st = await api.getListOrder()
    customOrder.value = new Map(Object.entries(st?.order || {}).map(([k, v]) => [k, Number(v)]))
  } catch {
    customOrder.value = new Map() // 后端不可达：自然顺序兜底（保存会在 drop 时报错 toast）
  }
}
watch(
  isCustom,
  (on) => {
    if (on) loadCustomOrder()
  },
  { immediate: true }
)
function toggleCustomSort() {
  sort.value = isCustom.value ? { key: '', dir: 1 } : { key: 'custom', dir: 1 }
  try {
    localStorage.setItem('1r-sort', JSON.stringify(sort.value))
  } catch {
    /* 写入失败可忽略 */
  }
}
// custom 比较器挂在 comparators 上（sorted 统一经 comparators[key] 分发）
comparators.custom = (a, b) => (customKey(a.id) ?? Infinity) - (customKey(b.id) ?? Infinity)
// 整库自定义序（拖拽重排的基准列表：树选中/标签/搜索过滤只是它的子序列，
// POST 全量整库顺序与 WPF ServerCustomOrderSave 存完整列表同语义）
const fullOrder = computed(() => {
  if (!customOrder.value) return props.servers.slice()
  return props.servers.slice().sort(comparators.custom)
})

// ---- 行拖拽：两条互斥链路，以被拖行种类分发（快照互斥，同时至多一条在拖）----
// 服务器行：行恒可拖（dragstart 始终记录快照），custom 模式内上/下半行 = 插到目标前/后，
// 对齐 WPF 列表 Drop 的 height/2 判定；drop 后 POST 整库新顺序 → 以响应重建 Map → sorted 即时重排；
// 非 custom 模式下重排不被接受（dragover 不 preventDefault → drop 被浏览器拒绝，
// 悬停到别的行时 toast 一次性说明原因——每次拖拽只提示一次）；
// 任意模式拖到「文件夹行」= 移入该文件夹（onFolderDragOver/Drop，跨数据源拒绝）。
// 文件夹行（可写源可拖）：拖 = 整个子树移动（folderOps.moveFolder），目标 =
// 别的文件夹行 /「..」上级行 / 树节点（SideTree 落区，见 tableBus.listDragFolder）；
// 不支持拖拽重排序（任何模式）——hover 服务器行时一次性 toast 说明（每次拖拽只提示一次）
const dragId = ref(null)
const dragServer = ref(null) // 被拖服务器快照（drop 时列表可能已变）
const dragFolder = ref(null) // 被拖文件夹行快照 { dsName, path, name }（同上）
const dropHint = ref(null) // { id, before }
const dropFolder = ref(null) // { dsName, path } 悬停中的文件夹行
let reorderHintShown = false // 非 custom 重排提示的本次拖拽去重（dragstart 复位）
let folderReorderHintShown = false // 文件夹重排序不支持提示的本次拖拽去重（dragstart 复位）
function onRowDragStart(server, e) {
  dragId.value = server.id
  dragServer.value = server
  listDragServer.value = server // SideTree 落区判定用（dragover 期读不到 dataTransfer 数据，见 tableBus）
  reorderHintShown = false
  e.dataTransfer.effectAllowed = 'move'
  e.dataTransfer.setData('text/plain', server.id) // Firefox 需要非空 data
  // 与树内拖拽（application/x-1r-tree-node）以类型区分——SideTree drop 分发依据
  e.dataTransfer.setData('application/x-1r-server-row', server.id)
}
function onRowDragEnd() {
  dragId.value = null
  dragServer.value = null
  dropHint.value = null
  dropFolder.value = null
  listDragServer.value = null
}
// 跨库拖拽悬停标记的 dragend 兜底（H18 自 SideTree 迁入）：dropEffect='none' 时按
// HTML 规范 drop 事件不会触发，跨库拒绝的提示改在源侧 dragend 出——dragover 期间
// 落区侧（树/列表两处 onFolderDragOver 与本组件 onRowDragOver）记种类
//（tableBus.crossDsHover），dragend 检查后 toast+复位；回到合法目标或成功 drop 即清
// 标记（不误报）。原实现挂在 SideTree 的 window 监听上，边栏收起（SideTree 卸载）时
// 监听随之消失——窄窗（<900px）下整条反馈链断裂（第四轮 H18）。列表行拖拽的源是
// 本组件的行，本组件挂载期间监听恒在（表格隐藏时无可拖行），迁到此处后窄窗不再失效。
function onListDragEndGlobal() {
  if (crossDsHover.value) {
    const kind = crossDsHover.value
    crossDsHover.value = CROSS_DS_NONE
    message.warning(kind === CROSS_DS_FOLDER ? t('toast.crossDsFolderMove') : t('toast.crossDsMove'))
  }
}
// 成功拖放（如拖到树节点/列表文件夹行放下）也冒泡 drop 到 window：复位跨库标记，
// 避免 dragend 兜底在成功操作后误报（capture 阶段挂——与 dragend 同生命周期）
function resetCrossDsHover() {
  crossDsHover.value = CROSS_DS_NONE
}
// 文件夹行拖拽源（draggable 由模板绑定 dsWritable）：快照挂本地 + tableBus
//（SideTree 落区判定），dataTransfer 类型 x-1r-list-folder 与另两条链路区分
function onFolderRowDragStart(f, e) {
  dragFolder.value = f
  listDragFolder.value = f
  folderReorderHintShown = false
  e.dataTransfer.effectAllowed = 'move'
  e.dataTransfer.setData('text/plain', f.path) // Firefox 需要非空 data
  e.dataTransfer.setData('application/x-1r-list-folder', fullKey(f.dsName, f.path))
}
function onFolderRowDragEnd() {
  dragFolder.value = null
  dropFolder.value = null
  listDragFolder.value = null
}
function onRowDragOver(server, e) {
  // 文件夹拖拽不落在服务器行（不支持行间重排序，任何模式）：不 preventDefault →
  // 浏览器拒收，toast 只提示一次/拖拽（对齐非 custom 服务器重排提示的先例）
  if (dragFolder.value) {
    // H27：跨库优先于一切提示——先弹「需要自定义排序」会诱导用户去开排序模式，
    // 开了也没用（真正原因是跨库不允许），归因错误的提示比没有提示更糟
    if (dragFolder.value.dsName !== server.dataSourceName) {
      crossDsHover.value = CROSS_DS_FOLDER
      return
    }
    if (!folderReorderHintShown) {
      folderReorderHintShown = true
      message.info(t('toast.folderNoReorder'))
    }
    return
  }
  if (!dragId.value || dragId.value === server.id) return
  // H27：同款——跨库比对先于重排模式提示（此前悬停对方库的服务器行会先弹
  // 「需要自定义排序」，归因错误）；dragend 兜底出正确文案（onListDragEndGlobal）
  if (dragServer.value && dragServer.value.dataSourceName !== server.dataSourceName) {
    crossDsHover.value = CROSS_DS_SERVER
    return
  }
  crossDsHover.value = CROSS_DS_NONE // 回到合法目标即清跨库标记（不误报 dragend）
  if (!isCustom.value) {
    // 非 custom 模式：不 preventDefault（重排 drop 被浏览器拒绝），toast 只提示一次/拖拽
    if (!reorderHintShown) {
      reorderHintShown = true
      message.info(t('toast.reorderNeedCustom'))
    }
    return
  }
  e.preventDefault()
  e.dataTransfer.dropEffect = 'move'
  const r = e.currentTarget.getBoundingClientRect()
  dropHint.value = { id: server.id, before: e.clientY - r.top < r.height / 2 }
}
async function onRowDrop(server, e) {
  const hint = dropHint.value
  dropHint.value = null
  if (!isCustom.value || !dragId.value || !hint || hint.id !== server.id || dragId.value === server.id) return
  e.preventDefault()
  const dragged = dragId.value
  dragId.value = null
  const ids = fullOrder.value.map((s) => s.id)
  const from = ids.indexOf(dragged)
  if (from >= 0) ids.splice(from, 1)
  const idx = ids.indexOf(server.id)
  if (idx < 0) return
  ids.splice(hint.before ? idx : idx + 1, 0, dragged)
  try {
    const saved = await api.saveListOrder(ids)
    const map = new Map()
    ;(saved?.ids || ids).forEach((id, i) => map.set(id, i))
    customOrder.value = map
  } catch (err) {
    console.warn('[ServerTable] saveListOrder failed:', err?.message || err)
    message.error(t('toast.reorderFailed'))
  }
}

// 文件夹行落区（含「..」上级行，同为 { dsName, path } 形状）：
// 服务器拖拽 = 同数据源且目标 ≠ 当前所在文件夹；文件夹拖拽 = 同数据源且目标不在
// 自身子树内（isDescendantPath 含相等 → 拖到自己身上同拒）。跨源/非法 = 不
// preventDefault → 浏览器拒绝 drop；目标父层同名文件夹的查重在 drop 执行层
//（folderOps.moveFolder——dragover 逐次 buildTree 查重不划算，罕见场景接受高亮后被拦）
function folderDropOk(f) {
  const s = dragServer.value
  if (s) return s.dataSourceName === f.dsName && (s.folderPath || '') !== f.path
  const d = dragFolder.value
  return !!d && d.dsName === f.dsName && !isDescendantPath(d.path, f.path)
}
function onFolderDragOver(f, e) {
  if (!dragServer.value && !dragFolder.value) return
  if (!folderDropOk(f)) {
    dropFolder.value = null
    // 跨库拒绝与树侧同款置位（tableBus.crossDsHover）：dropEffect=none 下 drop 不触发，
    // 提示改由 dragend 兜底出（SideTree 统一监听）——此前列表侧静默拒绝、树侧有 toast，
    // 同一操作两套反馈。同库原地/自身子树拒绝仍静默（与树侧 subtree 分支同口径）
    if (dragServer.value && dragServer.value.dataSourceName !== f.dsName) crossDsHover.value = CROSS_DS_SERVER
    else if (dragFolder.value && dragFolder.value.dsName !== f.dsName) crossDsHover.value = CROSS_DS_FOLDER
    return
  }
  e.preventDefault()
  e.dataTransfer.dropEffect = 'move'
  dropFolder.value = { dsName: f.dsName, path: f.path }
  crossDsHover.value = CROSS_DS_NONE // 回到合法目标即清标记（dragend 不误报，树侧同款）
}
function onFolderDragLeave(f) {
  if (dropFolder.value?.path === f.path && dropFolder.value?.dsName === f.dsName) dropFolder.value = null
}
function onFolderDrop(f, e) {
  const hint = dropFolder.value
  dropFolder.value = null
  if (!hint || hint.path !== f.path || hint.dsName !== f.dsName || !folderDropOk(f)) return
  e.preventDefault()
  const s = dragServer.value
  const d = dragFolder.value
  dragId.value = null
  dragServer.value = null
  dragFolder.value = null
  if (s) emit('move-to-folder', { server: s, dsName: f.dsName, path: f.path })
  else if (d) emit('move-folder', { folder: d, dsName: f.dsName, path: f.path })
}

// ---- 文件夹右键菜单（nf-menu）：
// - 文件夹行右键 = 新建子文件夹 / 重命名 / 删除——与 SideTree 树右键同一菜单集
//   （列表/树两侧统一；对齐 WPF 树右键能力，动作经 emit 由 ServerListView 的
//   folderOps 执行）；
// - 空白处右键 = 新建服务器 / 新建文件夹 / 导入服务器（新建服务器与导入不在
//   文件夹行菜单出现——那两项目标是"当前视图/选中文件夹"，行右键语境是"这个文件夹"，
//   目标文件夹 ≠ 选中文件夹时语义会漂移，只在空白菜单提供）。
//   新建服务器/导入交父级（openCreate 带当前 selection 的 initialFolder / 打开导入模态）；
//   全部数据源根无确定数据源 → 新建文件夹禁用并提示先选数据源----
const nfMenu = ref(null) // { x, y, target: { dsName, parentPath, folderPath? } | null } —— folderPath 有值=来自文件夹行
const dsWritable = (dsName) => datasources.value.find((d) => d.name === dsName)?.writable !== false
// 禁用判据并入 opsBusy（第三轮 G15：动作执行在父级 folderOps，busy 期间点击只被静默
// return 吞掉——列表侧与树右键菜单同口径禁用，两侧行为一致）
const nfMenuOk = computed(() => !!nfMenu.value?.target && dsWritable(nfMenu.value.target.dsName) && !props.opsBusy)
const nfMenuTip = computed(() => {
  if (!nfMenu.value?.target) return t('tree.selectDsFirst')
  return dsWritable(nfMenu.value.target.dsName) ? '' : t('cv.readOnly')
})
function openNfMenu(target, e) {
  const r = rootEl.value?.getBoundingClientRect()
  const px = r ? e.clientX - r.left : e.clientX
  const py = r ? e.clientY - r.top : e.clientY
  nfMenu.value = { x: r ? Math.max(0, Math.min(px, r.width - 200)) : px, y: r ? Math.max(0, py) : py, target }
}
function onBlankContext(e) {
  if (e.target.closest?.('.row') || e.target.closest?.('.thead')) return // 行/表头自带处理
  e.preventDefault()
  const sel = props.selection
  openNfMenu(sel?.dataSourceName ? { dsName: sel.dataSourceName, parentPath: sel.folderPath || '' } : null, e)
}
// FolderRow context emit：{ folder, x, y }（clientX/Y 已在子组件取好，此处适配 openNfMenu 的事件形状）；
// parentPath=文件夹自身路径（子文件夹建在其内），folderPath 供 重命名/删除 定位目标
function onFolderContext({ folder, x, y }) {
  openNfMenu({ dsName: folder.dsName, parentPath: folder.path, folderPath: folder.path }, { clientX: x, clientY: y })
}
function nfCreate() {
  const m = nfMenu.value
  nfMenu.value = null
  if (m?.target) emit('create-folder', m.target)
}
// 空白菜单新增两项：动作归 ServerListView（新建走 openCreate——带当前选中文件夹；
// 导入开模态——默认数据源/文件夹同样取当前选中）
function nfNewServer() {
  nfMenu.value = null
  emit('new-server')
}
function nfImport() {
  nfMenu.value = null
  emit('import-servers')
}
function nfRename() {
  const m = nfMenu.value
  nfMenu.value = null
  if (m?.target?.folderPath) emit('rename-folder', m.target)
}
function nfDelete() {
  const m = nfMenu.value
  nfMenu.value = null
  if (m?.target?.folderPath) emit('delete-folder', m.target)
}
// FolderRow 行内 ✎/✕ 按钮：与右键菜单 重命名/删除 同一目标形状（dsName 定位数据源、
// folderPath 定位文件夹本身），经同一 emit 链路由 ServerListView 的 folderOps 执行
const folderTarget = (f) => ({ dsName: f.dsName, parentPath: f.path, folderPath: f.path })

// ---- 渲染序列 ----
const sorted = computed(() => {
  const c = comparators[sort.value.key]
  if (!c) return filtered.value // 未排序 = 保持树序
  return filtered.value.slice().sort((a, b) => c(a, b) * sort.value.dir)
})
// 文件夹行：当前层级文件夹排前、服务器其后。文件夹间排序跟随当前排序方向，比较只用名称
//（地址/协议/最近连接对文件夹无意义）；custom 顺序键 = 保持传入顺序（ServerListView 按树模型
// 给出）。空文件夹（count 0）照常显示。
const folderRows = computed(() => {
  const list = props.folders.slice()
  if (sort.value.key && sort.value.key !== 'custom') {
    list.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }) * sort.value.dir)
  }
  return list.map((f) => ({ kind: 'folder', folder: f }))
})
// 「..」上级行：文件夹视图（选中了数据源内的子文件夹）时列表最顶行——双击=导航上级、
// 拖服务器入内 = 移到上级文件夹（与文件夹行共用 onFolderDragOver/Drop，目标=父路径）。
// 不参与排序/勾选；右键仅抑制浏览器默认菜单（上级行只承担导航，无自有菜单动作）
const parentRow = computed(() => {
  const sel = props.selection
  if (!sel?.dataSourceName || !sel.folderPath) return null
  const parts = sel.folderPath.split('/')
  parts.pop()
  return { kind: 'parent', dsName: sel.dataSourceName, path: parts.join('/') }
})
// 统一渲染序列（虚拟滚动与直渲染共用）：srvIndex 保留服务器在 sorted 内的下标
//（Shift 范围选择/锚点语义仍基于纯服务器列表）
const renderRows = computed(() => [
  ...(parentRow.value ? [parentRow.value] : []),
  ...folderRows.value,
  ...sorted.value.map((s, i) => ({ kind: 'server', server: s, srvIndex: i })),
])
// 「当前视图无实质内容」= 除「..」上级行外无任何文件夹/服务器行——空态插槽的触发条件
//（子文件夹视图只有 ".." 行时 renderRows.length=1，按 length 判空会漏掉空文件夹文案）
const hasSubstance = computed(() => renderRows.value.some((r) => r.kind !== 'parent'))
const rowKey = (row) =>
  row.kind === 'parent'
    ? 'p:' + row.dsName + ':' + row.path
    : row.kind === 'folder'
      ? 'f:' + row.folder.dsName + ':' + row.folder.path
      : row.server.id

// ---- 多选：勾选集/表头三态全选/文件夹行勾选（含子孙）/数据变化剔除。
// servers 域=本组件收到的过滤后列表（tags/搜索/SSE 重载），与剔除 watch 同源——被过滤
// 掉的行不进勾选集，两端口径一致（完整语义见 useRowChecks.js 文件头）----
const {
  checked,
  allChecked,
  allCb,
  rowClickSelect,
  onToggleSelect,
  toggleAll,
  clearChecked,
  folderChecks,
  onFolderToggleCheck,
} = useRowChecks({ sorted, servers: () => props.servers, folders: () => props.folders })

function onRowClick(server, ev, idx) {
  cursorId.value = server.id // 点击行 = 光标落位（Enter 连接光标行，↑↓ 由此起算）
  rowClickSelect(ev, idx, server.id) // 勾选分支：Ctrl/Shift（裸点击不改勾选集，见 useRowChecks）
}
// 视图变化作废不可见的光标行（树切换/搜索过滤后旧行号已无意义；光标是纯视觉焦点，
// 只随可见列表存在。Shift 锚点的作废归 useRowChecks，见其文件头）
watch(sorted, (list) => {
  if (cursorId.value != null && !list.some((s) => s.id === cursorId.value)) cursorId.value = null
})
watchEffect(() => emit('counted', sorted.value.length)) // 供面包屑「· N 台」

// ---- 右键菜单（浮层；快捷键提示：Enter/E/Ctrl+D/Del 均已接线）----
// 标签/提示走 i18n（computed：语言切换即时刷新）；未接线项的占位提示统一「即将推出」。
const MENU = computed(() => [
  { key: 'connect', label: t('ctx.connect'), hint: 'Enter', on: true },
  { key: 'new-window', label: t('ctx.newWindow'), hint: t('common.comingSoon') },
  { key: 'other-credential', label: t('ctx.otherCredential'), hint: t('common.comingSoon') },
  { key: 'edit', label: t('ctx.edit'), hint: 'E', on: true },
  { key: 'duplicate', label: t('ctx.duplicate'), hint: 'Ctrl+D', on: true },
  { key: 'copy-address', label: t('ctx.copyAddress'), on: true },
  { key: 'copy-username', label: t('ctx.copyUsername'), on: true },
  // H4：复制密码——WPF 右键高频动作（ProtocolActionHelper.cs:126-142，带二次验证门）的
  // web 平价：后端过验证门（与凭据查看/导出共用的 30s 窗口）后回传明文，前端写剪贴板
  { key: 'copy-password', label: t('ctx.copyPassword'), on: true },
  { key: 'shortcut', label: t('ctx.shortcut'), tip: t('common.comingSoon') },
  { key: 'delete', label: t('ctx.delete'), hint: 'Del', on: true },
])
const menu = ref(null) // { server, x, y }（x/y 相对本容器左上角）
const rootEl = ref(null)
function openMenu({ server, x, y }) {
  cursorId.value = server.id // 菜单锚定行 = 光标落位（Enter/菜单「连接」语义一致）
  const r = rootEl.value?.getBoundingClientRect()
  const px = r ? x - r.left : x
  const py = r ? y - r.top : y
  menu.value = {
    server,
    x: r ? Math.max(0, Math.min(px, r.width - 238)) : px, // 防溢出内收（菜单约 230px 宽）
    y: r ? Math.max(0, Math.min(py, r.height - 336)) : py,
  }
}
function onMenuAction(item) {
  if (!item.on || !menu.value) return
  const s = menu.value.server
  menu.value = null
  if (item.key === 'connect') emit('connect', s.id)
  else if (item.key === 'edit') emit('edit', s)
  else if (item.key === 'duplicate') emit('duplicate', s)
  else if (item.key === 'delete') emit('delete', s)
  else if (item.key === 'copy-address') {
    // Serial 等无地址协议：提示而非把协议名当地址写进剪贴板
    if (!s.address) message.warning(t('toast.noAddressToCopy'))
    else copyText(s.address + (s.port ? ':' + s.port : ''), t('common.address'))
  } else if (item.key === 'copy-username') copyText(s.userName, t('common.username'))
  else if (item.key === 'copy-password') copyServerPassword(s)
}

// H4 复制密码：后端过二次验证门（未开启验证时直通；30s 窗口内复验免弹）后回传明文，
// 前端复用 copyText 写剪贴板（WebView2/localhost 安全上下文 = 桌面剪贴板，与 WPF
// 服务端写剪贴板等效，且免去 Kestrel MTA 线程上调 System.Windows.Clipboard 的 STA
// 处理）。403=验证取消/未通过；Serial/Telnet 等无密码协议回传空 → 明确提示
async function copyServerPassword(s) {
  try {
    const resp = await api.copyPassword(s.id, s.dataSourceName)
    const pwd = resp?.password || ''
    if (!pwd) {
      message.warning(t('toast.noPasswordToCopy'))
      return
    }
    copyText(pwd, t('common.password'))
  } catch (e) {
    if (e?.status === 403) {
      message.error(t('toast.copyPwdNeedVerify'))
      return
    }
    console.warn('[ServerTable] copy password failed:', s.id, e?.message || e)
    message.error(t('toast.copyFailed', { what: t('common.password') }))
  }
}
async function copyText(text, what) {
  if (!text) {
    message.warning(t('toast.nothingToCopy', { what }))
    return
  }
  try {
    await navigator.clipboard.writeText(text) // 安全上下文（localhost / WebView2）
    message.success(t('toast.copied', { what }))
  } catch {
    const ta = document.createElement('textarea') // 回退 execCommand（非安全上下文兜底）
    ta.value = text
    ta.style.cssText = 'position:fixed;opacity:0'
    document.body.appendChild(ta)
    ta.select()
    const ok = document.execCommand('copy')
    ta.remove()
    ok ? message.success(t('toast.copied', { what })) : message.error(t('toast.copyFailed', { what }))
  }
}
function onGlobalDown(e) {
  if (menu.value && !e.target.closest?.('.ctx-menu')) menu.value = null
  if (nfMenu.value && !e.target.closest?.('.nf-menu')) nfMenu.value = null
}

// ---- 键盘导航：↑↓ 光标行、Enter 连接光标行、Ctrl+A 全选可见、
// E 编辑 / Del 删除 / Ctrl+D 复制（keyTargetServer 取目标行）----
// 光标（cursorId）是纯视觉焦点（行外框），与勾选（checked）相互独立，仅在排序后的可见列表内移动。
// 表格焦点（tableFocused）：用户点过表格区域才算"焦点在表格"，避免抢走搜索框等处输入——
// document focusin 追踪：焦点落到表格外可交互元素 → false；落到 body（点击了边栏/滚动条等
// 不可聚焦区，浏览器把焦点滑到 body）不算离开，保持原状；配合根节点 mousedown 置 true 兜底。
const cursorId = ref(null)
const tableFocused = ref(false)
function onDocFocusin(e) {
  if (e.target === document.body) return
  tableFocused.value = !!e.target.closest?.('.server-table')
}
function onTableMousedown() {
  tableFocused.value = true
}
function moveCursor(delta) {
  const list = sorted.value
  if (!list.length) return
  const cur = list.findIndex((s) => s.id === cursorId.value)
  const next = cur < 0 ? (delta > 0 ? 0 : list.length - 1) : Math.min(list.length - 1, Math.max(0, cur + delta))
  cursorId.value = list[next].id
  // 光标行滚入可视区：行 DOM 均已存在（仅类名切换），无需等 nextTick
  rootEl.value?.querySelector(`[data-id="${CSS.escape(String(cursorId.value))}"]`)?.scrollIntoView({ block: 'nearest' })
}
function onGlobalKey(e) {
  // Esc 不在此处理：全局 Esc 链（菜单→勾选→搜索→光标）由 ServerListView 统一调度，避免双触发
  // naive 对话框/模态打开时按键整体让位（DOM 存在性判断，参照 EditorDrawer 的
  // .n-base-select-menu 让位先例）：删除/批量确认等 dialog 无输入框（autoFocus:false），
  // 焦点停留在打开前位置 → tableFocused 仍真，Enter/E/Del/Ctrl+A 若不守卫会穿透到
  // 连接/编辑/删除分支（Enter 误连 P0 即此路径）。
  // H6：自绘浮层（本组件行菜单 .ctx-menu / 文件夹空白菜单 .nf-menu / 树右键菜单 .tree-ctx）
  // 同样让位——右键菜单开着时按 Enter/E/Del 会穿透（Menu 键呼出菜单后 Enter 直接拉起
  // 远程会话，第三轮 G3/第四轮 H6，与 .n-dialog 让位同一守卫形态。
  if (document.querySelector('.n-dialog, .n-modal, .ctx-menu, .nf-menu, .tree-ctx')) return
  if (!tableFocused.value) return
  // 表格内的可交互控件（表头复选框/批量条按钮等）聚焦时不抢按键：Enter/空格留给原生行为
  if (e.target.closest?.('input, textarea, select, button, [contenteditable]')) return
  // 按住不放的自动重复：Enter 会重复 emit connect（桌面端每个 invoke 各起一个会话任务，无去重），
  // Ctrl+A 重复全选也无意义——直接忽略；↑↓ 保留重复（按住快速导航是预期行为）
  if (e.repeat && e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    e.preventDefault() // 阻止页面/滚动容器滚动，光标移动优先
    moveCursor(e.key === 'ArrowDown' ? 1 : -1)
  } else if (e.key === 'Enter') {
    // 多选（勾选 >1 台）时禁用 Enter：此时批量操作语境（批量条已现），Enter 连光标行
    // 与勾选心智冲突且一键即拉起真实远程会话——owner 2026-09-21 决策，与 Del 同款禁用
    if (checked.value.size > 1) return
    if (cursorId.value != null) {
      e.preventDefault()
      emit('connect', cursorId.value)
    }
  } else if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key?.toLowerCase() === 'a') {
    e.preventDefault() // 抢在浏览器文本全选前，全选/清空当前视图（toggleAll 与表头三态同款：
    // 已全选时清空——键盘/鼠标通道对称；合并语义保留文件夹勾选展开的隐藏子孙）
    toggleAll()
  } else if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key?.toLowerCase() === 'd') {
    // Ctrl+D 复制：preventDefault 阻断浏览器「添加书签」默认；仅单选（≤1 勾选）生效，
    // 多选（>1）忽略——批量复制无对应动作，防「以为整批复制实际只复制一台」的误触
    e.preventDefault()
    if (checked.value.size > 1) return
    const s = keyTargetServer()
    if (s) emit('duplicate', s)
  } else if (!e.ctrlKey && !e.metaKey && !e.altKey && e.key === 'Delete') {
    // Del 删除（owner 2026-09-21 决策：多选（>1）时整体禁用——批量删除的误触代价是
    // 一次确认框也拦不住的数据丢失，且「已选 N 台」时 Del 的目标歧义无法自解释；
    // 批量删除走批量条按钮）。恰 1 台勾选或无勾选 = 单台（keyTargetServer），确认框
    // 显示名字可纠错
    if (checked.value.size > 1) return
    const s = keyTargetServer()
    if (s) emit('delete', s)
  } else if (e.key === 'ContextMenu' || (e.shiftKey && e.key === 'F10')) {
    // 键盘呼出右键菜单（Menu 键 / Shift+F10，对齐系统语境菜单惯例）：目标行与
    // E/Del 同源（keyTargetServer），定位到该行 DOM（无鼠标坐标）
    e.preventDefault() // 阻断部分平台 Menu 键再触发一次 contextmenu 事件
    const s = keyTargetServer()
    if (s) {
      const el = rootEl.value?.querySelector(`[data-id="${CSS.escape(String(s.id))}"]`)
      const r = el?.getBoundingClientRect()
      openMenu({ server: s, x: r ? r.left + 40 : 0, y: r ? r.bottom : 0 })
    }
  } else if (!e.ctrlKey && !e.metaKey && !e.altKey && e.key?.toLowerCase() === 'e') {
    // E 编辑（允许 Shift+e）
    const s = keyTargetServer()
    if (s) emit('edit', s)
  }
}

// 键盘操作目标行（E/Del/Ctrl+D 共用，「勾选优先单台」）：恰好勾选 1 台 → 该台；
// 否则光标行（Enter 连接同源）；两者皆无 → null 不动作
function keyTargetServer() {
  if (checked.value.size === 1) {
    const id = [...checked.value][0]
    return sorted.value.find((s) => s.id === id) || null
  }
  if (cursorId.value != null) return sorted.value.find((s) => s.id === cursorId.value) || null
  return null
}

// ---- 供 ServerListView 全局 Esc 链逐级回退调用：返回 true = 本次 Esc 消费在此级 ----
function closeMenuIfOpen() {
  if (menu.value) {
    menu.value = null
    return true
  }
  return false
}
function closeColMenuIfOpen() {
  // 列菜单状态在本组件（TableToolbar 只是受控展示）；Esc 链在右键菜单之后接入本级
  if (colMenu.value) {
    colMenu.value = false
    return true
  }
  return false
}
function closeNfMenuIfOpen() {
  // 文件夹/空白右键菜单（nfMenu）与服务器行菜单同级接入 Esc 链（此前只响应外部点击，
  // 开着菜单按 Esc 会击穿到清勾选——链序见 ServerListView onGlobalEsc）
  if (nfMenu.value) {
    nfMenu.value = null
    return true
  }
  return false
}
function clearCheckedIfAny() {
  if (checked.value.size) {
    clearChecked()
    return true
  }
  return false
}
function clearCursorIfAny() {
  if (cursorId.value != null) {
    cursorId.value = null
    return true
  }
  return false
}
defineExpose({ closeMenuIfOpen, closeNfMenuIfOpen, closeColMenuIfOpen, clearCheckedIfAny, clearCursorIfAny })

// 搜索框 ↑/↓ 焦点移交（tableBus.focusHandoff）：App.vue 顶栏搜索框按下方向键 →
// 表格接管键盘（tableFocused 置真——onDocFocusin 不会因这次没有真实 DOM 焦点变化而
// 感知），并把光标落到首/末行（已有光标则按方向步进）。同时 blur 搜索框：方向键的
// prevent 只挡了光标在 input 内移动，焦点仍留在 input——随后 Enter 的 e.target 还是
// 搜索框，会被上方 onGlobalKey 的控件守卫吞掉（Ctrl+F → ↓ → Enter 连接流断链，
// 第三轮 G2）；blur 后焦点落 body（onDocFocusin 对 body 不算离开），Enter/↑↓ 从此
// 进入表格键盘域，与 App.vue 搜索框注释宣称的行为一致
watch(focusHandoff, (req) => {
  if (!req) return
  tableFocused.value = true
  moveCursor(req.delta)
  document.activeElement?.blur()
})

// ---- 列状态：列宽（拖右缘调整/双击重置）+ 列显隐（▦ 列菜单），经 useColumns 持久化
// localStorage '1r-cols'（仅本地）。flex 列有自定义宽时改为定宽（--c-*-grow=0，
// flex-basis=px），未设时保持默认比例；隐藏列在表头与行两侧同时 v-if。
// colMenu/COL_LABELS 供 TableToolbar 展示（列菜单的点击外部关闭在其子组件内自理）----
const { HIDEABLE_COLS, isHidden, widthOf, setHidden, setWidth } = useColumns()
const hiddenCols = computed(() => {
  const o = {}
  for (const k of HIDEABLE_COLS) o[k] = isHidden(k)
  return o
})
const colVars = computed(() => {
  const px = (k, def) => {
    const w = widthOf(k)
    return w ? w + 'px' : def
  }
  const grow0 = (k) => (widthOf(k) ? { ['--c-' + k + '-grow']: '0' } : {})
  return {
    '--c-check': '30px',
    '--c-status': '42px',
    '--c-name': px('name', showFolder.value ? '2.3' : '2.8'),
    '--c-addr': px('addr', '1.6'),
    '--c-proto': px('proto', '84px'),
    '--c-tags': showFolder.value ? '1.2' : '1.5',
    // 备注列：默认比例随 tags 联动（根视图让位给 folder 列），可拖拽定宽
    '--c-note': px('note', showFolder.value ? '1.2' : '1.5'),
    '--c-folder': px('folder', '1.4'),
    '--c-time': px('time', '104px'),
    '--c-act': '100px',
    ...grow0('name'),
    ...grow0('addr'),
    ...grow0('note'),
    ...grow0('folder'),
  }
})
const colMenu = ref(false)
function toggleColMenu() {
  colMenu.value = !colMenu.value
}
const COL_LABELS = computed(() => ({
  name: t('col.name'),
  addr: t('col.address'),
  proto: t('col.protocol'),
  note: t('col.note'),
  folder: t('col.folder'),
  time: t('col.lastConnect'),
}))

// 列宽拖拽：表头右缘 5px 命中区（cursor col-resize），pointer capture 跟踪；
// 双击 = 重置该列（回默认 flex 比例）
const resizing = ref(null) // { k, startX, startW }
function onResizeStart(k, e) {
  const th = e.currentTarget.parentElement
  resizing.value = { k, startX: e.clientX, startW: th.getBoundingClientRect().width }
  e.currentTarget.setPointerCapture?.(e.pointerId)
}
function onResizeMove(e) {
  const r = resizing.value
  if (!r) return
  setWidth(r.k, Math.max(40, r.startW + e.clientX - r.startX))
}
function onResizeEnd() {
  resizing.value = null
}

// ---- 虚拟滚动：>500 行启用 useVirtualList（36px 定高，虚拟容器内行改 border-box 使几何高度
// 与常量精确一致；sticky 表头保持滚动容器首行，wrapper 的 marginTop 偏移不会影响其吸顶）。
// ≤500 行维持直渲染（性能开关常量化）----
const VIRTUAL_THRESHOLD = 500
const ROW_HEIGHT = 36
const useVirtual = computed(() => renderRows.value.length > VIRTUAL_THRESHOLD)
const {
  list: virtualRows,
  containerProps,
  wrapperProps,
} = useVirtualList(renderRows, {
  itemHeight: ROW_HEIGHT,
  overscan: 8,
})
// 两种模式共用单一 v-for：虚拟分支读 useVirtualList 的 { index, data } 项，直渲染分支
// 包成同形状（{ data }），行标记只维护一份
const rowList = computed(() => (useVirtual.value ? virtualRows.value : renderRows.value.map((data) => ({ data }))))

onMounted(() => {
  window.addEventListener('mousedown', onGlobalDown)
  window.addEventListener('keydown', onGlobalKey)
  document.addEventListener('focusin', onDocFocusin)
  // 跨库拖拽反馈兜底（H18，自 SideTree 迁入，见 onListDragEndGlobal 注释）
  window.addEventListener('dragend', onListDragEndGlobal)
  window.addEventListener('drop', resetCrossDsHover, true)
})
onBeforeUnmount(() => {
  window.removeEventListener('mousedown', onGlobalDown)
  window.removeEventListener('keydown', onGlobalKey)
  document.removeEventListener('focusin', onDocFocusin)
  window.removeEventListener('dragend', onListDragEndGlobal)
  window.removeEventListener('drop', resetCrossDsHover, true)
})
</script>

<template>
  <div ref="rootEl" class="server-table" :style="colVars" @mousedown="onTableMousedown">
    <!-- 批量条 + 表头工具簇（TableToolbar）：Teleport 至 ServerListView 面包屑行右侧
         （#crumb-actions），勾选/列状态仍归本组件；连接/批量编辑/导出经此转发到父级执行 -->
    <TableToolbar
      :checked-count="checked.size"
      :is-custom="isCustom"
      :col-menu="colMenu"
      :hidden-cols="hiddenCols"
      :col-labels="COL_LABELS"
      :folder-col-locked="!showFolder"
      @batch-connect="emit('batch-connect', [...checked])"
      @batch-delete="emit('batch-delete', [...checked])"
      @bulk-edit="emit('bulk-edit', [...checked])"
      @export="emit('export', [...checked])"
      @clear-checked="clearChecked"
      @toggle-custom="toggleCustomSort"
      @toggle-col-menu="toggleColMenu"
      @set-hidden="setHidden"
    />

    <div class="tbody" v-bind="useVirtual ? containerProps : undefined" @contextmenu="onBlankContext">
      <!-- 表头放在滚动容器内首行 + sticky：经典（非 overlay）滚动条下滚动内容盒比外层窄 ~17px，
           表头作为 .tbody 兄弟节点会与尾列（协议/最近连接/操作）错位；入内 sticky 天然对齐且滚动常驻。
           可隐藏列 v-if；每列右缘 5px 拖拽调宽（col-resize），双击重置 -->
      <div class="thead">
        <div class="hcell h-check">
          <input
            ref="allCb"
            type="checkbox"
            :checked="allChecked"
            :title="t('col.selectAll')"
            @click.stop
            @change="toggleAll"
          />
        </div>
        <div class="hcell h-status">{{ t('col.status') }}</div>
        <div v-if="!hiddenCols.name" class="hcell h-name sortable" @click="toggleSort('displayName')">
          {{ t('col.name') }} <span class="arrow">{{ arrow('displayName') }}</span
          ><span
            class="resizer"
            @pointerdown="onResizeStart('name', $event)"
            @pointermove="onResizeMove"
            @pointerup="onResizeEnd"
            @dblclick.stop="setWidth('name', null)"
          ></span>
        </div>
        <div v-if="!hiddenCols.addr" class="hcell h-addr sortable" @click="toggleSort('address')">
          {{ t('col.address') }} <span class="arrow">{{ arrow('address') }}</span
          ><span
            class="resizer"
            @pointerdown="onResizeStart('addr', $event)"
            @pointermove="onResizeMove"
            @pointerup="onResizeEnd"
            @dblclick.stop="setWidth('addr', null)"
          ></span>
        </div>
        <div v-if="!hiddenCols.proto" class="hcell h-proto sortable" @click="toggleSort('protocol')">
          {{ t('col.protocol') }} <span class="arrow">{{ arrow('protocol') }}</span
          ><span
            class="resizer"
            @pointerdown="onResizeStart('proto', $event)"
            @pointermove="onResizeMove"
            @pointerup="onResizeEnd"
            @dblclick.stop="setWidth('proto', null)"
          ></span>
        </div>
        <div class="hcell h-tags">{{ t('col.tags') }}</div>
        <!-- 备注列：文本直显（行内 ellipsis）+ 悬停 Markdown 弹层，不可排序 -->
        <div v-if="!hiddenCols.note" class="hcell h-note">
          {{ t('col.note')
          }}<span
            class="resizer"
            @pointerdown="onResizeStart('note', $event)"
            @pointermove="onResizeMove"
            @pointerup="onResizeEnd"
            @dblclick.stop="setWidth('note', null)"
          ></span>
        </div>
        <div v-if="showFolder && !hiddenCols.folder" class="hcell h-folder">
          {{ t('col.folder')
          }}<span
            class="resizer"
            @pointerdown="onResizeStart('folder', $event)"
            @pointermove="onResizeMove"
            @pointerup="onResizeEnd"
            @dblclick.stop="setWidth('folder', null)"
          ></span>
        </div>
        <div v-if="!hiddenCols.time" class="hcell h-time sortable" @click="toggleSort('lastConnectTime')">
          {{ t('col.lastConnect') }} <span class="arrow">{{ arrow('lastConnectTime') }}</span
          ><span
            class="resizer"
            @pointerdown="onResizeStart('time', $event)"
            @pointermove="onResizeMove"
            @pointerup="onResizeEnd"
            @dblclick.stop="setWidth('time', null)"
          ></span>
        </div>
        <div class="hcell h-act">{{ t('col.actions') }}</div>
      </div>
      <!-- 行区：>500 行启用虚拟滚动（wrapper 由 useVirtualList 撑总高 + marginTop 偏移窗口
           渲染，.virtual-wrap 限定其几何修正样式）；≤500 行直渲染——包装 div 此时无 class、
           纯块级透传不参与布局，两种模式行级绑定与几何完全同一份。
           序列 = 「..」上级行 + 文件夹行（勾选子孙/双击进入/右键新建/拖入移动）+ 服务器行 -->
      <div v-bind="useVirtual ? wrapperProps : undefined" :class="{ 'virtual-wrap': useVirtual }">
        <template v-for="{ data: row } in rowList" :key="rowKey(row)">
          <div
            v-if="row.kind === 'parent'"
            class="row prow"
            :class="{ 'drop-into': dropFolder && dropFolder.path === row.path && dropFolder.dsName === row.dsName }"
            :title="t('row.parentFolder')"
            @dblclick="emit('open-folder', { dsName: row.dsName, path: row.path })"
            @contextmenu.prevent
            @dragover="onFolderDragOver(row, $event)"
            @dragleave="onFolderDragLeave(row)"
            @drop="onFolderDrop(row, $event)"
          >
            <div class="cell cell-check"></div>
            <div class="cell cell-status"></div>
            <div class="cell cell-name p-name">
              <span class="p-icon">📁</span>
              <span class="p-label">..</span>
            </div>
          </div>
          <FolderRow
            v-else-if="row.kind === 'folder'"
            :folder="row.folder"
            :show-ds="showDs"
            :check-state="folderChecks.get(rowKey(row))"
            :drop-active="dropFolder && dropFolder.path === row.folder.path && dropFolder.dsName === row.folder.dsName"
            :draggable="dsWritable(row.folder.dsName)"
            @open="emit('open-folder', $event)"
            @context="onFolderContext"
            :writable="dsWritable(row.folder.dsName)"
            @toggle-check="onFolderToggleCheck(row.folder)"
            @dragstart="onFolderRowDragStart(row.folder, $event)"
            @dragover="onFolderDragOver(row.folder, $event)"
            @dragleave="onFolderDragLeave(row.folder)"
            @drop="onFolderDrop(row.folder, $event)"
            @dragend="onFolderRowDragEnd"
            @rename="emit('rename-folder', folderTarget(row.folder))"
            @delete="emit('delete-folder', folderTarget(row.folder))"
          />
          <ServerRow
            v-else
            :server="row.server"
            :selected="checked.has(row.server.id)"
            :highlighted="!!selection && selection.serverId === row.server.id"
            :cursor="row.server.id === cursorId"
            :show-folder="showFolder"
            :show-ds="showDs"
            :hidden-cols="hiddenCols"
            :query="query"
            :data-id="row.server.id"
            :draggable="true"
            :class="{
              'drop-before': dropHint && dropHint.id === row.server.id && dropHint.before,
              'drop-after': dropHint && dropHint.id === row.server.id && !dropHint.before,
            }"
            @toggle-select="onToggleSelect(row.server.id, row.srvIndex)"
            @row-click="onRowClick(row.server, $event, row.srvIndex)"
            @connect="emit('connect', row.server.id)"
            @edit="emit('edit', row.server)"
            @context-menu="openMenu"
            @dragstart="onRowDragStart(row.server, $event)"
            @dragend="onRowDragEnd"
            @dragover="onRowDragOver(row.server, $event)"
            @drop="onRowDrop(row.server, $event)"
          />
        </template>
      </div>
      <slot v-if="!hasSubstance" name="empty">
        <div class="empty">{{ servers.length ? t('empty.filtered') : t('empty.none') }}</div>
      </slot>
    </div>

    <!-- 文件夹右键菜单：文件夹行=新建子文件夹/重命名/删除（与树右键同集）；
         空白处=新建服务器/新建文件夹/导入服务器（前两项动作归父级） -->
    <div v-if="nfMenu" class="ctx-menu nf-menu" :style="{ left: nfMenu.x + 'px', top: nfMenu.y + 'px' }">
      <button v-if="!nfMenu.target?.folderPath" class="ctx-item" @click="nfNewServer">
        <span class="ctx-label">{{ t('topbar.newServer') }}</span>
      </button>
      <button class="ctx-item" :disabled="!nfMenuOk" :title="nfMenuTip" @click="nfCreate">
        <span class="ctx-label">{{ t('tree.newFolder') }}</span>
      </button>
      <button v-if="!nfMenu.target?.folderPath" class="ctx-item" @click="nfImport">
        <span class="ctx-label">{{ t('import.title') }}</span>
      </button>
      <template v-if="nfMenu.target?.folderPath">
        <button class="ctx-item" :disabled="!nfMenuOk" :title="nfMenuTip" @click="nfRename">
          <span class="ctx-label">{{ t('tree.renameFolder') }}</span>
        </button>
        <button class="ctx-item" :disabled="!nfMenuOk" :title="nfMenuTip" @click="nfDelete">
          <span class="ctx-label">{{ t('tree.deleteFolder') }}</span>
        </button>
      </template>
    </div>

    <div v-if="menu" class="ctx-menu" :style="{ left: menu.x + 'px', top: menu.y + 'px' }">
      <button
        v-for="it in MENU"
        :key="it.key"
        class="ctx-item"
        :disabled="!it.on"
        :title="it.tip || it.hint || ''"
        @click="onMenuAction(it)"
      >
        <span class="ctx-label">{{ it.label }}</span>
        <span class="ctx-hint">{{ it.hint || '' }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.server-table {
  position: relative;
  /* 右键菜单浮层定位基准 */
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

/* 行拖拽指示线（作用于 ServerRow 根节点；custom 模式行可抓取）。
   落点线为状态指示（G6）：走 --accent-focus（light 橙/绿亮 accent ×bg <3:1，dark 等值不变） */
:deep(.row[draggable='true']) {
  cursor: grab;
}

:deep(.row.drop-before) {
  box-shadow: inset 0 2px 0 var(--accent-focus);
}

:deep(.row.drop-after) {
  box-shadow: inset 0 -2px 0 var(--accent-focus);
}

/* 表头：列宽与 ServerRow/FolderRow 的 --c-* 同源；sticky 于滚动容器内首行（背景必须不透明，防行内容透出） */
.thead {
  position: sticky;
  top: 0;
  z-index: 5;
  display: flex;
  align-items: center;
  height: 32px;
  padding-right: 10px;
  border-bottom: 1px solid var(--border-strong);
  background: var(--bg-panel);
  color: var(--text-3);
  font-size: var(--fs-caption);
  user-select: none;
}

.hcell {
  display: flex;
  align-items: center;
  min-width: 0;
  padding-right: 10px;
  white-space: nowrap;
}

.h-check {
  flex: 0 0 var(--c-check);
  justify-content: center;
  padding-right: 0;
}

/* 单声明列宽规则单行化：flex 比例列（--c-*-grow=0 时切定宽）与定宽列各一档 */
.h-status {
  flex: 0 0 var(--c-status);
}
.h-name {
  flex: var(--c-name) var(--c-name-grow, 1) 0;
}
.h-addr {
  flex: var(--c-addr) var(--c-addr-grow, 1) 0;
}
.h-proto {
  flex: 0 0 var(--c-proto);
}
.h-tags {
  flex: var(--c-tags) 1 0;
}
.h-note {
  flex: var(--c-note) var(--c-note-grow, 1) 0;
}
.h-folder {
  flex: var(--c-folder) var(--c-folder-grow, 1) 0;
}
.h-time {
  flex: 0 0 var(--c-time);
}
.h-act {
  flex: 0 0 var(--c-act);
  justify-content: flex-end;
  padding-right: 0;
}

/* 列宽拖拽命中区（右缘 5px）：不拦截表头排序点击（指针事件独立在 resizer 上） */
.resizer {
  flex: 0 0 5px;
  align-self: stretch;
  width: 5px;
  height: 32px;
  margin-right: -10px;
  /* 抵消 hcell 的 padding-right，命中区贴列右缘 */
  cursor: col-resize;
}

.resizer:hover {
  background: var(--accent);
  /* hover 反馈不低于 --opacity-hint（0.7）：此前 0.45 弱到像失效，且与禁用 0.5 仅差 0.05 */
  opacity: var(--opacity-hint);
}

.sortable {
  cursor: pointer;
}

.sortable:hover {
  color: var(--text-1);
}

.arrow {
  font-size: 0.6923rem;
  opacity: var(--opacity-hint); /* 装饰性元素弱化档（排序箭头，非交互信息） */
}

.tbody {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

/* 虚拟滚动窗口：wrapper 由 useVirtualList 撑总高；行改 border-box 让几何高度
   与 ROW_HEIGHT=36 精确一致（默认 content-box 下 36px+1px 边框=37px 会累积漂移） */
.virtual-wrap {
  contain: content;
}

.virtual-wrap :deep(.row) {
  box-sizing: border-box;
}

/* FolderRow 根节点（.frow）在子组件模板中，但子组件根元素携带父组件 scope 属性，可直接选中 */
.virtual-wrap .frow {
  box-sizing: border-box;
  /* 与 ServerRow 同款：虚拟分支 36px 几何精确一致 */
}

/* 「..」上级行：文件夹视图顶行——列宽消费 --c-*（.cell 样式 scoped 于 ServerRow/FolderRow
   各自组件，此处自带一份），36px 几何与真实行对齐；双击回上级、拖入=移到上级文件夹 */
.prow {
  display: flex;
  align-items: center;
  height: 36px;
  padding: 0 10px 0 0;
  border-bottom: 1px solid var(--border);
  user-select: none;
}
.prow:hover {
  background: var(--bg-hover);
}
.prow.drop-into {
  background: var(--accent-container);
  outline: 1px dashed var(--accent-focus); /* 拖入虚线轮廓同为状态指示（G6） */
  outline-offset: -1px;
}
.prow .cell {
  display: flex;
  align-items: center;
  min-width: 0;
}
.prow .cell-check {
  flex: 0 0 var(--c-check);
}
.prow .cell-status {
  flex: 0 0 var(--c-status);
}
.prow .cell-name {
  flex: 1 1 0;
  gap: 8px;
}
.p-icon {
  flex: 0 0 22px;
  text-align: center;
  font-size: var(--fs-title);
}
.p-label {
  color: var(--text-1);
  font-size: var(--fs-body);
}

.empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 160px;
  color: var(--text-4);
  font-size: var(--fs-body);
}

/* 右键菜单浮层。ctx-menu 族锚点（G18）：SideTree .tree-ctx 拷贝本参数 */
.ctx-menu {
  position: absolute;
  z-index: 30;
  display: flex;
  flex-direction: column;
  min-width: 210px;
  padding: 4px;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-box);
  background: var(--bg-elevated);
  box-shadow: var(--shadow-menu);
}

.ctx-item {
  /* 下拉/右键菜单项统一内距档 7px 10px（G16 锚点：TableToolbar .col-item / SideTree
     .tree-ctx .ctx-item 与此同源拷贝） */
  display: flex;
  align-items: center;
  justify-content: space-between;
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

.ctx-hint {
  color: var(--text-4);
  font-size: var(--fs-caption); /* 菜单项提示统一 caption（G16，与 col-hint 同档） */
}
</style>
