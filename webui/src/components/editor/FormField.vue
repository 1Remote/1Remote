<script>
/**
 * AUTOCOMPLETE 的远程建议缓存（fix batch4 Task B）：模块级状态——Serial 编辑器的
 * SerialPort/BitRate 是两个 FormField 实例，共享同一次 /api/serial/options 拉取
 *（建议源是后端机器的 COM 口/波特率表，与表单数据无关，无需按数据源隔离或重拉）。
 * shallowRef 保持响应式：数据到达后各实例的 acOptions 自动重算（数组只整组替换，
 * 浅响应足够）；用 shallowRef 而非 ref 是为避免与下方 <script setup> 的 vue import
 * 命名冲突（两块 script 编译到同一模块作用域，重复声明会报错）。
 * 失败静默置空：空建议 = 纯文本输入，不阻断表单也不弹错误——建议只是便利功能，
 * 与 CredentialPicker 的差异：此处无"必须从库中选择"的语义，退化后功能完整。
 */
import { shallowRef } from 'vue'
import { api } from '../../api'

const serialPortSuggestions = shallowRef([])
const serialBaudRateSuggestions = shallowRef([])
let serialOptionsRequested = false

function ensureSerialOptionsLoaded() {
  if (serialOptionsRequested) return
  serialOptionsRequested = true
  api.serialOptions()
    .then((resp) => {
      serialPortSuggestions.value = Array.isArray(resp?.ports) ? resp.ports : []
      serialBaudRateSuggestions.value = Array.isArray(resp?.baudRates) ? resp.baudRates : []
    })
    .catch(() => {
      // API 失败（含开发模式后端未起）：留空建议，字段退化为纯文本输入；
      // 复位请求标记让下次渲染重试——否则本会话内建议功能整段失效（批次4-B 评审）
      serialPortSuggestions.value = []
      serialBaudRateSuggestions.value = []
      serialOptionsRequested = false
    })
}

/** 按建议源取候选（'serial-ports' | 'serial-baud-rates'；未知源 = 空列表）。 */
function serialSuggestions(source) {
  ensureSerialOptionsLoaded()
  if (source === 'serial-ports') return serialPortSuggestions.value
  if (source === 'serial-baud-rates') return serialBaudRateSuggestions.value
  return []
}
</script>

<script setup>
/**
 * 通用字段渲染器（Plan 2 Task 7）：按 field.type 分发到具体控件，纯展示组件——
 *  - 不读 visibleWhen（可见性由父级抽屉用 editor/visibility.js 的 isVisible 求值并隐藏整行）；
 *  - 不直接改 json：父级按字段 v-model 绑定到 json 对象属性，本组件只 emit update:modelValue；
 *  - 隐藏字段值保留透传的约定同样由父级保证（隐藏≠删值）。
 * 字段描述符形状见 editor/fieldTypes.js；i18n：字段 labelKey 由 schemas.js 兜底注入
 * （editor.f.*，Task 11）；SELECT 选项 labelKey 缺失显示 String(value)——Serial 的
 * 技术字面量选项（'8'/'NONE'…）依赖该回退（有意不译）。
 * icon → IconPicker、credential → CredentialPicker（Task 9）：credential 的选项按数据源隔离，
 * dataSourceName 由父级（EditorDrawer）逐层传入（SubformList 透传，保持行内同数据源）；
 * icon 额外接收 tint（当前 ColorHex 的低饱和底色，fix-batch1 #6 颜色即时联动图标预览）。
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import SubformList from './SubformList.vue'
import IconPicker from './IconPicker.vue'
import CredentialPicker from './CredentialPicker.vue'
import MarkdownField from './MarkdownField.vue'
import { FIELD } from '../../editor/fieldTypes.js'
import { opaqueHex } from '../../utils/color.js'

const props = defineProps({
  /** @type {FieldDescriptor} 字段描述符（fieldTypes.js） */
  field: { type: Object, required: true },
  /** json 中 field.key 处的当前值（任意类型；switch 可能是 null，subform 是数组） */
  modelValue: { type: null, default: null },
  disabled: { type: Boolean, default: false },
  /** credential 字段的凭据库数据源（透传给 CredentialPicker） */
  dataSourceName: { type: String, default: '' },
  /** icon 字段的预览底色（#RRGGBB；EditorDrawer 由 ColorHex 实时派生，fix-batch1 #6） */
  tint: { type: String, default: '' },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()

