<script setup>
// 行列表（spec §3.4，Task 16）：div+flex 自写轻量表格（列配置以 CSS 变量形式驱动表头/行对齐）。
// - 过滤：selection 非空 → 数据源匹配 + 文件夹递归含子级（spec §3.2，根=整库）；serverId 仅作行高亮
// - 排序：名称/地址（自然 IP）/协议/最近连接，点表头升降切换；localStorage '1r-sort' 持久化（列宽列显 Plan 4）
// - 多选：单击=单选、Ctrl/⌘=切换、Shift=范围（锚点=上次点击行）；表头三态全选；视图变化剔除不可见勾选
// - 键盘（spec §8.2，Task 18 + Plan 4 Task 3）：↑↓ 移动光标行（sorted 可见列表内）、Enter 连接光标行、
//   Ctrl+A 全选可见、E 编辑 / Del 删除 / Ctrl+D 复制（目标行 = 恰好单选该台，否则光标行）；
//   Esc 不在此处理——全局 Esc 链（菜单→勾选→搜索→光标）由 ServerListView 统一调度（见其 onGlobalEsc）
// - 批量条：选中 ≥1 时渲染于表头上方；连接/导出 emit 到父级执行（导出 Plan 4 Task 3 接线），
//   批量编辑（Plan 2 Task 10）同
// - 右键菜单：连接/编辑/复制/复制地址/复制用户名/删除可用，其余占位禁用（title 提示）；
//   点击外部/Esc 关闭
// - 空态：默认居中提示按「传入列表空=空库 / 非空但过滤后无行=无匹配」二分；
//   具名插槽 empty 供父级覆写（ServerListView 的引导卡片/无匹配态在表格外层接管，见 Task 20）
import { computed, onBeforeUnmount, onMounted, ref, watch, watchEffect } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import ServerRow from './ServerRow.vue'
import { api } from '../api'
import { naturalIpCompare } from '../utils/compare'

const props = defineProps({
  servers: { type: Array, default: () => [] },
  selection: { type: Object, default: null }, // { dataSourceName, folderPath, serverId? } | null
})
const emit = defineEmits(['connect', 'batch-connect', 'bulk-edit', 'export', 'edit', 'duplicate', 'delete', 'counted'])
const { t } = useI18n()
const message = useMessage()

// ---- 过滤：搜索/标签过滤已由 ServerListView（applyServerFilters）收窄后经 servers prop 传入，
// 此处仅剩树选中过滤（spec §3.2，根=整库）；两层交集自然复合 ----
const filtered = computed(() => {
  const sel = props.selection
  if (!sel || !sel.dataSourceName) return props.servers // 未选树节点 → 全部
  return props.servers.filter(s => {
    if (s.dataSourceName !== sel.dataSourceName) return false
    if (!sel.folderPath) return true // 根视图 = 整库递归
    return s.folderPath === sel.folderPath || s.folderPath.startsWith(sel.folderPath + '/')
  })
})
const showFolder = computed(() => !props.selection || !props.selection.folderPath) // 仅根视图显示文件夹列

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
  sort.value = sort.value.key === key ? { key, dir: -sort.value.dir } : { key, dir: 1 }
  try {
    localStorage.setItem('1r-sort', JSON.stringify(sort.value))
  } catch {
    /* 隐私模式等写入失败可忽略 */
  }
}
const arrow = (k) => (sort.value.key === k ? (sort.value.dir > 0 ? '▲' : '▼') : '↕')

// ---- 自定义顺序（Plan 4 Task 4，spec §3.4 行拖拽）：sort.key==='custom'（工具条 ≡ 切换）
// 时行可拖拽重排，顺序 = /api/ui-state/list-order 的 ServerCustomOrder（与 WPF 列表视图
// 共用同一字典；未知 id 排末尾、稳定保持自然序——WPF 默认 0 排最前，记录偏差）。
// 非 custom 模式拖拽禁用（draggable=false + 光标默认）。
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
watch(isCustom, on => {
  if (on) loadCustomOrder()
}, { immediate: true })
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

