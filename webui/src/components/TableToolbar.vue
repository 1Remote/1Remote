<script setup>
// 面包屑行右侧工具簇：整块 Teleport 至 ServerListView 的 #crumb-actions 容器（目标容器
// 在骨架屏/空态 v-if 链之外恒存在，先于本组件挂载可用；内容随本组件卸载一并消失，
// 即只在表格可见时出现）。布局与宿主行内联，不自带整条背景/边框。
// 组成：批量条（勾选 ≥1 时）+ ≡ 自定义顺序开关 + ▦ 列菜单。
// 状态（勾选/排序模式/列显隐）全部留在 ServerTable，本组件纯展示 + emit 转发。
import { onBeforeUnmount, onMounted, ref } from 'vue'
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
  'batch-connect',
  'batch-delete',
  'bulk-edit',
  'export',
  'clear-checked',
  'toggle-custom',
  'toggle-col-menu',
  'set-hidden',
])
const { t } = useI18n()

// 列菜单：点击工具簇外部关闭（window mousedown）。Esc 关闭经 ServerListView 全局 Esc 链
//（closeColMenuIfOpen 分支——菜单开时 Esc 只关菜单，不清勾选/搜索/光标）
function onGlobalDownCloseColMenu(e) {
  if (props.colMenu && !e.target.closest?.('.table-tools')) emit('toggle-col-menu')
}
onMounted(() => window.addEventListener('mousedown', onGlobalDownCloseColMenu))
onBeforeUnmount(() => window.removeEventListener('mousedown', onGlobalDownCloseColMenu))

// 常驻提示轮换（无勾选时）：双击连接 / Ctrl·Shift 多选各占一半时间，6s 慢速交替——
// 多选入口（Ctrl+点击/Shift+点击）没有其他常驻可查处，一次性 toast 已按 owner 要求
// 移除（46d82931），此处是唯一的常驻发现位；轮换而非并列，避免提示行变宽挤压工具簇
const HINT_KEYS = ['list.doubleClickHint', 'list.multiSelectHint']
const hintIdx = ref(0)
let hintTimer = null
onMounted(() => {
  hintTimer = setInterval(() => {
    hintIdx.value = (hintIdx.value + 1) % HINT_KEYS.length
  }, 6000)
})
onBeforeUnmount(() => clearInterval(hintTimer))
</script>

<template>
  <Teleport to="#crumb-actions">
    <!-- 批量条：勾选 ≥1 时出现；连接/批量编辑/导出 emit 到 ServerTable 转发父级执行 -->
    <div v-if="checkedCount" class="batch-bar">
      <span class="bb-count">{{ t('batch.selected', { n: checkedCount }) }}</span>
      <button class="bb-btn bb-primary" :title="t('batch.connectTitle')" @click="emit('batch-connect')">
        ▶ {{ t('batch.connect') }}
      </button>
      <!-- 批量编辑：emit 勾选 id 数组；恰勾 1 台时按钮显「编辑」（ctx.edit，14 语言有译），
           抽屉转单台编辑由 ServerListView.openBulkEdit 判 1 台分流，>1 台才进 bulk 模式 -->
      <button
        class="bb-btn"
        :title="checkedCount === 1 ? t('batch.editSingleTitle') : t('batch.editTitle', { n: checkedCount })"
        @click="emit('bulk-edit')"
      >
        ✎ {{ checkedCount === 1 ? t('ctx.edit') : t('batch.edit') }}
      </button>
      <!-- 导出：emit 勾选 id 数组，blob 下载（含 403 二次验证提示）由 ServerListView 执行 -->
      <button class="bb-btn" :title="t('batch.exportTitle')" @click="emit('export')">⤓ {{ t('batch.export') }}</button>
      <!-- 删除：danger 样式与连接主按钮相区分；确认对话框（含逐台删除进度）由 ServerListView 执行 -->
      <button class="bb-btn bb-danger" :title="t('batch.deleteTitle')" @click="emit('batch-delete')">
        🗑 {{ t('batch.delete') }}
      </button>
      <button class="bb-x" :title="t('batch.clear')" @click="emit('clear-checked')">✕</button>
    </div>
    <!-- 无勾选时的常驻轻提示（--text-4 小字，克制）：双击连接与 Ctrl/Shift 多选两条
         6s 轮换（发现位唯一化，见 script）；有勾选时让位给批量条（同位置互斥，不叠加噪音） -->
    <Transition name="hint-fade" mode="out-in">
      <span v-if="!checkedCount" :key="HINT_KEYS[hintIdx]" class="tt-hint">{{ t(HINT_KEYS[hintIdx]) }}</span>
    </Transition>
    <!-- ≡ = 自定义顺序模式开关（开启后行可拖拽重排）；
         ▦ = 列菜单（显隐 + 列宽说明），下拉以本簇为锚向下展开 -->
    <div class="table-tools">
      <button
        class="tt-btn"
        :class="{ active: isCustom }"
        :title="t('list.customOrder')"
        @click="emit('toggle-custom')"
      >
        ≡
      </button>
      <button class="tt-btn" :class="{ active: colMenu }" :title="t('cols.menu')" @click="emit('toggle-col-menu')">
        ▦
      </button>
      <div v-if="colMenu" class="col-menu">
        <label v-for="k in HIDEABLE_COLS" :key="k" class="col-item">
          <input
            type="checkbox"
            :checked="!hiddenCols[k]"
            @change="emit('set-hidden', k, $event.target.checked ? false : true)"
          />
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
/* 批量操作条：与面包屑/工具簇同行内联——不自带整条背景/边框（宿主行自有 36px 高与底边线）；
   首次出现（勾选 0→N）滑入 + 淡入，让"批量操作来了"有可感知的入场 */
