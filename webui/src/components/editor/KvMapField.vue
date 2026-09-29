<script setup>
/**
 * 字符串字典编辑器（FIELD.KV_MAP）：json 值是 {key: value} 对象
 * （PascalCase 域直通，键名 = 用户数据原样），渲染为行式 [key 输入][value 输入][删行✕]
 * 小表 + 底部加行按钮——形态对齐 KeyValueLines（同款边框容器/加行按钮/等宽字体）。
 *
 * 当前唯一消费方：AppArgument.Selections（取值表，Dictionary<string,string>）。
 * WPF 编辑形态是 ArgumentEditView.xaml:144-160 的多行文本框（Selection 型每行
 * "key|描述"、Normal 型每行一个 key，保存时拼回字典，ArgumentEditViewModel.cs:193-238）；
 * web 以显式双列取代行文法（key/value 各一列，语义无需用户记忆分隔符）。
 *
 * 序列化约定：
 *  - 仅丢弃 key 为空串（''）的行（新加空行/删空行不产生字典项，值不因空行变脏——
 *    与 KeyValueLines 的空名行丢弃同语义）；key 不做 trim/归一，原样透传——
 *    含空格键名（如 "Full HD"）必须保真，trim 权威在 C# setter（AppArgument.cs:159
 *    过滤空白键），web 侧剥字符会打断输入（评审修复）。
 *  - value 原样保留（空 value 落库时由 C# setter 归一为 value=key，
 *    AppArgument.cs:169-174，与 WPF"仅 key 行"语义等价）。
 *  - 重复 key 后者覆盖前者（JSON 对象无重复键，C# Dictionary 语义亦然）。
 *  - 空表 → 提交 {}（非 null：null 会绕过 C# setter 的空防护直落 NRE 风险，
 *    与 batch patch 的「JSON null 一律拒绝」同一纪律）。
 *
 * 解析（parseRows）：对象按 Object.keys 顺序转行（JSON 键序 = 插入序， Newtonsoft
 * 保留读取顺序），非字符串值经 String() 归一，保证任何存量值的行级往返保真。
 */
import { onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSaveIssues } from '../../editor/saveIssues.js'

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

/** 行数组 → 对象（空串 key 行丢弃、key 不 trim 原样透传、重复键后者覆盖；空表 → {}）。 */
function serializeRows(rows) {
  const out = {}
  for (const r of rows) {
    const k = String(r.key ?? '')
    if (k === '') continue
    out[k] = String(r.value ?? '')
  }
  return out
}

// ---- 本地行态与外部值同步 ----
// 回环挡板（评审修复）：与 KeyValueLines 不同，值是对象——props.modelValue 经父级
// 响应式链是 Proxy（EditorDrawer 的 reactive json → SubformList 透传），与 emit 出去
// 的普通对象永不做同一性相等（v === lastEmitted 恒 false → 每次键入都 re-parse，
// 叠加序列化 trim 曾把含空格键名瞬间剥掉）。挡板改内容比较（JSON.stringify 相等：
// serializeRows 按行序建键，回读 proxy 的键序 = 插入序，内容一致即同一回环）。
// 仅在外部真实变更（加载/回读）时重新解析——半输入态不被打断。
const rows = ref([])
let lastEmitted = null
watch(
  () => props.modelValue,
  (v) => {
    if (v != null && lastEmitted != null && JSON.stringify(v) === JSON.stringify(lastEmitted)) return
    if (v === lastEmitted) return // null/undefined 快速路径（两态均为原值时内容比较不可达）
    rows.value = parseRows(v)
  },
  { immediate: true }
)
function commit() {
  lastEmitted = serializeRows(rows.value)
  emit('update:modelValue', lastEmitted)
}

// 半填行（round11 U25）：key 空但 value 非空——序列化丢弃空 key 行（JSON 对象无空键），
// 此前保存无提示、重开即消失。key 框常驻红框提示 + 经保存问题总线自报
//（EditorDrawer 拦截保存并在横幅标注）；无提供方（单独使用）时自报为无操作。
// V3（round16）：本组件唯一的落点在子表单行内（AppArgument.Selections，每行一张
// 取值表），同一字段键会有多个实例并存——自报键追加实例号互不干扰，否则后报
// "没问题"的那份会删掉别行"有半行"的记录（修好一行替另一行解锁保存拦截，
// 半行被静默丢弃）。键只作存在性/计数消费（不进用户文案），后缀无展示副作用。
const { report } = useSaveIssues()
let issueUidSeq = 0 // 模块级实例号（组件级常量，不参与渲染）
const issueKey = `${props.field.key}#${++issueUidSeq}`
const rowIsHalf = (r) => String(r.key ?? '') === '' && String(r.value ?? '') !== ''
watch(
  () => rows.value.some(rowIsHalf),
  (has) => report(issueKey, has),
  { immediate: true }
)
onBeforeUnmount(() => report(issueKey, false))

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
          :status="rowIsHalf(row) ? 'error' : undefined"
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
    <button class="dashed-add" type="button" :disabled="disabled" @click="addRow">+ {{ t('editor.addRow') }}</button>
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

/* 行列表容器/行/删除钮/等宽走 theme.css 全局并集（round14 M5 收敛，类名不变）；
   本地只留分宽比例 */
.kvm-key {
  flex: 1 1 50%;
  min-width: 0;
}

.kvm-value {
  flex: 1 1 50%;
  min-width: 0;
}

/* 加行按钮走全局 .dashed-add（theme.css，round14 M4 收敛） */
</style>
