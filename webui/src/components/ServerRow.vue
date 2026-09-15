<script setup>
// 单行（spec §3.4，对齐已确认样张 v2）：36px flex 行，列宽不自持——由父级 ServerTable 经 CSS
// 变量（--c-*）下发，表头与行严格对齐；本组件只管渲染与交互 emit。
// 交互：单击=单选（父级据 event 修饰键做 Ctrl/Shift 多选）、双击=连接（Task 18 接线）、
// 复选框=切换勾选、右键/hover ⋯=菜单、▸=连接、✎=编辑（Plan 2 占位 emit）。
import StatusDot from './StatusDot.vue'
import ProtocolBadge from './ProtocolBadge.vue'
import { formatRelativeTime } from '../utils/time'

defineProps({
  server: { type: Object, required: true },
  selected: { type: Boolean, default: false }, // 复选框勾选态（批量操作）
  highlighted: { type: Boolean, default: false }, // 边栏树叶选中对应行的高亮
  showFolder: { type: Boolean, default: false }, // 仅根视图显示「文件夹」列（spec §3.2）
})
const emit = defineEmits(['toggle-select', 'row-click', 'connect', 'edit', 'context-menu'])

const iconSrc = (s) => (s.iconBase64 ? 'data:image/png;base64,' + s.iconBase64 : '')
const initial = (p) => (p || '?').charAt(0).toUpperCase()
// 地址列：Serial 等无地址协议回退显示协议名；有端口拼 ':port'
const addressText = (s) => (s.address ? s.address + (s.port ? ':' + s.port : '') : s.protocol)
// 标签胶囊：最多 2 个 + 溢出计数（完整列表见 title）
const overflow = (s) => Math.max(0, s.tags.length - 2)
const relTime = (s) => formatRelativeTime(s.lastConnectTime) || '从未'
</script>

<template>
  <div
    class="row"
    :class="{ selected: selected, highlighted: highlighted }"
    @click="emit('row-click', $event)"
    @dblclick="emit('connect')"
    @contextmenu.prevent="emit('context-menu', { server, x: $event.clientX, y: $event.clientY })"
  >
    <div class="cell cell-check">
      <input type="checkbox" class="cb" :checked="selected" title="选择" @click.stop @change="emit('toggle-select')" />
    </div>
    <div class="cell cell-status"><StatusDot :state="server.connectionState" /></div>
    <div class="cell cell-name" :title="server.displayName">
      <img v-if="server.iconBase64" class="icon" :src="iconSrc(server)" alt="" />
      <span
        v-else
        class="icon icon-fb"
        :style="server.color ? { background: server.color + '26', color: server.color } : null"
      >{{ initial(server.protocol) }}</span>
      <span class="name">{{ server.displayName }}</span>
    </div>
    <div class="cell cell-addr" :title="addressText(server)">{{ addressText(server) }}</div>
    <div class="cell cell-proto"><ProtocolBadge :protocol="server.protocol" /></div>
    <div class="cell cell-tags" :title="server.tags.join('、')">
      <span v-for="t in server.tags.slice(0, 2)" :key="t" class="tag">{{ t }}</span>
      <span v-if="overflow(server)" class="tag tag-more">+{{ overflow(server) }}</span>
    </div>
    <div v-if="showFolder" class="cell cell-folder" :title="server.folderPath">{{ server.folderPath || '—' }}</div>
    <div class="cell cell-time" :title="relTime(server)">{{ relTime(server) }}</div>
    <div class="cell cell-act" @click.stop>
      <button class="act" title="连接" @click="emit('connect')">▸</button>
      <button class="act" title="编辑（Plan 2）" @click="emit('edit')">✎</button>
      <button
        class="act"
        title="更多"
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
  flex: var(--c-name, 2.3) 1 0;
  gap: 8px;
}
.cell-addr {
  flex: var(--c-addr, 1.6) 1 0;
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
  flex: var(--c-folder, 1.4) 1 0;
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
.act:hover {
  background: var(--bg-elevated);
  color: var(--accent-text);
}
</style>
