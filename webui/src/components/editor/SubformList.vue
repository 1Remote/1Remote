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
 * ArgumentList 的 Value 按描述符统一渲染为文本框：WPF 按 Type 逐行切换渲染
 * （Secret=密码框/Flag=勾选/Selection=下拉，见 ArgumentListControl.xaml:126-145），
 * 静态 schema 无法按行内另一字段取值切换控件类型，掩码/控件特化留作 Plan 3+ 润色
 * （schemas.js appArgumentListField 注释同述；值语义如 Flag 存 "1"/"" 不受影响）。
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import FormField from './FormField.vue'

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

function addRow() {
  emit('update:modelValue', [...rows.value, { ...props.rowDefaults }])
}

function removeRow(index) {
  const next = rows.value.slice()
  next.splice(index, 1)
  emit('update:modelValue', next)
}

function updateRowField(index, key, value) {
  emit(
    'update:modelValue',
    rows.value.map((row, i) => (i === index ? { ...row, [key]: value } : row)),
  )
}

const keyOf = (row, i) => (props.rowKey ? props.rowKey(row, i) : i)
</script>

<template>
  <div class="subform-list">
    <div v-for="(row, i) in rows" :key="keyOf(row, i)" class="sf-row">
      <div class="sf-row-head">
        <span class="sf-row-title">#{{ i + 1 }}</span>
        <button class="sf-del" type="button" :title="t('editor.removeRow')" @click="removeRow(i)">✕</button>
      </div>
      <div class="sf-row-body">
        <FormField
          v-for="f in fields"
          :key="f.key"
          :field="f"
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
  border-radius: 6px;
  background: var(--bg-elevated);
  padding: 8px 10px;
}
.sf-row-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}
.sf-row-title {
  font-size: 11px;
  color: var(--text-4);
}
.sf-del {
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--text-4);
  font-size: 10px;
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
}
.sf-add {
  align-self: flex-start;
  border: 1px dashed var(--border-strong);
  border-radius: 5px;
  background: transparent;
  color: var(--text-3);
  font-size: 12px;
  line-height: 1;
  padding: 6px 12px;
  cursor: pointer;
}
.sf-add:hover {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}
</style>