const label = computed(() => (props.field.labelKey ? t(props.field.labelKey) : props.field.key))
const placeholder = computed(() => (props.field.placeholderKey ? t(props.field.placeholderKey) : undefined))

// ---- switch 行式重构（fix-batch4 Task A #3/#4，对齐 WPF 复选框行
// CredentialView.xaml:191-215：空标题列 + 输入列 [CheckBox+文字]）----
// 默认标签列留空（开关起点即其他输入框的左缘，宽度对齐），描述文字紧跟开关右侧
// （6px 间隔、可换行）；switchWithLabel=true 的字段例外（IsPingBeforeConnect 可用性
// 检测行，WPF HostView.xaml:29-38 该行标签列有文字）：标签列显示 labelKey 文案，
// 控件列描述文字改用 switchTextKey。
const isSwitch = computed(() => props.field.type === FIELD.SWITCH)
const showLabelInColumn = computed(() => !isSwitch.value || !!props.field.switchWithLabel)
const switchText = computed(() => (props.field.switchTextKey ? t(props.field.switchTextKey) : label.value))

// ---- markdown：编辑 ⇄ 预览状态上提（fix-batch4 Task A #2，owner 反馈省垂直空间）----
// 切换按钮移到标签列右侧，MarkdownField 改为受控（props.preview 二选一渲染、不再自持
// mode）；每字段独立一份状态（当前仅 basic 组 Note 一个 MARKDOWN 字段，无递归场景）。
const mdPreview = ref(false)

// ---- number：本地原文态 + asString 语义（fieldTypes.js）----
// C# int 属性存数字、string 属性（Port/BitRate）asString 存字符串；清空按 asString
// 存 '' 或 null。非数字输入（如 '33a'）原样存字符串交给保存校验（后端 400，对齐
// WPF IDataErrorInfo 的"保留输入、标红"语义），不在输入侧吞掉用户输入。
const numberRaw = ref('')
watch(
  () => props.modelValue,
  (v) => {
    const s = v == null ? '' : String(v)
    if (s === numberRaw.value) return
    // 半输入态不打断：'3.' 解析为 3 时若直接回写显示会吃掉小数点——仅当当前原文
    // 解析结果与新值不一致（外部加载/回读）才覆盖本地输入
    const raw = numberRaw.value.trim()
    if (raw !== '' && String(Number(raw)) === s) return
    numberRaw.value = s
  },
  { immediate: true },
)
function onNumberInput(v) {
  numberRaw.value = v
  const s = String(v).trim()
  if (s === '') {
    emit('update:modelValue', props.field.asString ? '' : null)
    return
  }
  const n = Number(s)
  emit('update:modelValue', props.field.asString ? s : Number.isNaN(n) ? s : n)
}

// ---- select：选项 label 回退 String(value)；值缺失时传 undefined 交由 n-select 置空 ----
const selectOptions = computed(() =>
  (props.field.options || []).map((o) => ({ value: o.value, label: o.labelKey ? t(o.labelKey) : String(o.value) })),
)
const selectValue = computed(() => (props.modelValue === null || props.modelValue === undefined || props.modelValue === '' ? undefined : props.modelValue))

// ---- autocomplete（fix batch4 Task B）：可输入下拉 = n-auto-complete ----
// naive 的 AutoComplete 不做选项过滤（options 原样展示）且默认空输入不弹菜单
// （getShow 缺省 = !!value，见 naive-ui AutoComplete.mjs 的 mergedShowOptions）——
// 这里自行按输入做包含匹配（大小写不敏感），get-show 恒 true 让「空输入聚焦也显示
// 全量建议」；建议不约束取值，任意键入仍原样进 json（校验交给后端 WPF 平价规则）。
const acOptions = computed(() => {
  const list = props.field.suggestionsSource
    ? serialSuggestions(props.field.suggestionsSource)
    : Array.isArray(props.field.suggestions) ? props.field.suggestions : []
  const q = String(props.modelValue ?? '').trim().toLowerCase()
  const source = q === '' ? list : list.filter((s) => String(s).toLowerCase().includes(q))
  return source.map((s) => String(s))
})

