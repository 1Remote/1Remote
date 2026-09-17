<script setup>
// 文件夹行（列表内子文件夹入口）：36px flex 行，列宽消费父级 ServerTable 下发的 --c-* CSS
// 变量（.cell 样式 scoped 于本组件，与 ServerRow 各自持一份，兜底值同源）。
// 交互：双击=进入；右键=在该文件夹内新建子文件夹（菜单浮层归 ServerTable）；
// 拖服务器入内=移动进去——是否接受 drop 由父级判定（dragover/drop 原事件透传，
// preventDefault 在父级命中判定内完成）。
import { useI18n } from 'vue-i18n'

defineProps({
  folder: { type: Object, required: true }, // { name, path, dsName, count }
  showDs: { type: Boolean, default: false }, // 「全部数据」根视图：名称旁前缀数据源名（跨库同名文件夹区分）
  dropActive: { type: Boolean, default: false }, // 拖拽悬停高亮（父级 dropFolder 命中本行时置真）
})
const emit = defineEmits(['open', 'context', 'dragover', 'dragleave', 'drop'])
const { t } = useI18n()
</script>

<template>
  <div class="row frow" :class="{ 'drop-into': dropActive }" :title="folder.path"
    @dblclick="emit('open', folder)"
    @contextmenu.prevent="emit('context', { folder, x: $event.clientX, y: $event.clientY })"
    @dragover="emit('dragover', $event)" @dragleave="emit('dragleave')" @drop="emit('drop', $event)">
    <div class="cell cell-check"></div>
    <div class="cell cell-status"></div>
    <div class="cell cell-name f-name">
      <span class="f-icon">📁</span>
      <span class="name">{{ folder.name }}</span>
      <span v-if="showDs" class="f-ds">{{ folder.dsName }}</span>
    </div>
    <div class="cell cell-count">{{ t('crumb.count', { n: folder.count }) }}</div>
  </div>
</template>

<style scoped>
/* 文件夹行：列对齐复用 --c-* 变量（ServerRow 的 .cell 样式 scoped 于彼组件，此处自带一份）；
   双击=进入、右键=新建文件夹、拖服务器入内=移动 */
.frow {
  display: flex;
  align-items: center;
  height: 36px;
  padding: 0 10px 0 0;
  border-bottom: 1px solid var(--border);
  user-select: none;
  cursor: default;
}

.frow:hover {
  background: var(--bg-hover);
}

.frow.drop-into {
  background: var(--accent-container);
  outline: 1px dashed var(--accent);
  outline-offset: -1px;
}

.frow .cell {
  display: flex;
  align-items: center;
  min-width: 0;
}

.frow .cell-check {
  flex: 0 0 var(--c-check, 30px);
}

.frow .cell-status {
  flex: 0 0 var(--c-status, 58px);
}

.frow .cell-name {
  flex: 1 1 0;
  gap: 8px;
  min-width: 0;
}

.frow .f-icon {
  flex: 0 0 22px;
  text-align: center;
  font-size: 14px;
}

.frow .name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-1);
  font-size: 12.5px;
}

.frow .f-ds {
  flex: 0 0 auto;
  margin-left: 6px;
  color: var(--text-4);
  font-size: 11px;
}

.frow .cell-count {
  flex: 0 0 auto;
  margin-left: auto;
  padding-right: 10px;
  color: var(--text-4);
  font-size: 11.5px;
  white-space: nowrap;
}
</style>
