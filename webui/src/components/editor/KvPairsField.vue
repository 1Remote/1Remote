<script setup>
/**
 * 键值对行编辑器（[{Key, Value}] PascalCase 数组）：行式 [名称][值][删行✕] +
 * 底部加行按钮——形态对齐 KeyValueLines/KvMapField（同款边框容器/加行按钮/
 * 等宽字体）。
 *
 * 当前消费方：RunnerCard 环境变量/特殊字符（WPF ExternalRunnerSettings 的
 * ListBox 行式编辑对齐——WPF 原版即 [Key TextBox][Value 宏补全][✕] 行编辑器，
 * web 此前用"每行 KEY=VALUE 文本域 + 保存前解析"偏离了 WPF 形态，解析丢行 +
 * 回读覆盖曾造成"输入到一半被清空"，round11 用户报告后改回行编辑器）。
 *
 * 数据语义（纯逻辑在 editor/kvPairs.js，含测试）：
 * - 全空行（名称与值均为空白）不进输出——加行后未输入不产生数据；
 * - 半填行（值非空、名称为空）随输出保留（自动保存上下文没有"阻止保存"的
 *   时机，静默丢弃＝U25 类缺陷）；名称框标红提示不生效；
 * - 值列可带候选（环境变量的宏补全，WPF AutoCompleteComboBox 对齐）：候选随
 *   输入过滤（大小写不敏感子串），无候选时退化为普通输入框。
 * - 回环挡板：emit 后的回声（同内容）不触发重新解析；外部真实变化（重载/
 *   winscp 旁路注入）合并进本地行态，本地半填行不被覆盖（半输入态不打断）。
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import SuggestInput from './SuggestInput.vue'
import { mergeExternal, pairsFromRows, rowIsHalf, samePairs } from '../../editor/kvPairs.js'

const props = defineProps({
  /** [{Key, Value}] PascalCase 数组（null/undefined 按空表处理） */
  modelValue: { type: null, default: null },
  /** 值列候选（如宏名清单）；空数组=值列为普通输入框 */
  suggestions: { type: Array, default: () => [] },
  disabled: { type: Boolean, default: false },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()

// ---- 本地行态与外部值同步（回环挡板 + 半填行合并，见文件头） ----
const rows = ref([])
let lastEmitted = null
watch(
  () => props.modelValue,
  (v) => {
    if (v != null && samePairs(v, lastEmitted)) return
    if (v == null && lastEmitted == null) return
    rows.value = mergeExternal(rows.value, v)
  },
  { immediate: true }
)
function commit() {
  lastEmitted = pairsFromRows(rows.value)
  emit('update:modelValue', lastEmitted)
}

function addRow() {
  rows.value.push({ key: '', value: '' })
  // 新行不入 json（全空行序列化丢弃）——commit 统一 emit 时机，值不因加空行变脏
  commit()
}
function removeRow(i) {
  rows.value.splice(i, 1)
  commit()
}
function updateRow(i, k, v) {
  rows.value[i][k] = v
  commit()
}
</script>

<template>
  <div class="kvp">
    <!-- 行列表：与 n-input 同观的边框容器（KvMapField.kvm-box 同款） -->
    <div class="kvp-box">
      <div v-for="(row, i) in rows" :key="i" class="kvp-row">
        <!-- 名称：半填行标红（值为空提示由 hint 行承载，此处仅状态色） -->
        <n-input
          class="kvp-key"
          size="small"
          :value="row.key"
          :status="rowIsHalf(row) ? 'error' : undefined"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          @update:value="updateRow(i, 'key', $event)"
        />
        <!-- 值：有候选=带候选输入框（SuggestInput，round11 U21 统一控件：随输入
             过滤 + Esc 取消候选），无候选=普通输入框 -->
        <SuggestInput
          v-if="suggestions.length"
          class="kvp-value"
          :value="row.value"
          :suggestions="suggestions"
          :disabled="disabled"
          @update:value="updateRow(i, 'value', $event)"
        />
        <n-input
          v-else
          class="kvp-value"
          size="small"
          :value="row.value"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          @update:value="updateRow(i, 'value', $event)"
        />
        <button class="kvp-del" type="button" :title="t('editor.removeRow')" :disabled="disabled" @click="removeRow(i)">
          ✕
        </button>
      </div>
    </div>
    <button class="dashed-add" type="button" :disabled="disabled" @click="addRow">+ {{ t('editor.addRow') }}</button>
  </div>
</template>

<style scoped>
.kvp {
  width: 100%;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

/* 行列表容器/行/删除钮/等宽走 theme.css 全局并集（round14 M5 收敛，类名不变）；
   本地只留分宽比例 */
.kvp-key {
  flex: 1 1 40%;
  min-width: 0;
}
.kvp-value {
  flex: 1 1 60%;
  min-width: 0;
}

/* 加行按钮走全局 .dashed-add（theme.css，round14 M4 收敛） */
</style>
