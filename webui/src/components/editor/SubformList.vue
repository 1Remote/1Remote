<script setup>
/**
 * 子表单列表渲染器（Plan 2 Task 7）：json 数组字段的行编辑——AlternateCredentials
 * （Credential[]）与 ArgumentList（AppArgument[]）共用。行 = 紧凑卡片，行内字段用
 * FormField 递归渲染（深度限 2：schema 约定 subform 行内字段不再嵌 SUBFORM 类型，
 * 若未来描述符嵌套更深需在此引入深度守卫——当前 9 协议 schema 不存在更深嵌套）。
 *
 * 不可变更新约定：所有变更都 emit 新数组/新行对象（浅拷贝），不原地改动 modelValue；
 * 行对象更新用展开复制而非按字段描述符重建——行内未列入 schema 的字段（如
 * AppArgument.Selections 字典）原样保留（schemas.js 的透传保真约定）。
 * 增删行之外无重排（拖拽排序归 Plan 4）。
 *
 * 行折叠：行默认折叠——表头只显示行名（Name 值，缺失 →
 *（未命名））+ 展开箭头 + 删除；点表头切换展开整行编辑；新增行自动展开。
 *
 * 行字段可见性（评审修复）：行体渲染前按 isVisible(f, row) 过滤——subform 行字段的
 * visibleWhen 求值数据源是行对象（如 Selections 依赖行内 Type ∈ Normal/Selection，
 * 对齐 WPF ArgumentEditView 的 SelectionsVisibility），不是抽屉的顶层 json。
 * 「隐藏≠删值」约定同顶层字段：不渲染仅不进 DOM，行对象上的值保留并随 json
 * 原样透传（Type 切回可见时值仍在）。
 *
 * ArgumentList 的 Value 按描述符统一渲染为文本框：WPF 按 Type 逐行切换渲染
 * （Secret=密码框/Flag=勾选/Selection=下拉，见 ArgumentListControl.xaml:126-145），
 * 静态 schema 无法按行内另一字段取值切换控件类型，掩码/控件特化留作 Plan 3+ 润色
 * （schemas.js appArgumentListField 注释同述；值语义如 Flag 存 "1"/"" 不受影响）。
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import FormField from './FormField.vue'
import { isVisible } from '../../editor/visibility.js'

const props = defineProps({
  /** @type {FieldDescriptor[]} 行字段描述符（subform.fields） */
  fields: { type: Array, default: () => [] },
  /** json 数组值；可能为 null/undefined → 按空表处理 */
  modelValue: { type: Array, default: null },
  /** 新行初值（subform.rowDefaults，如 AppArgument 的 Type:'Normal' 等） */
  rowDefaults: { type: Object, default: () => ({}) },
  /** credential 字段的凭据库数据源（透传给行内 FormField/CredentialPicker） */
  dataSourceName: { type: String, default: '' },
  /** 可选行 key 生成器 (row, index) => string；缺省用索引（无稳定业务键） */
  rowKey: { type: Function, default: null },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()

const rows = computed(() => (Array.isArray(props.modelValue) ? props.modelValue : []))

/**
 * 行折叠态：默认折叠——表头只显示行名 + 展开箭头 + 删除，
 * 点表头展开整行编辑器；新增行自动展开（用户刚创建，立即填写）。
 * 状态按行索引记录（keyOf 无稳定业务键时的同一口径）：删行后索引位移只影响
 * 折叠展示态（瞬态 UI 状态），不影响值；属可接受的简化。
 */
const expandedRows = ref(new Set())
const isExpanded = (i) => expandedRows.value.has(i)
function toggleRow(i) {
  if (expandedRows.value.has(i)) expandedRows.value.delete(i)
  else expandedRows.value.add(i)
}

/**
 * 折叠表头的行名：取行内 Name 字段值（AlternateCredentials 与 ArgumentList 均有），
 * 无 Name 字段的 schema 回退首字段值；空值 →（未命名）。
 * （任务描述按 fields[0]，此处优先 Name：ArgumentList 的 fields[0] 是 Type（'Normal' 等
 * 技术字面量），作行名无辨识度——AlternateCredentials 的 fields[0] 恰为 Name，结果不变。）
 */
const titleKey = computed(() => (props.fields.some((f) => f.key === 'Name') ? 'Name' : props.fields[0]?.key))
function rowTitle(row) {
  const v = row?.[titleKey.value]
  return v != null && String(v).trim() !== '' ? String(v) : t('editor.unnamedRow')
}

function addRow() {
  expandedRows.value.add(rows.value.length) // 新行索引 = 当前行数（追加位），自动展开
  emit('update:modelValue', [...rows.value, { ...props.rowDefaults }])
}

