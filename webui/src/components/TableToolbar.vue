<script setup>
// 面包屑行右侧工具簇：整块 Teleport 至 ServerListView 的 #crumb-actions 容器（目标容器
// 在骨架屏/空态 v-if 链之外恒存在，先于本组件挂载可用；内容随本组件卸载一并消失，
// 即只在表格可见时出现）。布局与宿主行内联，不自带整条背景/边框。
// 组成：批量条（勾选 ≥1 时）+ ≡ 自定义顺序开关 + ▦ 列菜单。
// 状态（勾选/排序模式/列显隐）全部留在 ServerTable，本组件纯展示 + emit 转发。
import { onBeforeUnmount, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { HIDEABLE_COLS } from '../composables/useColumns'

const props = defineProps({
  checkedCount: { type: Number, default: 0 }, // 勾选服务器数（≥1 显示批量条）
  isCustom: { type: Boolean, default: false }, // ≡ 自定义顺序模式激活（行可拖拽重排）
  colMenu: { type: Boolean, default: false }, // ▦ 列菜单展开态
  hiddenCols: { type: Object, default: () => ({}) }, // { colKey: bool }（true=隐藏）
  colLabels: { type: Object, default: () => ({}) }, // { colKey: i18n 标签 }
})
const emit = defineEmits([
  'batch-connect', 'bulk-edit', 'export', 'clear-checked',
  'toggle-custom', 'toggle-col-menu', 'set-hidden',
])
const { t } = useI18n()

// 列菜单：点击工具簇外部关闭（window mousedown）。Esc 未接线——不在本组件处理
// （全局 Esc 链归 ServerListView，未覆盖列菜单）
function onGlobalDownCloseColMenu(e) {
  if (props.colMenu && !e.target.closest?.('.table-tools')) emit('toggle-col-menu')
}
onMounted(() => window.addEventListener('mousedown', onGlobalDownCloseColMenu))
onBeforeUnmount(() => window.removeEventListener('mousedown', onGlobalDownCloseColMenu))
</script>

<template>
  <Teleport to="#crumb-actions">
    <!-- 批量条：勾选 ≥1 时出现；连接/批量编辑/导出 emit 到 ServerTable 转发父级执行 -->
    <div v-if="checkedCount" class="batch-bar">
      <span class="bb-count">{{ t('batch.selected', { n: checkedCount }) }}</span>
      <button class="bb-btn bb-primary" :title="t('batch.connectTitle')" @click="emit('batch-connect')">▶
        {{ t('batch.connect') }}</button>
      <!-- 批量编辑：emit 勾选 id 数组，抽屉批量模式由 ServerListView 打开 -->
      <button class="bb-btn" :title="t('batch.editTitle')" @click="emit('bulk-edit')">✎ {{ t('batch.edit')
      }}</button>
      <!-- 导出：emit 勾选 id 数组，blob 下载（含 403 二次验证提示）由 ServerListView 执行 -->
      <button class="bb-btn" :title="t('batch.exportTitle')" @click="emit('export')">⤓ {{
        t('batch.export') }}</button>
      <button class="bb-x" :title="t('batch.clear')" @click="emit('clear-checked')">✕</button>
    </div>
    <!-- ≡ = 自定义顺序模式开关（开启后行可拖拽重排）；
         ▦ = 列菜单（显隐 + 列宽说明），下拉以本簇为锚向下展开 -->
    <div class="table-tools">
      <button class="tt-btn" :class="{ active: isCustom }" :title="t('list.customOrder')"
        @click="emit('toggle-custom')">≡</button>
      <button class="tt-btn" :class="{ active: colMenu }" :title="t('cols.menu')" @click="emit('toggle-col-menu')">▦</button>
      <div v-if="colMenu" class="col-menu">
        <label v-for="k in HIDEABLE_COLS" :key="k" class="col-item">
          <input type="checkbox" :checked="!hiddenCols[k]"
            @change="emit('set-hidden', k, $event.target.checked ? false : true)" />
          <span>{{ colLabels[k] }}</span>
        </label>
        <label class="col-item col-item-fixed" :title="t('cols.fixed')">
          <input type="checkbox" checked disabled />
          <span>{{ t('col.status') }} · {{ t('col.tags') }} · {{ t('col.actions') }}</span>
        </label>
        <div class="col-hint">{{ t('cols.hint') }}</div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
/* 批量操作条：与面包屑/工具簇同行内联——不自带整条背景/边框（宿主行自有 34px 高与底边线） */
.batch-bar {
  display: flex;
  align-items: center;
  gap: 8px;
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

/* 表头工具簇：与批量条同宿面包屑行右侧，正常流内联排布；
   relative 仅作 ▦ 下拉（.col-menu）的定位锚点 */
.table-tools {
  position: relative;
  display: flex;
  align-items: center;
  gap: 4px;
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

/* 列菜单：工具簇 ▦ 下拉。以 .table-tools 为定位基准，整块悬于其下方 3px，右缘对齐；
   z 高于表格 sticky 表头（同根堆叠上下文），面包屑行无 overflow 裁剪，
   跨行悬于表头上方完整可见 */
.col-menu {
  position: absolute;
  top: calc(100% + 3px);
  right: 0;
  z-index: 30;
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 220px;
  padding: 6px;
  border: 1px solid var(--border-strong);
  border-radius: 8px;
  background: var(--bg-elevated);
  box-shadow: 0 6px 24px rgb(0 0 0 / 25%);
}

.col-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 6px;
  border-radius: 5px;
  color: var(--text-2);
  font-size: 12.5px;
  cursor: pointer;
}

.col-item:hover {
  background: var(--bg-hover);
}

.col-item input {
  accent-color: var(--accent);
}

.col-item-fixed {
  color: var(--text-4);
  cursor: default;
}

.col-item-fixed:hover {
  background: transparent;
}

.col-hint {
  margin-top: 4px;
  padding: 4px 6px 0;
  border-top: 1px solid var(--border);
  color: var(--text-4);
  font-size: 11px;
}
</style>
