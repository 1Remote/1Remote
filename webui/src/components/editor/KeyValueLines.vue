<script setup>
/**
 * 键值行编辑器（FIELD.KEY_VALUE_LINES）：RDP「额外指令」
 * （RdpControlAdditionalSettings）的行式编辑——WPF 现状是 AvalonEdit 文本域 +
 * 属性名补全（RdpFormView.xaml:525-611 + RdpFormView.xaml.cs 的 CompletionWindow），
 * web 重构为 [属性名自动补全][值输入][删行✕] 的行编辑器 + 底部加行按钮。
 *
 * 存储格式（与 WPF 逐字节兼容，序列化以 WPF 解析器为准——RDP.cs:515
 * SplitAdditionalSettings，连接时 ApplyRdpControlAdditionalSettings 消费）：
 *  - 一行一条 `属性名:类型:值`，类型分隔符 ∈ ':s:'（string）| ':i:'（int/bool，bool
 *    写 0/1）| ':b:'（解析器接受但补全源不生成）；行分隔符 \r 与 \n 均可（WPF 以
 *    {'\r','\n'} Split + RemoveEmptyEntries，空行忽略）；消费时 key/value 各自 Trim。
 *  - 解析（parseRows，宽松）：按行拆分后取三种分隔符**首个**（大小写不敏感）出现处
 *    切开——name 含分隔符（如 'EnableAutoReconnect:i:'），value 为余下原文；无分隔
 *    符的行（WPF 侧为 format error）整行进 name、value 置空，保证**任何**存量值的
 *    行级往返保真（不丢字符、不重排，脏检测只对真实编辑成立）。
 *  - 序列化（serializeRows）：丢弃属性名为空白（Trim 后空）的行（空行等价于 WPF 的
 *    RemoveEmptyEntries 语义），其余按 name+value 原样拼接（不做 Trim——存原文，
 *    Trim 语义留给 WPF 消费端），'\n' 连接、无尾随换行；全部为空行时序列化为 ''。
 *
 * 候选（kvSuggestions，schemas.js 注入 editor/rdpProperties.js 的 103 项清单）：
 * 完整 'name:type:' 串——与 WPF 补全插入的文本一致（用户选中后只补值）。恒全量展示
 * （同 AUTOCOMPLETE 的 Serial 模式：get-show 恒 true、不按输入过滤），候选不约束
 * 取值（任意键入原样进 json，连接时由 WPF 平价的解析器校验）。
 *
 * placeholder/说明文案：属性名框 placeholder 与字段下方两行说明 = WPF 的 HelpBrush
 * 提示与文本域旁说明原文移植（RdpFormView.xaml:570/601-606，WPF 为字面量英文——
 * 14 语言同显，locales 端 14 键同值；见 convert-locales.mjs 的映射策略注释）。
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({
  /** @type {FieldDescriptor} 字段描述符（type=KEY_VALUE_LINES，kvSuggestions 为候选表） */
  field: { type: Object, required: true },
  /** json 字符串值（"a:i:1\nb:s:x"）；null/undefined 按空表处理 */
  modelValue: { type: null, default: null },
  /** 属性名框 placeholder（FormField 由 placeholderKey 解析后传入） */
  placeholder: { type: String, default: undefined },
  disabled: { type: Boolean, default: false },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()

// WPF 解析器的三种类型分隔符（RDP.cs:519）
const SEPARATORS = [':s:', ':i:', ':b:']

/**
 * 字符串 → 行数组（宽松解析，往返保真；见文件头注释）。
 * @param {string} text
 * @returns {{name: string, value: string}[]}
 */
function parseRows(text) {
  const s = text == null ? '' : String(text)
  if (s.trim() === '') return []
  // 空行忽略（对齐 WPF Split(RemoveEmptyEntries)）；空白行 WPF 会保留为 format error
  // 行——这里同样保留（name=整行），不额外丢字符
  return s
    .split(/[\r\n]+/)
    .filter((line) => line !== '')
    .map((line) => {
      // 三种分隔符的首个出现位置（大小写不敏感，对齐 WPF IndexOf(OrdinalIgnoreCase)）
      let at = -1
      for (const sep of SEPARATORS) {
        const i = line.toLowerCase().indexOf(sep)
        if (i >= 0 && (at < 0 || i < at)) at = i
      }
      if (at < 0) return { name: line, value: '' }
      // 三种分隔符长度均为 3：name 含分隔符（'EnableAutoReconnect:i:'）
      return { name: line.slice(0, at + 3), value: line.slice(at + 3) }
    })
}