function removeRow(index) {
  const next = rows.value.slice()
  next.splice(index, 1)
  // 折叠态按索引重排：删除行之后的展开标记随索引前移，保持已展开的行仍展开
  const shifted = new Set()
  for (const i of expandedRows.value) shifted.add(i > index ? i - 1 : i)
  expandedRows.value = shifted
  emit('update:modelValue', next)
}

function updateRowField(index, key, value) {
  emit(
    'update:modelValue',
    rows.value.map((row, i) => (i === index ? { ...row, [key]: value } : row))
  )
}

const keyOf = (row, i) => (props.rowKey ? props.rowKey(row, i) : i)

/**
 * 行体渲染的字段集：visibleWhen 以行对象为求值源过滤（见文件头注释——依赖的是
 * 行内字段如 Type，不是顶层 json；「隐藏≠删值」，未渲染字段的值随行对象透传）。
 * @param {Object} row 当前行（json 域）
 */
function visibleRowFields(row) {
  return props.fields.filter((f) => isVisible(f, row))
}

/**
 * 行字段的"浏览…"按钮条件：filePickWhen 以行对象求值——
 * 数据源与语义同 visibleWhen（借道 isVisible 的条件求值器传入）。不满足时返回
 * 剥离 filePick 的浅拷贝（勿改 schema 常量——描述符是模块级共享对象），满足时
 * 原样透传。当前唯一消费方：ArgumentList 的 Value（仅 Type=File 的行带按钮，
 * 对齐 WPF ArgumentFile 模板，ArgumentListControl.xaml:90-106）。
 * @param {Object} row 当前行（json 域）
 * @param {FieldDescriptor} f 行字段描述符
 */
function rowFieldFor(row, f) {
  if (!f.filePick || !f.filePickWhen) return f
  return isVisible({ visibleWhen: f.filePickWhen }, row) ? f : { ...f, filePick: undefined }
}
</script>

<template>
  <div class="subform-list">
    <div v-for="(row, i) in rows" :key="keyOf(row, i)" class="sf-row" :class="{ collapsed: !isExpanded(i) }">
      <!-- 表头：默认折叠只显示 行名 + 展开箭头 + 删除；点表头任意处切换展开 -->
      <div class="sf-row-head" role="button" :aria-expanded="isExpanded(i)" @click="toggleRow(i)">
        <span class="sf-chev" aria-hidden="true">{{ isExpanded(i) ? '▾' : '▸' }}</span>
        <span class="sf-row-title" :title="rowTitle(row)">{{ rowTitle(row) }}</span>
        <button class="sf-del" type="button" :title="t('editor.removeRow')" @click.stop="removeRow(i)">✕</button>
      </div>
      <div v-if="isExpanded(i)" class="sf-row-body">
        <FormField
          v-for="f in visibleRowFields(row)"
          :key="f.key"
          :field="rowFieldFor(row, f)"
          :model-value="row[f.key]"
          :data-source-name="dataSourceName"
          @update:model-value="(v) => updateRowField(i, f.key, v)"
        />
      </div>
    </div>
    <button class="sf-add" type="button" @click="addRow">+ {{ t('editor.addRow') }}</button>
  </div>
</template>

<style scoped>
/* 行卡片：紧凑（抽屉内嵌于字段行控件列），主题变量取色 */
.subform-list {
  width: 100%;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.sf-row {
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  padding: 8px 10px;
}
/* 折叠行：只剩表头一行，内距收紧 */
.sf-row.collapsed {
  padding: 4px 10px;
}
.sf-row-head {
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  user-select: none;
  padding: 1px 0;
}
.sf-row-head:hover .sf-row-title {
  color: var(--text-1);
}
/* 展开箭头：折叠 ▸ / 展开 ▾，随状态切换（无需 i18n 的纯方向指示） */
.sf-chev {
  flex: 0 0 auto;
  color: var(--text-4);
  font-size: var(--fs-micro);
  line-height: 1;
}
.sf-row-title {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--fs-body);
  font-weight: 500;
  color: var(--text-2);
}
.sf-del {
  flex: 0 0 auto;
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--text-4);
  font-size: var(--fs-micro);
  line-height: 1;
  padding: 3px;
  cursor: pointer;
}
.sf-del:hover {
  background: var(--bg-hover);
  color: var(--danger);
}
.sf-row-body {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 8px;
}
.sf-add {
  align-self: flex-start;
  border: 1px dashed var(--border-strong);
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 5px 12px;
  cursor: pointer;
}
.sf-add:hover {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}
</style>