.batch-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  animation: bb-in 0.18s ease-out;
}

@keyframes bb-in {
  from {
    opacity: 0;
    transform: translateY(-6px);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .batch-bar {
    animation: none;
  }
}

.bb-count {
  font-size: var(--fs-body);
  color: var(--text-2);
}

.bb-btn {
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-2);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 5px 10px;
  cursor: pointer;
}

.bb-btn:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
}

.bb-btn:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}

.bb-primary {
  border-color: var(--accent);
  color: var(--accent-text);
}

/* 主按钮 hover 守卫（E3，对齐 EditorDrawer .ed-primary）：保住 accent 边框——
   否则上方 .bb-btn:hover:not(:disabled)（特异性更高）会把边框退化成 --border-strong */
.bb-primary:hover:not(:disabled) {
  border-color: var(--accent);
  background: var(--bg-hover);
  color: var(--accent-text);
}

/* 删除按钮：danger 描边/文字与其它批量动作相区分（悬停加重底色） */
.bb-danger {
  border-color: var(--danger);
  color: var(--danger);
}

.bb-danger:hover:not(:disabled) {
  background: var(--danger);
  color: var(--text-on-accent);
}

.bb-x {
  margin-left: auto;
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  width: var(--ctrl-h-s);
  height: var(--ctrl-h-s);
  border-radius: var(--radius-ctrl);
  cursor: pointer;
}

.bb-x:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}

/* 无勾选轻提示：右侧工具簇左侧同行，小字弱化（常驻但克制——不弹 toast 打扰）；
   轮换切换用 0.4s 交叉淡入淡出（reduced-motion 时关闭过渡，文字仍切换） */
.tt-hint {
  color: var(--text-4);
  font-size: var(--fs-caption);
  white-space: nowrap;
}
.hint-fade-enter-active,
.hint-fade-leave-active {
  transition: opacity 0.4s ease;
}
.hint-fade-enter-from,
.hint-fade-leave-to {
  opacity: 0;
}
@media (prefers-reduced-motion: reduce) {
  .hint-fade-enter-active,
  .hint-fade-leave-active {
    transition: none;
  }
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
  height: var(--ctrl-h-s);
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-panel);
  color: var(--text-3);
  font-size: var(--fs-body);
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
  border-radius: var(--radius-box);
  background: var(--bg-elevated);
  box-shadow: var(--shadow-menu);
}

.col-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 6px;
  border-radius: var(--radius-ctrl);
  color: var(--text-2);
  font-size: var(--fs-body);
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
  font-size: var(--fs-caption);
}
</style>
