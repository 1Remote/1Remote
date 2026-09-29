<script setup>
/**
 * 凭据库下拉（Plan 2 Task 9）：InheritedCredentialName 的选择器。
 *  - 远程选项：GET /api/credentials/names?ds=<当前数据源>（WPF 凭据库按数据源隔离），
 *    ds prop 变化时重拉；
 *  - 手动输入逃生口：n-select 的 filterable + tag——naive 的 tag 语义（非 multiple、非 remote）
 *    在输入无匹配项时按回车即以输入原文为值选中（Select.mjs handlePatternInput 的
 *    beingCreated 选项），不必另做「手动输入」切换态；
 *  - 清空（clearable）= ''：InheritedCredentialName 空串 = 手动输入凭据
 *    （与 WPF 下拉「（手动）」语义一致）；
 *  - 现值不在选项中（凭据已被删除等）：把现值补进选项呈现原文，不静默清除——
 *    用户看得见才会去改它。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'

const props = defineProps({
  /** 当前凭据名（json 的 InheritedCredentialName），空串 = 手动输入 */
  modelValue: { type: String, default: '' },
  /** 凭据库所属数据源（选项按数据源隔离）；空串时按 Local 请求 */
  dataSourceName: { type: String, default: '' },
  disabled: { type: Boolean, default: false },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()
const message = useMessage()

const names = ref([])
const loading = ref(false)
let loadSeq = 0 // 乱序完成保护（round17）：ds 快速连选时两个请求并发，慢的后到会用
// 旧数据源的名字列表覆盖新值——序号落后者整体丢弃

async function loadNames() {
  const my = ++loadSeq
  loading.value = true
  try {
    const resp = await api.credentialNames(props.dataSourceName || 'Local')
    if (my !== loadSeq) return
    names.value = Array.isArray(resp?.names) ? resp.names : []
  } catch (e) {
    if (my !== loadSeq) return
    // 404=未知数据源；加载失败保持空选项（手动输入仍可用），不打断表单
    console.warn('[CredentialPicker] load names failed:', e?.message || e)
    names.value = []
    message.error(t('editor.credLoadFailed'))
  } finally {
    if (my === loadSeq) loading.value = false
  }
}
onMounted(loadNames)
watch(() => props.dataSourceName, loadNames)

const options = computed(() => {
  const list = names.value.map((n) => ({ label: n, value: n }))
  const cur = props.modelValue
  if (cur && !names.value.includes(cur)) list.push({ label: cur, value: cur })
  return list
})

// 空串 → undefined（n-select 置空显示 placeholder）；清空回调把 null 归一为空串
const value = computed(() => (props.modelValue === '' || props.modelValue == null ? undefined : props.modelValue))
function onUpdate(v) {
  emit('update:modelValue', v ?? '')
}
</script>

<template>
  <div class="cred-picker">
    <n-select
      size="small"
      :value="value"
      :options="options"
      :loading="loading"
      :disabled="disabled"
      filterable
      tag
      clearable
      :placeholder="t('editor.credSelectHint')"
      @update:value="onUpdate"
    />
    <!-- H17：全新用户凭据库必然为空——空下拉无出路（无说明、无创建入口指引），
         创建入口在 设置→凭据库（距此三层操作）。加载中/已有现值不提示 -->
    <div v-if="!loading && !names.length && !value" class="cred-empty-hint">{{ t('editor.credEmptyHint') }}</div>
  </div>
</template>

<style scoped>
/* H17：空凭据库指引——弱化小字置于下拉正下方（FormField 控件列内），不抢焦点 */
.cred-picker {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.cred-empty-hint {
  color: var(--text-4);
  font-size: var(--fs-caption);
  line-height: 1.5;
}
</style>