// ---- password：明文/密文切换（眼睛按钮，i18n 提示）----
const showPassword = ref(false)

// ---- tags：n-dynamic-tags（fix-batch3 Task A #3，owner 反馈自绘 chips 位置/宽度/
// 边框与其他输入框不一致 → 换 naive 原生组件；chips 的添加/删除/回车确认由组件自带）。
// 值归一不依赖后端静默处理（C# Tags setter 自带 Distinct+Trim+去空格，ProtocolBase.cs:115）：
// update handler 里 Trim + 去空串 + 去重后回传，防 n-dynamic-tags 允许的重复输入原样入 json。
function onTagsUpdate(v) {
  const arr = (Array.isArray(v) ? v : [])
    .map((s) => String(s).trim())
    .filter((s, i, a) => s !== '' && a.indexOf(s) === i)
  emit('update:modelValue', arr)
}

// ---- color：8 色固定色板 + 原始 hex 文本（WPF 为 ColorPickerWPF 全功能拾色器，
// web 端 Plan 2 简化为色板；ColorHex 为 C# 的 #AARRGGBB 格式，默认 '#00000000'）。
// 色块预览需转 CSS 的 #RRGGBBAA 顺序；文本输入原样存取不做归一化（透传保真）。
// fix-batch1 #6：当前色独立 swatch（不透明才有色，透明/无效 = 无色斜线示意），
// 色板命中项带选中环；选色即时联动图标预览 tint（EditorDrawer 的 iconTint）。
// fix-batch4 Task A #1：整体收进一个与 n-input 同观的边框容器（见模板/样式），
// 语义（disabled / toCssColor / isSwatchActive / title 提示）不变。
const COLOR_SWATCHES = ['#00000000', '#FF565A63', '#FFEF6A6A', '#FFF0B25F', '#FF26A269', '#FF2C5AFF', '#FF8B5CF6', '#FFEC4899']
function toCssColor(hex) {
  return typeof hex === 'string' && /^#[0-9a-fA-F]{8}$/.test(hex) ? '#' + hex.slice(3) + hex.slice(1, 3) : hex
}
// 当前色（#RRGGBB）：全透明/格式非法 → ''（无色，swatch 用斜线示意；utils/color.js 同口径）
const currentColor = computed(() => opaqueHex(props.modelValue) || '')
const currentColorTitle = computed(() =>
  currentColor.value ? t('editor.colorCurrent') + ': ' + props.modelValue : t('editor.colorCurrent') + ': ' + t('editor.colorNone'),
)
const isSwatchActive = (sw) => typeof props.modelValue === 'string' && sw === props.modelValue

const FIELD_TYPE = FIELD // 模板中使用类型常量做分发
</script>