// ---- 行拖拽重排（仅 custom 模式；上/下半行 = 插到目标前/后，对齐 WPF 列表 Drop 的
// height/2 判定；drop 后 POST 整库新顺序 → 以响应重建 Map → sorted 即时重排）----
const dragId = ref(null)
const dropHint = ref(null) // { id, before }
function onRowDragStart(server, e) {
  dragId.value = server.id
  e.dataTransfer.effectAllowed = 'move'
  e.dataTransfer.setData('text/plain', server.id) // Firefox 需要非空 data
}
function onRowDragEnd() {
  dragId.value = null
  dropHint.value = null
}
function onRowDragOver(server, e) {
  if (!isCustom.value || !dragId.value || dragId.value === server.id) return
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
  const ids = fullOrder.value.map(s => s.id)
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
const sorted = computed(() => {
  const c = comparators[sort.value.key]
  if (!c) return filtered.value // 未排序 = 保持树序
  return filtered.value.slice().sort((a, b) => c(a, b) * sort.value.dir)
})

// ---- 多选 ----
const checked = ref(new Set())
let anchorIdx = -1
const allChecked = computed(() => sorted.value.length > 0 && sorted.value.every(s => checked.value.has(s.id)))
const someChecked = computed(() => !allChecked.value && sorted.value.some(s => checked.value.has(s.id)))
const allCb = ref(null) // 三态复选框：indeterminate 需写 DOM 属性
watchEffect(() => {
  if (allCb.value) allCb.value.indeterminate = someChecked.value
})
function toggleChecked(id) {
  const next = new Set(checked.value)
  next.has(id) ? next.delete(id) : next.add(id)
  checked.value = next
}
function onRowClick(server, ev, idx) {
  cursorId.value = server.id // 点击行 = 光标落位（Enter 连接光标行，↑↓ 由此起算）
  if (ev.shiftKey && anchorIdx >= 0) {
    const lo = Math.min(anchorIdx, idx)
    const hi = Math.max(anchorIdx, idx)
    checked.value = new Set(sorted.value.slice(lo, hi + 1).map(s => s.id))
  } else if (ev.ctrlKey || ev.metaKey) {
    toggleChecked(server.id)
    anchorIdx = idx
  } else {
    checked.value = new Set([server.id]) // 单击=单选，清空其余
    anchorIdx = idx
  }
}
function onToggleSelect(server, idx) {
  toggleChecked(server.id)
  anchorIdx = idx
}
function toggleAll() {
  checked.value = allChecked.value ? new Set() : new Set(sorted.value.map(s => s.id))
}
function clearChecked() {
  checked.value = new Set()
  anchorIdx = -1
}
// 过滤/数据变化后剔除不可见行勾选，批量条计数始终对当前视图有效；同时作废 Shift 范围锚点
// 与不可见的光标行（树切换/搜索过滤后旧行号已无意义，Shift 选区必须重新锚定）
watch(sorted, list => {
  anchorIdx = -1
  if (cursorId.value != null && !list.some(s => s.id === cursorId.value)) cursorId.value = null
  if (!checked.value.size) return
  const ids = new Set(list.map(s => s.id))
  const kept = [...checked.value].filter(id => ids.has(id))
  if (kept.length !== checked.value.size) checked.value = new Set(kept)
})
watchEffect(() => emit('counted', sorted.value.length)) // 供面包屑「· N 台」

// ---- 右键菜单（浮层；快捷键提示对齐 spec §8.2：Enter/E/Ctrl+D/Del 均已接线）----
// 标签/提示走 i18n（computed：语言切换即时刷新）；未接线项的占位提示统一「即将推出」，
// 内部计划号（Plan 2/4）只留在代码注释，不进 UI。
// Plan 2 Task 8：编辑/复制/删除已接线；Plan 4 Task 3：E/Ctrl+D/Del 键盘与菜单同 emit 链路。
const MENU = computed(() => [
  { key: 'connect', label: t('ctx.connect'), hint: 'Enter', on: true },
  { key: 'new-window', label: t('ctx.newWindow'), hint: t('common.comingSoon') },
  { key: 'other-credential', label: t('ctx.otherCredential'), hint: t('common.comingSoon') },
  { key: 'edit', label: t('ctx.edit'), hint: 'E', on: true },
  { key: 'duplicate', label: t('ctx.duplicate'), hint: 'Ctrl+D', on: true },
  { key: 'copy-address', label: t('ctx.copyAddress'), on: true },
  { key: 'copy-username', label: t('ctx.copyUsername'), on: true },
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
}

// ---- 键盘导航（spec §8.2，Task 18 + Plan 4 Task 3）：↑↓ 光标行、Enter 连接光标行、
// Ctrl+A 全选可见、E 编辑 / Del 删除 / Ctrl+D 复制（keyTargetServer 取目标行）----
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
  const cur = list.findIndex(s => s.id === cursorId.value)
  const next = cur < 0 ? (delta > 0 ? 0 : list.length - 1) : Math.min(list.length - 1, Math.max(0, cur + delta))
  cursorId.value = list[next].id
  // 光标行滚入可视区：行 DOM 均已存在（仅类名切换），无需等 nextTick
  rootEl.value?.querySelector(`[data-id="${CSS.escape(String(cursorId.value))}"]`)?.scrollIntoView({ block: 'nearest' })
}
function onGlobalKey(e) {
  // Esc 不在此处理：全局 Esc 链（菜单→勾选→搜索→光标）由 ServerListView 统一调度，避免双触发
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
    if (cursorId.value != null) {
      e.preventDefault()
      emit('connect', cursorId.value)
    }
  } else if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key?.toLowerCase() === 'a') {
    e.preventDefault() // 抢在浏览器文本全选前，全选当前视图（spec §8.2）
    checked.value = new Set(sorted.value.map(s => s.id))
  } else if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key?.toLowerCase() === 'd') {
    // Ctrl+D 复制（spec §8.2，Plan 4 Task 3）：preventDefault 阻断浏览器「添加书签」默认
    e.preventDefault()
    const s = keyTargetServer()
    if (s) emit('duplicate', s)
  } else if (!e.ctrlKey && !e.metaKey && !e.altKey && e.key === 'Delete') {
    // Del 删除（spec §8.2）：只 emit server 对象，确认对话框由 ServerListView 的 onDelete 统一弹出
    const s = keyTargetServer()
    if (s) emit('delete', s)
  } else if (!e.ctrlKey && !e.metaKey && !e.altKey && e.key?.toLowerCase() === 'e') {
    // E 编辑（spec §8.2，允许 Shift+e）
    const s = keyTargetServer()
    if (s) emit('edit', s)
  }
}

