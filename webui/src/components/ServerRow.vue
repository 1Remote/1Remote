<script setup>
// 单行（spec §3.4，对齐已确认样张 v2）：36px flex 行，列宽不自持——由父级 ServerTable 经 CSS
// 变量（--c-*）下发，表头与行严格对齐；本组件只管渲染与交互 emit。
// 交互：单击=单选（父级据 event 修饰键做 Ctrl/Shift 多选）、双击=连接（Task 18 接线）、
// 复选框=切换勾选、右键/hover ⋯=菜单、▸=连接、✎=编辑（Plan 2 Task 8 接线，与菜单「编辑」同链路）。
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'
import StatusDot from './StatusDot.vue'
import ProtocolBadge from './ProtocolBadge.vue'
import { formatRelativeTime } from '../utils/time'
import { splitHighlight } from '../utils/highlight'

const props = defineProps({
  server: { type: Object, required: true },
  selected: { type: Boolean, default: false }, // 复选框勾选态（批量操作）
  highlighted: { type: Boolean, default: false }, // 边栏树叶选中对应行的高亮
  cursor: { type: Boolean, default: false }, // 键盘导航光标行（↑↓ 移动 / Enter 连接，spec §8.2）
  showFolder: { type: Boolean, default: false }, // 仅根视图显示「文件夹」列（spec §3.2）
  showDs: { type: Boolean, default: false }, // 全部数据源根（fix-batch1 Task 2）：文件夹列前缀数据源名
  hiddenCols: { type: Object, default: null }, // 列显隐（Plan 4 Task 5）：{name/addr/proto/folder/time: bool}
  query: { type: String, default: '' }, // 搜索过滤词（fix-batch1 #1）：非空时名称/地址单元格做命中高亮
})
const emit = defineEmits(['toggle-select', 'row-click', 'connect', 'edit', 'context-menu'])
const { t, locale } = useI18n()

const iconSrc = (s) => (s.iconBase64 ? 'data:image/png;base64,' + s.iconBase64 : '')
const initial = (p) => (p || '?').charAt(0).toUpperCase()
// 地址列：Serial 等无地址协议回退显示协议名；有端口拼 ':port'
const addressText = (s) => (s.address ? s.address + (s.port ? ':' + s.port : '') : s.protocol)
// 文件夹列（fix-batch1 Task 2）：根视图递归展示全库，列为「数据源 / 路径」定位信息；
// 全部数据源根额外前缀数据源名（同名路径跨数据源区分），进入文件夹后列隐藏（面包屑承载路径）
const folderText = (s) =>
  props.showDs
    ? s.dataSourceName + (s.folderPath ? ' / ' + s.folderPath : '')
    : (s.folderPath || '—')
// 标签胶囊：最多 2 个 + 溢出计数（完整列表见 title）
const overflow = (s) => Math.max(0, s.tags.length - 2)
// 从未连接 = formatRelativeTime 返回 null 时的占位文案；相对时间显式注入当前 i18n locale
// （渲染期读 locale.value，语言切换即时重格式化，不再依赖 navigator.language）
const relTime = (s) => formatRelativeTime(s.lastConnectTime, Date.now(), locale.value) || t('status.never')

// 搜索命中高亮分段（fix-batch1 #1）：查询非空时名称/地址同时高亮；拼音等无法定位原文的
// 命中不高亮（splitHighlight 内处理，见其文件头注释）
const nameSegs = computed(() => splitHighlight(props.server.displayName, props.query))
const addrSegs = computed(() => splitHighlight(addressText(props.server), props.query))
</script>