<template>
  <div class="form-field" :class="'ff-' + field.type">
    <div class="ff-label" :title="label">
      <!-- #3/#4：switch 行标签列默认留空（控件列 [开关][文字] 自解释）；switchWithLabel 例外 -->
      <span v-if="showLabelInColumn" class="ff-label-text">{{ label }}<span v-if="field.required" class="ff-required">*</span></span>
      <!-- #2：MARKDOWN 的 编辑 ⇄ 预览 切换（标签列右侧；i18n 键沿用 MarkdownField 原有） -->
      <button
        v-if="field.type === FIELD_TYPE.MARKDOWN"
        class="ff-md-toggle"
        type="button"
        :disabled="disabled"
        :title="mdPreview ? t('editor.mdEdit') : t('editor.mdPreview')"
        @click="mdPreview = !mdPreview"
      >{{ mdPreview ? '✎' : '👁' }}</button>
    </div>

    <div class="ff-control">
      <!-- text -->
      <n-input
        v-if="field.type === FIELD_TYPE.TEXT"
        size="small"
        :value="modelValue ?? ''"
        :placeholder="placeholder"
        :disabled="disabled"
        :input-props="{ spellcheck: false }"
        @update:value="emit('update:modelValue', $event)"
      />

      <!-- number：inputmode 引导数字键盘；值语义见 onNumberInput -->
      <n-input
        v-else-if="field.type === FIELD_TYPE.NUMBER"
        size="small"
        :value="numberRaw"
        :placeholder="placeholder"
        :disabled="disabled"
        :input-props="{ inputmode: 'decimal', spellcheck: false }"
        @update:value="onNumberInput"
      />

      <!-- select -->
      <n-select
        v-else-if="field.type === FIELD_TYPE.SELECT"
        size="small"
        :value="selectValue"
        :options="selectOptions"
        :disabled="disabled"
        :placeholder="placeholder"
        @update:value="emit('update:modelValue', $event)"
      />

      <!-- autocomplete（fix batch4 Task B）：可输入下拉（Serial 的端口/波特率）——选项按输入
           包含匹配过滤（acOptions）、空输入聚焦显示全量建议（get-show 恒 true）；选中与直接
           键入均为字符串值直通 json（建议只是候选，不约束取值） -->
      <n-auto-complete
        v-else-if="field.type === FIELD_TYPE.AUTOCOMPLETE"
        size="small"
        :value="modelValue ?? ''"
        :options="acOptions"
        :placeholder="placeholder"
        :disabled="disabled"
        :input-props="{ spellcheck: false }"
        :get-show="() => true"
        @update:value="emit('update:modelValue', $event ?? '')"
      />

      <!-- switch（#3/#4）：控件在前、描述文字紧跟（标签列留空见上方 showLabelInColumn）；
           json 值可能为 null——显示按 false，写回真实布尔 -->
      <template v-else-if="field.type === FIELD_TYPE.SWITCH">
        <n-switch
          size="small"
          :value="!!modelValue"
          :disabled="disabled"
          @update:value="emit('update:modelValue', $event)"
        />
        <span class="ff-switch-text">{{ switchText }}</span>
      </template>

      <!-- password：眼睛切换明文/密文 -->
      <n-input
        v-else-if="field.type === FIELD_TYPE.PASSWORD"
        size="small"
        :type="showPassword ? 'text' : 'password'"
        :value="modelValue ?? ''"
        :placeholder="placeholder"
        :disabled="disabled"
        :input-props="{ spellcheck: false }"
        @update:value="emit('update:modelValue', $event)"
      >
        <template #suffix>
          <button
            class="ff-eye"
            type="button"
            :title="showPassword ? t('editor.hidePassword') : t('editor.showPassword')"
            @click="showPassword = !showPassword"
          >👁</button>
        </template>
      </n-input>

      <!-- tags：n-dynamic-tags（chips 添加/删除/回车确认由组件自带；值经 onTagsUpdate 归一回传） -->
      <n-dynamic-tags
        v-else-if="field.type === FIELD_TYPE.TAGS"
        size="small"
        :value="Array.isArray(modelValue) ? modelValue : []"
        :disabled="disabled"
        @update:value="onTagsUpdate"
      />

      <!-- textarea -->
      <n-input
        v-else-if="field.type === FIELD_TYPE.TEXTAREA"
        type="textarea"
        size="small"
        :rows="3"
        :value="modelValue ?? ''"
        :placeholder="placeholder"
        :disabled="disabled"
        @update:value="emit('update:modelValue', $event)"
      />

      <!-- markdown：编辑 ⇄ 预览（MarkdownField，fix-batch2 Task C #4；值域同 textarea；
           fix-batch4 #2 受控化：preview 状态由本组件持有，切换按钮在标签列右侧）；
           placeholder 透传编辑态 textarea（同 placeholderKey，当前 Note 字段无键 → 空） -->
      <MarkdownField
        v-else-if="field.type === FIELD_TYPE.MARKDOWN"
        :model-value="String(modelValue ?? '')"
        :disabled="disabled"
        :preview="mdPreview"
        :placeholder="placeholder"
        @update:model-value="emit('update:modelValue', $event)"
      />

      <!-- icon：IconPicker（内置网格/本地上传/exe 提取/清除）；tint = 当前 ColorHex 低饱和底色 -->
      <IconPicker
        v-else-if="field.type === FIELD_TYPE.ICON"
        :model-value="modelValue || ''"
        :tint="tint"
        :disabled="disabled"
        @update:model-value="emit('update:modelValue', $event)"
      />

      <!-- color（#1 单输入组）：与 n-input small 同观的边框容器内
           [当前色块 16×16][hex 文本（透明无边框）][竖分隔线][8 色板小点 14×14]；
           容器撑满控件列 → 与其他输入框左对齐同宽（owner：旧的松散摆放不像输入框）。
           类名用 ff-color-box 而非 ff-color：根行已有 'ff-' + type 的 ff-color，
           同名会经"同级特异性后者胜"把容器样式泄漏到根行（旧版 .ff-color 的
           display:flex 覆写根行 148px 网格正是颜色行错位的根因） -->
      <div v-else-if="field.type === FIELD_TYPE.COLOR" class="ff-color-box">
        <span
          class="ff-cur"
          :class="{ none: !currentColor }"
          :style="currentColor ? { background: currentColor } : null"
          :title="currentColorTitle"
        ></span>
        <input
          class="ff-hex"
          type="text"
          spellcheck="false"
          :value="modelValue ?? ''"
          :disabled="disabled"
          @input="emit('update:modelValue', $event.target.value)"
        />
        <span class="ff-color-sep" aria-hidden="true"></span>
        <button
          v-for="sw in COLOR_SWATCHES"
          :key="sw"
          class="ff-sw"
          :class="{ none: sw === '#00000000', active: isSwatchActive(sw) }"
          :style="{ background: toCssColor(sw) }"
          :title="sw"
          :disabled="disabled"
          @click="emit('update:modelValue', sw)"
        ></button>
      </div>

      <!-- credential：CredentialPicker（选项按数据源隔离；清空 = 手动输入） -->
      <CredentialPicker
        v-else-if="field.type === FIELD_TYPE.CREDENTIAL"
        :model-value="modelValue || ''"
        :data-source-name="dataSourceName"
        :disabled="disabled"
        @update:model-value="emit('update:modelValue', $event)"
      />

      <!-- subform -->
      <SubformList
        v-else-if="field.type === FIELD_TYPE.SUBFORM"
        :fields="field.subform?.fields || []"
        :row-defaults="field.subform?.rowDefaults || {}"
        :data-source-name="dataSourceName"
        :model-value="modelValue"
        @update:model-value="emit('update:modelValue', $event)"
      />

      <!-- 未知类型（schema 约定之外）：兜底只读呈现，避免整个表单渲染失败 -->
      <span v-else class="ff-unknown">{{ modelValue == null ? '' : String(modelValue) }}</span>
    </div>
  </div>
