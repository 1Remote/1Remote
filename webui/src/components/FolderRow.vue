<script setup>
// 文件夹行（列表内子文件夹入口）：36px flex 行，列宽消费父级 ServerTable 下发的 --c-* CSS
// 变量（.cell 样式 scoped 于本组件，与 ServerRow 各自持一份，兜底值同源）。
// 交互：双击=进入；右键=新建子文件夹/重命名/删除（菜单浮层归 ServerTable，与 SideTree
// 树右键同一菜单集）；拖服务器入内=移动进去——是否接受 drop 由父级判定（dragover/drop
// 原事件透传，preventDefault 在父级命中判定内完成）；本行自身可拖（数据源可写时）=
// 拖动整个子树移动到别处（dragstart/dragend 同样透传，分发与落点判定全归父级，
// 与 ServerRow 拖拽同模式）。
// 勾选复选框=选中该文件夹全部子孙服务器（含子文件夹深处）：三态（全选/半选/未选）由
// 父级按「子孙 id ∩ checked」派生传入，本组件只渲染与上抛 toggle——勾选集合始终是服务器
// id 集（父级 checked），文件夹不占 id。
import { ref, watchEffect } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({
  folder: { type: Object, required: true }, // { name, path, dsName, count }
  showDs: { type: Boolean, default: false }, // 「全部数据」根视图：名称旁前缀数据源名（跨库同名文件夹区分）
  dropActive: { type: Boolean, default: false }, // 拖拽悬停高亮（父级 dropFolder 命中本行时置真）
  checkState: { type: Object, default: null }, // { checked, indeterminate, count } | null（无子孙=禁用未选）
  writable: { type: Boolean, default: true }, // 数据源可写：只读源禁用 重命名/删除 按钮（title 提示）
})
const emit = defineEmits([
  'open',
  'context',
  'dragstart',
  'dragover',
  'dragleave',
  'drop',
  'dragend',
  'toggle-check',
  'rename',
  'delete',
])
const { t } = useI18n()

// 三态复选框：indeterminate 无对应 HTML 属性、须写 DOM 属性（与 ServerTable 表头全选同款）；
// 虚拟滚动下本组件逐行实例化，ref 只指向本行复选框
const cbEl = ref(null)
watchEffect(() => {
  if (cbEl.value) cbEl.value.indeterminate = !!props.checkState?.indeterminate
})
// 空（虚拟）文件夹：复选框可用——勾选的是「文件夹本身」（checkedFolders，批量删除
// 空文件夹的通道，owner 2026-09-21 第二轮反馈）；有子孙时仍为级联勾选服务器
</script>

<template>
  <div
    class="row frow"
    :class="{ 'drop-into': dropActive }"
    :title="folder.path"
    @dblclick="emit('open', folder)"
    @contextmenu.prevent="emit('context', { folder, x: $event.clientX, y: $event.clientY })"
    @dragstart="emit('dragstart', $event)"
    @dragover="emit('dragover', $event)"
    @dragleave="emit('dragleave')"
    @drop="emit('drop', $event)"
    @dragend="emit('dragend')"
  >
    <!-- 双击进入的识别区域不含本列（与 ServerRow .cell-check 同款排除） -->
    <div class="cell cell-check" @dblclick.stop>
      <input
        ref="cbEl"
        type="checkbox"
        class="cb"
        :checked="!!checkState?.checked"
        :title="checkState?.count ? t('row.selectFolder') : t('row.selectEmptyFolder')"
        @click.stop
        @dblclick.stop
        @change="emit('toggle-check')"
      />
    </div>
    <div class="cell cell-status"></div>
    <div class="cell cell-name f-name">
      <span class="f-icon">📁</span>
      <span class="name">{{ folder.name }}</span>
      <span v-if="showDs" class="f-ds">{{ folder.dsName }}</span>
    </div>
    <div class="cell cell-count">{{ t('folder.contains', { n: folder.count }) }}</div>
    <!-- 操作列：与 ServerRow 同款常显小按钮（✎ 重命名 / ✕ 删除，叉图标与批量删除一致），接父级 folderOps；
         只读数据源禁用（title 说明）。点击不冒泡到行级点击/双击 -->
    <div class="cell cell-act" @click.stop @dblclick.stop>
      <button
        class="act"
        :title="writable ? t('tree.renameFolder') : t('cv.readOnly')"
        :disabled="!writable"
        @click="emit('rename', folder)"
      >
        ✎
      </button>
      <button
        class="act"
        :title="writable ? t('tree.deleteFolder') : t('cv.readOnly')"
        :disabled="!writable"
        @click="emit('delete', folder)"
      >
        ✕
      </button>
    </div>
  </div>
</template>

<style scoped>
/* 文件夹行：列对齐复用 --c-* 变量（ServerRow 的 .cell 样式 scoped 于彼组件，此处自带一份）；
   双击=进入、右键=新建文件夹、拖服务器入内=移动、可写数据源下行自身可拖（子树移动） */
.frow {
  display: flex;
  align-items: center;
  height: 36px;
  padding: 0 10px 0 0;
  border-bottom: 1px solid var(--border);
  user-select: none;
  cursor: default;
}

.frow[draggable='true'] {
  cursor: grab;
}

.frow:hover {
  background: var(--bg-hover);
}

.frow.drop-into {
  background: var(--accent-container);
  outline: 1px dashed var(--accent-focus); /* 拖入虚线轮廓同为状态指示（G6），与树侧同源 */
  outline-offset: -1px;
}

.frow .cell {
  display: flex;
  align-items: center;
  min-width: 0;
}

.frow .cell-check {
  flex: 0 0 var(--c-check, 30px);
  justify-content: center; /* 复选框居中于列内（与 ServerRow .cell-check 同款） */
  padding-right: 0;
}

.frow .cell-status {
  flex: 0 0 var(--c-status, 42px);
}

.frow .cell-name {
  flex: 1 1 0;
  gap: 8px;
  min-width: 0;
}

.frow .f-icon {
  flex: 0 0 22px;
  text-align: center;
  font-size: var(--fs-title);
}

.frow .name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-1);
  font-size: var(--fs-body);
}

.frow .f-ds {
  flex: 0 0 auto;
  margin-left: 6px;
  color: var(--text-4);
  font-size: var(--fs-caption);
}

.frow .cell-count {
  flex: 0 0 auto;
  margin-left: auto;
  padding-right: 10px;
  color: var(--text-4);
  font-size: var(--fs-caption);
  white-space: nowrap;
}

/* 操作列：消费父级 --c-act 与 ServerRow 同宽，按钮同款常显（hover 浮现降低可发现性） */
.frow .cell-act {
  flex: 0 0 var(--c-act, 100px);
  gap: 2px;
  justify-content: flex-end;
  padding-right: 0;
}

.act {
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  width: var(--ctrl-h-s);
  height: var(--ctrl-h-s);
  border-radius: var(--radius-ctrl);
  cursor: pointer;
}

.act:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-1);
}

.act:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}

/* 文件夹勾选复选框：强调色随主题；空（虚拟）文件夹禁用置灰 */
.cb {
  accent-color: var(--accent);
}
.cb:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
</style>