<template>
  <div
    class="row"
    :class="{ selected: selected, highlighted: highlighted, cursor: cursor }"
    @click="emit('row-click', $event)"
    @dblclick="emit('connect')"
    @contextmenu.prevent="emit('context-menu', { server, x: $event.clientX, y: $event.clientY })"
  >
    <div class="cell cell-check">
      <input type="checkbox" class="cb" :checked="selected" :title="t('row.select')" @click.stop @change="emit('toggle-select')" />
    </div>
    <div class="cell cell-status"><StatusDot :state="server.connectionState" /></div>
    <div v-if="!hiddenCols || !hiddenCols.name" class="cell cell-name" :title="server.displayName">
      <img v-if="server.iconBase64" class="icon" :src="iconSrc(server)" alt="" />
      <span
        v-else
        class="icon icon-fb"
        :style="server.color ? { background: server.color + '26', color: server.color } : null"
      >{{ initial(server.protocol) }}</span>
      <span class="name"><template v-for="(seg, i) in nameSegs" :key="i"><span v-if="seg.hit" class="hl">{{ seg.text }}</span><template v-else>{{ seg.text }}</template></template></span>
    </div>
    <div v-if="!hiddenCols || !hiddenCols.addr" class="cell cell-addr" :title="addressText(server)"><template v-for="(seg, i) in addrSegs" :key="i"><span v-if="seg.hit" class="hl">{{ seg.text }}</span><template v-else>{{ seg.text }}</template></template></div>
    <div v-if="!hiddenCols || !hiddenCols.proto" class="cell cell-proto"><ProtocolBadge :protocol="server.protocol" /></div>
    <div class="cell cell-tags" :title="server.tags.join(t('row.tagSep'))">
      <!-- 循环变量命名 tag：避免遮蔽 i18n 的 t（title 属性在循环外也用到 t） -->
      <span v-for="tag in server.tags.slice(0, 2)" :key="tag" class="tag">{{ tag }}</span>
      <span v-if="overflow(server)" class="tag tag-more">+{{ overflow(server) }}</span>
    </div>
    <div v-if="showFolder && (!hiddenCols || !hiddenCols.folder)" class="cell cell-folder" :title="folderText(server)">{{ folderText(server) }}</div>
    <div v-if="!hiddenCols || !hiddenCols.time" class="cell cell-time" :title="relTime(server)">{{ relTime(server) }}</div>
    <div class="cell cell-act" @click.stop>
      <button class="act" :title="t('row.connect')" @click="emit('connect')">▸</button>
      <!-- 编辑按钮（Plan 2 Task 8 接线）：与右键菜单「编辑」同一 emit 链路，经 ServerTable 转发 server 对象 -->
      <button class="act" :title="t('row.edit')" @click="emit('edit')">✎</button>
      <button
        class="act"
        :title="t('row.more')"
        @click="emit('context-menu', { server, x: $event.clientX, y: $event.clientY })"
      >⋯</button>
    </div>
  </div>
</template>

<style scoped>
/* 列宽消费父级下发的 --c-*（见 ServerTable colVars），带独立使用时的兜底值 */
.row {
  display: flex;
  align-items: center;
  height: 36px;
  padding: 0 10px 0 0;
  border-bottom: 1px solid var(--border);
  user-select: none;
}
.row:hover {
  background: var(--bg-hover);
}
.row.selected {
  background: var(--accent-container);
}
.row.highlighted {
  box-shadow: inset 2px 0 0 var(--accent); /* 树叶选中行：左侧强调色细条 */
}
.row.cursor {
  outline: 1px solid var(--border-strong); /* 键盘光标行：subtle 外框（不占布局） */
  outline-offset: -1px;
}

.cell {
  display: flex;
  align-items: center;
  min-width: 0;
  padding-right: 10px;
  font-size: 12.5px;
  color: var(--text-2);
}
.cell-check {
  flex: 0 0 var(--c-check, 30px);
  justify-content: center;
  padding-right: 0;
}
.cell-status {
  flex: 0 0 var(--c-status, 58px);
}
.cell-name {
  flex: var(--c-name, 2.3) var(--c-name-grow, 1) 0;
  gap: 8px;
}
.cell-addr {
  flex: var(--c-addr, 1.6) var(--c-addr-grow, 1) 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.cell-proto {
  flex: 0 0 var(--c-proto, 84px);
}
.cell-tags {
  flex: var(--c-tags, 1.2) 1 0;
  gap: 4px;
  overflow: hidden;
}
.cell-folder {
  flex: var(--c-folder, 1.4) var(--c-folder-grow, 1) 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-4);
}
.cell-time {
  flex: 0 0 var(--c-time, 104px);
  color: var(--text-3);
  white-space: nowrap;
}
.cell-act {
  flex: 0 0 var(--c-act, 100px);
  gap: 2px;
  justify-content: flex-end;
  padding-right: 0;
  opacity: 0; /* hover 操作浮现（spec §3.4） */
}
.row:hover .cell-act,
.row.selected .cell-act {
  opacity: 1;
}

.icon {
  flex: 0 0 22px;
  width: 22px;
  height: 22px;
  border-radius: 5px;
  object-fit: cover;
}
.icon-fb {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-elevated); /* 无自定义色 → 中性瓦片；有色 → 内联低饱和底+同色字 */
  color: var(--text-3);
  font-size: 12px;
  font-weight: 600;
}
.name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-1);
}

/* 搜索命中高亮（fix-batch1 #1）：mark 语义的强调底色（不加粗，保持行高一致） */
.hl {
  background: var(--accent-container);
  border-radius: 2px;
}

.tag {
  flex: 0 0 auto;
  max-width: 96px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 10.5px;
  line-height: 1;
  padding: 2.5px 8px;
}
.tag-more {
  color: var(--text-4);
}

.cb {
  accent-color: var(--accent);
}
.act {
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: 13px;
  line-height: 1;
  width: 26px;
  height: 24px;
  border-radius: 5px;
  cursor: pointer;
}
.act:hover:not(:disabled) {
  background: var(--bg-elevated);
  color: var(--accent-text);
}
.act:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
</style>