</template>

<style scoped>
/* 字段行：左标签列 + 右控件列（抽屉宽度 560-900px，标签 148px 紧凑对齐 WPF 行式布局） */
.form-field {
  display: grid;
  grid-template-columns: 148px minmax(0, 1fr);
  gap: 4px 10px;
  align-items: center;
}
/* 标签列：flex 行（文字 + 可选的 MARKDOWN 切换按钮，#2），文字省略、按钮恒右贴 */
.ff-label {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 4px;
}
.ff-label-text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12.5px;
  color: var(--text-2);
}
.ff-required {
  margin-left: 2px;
  color: var(--danger);
}
/* MARKDOWN 编辑 ⇄ 预览切换（#2）：低调图标文字按钮，标签列内右贴 */
.ff-md-toggle {
  margin-left: auto;
  flex: 0 0 auto;
  border: none;
  background: transparent;
  color: var(--text-4);
  font-size: 12px;
  line-height: 1;
  padding: 2px;
  border-radius: 3px;
  cursor: pointer;
}
.ff-md-toggle:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-1);
}
.ff-md-toggle:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.ff-control {
  min-width: 0;
  display: flex;
  align-items: center;
}
.ff-control > :deep(*) {
  width: 100%;
}

/* switch 行（#3/#4）：控件在前、文字紧跟；标签列留空（见模板），开关起点即
   其他输入框的左缘（.ff-control 的 148px 列起点） */