/** 行数组 → 字符串（见文件头序列化约定：空名行丢弃、原样拼接、\n 连接）。 */
function serializeRows(rows) {
  return rows
    .filter((r) => String(r.name ?? '').trim() !== '')
    .map((r) => String(r.name ?? '') + String(r.value ?? ''))
    .join('\n')
}

// ---- 本地行态与外部值同步 ----
// 自身 emit 后的回环用 lastEmitted 挡掉（父级把同一字符串写回 modelValue）；仅在
// 外部真实变更（加载/协议切换/回读）时重新解析——半输入态不被打断。
const rows = ref([])
let lastEmitted = null
watch(
  () => props.modelValue,
  (v) => {
    const s = v == null ? '' : String(v)
    if (s === lastEmitted) return
    rows.value = parseRows(s)
  },
  { immediate: true }
)
function commit() {
  lastEmitted = serializeRows(rows.value)
  emit('update:modelValue', lastEmitted)
}

function addRow() {
  rows.value.push({ name: '', value: '' })
  // 新行不入 json（空名行序列化时丢弃）——commit 仅用于同步 emit 时机统一；
  // 不 commit 也可，但保持"任何行操作都走 commit"的单一路径（值不因加空行变脏）
  commit()
}
function removeRow(i) {
  rows.value.splice(i, 1)
  commit()
}
function updateRow(i, key, v) {
  rows.value[i][key] = v
  commit()
}

// 候选恒全量（同 Serial 的 AUTOCOMPLETE 模式）
const nameOptions = () => (Array.isArray(props.field.kvSuggestions) ? props.field.kvSuggestions : [])
</script>

<template>
  <div class="kvl">
    <!-- 行列表：装在与 n-input 同观的边框容器内（对齐 WPF 的 AvalonEdit 边框形态） -->
    <div class="kvl-box">
      <div v-for="(row, i) in rows" :key="i" class="kvl-row">
        <!-- 属性名：可输入下拉（恒全量候选；placeholder=WPF HelpBrush 原文） -->
        <n-auto-complete
          class="kvl-name"
          size="small"
          :value="row.name"
          :options="nameOptions()"
          :placeholder="placeholder"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          :get-show="() => true"
          @update:value="updateRow(i, 'name', $event ?? '')"
        />
        <!-- 值：自由输入（:i: 型的整数校验留给后端/WPF 解析器，不在输入侧吞值）；
             placeholder：与属性名框区分列语义——zh「值」/en "Value"，
             其余语言回落 en（无 WPF 对应词条） -->
        <n-input
          class="kvl-value"
          size="small"
          :value="row.value"
          :placeholder="t('editor.kvValue')"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          @update:value="updateRow(i, 'value', $event)"
        />
        <button class="kvl-del" type="button" :title="t('editor.removeRow')" :disabled="disabled" @click="removeRow(i)">
          ✕
        </button>
      </div>
    </div>
    <button class="kvl-add" type="button" :disabled="disabled" @click="addRow">+ {{ t('editor.kvAddParam') }}</button>
    <!-- 字段下方说明（WPF 文本域旁两行说明原文移植） -->
    <p class="kvl-hint">{{ t('editor.kvHint') }}</p>
    <p class="kvl-hint kvl-note">{{ t('editor.kvPriority') }}</p>
  </div>
</template>

<style scoped>
.kvl {
  width: 100%;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

/* 行列表容器：与 n-input small 同观的边框（对齐 WPF 的 AvalonEdit Border + 行号区） */
.kvl-box {
  border: 1px solid var(--border);
  border-radius: 3px;
  background: var(--bg-elevated);
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.kvl-box:focus-within {
  border-color: var(--accent);
}

.kvl-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

/* 属性名 / 值：固定比例分宽（name 约 55%——属性名+类型后缀更长），min-width 防挤压 */
.kvl-name {
  flex: 1 1 55%;
  min-width: 0;
}

.kvl-value {
  flex: 1 1 45%;
  min-width: 0;
}

/* 等宽字体：属性名/值均为技术字面量（对齐 hex 输入的 ff-hf 口径） */
.kvl-name :deep(input),
.kvl-value :deep(input) {
  font-family: ui-monospace, 'Cascadia Mono', Consolas, 'Courier New', monospace;
}

.kvl-del {
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

.kvl-del:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--danger);
}

.kvl-del:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

/* 加行按钮：SubformList 的 sf-add 同款（虚线边框低调按钮） */
.kvl-add {
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

.kvl-add:hover:not(:disabled) {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}

.kvl-add:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

/* 说明文字：WPF 原文两行（第二行 Opacity 0.7 弱化） */
.kvl-hint {
  margin: 0;
  color: var(--text-3);
  font-size: 0.8846rem;
  line-height: 1.5;
}

.kvl-note {
  color: var(--text-4);
}
</style>
