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

// ---- password：明文/密文切换（眼睛按钮，i18n 提示）----
const showPassword = ref(false)

// ---- tags：轻量 chips 输入（沿用 Plan 1 标签 chips + ✕ 的样式模式：Enter 添加、✕ 移除）----
const tagDraft = ref('')
function addTag() {
  const v = tagDraft.value.trim()
  if (!v) return
  const arr = Array.isArray(props.modelValue) ? props.modelValue.slice() : []
  if (!arr.includes(v)) arr.push(v)
  tagDraft.value = ''
  emit('update:modelValue', arr)
}
function removeTag(i) {
  const arr = (Array.isArray(props.modelValue) ? props.modelValue : []).slice()
  arr.splice(i, 1)
  emit('update:modelValue', arr)
}

// ---- color：8 色固定色板 + 原始 hex 文本（WPF 为 ColorPickerWPF 全功能拾色器，
// web 端 Plan 2 简化为色板；ColorHex 为 C# 的 #AARRGGBB 格式，默认 '#00000000'）。
// 色块预览需转 CSS 的 #RRGGBBAA 顺序；文本输入原样存取不做归一化（透传保真）。
// fix-batch1 #6：当前色独立 swatch（不透明才有色，透明/无效 = 无色斜线示意），
// 色板命中项带选中环；选色即时联动图标预览 tint（EditorDrawer 的 iconTint）。
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
      {{ label }}<span v-if="field.required" class="ff-required">*</span>
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

      <!-- switch：json 值可能为 null——显示按 false，写回真实布尔 -->
      <n-switch
        v-else-if="field.type === FIELD_TYPE.SWITCH"
        size="small"
        :value="!!modelValue"
        :disabled="disabled"
        @update:value="emit('update:modelValue', $event)"
      />

      <!-- password：眼睛切换明文/密文 -->
      <n-input
        v-else-if="field.type === FIELD_TYPE.PASSWORD"
        size="small"
        :type="showPassword ? 'text' : 'password'"
        :value="modelValue ?? ''"
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

      <!-- tags：chips + 回车添加 -->
      <div v-else-if="field.type === FIELD_TYPE.TAGS" class="ff-tags" :class="{ disabled }">
        <span v-for="(tg, i) in (Array.isArray(modelValue) ? modelValue : [])" :key="tg + ':' + i" class="ff-tag-chip">
          {{ tg }}<button class="ff-tag-x" type="button" :title="t('editor.removeTag')" @click="removeTag(i)">✕</button>
        </span>
        <input
          class="ff-tag-input"
          type="text"
          spellcheck="false"
          :disabled="disabled"
          v-model="tagDraft"
          @keydown.enter.prevent="addTag"
        />
      </div>

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

      <!-- markdown：编辑 ⇄ 预览（MarkdownField，fix-batch2 Task C #4；值域同 textarea） -->
      <MarkdownField
        v-else-if="field.type === FIELD_TYPE.MARKDOWN"
        :model-value="String(modelValue ?? '')"
        :disabled="disabled"
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

      <!-- color：当前色 swatch + 色板 + 原始 hex -->
      <div v-else-if="field.type === FIELD_TYPE.COLOR" class="ff-color">
        <span
          class="ff-swatch ff-cur"
          :class="{ none: !currentColor }"
          :style="currentColor ? { background: currentColor } : null"
          :title="currentColorTitle"
        ></span>
        <div class="ff-swatches">
          <button
            v-for="sw in COLOR_SWATCHES"
            :key="sw"
            class="ff-swatch"
            :class="{ none: sw === '#00000000', active: isSwatchActive(sw) }"
            :style="{ background: toCssColor(sw) }"
            :title="sw"
            :disabled="disabled"
            @click="emit('update:modelValue', sw)"
          ></button>
        </div>
        <n-input
          size="small"
          class="ff-hex"
          :value="modelValue ?? ''"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          @update:value="emit('update:modelValue', $event)"
        />
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
.ff-label {
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
.ff-control {
  min-width: 0;
  display: flex;
  align-items: center;
}
.ff-control > :deep(*) {
  width: 100%;
}

/* switch 行：控件不占满（对齐 WPF 开关行） */
.ff-switch .ff-control > :deep(*) {
  width: auto;
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

/* tags：chips 输入（沿用 Plan 1 标签 chips 样式模式）；fix-batch1 #6 单行化——
   chips 不换行、行内横向溢出滚动，输入框固定收尾（不做聚焦展开）。
   fix-batch2 Task C #5：外框对齐名称输入框观感——border-strong / 圆角 7px / 固定高
   34px / 水平内边距，chips 间 6px 间距；仍处 value 列（148px 标签列布局不变） */
.ff-tags {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-wrap: nowrap;
  align-items: center;
  gap: 6px;
  height: 34px;
  padding: 0 8px;
  border: 1px solid var(--border-strong);
  border-radius: 7px;
  background: var(--bg-elevated);
  overflow-x: auto;
  scrollbar-width: thin;
}
.ff-tags.disabled {
  opacity: 0.55;
}
.ff-tag-chip {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  gap: 3px;
  max-width: 160px;
  overflow: hidden;
  white-space: nowrap;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--bg-hover);
  color: var(--text-2);
  font-size: 11px;
  line-height: 1;
  padding: 3px 4px 3px 8px;
}
.ff-tag-x {
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--text-4);
  font-size: 9px;
  line-height: 1;
  padding: 2px;
  cursor: pointer;
}
.ff-tag-x:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.ff-tag-input {
  flex: 1 1 80px;
  min-width: 56px;
  border: none;
  background: transparent;
  color: var(--text-1);
  font-size: 12px;
  outline: none;
}

/* icon 选择器自带缩略图 + 按钮样式（IconPicker.vue），此处无需行内样式 */

/* color：当前色 swatch + 色板 + hex 输入（fix-batch1 #6） */
.ff-color {
  display: flex;
  align-items: center;
  gap: 8px;
}
.ff-cur {
  flex: 0 0 auto;
  border-color: var(--text-4); /* 当前色与色板区分：常显边框 */
  border-radius: 50%;
  cursor: default;
}
.ff-swatches {
  display: flex;
  gap: 4px;
  flex: 0 0 auto;
}
.ff-swatch {
  width: 18px;
  height: 18px;
  border: 1px solid var(--border-strong);
  border-radius: 4px;
  cursor: pointer;
  padding: 0;
}
.ff-swatch.active {
  box-shadow: 0 0 0 2px var(--accent); /* 色板命中当前值的选中环 */
}
.ff-swatch.none {
  background: transparent;
  position: relative;
}
.ff-swatch.none::after {
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
.ff-swatch:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.ff-hex {
  flex: 1;
  min-width: 0;
}

.ff-unknown {
  font-size: 12px;
  color: var(--text-4);
}
</style>
