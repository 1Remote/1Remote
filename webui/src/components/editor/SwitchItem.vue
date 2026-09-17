<script setup>
/**
 * 单个开关项（fix-batch5 Task A #3，从 FormField 的 SWITCH 分支拆出）：渲染
 * [n-switch][6px][描述文字] 的最小单元，FormField 的单字段开关行与 EditorDrawer 的
 * 连续开关聚合行（.ed-switch-row）共用——两处的开关/文字间距、字号、颜色保持一致。
 * 纯展示组件：值由父级绑定（json 值可能为 null——显示按 false，写回真实布尔），
 * 只 emit update:modelValue；描述文字 switchTextKey 优先、缺失回退 labelKey
 * （语义与 FormField 原实现相同），宽度不设限由外层决定（单字段行内被 FormField 的
 * .ff-control > :deep(*) 100% 规则撑满、文字 flex 填充；聚合行内按内容收缩换行）。
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({
  /** @type {FieldDescriptor} 字段描述符（fieldTypes.js；只消费 key/labelKey/switchTextKey） */
  field: { type: Object, required: true },
  /** json 中 field.key 处的当前值（布尔或 null：null 显示按 false） */
  modelValue: { type: null, default: null },
  disabled: { type: Boolean, default: false },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()

const label = computed(() => (props.field.labelKey ? t(props.field.labelKey) : props.field.key))
const switchText = computed(() => (props.field.switchTextKey ? t(props.field.switchTextKey) : label.value))
</script>

<template>
  <div class="sw-item">
    <n-switch
      size="small"
      :value="!!modelValue"
      :disabled="disabled"
      @update:value="emit('update:modelValue', $event)"
    />
    <span class="sw-item-text">{{ switchText }}</span>
  </div>
</template>

<style scoped>
/* 项内 [开关][6px][文字]；文字与 FormField 原 .ff-switch-text 同款（字号/行高/颜色） */
.sw-item {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.sw-item-text {
  flex: 1 1 auto;
  min-width: 0;
  font-size: 12.5px;
  line-height: 1.4;
  color: var(--text-2);
  overflow-wrap: break-word; /* 无空格长词（如德语复合词）在窄聚合行内不溢出 */
}
</style>