// 键盘操作目标行（E/Del/Ctrl+D 共用，spec §8.2「勾选优先单台」）：恰好勾选 1 台 → 该台；
// 否则光标行（Enter 连接同源）；两者皆无 → null 不动作
function keyTargetServer() {
  if (checked.value.size === 1) {
    const id = [...checked.value][0]
    return sorted.value.find(s => s.id === id) || null
  }
  if (cursorId.value != null) return sorted.value.find(s => s.id === cursorId.value) || null
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
defineExpose({ closeMenuIfOpen, clearCheckedIfAny, clearCursorIfAny })

onMounted(() => {
  window.addEventListener('mousedown', onGlobalDown)
  window.addEventListener('keydown', onGlobalKey)
  document.addEventListener('focusin', onDocFocusin)
})
onBeforeUnmount(() => {
  window.removeEventListener('mousedown', onGlobalDown)
  window.removeEventListener('keydown', onGlobalKey)
  document.removeEventListener('focusin', onDocFocusin)
})

// ---- 列宽（flex 比例，样张 v2；隐藏文件夹列时把宽度让给名称/标签）----
const colVars = computed(() => ({
  '--c-check': '30px',
  '--c-status': '58px',
  '--c-name': showFolder.value ? '2.3' : '2.8',
  '--c-addr': '1.6',
  '--c-proto': '84px',
  '--c-tags': showFolder.value ? '1.2' : '1.5',
  '--c-folder': '1.4',
  '--c-time': '104px',
  '--c-act': '100px',
}))
</script>

<template>
  <div ref="rootEl" class="server-table" :style="colVars" @mousedown="onTableMousedown">
    <div v-if="checked.size" class="batch-bar">
      <span class="bb-count">{{ t('batch.selected', { n: checked.size }) }}</span>
      <button class="bb-btn bb-primary" :title="t('batch.connectTitle')" @click="emit('batch-connect', [...checked])">▶ {{ t('batch.connect') }}</button>
      <!-- 批量编辑（Plan 2 Task 10）：emit 勾选 id 数组，抽屉批量模式由 ServerListView 打开 -->
      <button class="bb-btn" :title="t('batch.editTitle')" @click="emit('bulk-edit', [...checked])">✎ {{ t('batch.edit') }}</button>
      <!-- 导出（Plan 4 Task 3）：emit 勾选 id 数组，blob 下载（含 403 二次验证提示）由 ServerListView 执行 -->
      <button class="bb-btn" :title="t('batch.exportTitle')" @click="emit('export', [...checked])">⤓ {{ t('batch.export') }}</button>
      <button class="bb-x" :title="t('batch.clear')" @click="clearChecked">✕</button>
    </div>

    <div class="tbody">
      <!-- 表头工具簇（Plan 4）：浮于表头右端（.server-table 为定位基准）。
           ≡ = 自定义顺序模式开关（开启后行可拖拽重排，Plan 4 Task 4） -->
      <div class="table-tools">
        <button class="tt-btn" :class="{ active: isCustom }" :title="t('list.customOrder')" @click="toggleCustomSort">≡</button>
      </div>
      <!-- 表头放在滚动容器内首行 + sticky：经典（非 overlay）滚动条下滚动内容盒比外层窄 ~17px，
           表头作为 .tbody 兄弟节点会与尾列（协议/最近连接/操作）错位；入内 sticky 天然对齐且滚动常驻 -->
      <div class="thead">
        <div class="hcell h-check">
          <input ref="allCb" type="checkbox" :checked="allChecked" :title="t('col.selectAll')" @click.stop @change="toggleAll" />
        </div>
        <div class="hcell h-status">{{ t('col.status') }}</div>
        <div class="hcell h-name sortable" @click="toggleSort('displayName')">{{ t('col.name') }} <span class="arrow">{{ arrow('displayName') }}</span></div>
        <div class="hcell h-addr sortable" @click="toggleSort('address')">{{ t('col.address') }} <span class="arrow">{{ arrow('address') }}</span></div>
        <div class="hcell h-proto sortable" @click="toggleSort('protocol')">{{ t('col.protocol') }} <span class="arrow">{{ arrow('protocol') }}</span></div>
        <div class="hcell h-tags">{{ t('col.tags') }}</div>
        <div v-if="showFolder" class="hcell h-folder">{{ t('col.folder') }}</div>
        <div class="hcell h-time sortable" @click="toggleSort('lastConnectTime')">{{ t('col.lastConnect') }} <span class="arrow">{{ arrow('lastConnectTime') }}</span></div>
        <div class="hcell h-act">{{ t('col.actions') }}</div>
      </div>
      <ServerRow
        v-for="(s, i) in sorted"
        :key="s.id"
        :server="s"
        :selected="checked.has(s.id)"
        :highlighted="!!selection && selection.serverId === s.id"
        :cursor="s.id === cursorId"
        :show-folder="showFolder"
        :data-id="s.id"
        :draggable="isCustom"
        :class="{
          'drop-before': dropHint && dropHint.id === s.id && dropHint.before,
          'drop-after': dropHint && dropHint.id === s.id && !dropHint.before,
        }"
        @toggle-select="onToggleSelect(s, i)"
        @row-click="onRowClick(s, $event, i)"
        @connect="emit('connect', s.id)"
        @edit="emit('edit', s)"
        @context-menu="openMenu"
        @dragstart="onRowDragStart(s, $event)"
        @dragend="onRowDragEnd"
        @dragover="onRowDragOver(s, $event)"
        @drop="onRowDrop(s, $event)"
      />
      <slot v-if="!sorted.length" name="empty">
        <div class="empty">{{ servers.length ? t('empty.filtered') : t('empty.none') }}</div>
      </slot>
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
  position: relative; /* 右键菜单浮层定位基准 */
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

