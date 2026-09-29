<script setup>
/**
 * 带候选的输入框（round11 U21 统一控件）：n-auto-complete 的标准行为封装。
 * 此前 FormField（AUTOCOMPLETE 型，Serial 端口/波特率）与 KeyValueLines（RDP 额外
 * 指令的 103 项候选）各持一份"恒全量、不随输入过滤、聚焦即铺开"的配置——长列表
 * 场景（U21：一次弹出上百项）肉眼找项负担大，且两处行为分叉。收敛为单一控件：
 * ① 候选随输入过滤（大小写不敏感子串；输入为空 = 全部候选，与"聚焦即见全部"
 *    等价——少量候选的旧体验不变，候选仍是建议不约束取值，任意键入原样透传）；
 * ② 输入过程中按 Esc 取消候选显示（本输入框内吞掉一次 Esc，不再冒泡到外层
 *    Esc 链——EditorDrawer 返回导航等不被误触；再次输入/重新聚焦恢复候选）；
 * ③ 值语义纯受控：只 emit update:value，不读不写父级状态。
 * 消费方：FormField（AUTOCOMPLETE）/ KeyValueLines（名称列）/ KvPairsField（值列）。
 */
import { computed, ref } from 'vue'

const props = defineProps({
  /** 当前输入值（字符串；受控） */
  value: { type: String, default: '' },
  /** 候选清单（字符串数组，原样——过滤在本控件内做） */
  suggestions: { type: Array, default: () => [] },
  placeholder: { type: String, default: undefined },
  disabled: { type: Boolean, default: false },
  /** 行内校验红（KvPairsField 半填行等场景透传为错误态） */
  invalid: { type: Boolean, default: false },
})
const emit = defineEmits(['update:value'])

// Esc 后抑制候选显示，直到再次输入/聚焦（"取消输入过滤"的恢复语义）
const suppressed = ref(false)
const filtered = computed(() => {
  if (suppressed.value) return []
  const list = props.suggestions.map((s) => String(s))
  const q = String(props.value ?? '').toLowerCase()
  return q ? list.filter((s) => s.toLowerCase().includes(q)) : list
})

function onInput(v) {
  suppressed.value = false
  emit('update:value', v ?? '')
}
function onFocus() {
  suppressed.value = false
}
function onKeydown(e) {
  // 有候选在显示时 Esc 只关候选（吞掉一次，不冒泡到外层 Esc 链）；无候选时放行
  if (e.key === 'Escape' && !suppressed.value && filtered.value.length) {
    suppressed.value = true
    e.stopPropagation()
  }
}
</script>

<template>
  <n-auto-complete
    size="small"
    :value="value"
    :options="filtered"
    :placeholder="placeholder"
    :status="invalid ? 'error' : undefined"
    :disabled="disabled"
    :input-props="{ spellcheck: false }"
    :get-show="() => !suppressed"
    @update:value="onInput"
    @focus="onFocus"
    @keydown="onKeydown"
  />
</template>
