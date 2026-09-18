<script setup>
/**
 * 字符串字典编辑器（FIELD.KV_MAP，batch8 Task C #7⑤）：json 值是 {key: value} 对象
 * （PascalCase 域直通，键名 = 用户数据原样），渲染为行式 [key 输入][value 输入][删行✕]
 * 小表 + 底部加行按钮——形态对齐 KeyValueLines（同款边框容器/加行按钮/等宽字体）。
 *
 * 当前唯一消费方：AppArgument.Selections（取值表，Dictionary<string,string>）。
 * WPF 编辑形态是 ArgumentEditView.xaml:144-160 的多行文本框（Selection 型每行
 * "key|描述"、Normal 型每行一个 key，保存时拼回字典，ArgumentEditViewModel.cs:193-238）；
 * web 以显式双列取代行文法（key/value 各一列，语义无需用户记忆分隔符）。
 *
 * 序列化约定：
 *  - 空白 key 的行丢弃（空行等价于不产生字典项；C# setter 侧本就过滤空白键，
 *    AppArgument.cs:159——双保险）；value 原样保留（空 value 落库时由 C# setter
 *    归一为 value=key，AppArgument.cs:169-174，与 WPF"仅 key 行"语义等价）。
 *  - 重复 key 后者覆盖前者（JSON 对象无重复键，C# Dictionary.Add 亦然）。
 *  - 空表 → 提交 {}（非 null：null 会绕过 C# setter 的空防护直落 NRE 风险，
 *    与 batch patch 的「JSON null 一律拒绝」同一纪律）。
 *
 * 解析（parseRows）：对象按 Object.keys 顺序转行（JSON 键序 = 插入序， Newtonsoft
 * 保留读取顺序），非字符串值经 String() 归一，保证任何存量值的行级往返保真。
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({
  /** @type {FieldDescriptor} 字段描述符（type=KV_MAP；当前无 KV_MAP 专属扩展属性） */
  field: { type: Object, required: true },
  /** json 对象值（{key: value}）；null/undefined/非对象按空表处理 */
  modelValue: { type: null, default: null },
  disabled: { type: Boolean, default: false },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()

/** 对象 → 行数组（键序保持；值归一字符串）。 */
function parseRows(obj) {
  if (obj == null || typeof obj !== 'object' || Array.isArray(obj)) return []
  return Object.keys(obj).map((k) => ({ key: String(k), value: obj[k] == null ? '' : String(obj[k]) }))
}

/** 行数组 → 对象（空白 key 行丢弃、重复键后者覆盖；空表 → {}，见文件头序列化约定）。 */
function serializeRows(rows) {
  const out = {}
  for (const r of rows) {
    const k = String(r.key ?? '').trim()
    if (k === '') continue
    out[k] = String(r.value ?? '')
  }
  return out
}

// ---- 本地行态与外部值同步（回环挡板同 KeyValueLines：emit 后父级写回同一对象不打断输入）----
const rows = ref([])
let lastEmitted = null
watch(
  () => props.modelValue,
  (v) => {
    if (v === lastEmitted) return
    rows.value = parseRows(v)
  },
  { immediate: true }
)
function commit() {
  lastEmitted = serializeRows(rows.value)
  emit('update:modelValue', lastEmitted)
}

function addRow() {
  rows.value.push({ key: '', value: '' })
  // 新行不入 json（空 key 行序列化时丢弃）——commit 统一 emit 时机，值不因加空行变脏
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
  <div class="kvm">
    <!-- 行列表：与 n-input 同观的边框容器（KeyValueLines 的 kvl-box 同款） -->
    <div class="kvm-box">
      <div v-for="(row, i) in rows" :key="i" class="kvm-row">
        <n-input
          class="kvm-key"
          size="small"
          :value="row.key"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          @update:value="updateRow(i, 'key', $event)"
        />
        <n-input
          class="kvm-value"
          size="small"
          :value="row.value"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          @update:value="updateRow(i, 'value', $event)"
        />
        <button class="kvm-del" type="button" :title="t('editor.removeRow')" :disabled="disabled" @click="removeRow(i)">
          ✕
        </button>
      </div>
    </div>
    <button class="kvm-add" type="button" :disabled="disabled" @click="addRow">+ {{ t('editor.addRow') }}</button>
  </div>
</template>

<style scoped>
.kvm {
  width: 100%;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

/* 行列表容器：与 n-input small 同观的边框（KeyValueLines.kvl-box 同款） */
.kvm-box {
  border: 1px solid var(--border);
  border-radius: 3px;
  background: var(--bg-elevated);
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.kvm-box:focus-within {
  border-color: var(--accent);
}

.kvm-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

/* key / value：对半分宽（Selections 的 key/value 长度相近），min-width 防挤压 */
.kvm-key {
  flex: 1 1 50%;
  min-width: 0;
}

.kvm-value {
  flex: 1 1 50%;
  min-width: 0;
}

/* 等宽字体：key/value 为技术字面量（对齐 KeyValueLines 的 kvl-name/kvl-value 口径） */
.kvm-key :deep(input),
.kvm-value :deep(input) {
  font-family: ui-monospace, 'Cascadia Mono', Consolas, 'Courier New', monospace;
}

.kvm-del {
  flex: 0 0 auto;
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--text-4);
  font-size: 0.7692rem;
  line-height: 1;
  padding: 3px;
  cursor: pointer;
}

.kvm-del:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--danger);
}

.kvm-del:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

/* 加行按钮：SubformList 的 sf-add / KeyValueLines 的 kvl-add 同款（虚线边框低调按钮） */
.kvm-add {
  align-self: flex-start;
  border: 1px dashed var(--border-strong);
  border-radius: 5px;
  background: transparent;
  color: var(--text-3);
  font-size: 0.9231rem;
  line-height: 1;
  padding: 6px 12px;
  cursor: pointer;
}

.kvm-add:hover:not(:disabled) {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}

.kvm-add:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
</style>