.ff-switch .ff-control > :deep(*) {
  width: auto;
}
.ff-switch .ff-control {
  gap: 6px;
}
.ff-switch-text {
  flex: 1 1 auto;
  min-width: 0;
  font-size: 12.5px;
  line-height: 1.4;
  color: var(--text-2);
}

/* password 眼睛按钮 */
.ff-eye {
  border: none;
  background: transparent;
  color: var(--text-4);
  font-size: 12px;
  line-height: 1;
  padding: 2px;
  cursor: pointer;
}
.ff-eye:hover {
  color: var(--text-1);
}

/* tags：n-dynamic-tags（Task A #3）——宽度由上方 .ff-control > :deep(*) 的 100% 规则
   撑满控件列（与 n-input 同宽，owner 要的效果），chips 换行/删除/禁用态均组件自带，
   不再需要自绘 chips 样式 */

/* icon 选择器自带缩略图 + 按钮样式（IconPicker.vue），此处无需行内样式 */

/* color（#1 单输入组，fix-batch4 Task A）：与 n-input small 同观的边框容器——高 28px、
   1px 边框、3px 圆角、focus-within 亮边（naive 的 --n-* 变量不外泄到兄弟节点，用主题
   变量近似即可）；内部 [当前色块][hex 文本][竖分隔线][8 色板小点]。容器由上方
   .ff-control > :deep(*) 的 100% 规则撑满控件列 → 与其他输入框左对齐同宽。
   类名避开根行的 ff-color（见模板注释） */
.ff-color-box {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 28px;
  padding: 0 6px;
  border: 1px solid var(--border);
  border-radius: 3px;
  background: var(--bg-elevated);
}
.ff-color-box:focus-within {
  border-color: var(--accent);
}
/* 当前色块 16×16（常显边框，title 提示当前值/无色；语义与旧 swatch 相同） */
.ff-cur {
  flex: 0 0 auto;
  width: 16px;
  height: 16px;
  border: 1px solid var(--border-strong);
  border-radius: 4px;
  cursor: default;
}
/* hex 文本：透明无边框原生输入（原样存取不归一化），等宽字体便于核对 #AARRGGBB */
.ff-hex {
  flex: 1 1 auto;
  min-width: 0;
  height: 26px;
  border: none;
  outline: none;
  padding: 0;
  background: transparent;
  color: var(--text-1);
  font-size: 12px;
  font-family: ui-monospace, 'Cascadia Mono', Consolas, 'Courier New', monospace;
}
.ff-hex:disabled {
  color: var(--text-3);
  cursor: not-allowed;
}
/* 色板与 hex 之间的竖分隔线 */
.ff-color-sep {
  flex: 0 0 auto;
  width: 1px;
  height: 14px;
  background: var(--border);
}
/* 色板小点 14×14：点击 = 设值；命中当前值带选中环 */
.ff-sw {
  flex: 0 0 auto;
  width: 14px;
  height: 14px;
  border: 1px solid var(--border-strong);
  border-radius: 3px;
  cursor: pointer;
  padding: 0;
}
.ff-sw.active {
  box-shadow: 0 0 0 2px var(--accent);
}
.ff-cur.none,
.ff-sw.none {
  background: transparent;
  position: relative;
}
.ff-cur.none::after,
.ff-sw.none::after {
  /* 透明色：斜线示意（#00000000 = 无色，C# 默认） */
  content: '';
  position: absolute;
  left: 1px;
  right: 1px;
  top: 50%;
  height: 1px;
  background: var(--danger);
  transform: rotate(-45deg);
}
.ff-sw:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.ff-unknown {
  font-size: 12px;
  color: var(--text-4);
}
</style>