/* 批量操作条（spec §3.4：选中 ≥1 展开；spec §3.1 面包屑行的简化落位） */
.batch-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  height: 36px;
  padding: 0 10px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-elevated);
}
.bb-count {
  font-size: 12.5px;
  color: var(--text-2);
}
.bb-btn {
  border: 1px solid var(--border);
  border-radius: 6px;
  background: transparent;
  color: var(--text-2);
  font-size: 12px;
  line-height: 1;
  padding: 5px 10px;
  cursor: pointer;
}
.bb-btn:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
}
.bb-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.bb-primary {
  border-color: var(--accent);
  color: var(--accent-text);
}
.bb-x {
  margin-left: auto;
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: 13px;
  width: 26px;
  height: 26px;
  border-radius: 6px;
  cursor: pointer;
}
.bb-x:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}

/* 表头工具簇：浮于表头行右端（.server-table relative 定位基准），z 高于 sticky 表头 */
.table-tools {
  position: absolute;
  top: 0;
  right: 14px;
  z-index: 6;
  display: flex;
  align-items: center;
  gap: 4px;
  height: 32px;
}
.tt-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 22px;
  border: 1px solid var(--border);
  border-radius: 5px;
  background: var(--bg-panel);
  color: var(--text-3);
  font-size: 12px;
  line-height: 1;
  cursor: pointer;
}
.tt-btn:hover {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.tt-btn.active {
  border-color: var(--accent);
  color: var(--accent-text);
}

/* 行拖拽指示线（作用于 ServerRow 根节点；custom 模式行可抓取） */
:deep(.row[draggable='true']) {
  cursor: grab;
}
:deep(.row.drop-before) {
  box-shadow: inset 0 2px 0 var(--accent);
}
:deep(.row.drop-after) {
  box-shadow: inset 0 -2px 0 var(--accent);
}

/* 表头：列宽与 ServerRow 的 --c-* 同源；sticky 于滚动容器内首行（背景必须不透明，防行内容透出） */
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
  font-size: 11.5px;
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
.h-status {
  flex: 0 0 var(--c-status);
}
.h-name {
  flex: var(--c-name) 1 0;
}
.h-addr {
  flex: var(--c-addr) 1 0;
}
.h-proto {
  flex: 0 0 var(--c-proto);
}
.h-tags {
  flex: var(--c-tags) 1 0;
}
.h-folder {
  flex: var(--c-folder) 1 0;
}
.h-time {
  flex: 0 0 var(--c-time);
}
.h-act {
  flex: 0 0 var(--c-act);
  justify-content: flex-end;
  padding-right: 0;
}
.sortable {
  cursor: pointer;
}
.sortable:hover {
  color: var(--text-1);
}
.arrow {
  font-size: 9px;
  opacity: 0.7;
}

.tbody {
  flex: 1;
  min-height: 0;
  overflow: auto;
}
.empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 160px;
  color: var(--text-4);
  font-size: 12.5px;
}

/* 右键菜单浮层 */
.ctx-menu {
  position: absolute;
  z-index: 30;
  display: flex;
  flex-direction: column;
  min-width: 210px;
  padding: 4px;
  border: 1px solid var(--border-strong);
  border-radius: 8px;
  background: var(--bg-elevated);
  box-shadow: 0 6px 24px rgb(0 0 0 / 25%);
}
.ctx-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  border: none;
  border-radius: 5px;
  background: transparent;
  color: var(--text-2);
  font-size: 12.5px;
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
  opacity: 0.6;
  cursor: not-allowed;
}
.ctx-hint {
  color: var(--text-4);
  font-size: 10.5px;
}
</style>
