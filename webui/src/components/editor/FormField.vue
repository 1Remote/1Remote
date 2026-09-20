<script setup>
/**
 * 通用字段渲染器：按 field.type 分发到具体控件（text / number / select / switch /
 * tags / password / textarea / markdown / icon / color / credential / autocomplete /
 * key-value-lines / kv-map / subform，未知类型兜底只读呈现），值层面保持纯受控——
 *  - 不读 visibleWhen（可见性由父级抽屉用 editor/visibility.js 的 isVisible 求值并隐藏整行）；
 *  - 不直接改 json：父级按字段 v-model 绑定到 json 对象属性，本组件只 emit update:modelValue；
 *  - 隐藏字段值保留透传的约定同样由父级保证（隐藏≠删值）。
 * 四个字段类型带远程交互（其余仍为纯展示）：
 *  - TEXT 的路径字段（filePick 描述符）：行内"浏览…"按钮——
 *    POST /api/files/pick 弹后端原生文件对话框回填路径（WPF 表单 Select 按钮
 *    的 web 平价，见 onFilePick）；
 *  - TEXTAREA 的脚本字段（actions: ['select','test']）：行内 [选择][测试]
 *    两按钮——选择 = POST /api/files/pick 弹后端原生文件对话框回填路径；测试 =
 *    POST /api/scripts/test 执行命令并把命令/输出/退出码弹 naive dialog 呈现
 *    （对齐 WPF 脚本行两按钮，见 onScriptSelect/onScriptTest）；
 *  - TAGS：输入框下常驻已有标签候选 chips（点击即加，对齐 WPF TagsEditor
 *    的 TagsForSelect），数据经 composables/useTagSuggestions.js 模块级缓存。
 * 字段描述符形状见 editor/fieldTypes.js；i18n：字段 labelKey 由 schemas.js 兜底注入
 *（editor.f.*）；SELECT 选项 labelKey 缺失显示 String(value)——Serial 的
 * 技术字面量选项（'8'/'NONE'…）依赖该回退（有意不译）。
 * icon → IconPicker、credential → CredentialPicker：credential 的选项按数据源隔离，
 * dataSourceName 由父级（EditorDrawer）逐层传入（SubformList 透传，保持行内同数据源）；
 * icon 额外接收 tint（当前 ColorHex 的低饱和底色，即时联动图标预览）。
 * AUTOCOMPLETE 的远程建议（Serial 端口/波特率）经 composables/useSerialOptions.js
 * 的模块级缓存拉取一次，两字段共享；SELECT 的动态选项（optionsSource 'runners:*'，
 * SelectedRunnerName）同样经 composables/useRunnerOptions.js 模块级缓存共享。
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import SubformList from './SubformList.vue'
import IconPicker from './IconPicker.vue'
import CredentialPicker from './CredentialPicker.vue'
import MarkdownField from './MarkdownField.vue'
import SwitchItem from './SwitchItem.vue'
import KeyValueLines from './KeyValueLines.vue'
import HelpLink from '../HelpLink.vue'
import KvMapField from './KvMapField.vue'
import { FIELD } from '../../editor/fieldTypes.js'
import { showScriptTestResult } from '../../editor/scriptTest.js'
import { api } from '../../api'
import { opaqueHex } from '../../utils/color.js'
import { useSerialOptions } from '../../composables/useSerialOptions.js'
import { useRunnerOptions } from '../../composables/useRunnerOptions.js'
import { useTagSuggestions } from '../../composables/useTagSuggestions.js'

const props = defineProps({
  /** @type {FieldDescriptor} 字段描述符（fieldTypes.js） */
  field: { type: Object, required: true },
  /** json 中 field.key 处的当前值（任意类型；switch 可能是 null，subform 是数组） */
  modelValue: { type: null, default: null },
  disabled: { type: Boolean, default: false },
  /** credential 字段的凭据库数据源（透传给 CredentialPicker） */
  dataSourceName: { type: String, default: '' },
  /** icon 字段的预览底色（#RRGGBB；EditorDrawer 由 ColorHex 实时派生） */
  tint: { type: String, default: '' },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()
const { serialSuggestions } = useSerialOptions()
const { runnerNames } = useRunnerOptions()

const label = computed(() => (props.field.labelKey ? t(props.field.labelKey) : props.field.key))
const placeholder = computed(() => (props.field.placeholderKey ? t(props.field.placeholderKey) : undefined))

// ---- text 的"浏览…"按钮：filePick 描述符（fieldTypes.js）——
// WPF 表单路径字段旁 SelectFileHelper.OpenFile 按钮的 web 平价（全集审计见
// schemas.js 的 filePick 注释块）。按钮调 POST /api/files/pick（后端弹 WPF 同款
// OpenFileDialog，filter 照抄各 WPF 调用点；path 传当前值作初始目录），选中回填
// 裸路径；404=用户取消静默（api.pickFile 约定）。按钮文案复用 settings.r.f.browse
//（"Browse…"——与 RunnerCard/CredentialVault 的原生文件选择按钮同一词条，WPF 的
// 按钮文案 Select 在 web 已被脚本行的"选择"占用，统一走 Browse 系）。
// 子表单行内的条件按钮（ArgumentList Value 仅 File 型行）由 SubformList 按
// filePickWhen 求值后剥离/保留描述符，本组件只看 filePick 有无。
const filePickBusy = ref(false) // 请求寿命 = 用户开着对话框的时间（同 scriptBusy 语义）
async function onFilePick() {
  if (filePickBusy.value) return
  filePickBusy.value = true
  try {
    const resp = await api.pickFile(props.field.filePick.filter, {
      path: String(props.modelValue ?? ''),
      title: props.field.filePick.titleKey ? t(props.field.filePick.titleKey) : '',
    })
    if (resp?.path) emit('update:modelValue', resp.path)
  } catch (e) {
    if (e?.status !== 404) message.error(t('settings.r.pickFailed')) // 404=用户取消，静默
  } finally {
    filePickBusy.value = false
  }
}

// ---- switch 行式（对齐 WPF 复选框行 CredentialView.xaml:191-215：空标题列 + 输入列
// [CheckBox+文字]）----
// 默认标签列留空（开关起点即其他输入框的左缘，宽度对齐），描述文字紧跟开关右侧
//（6px 间隔、可换行）；switchWithLabel=true 的字段例外（IsPingBeforeConnect 可用性
// 检测行，WPF HostView.xaml:29-38 该行标签列有文字）：标签列显示 labelKey 文案，
// 控件列描述文字改用 switchTextKey。[开关][文字] 的渲染拆出 SwitchItem 子组件
//（EditorDrawer 的连续开关聚合行共用，两处观感一致）。
const isSwitch = computed(() => props.field.type === FIELD.SWITCH)
const showLabelInColumn = computed(() => !isSwitch.value || !!props.field.switchWithLabel)

// ---- markdown：编辑 ⇄ 预览状态由本组件持有（省垂直空间）----
// 切换按钮在标签列右侧，MarkdownField 为受控组件（props.preview 二选一渲染、
// 不自持 mode）；每字段独立一份状态（当前仅 basic 组 Note 一个 MARKDOWN 字段，
// 无递归场景）。
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
  { immediate: true }
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
// optionsSource（动态选项，fieldTypes.js）：当前唯一源 'runners:<ProtocolKey>'——
// [''(跟随全局)] + 该协议运行器名（useRunnerOptions 模块级缓存，失败静默退化空列表）。
// 该源的 '' 是真实选项值（C# 侧空串 = 跟随全局，ProtocolBase.cs:214-222），不走下方
// selectValue 的空值→undefined 置空分支。
const selectOptions = computed(() => {
  const src = props.field.optionsSource
  if (typeof src === 'string' && src.startsWith('runners:')) {
    const names = runnerNames(src.slice('runners:'.length))
    return [{ value: '', label: t('editor.o.followGlobalSettings') }, ...names.map((n) => ({ value: n, label: n }))]
  }
  return (props.field.options || []).map((o) => ({
    value: o.value,
    label: o.labelKey ? t(o.labelKey) : String(o.value),
  }))
})
const selectValue = computed(() => {
  if (props.field.optionsSource) return props.modelValue == null ? '' : props.modelValue
  return props.modelValue === null || props.modelValue === undefined || props.modelValue === ''
    ? undefined
    : props.modelValue
})

// ---- autocomplete：可输入下拉 = n-auto-complete。naive 默认空输入不弹菜单
//（getShow 缺省 = !!value）——get-show 恒 true 让聚焦即显示。下拉恒展示全部建议
//（不按输入过滤，聚焦即见所有备选项）；输入的值仍可自由键入（n-auto-complete
// 本身支持）——建议只是候选、不约束取值，任意键入原样进 json（校验交给后端的
// WPF 平价规则）。建议列表：suggestionsSource 指向远程串口建议（useSerialOptions，
// 失败静默退化为空建议 = 纯文本输入），否则用字段自带的静态 suggestions。
const acOptions = computed(() => {
  const list = props.field.suggestionsSource
    ? serialSuggestions(props.field.suggestionsSource)
    : Array.isArray(props.field.suggestions)
      ? props.field.suggestions
      : []
  // 恒全量：不按输入过滤，下拉始终展示所有建议
  return list.map((s) => String(s))
})

// ---- password：明文/密文切换（眼睛按钮，i18n 提示）----
const showPassword = ref(false)

// ---- tags：n-dynamic-tags（chips 的添加/删除/回车确认由组件自带）。
// 值归一不依赖后端静默处理（C# Tags setter 自带 Distinct+Trim+去空格，ProtocolBase.cs:115）：
// update handler 里 Trim + 去空串 + 去重后回传，防 n-dynamic-tags 允许的重复输入原样入 json。
function normalizeTags(arr) {
  return (Array.isArray(arr) ? arr : [])
    .map((s) => String(s).trim())
    .filter((s, i, a) => s !== '' && a.indexOf(s) === i)
}
function onTagsUpdate(v) {
  emit('update:modelValue', normalizeTags(v))
}

// ---- tags 候选 chips：已有标签名（useTagSuggestions 模块级缓存，
// stale-while-revalidate——TAGS 字段组件创建时拉一次）。已选中的不再展示，最多 12 个
//（克制：候选是"快速点选"，不是完整列表，长列表交给输入）；点击 = 追加回 json。
// 显示名截断 TAG_SUGGEST_MAX 字符（长标签不撑破两行候选区），:title 恒给全名。
const TAG_SUGGEST_MAX = 20
const clipTag = (s) => (s.length > TAG_SUGGEST_MAX ? s.slice(0, TAG_SUGGEST_MAX) + '…' : s)
const { tags: allTags, refresh: refreshTagSuggestions } = useTagSuggestions()
if (props.field.type === FIELD.TAGS) refreshTagSuggestions()
const tagSuggestions = computed(() => {
  const current = normalizeTags(props.modelValue)
  return allTags.value.filter((s) => !current.includes(s)).slice(0, 12)
})
function addTag(tag) {
  onTagsUpdate([...normalizeTags(props.modelValue), tag])
}

// ---- 脚本字段行内按钮（对齐 WPF 脚本行的 Select/Test 两按钮，
// ServerEditorPageView.xaml:162-216）：schemas.js 给 CommandBeforeConnected/
// CommandAfterDisconnected 挂 actions: ['select','test']。
//  - 选择：后端弹 WPF 同款原生文件对话框（filter script|*.bat;*.cmd;*.ps1;*.py|*|.*，
//    title 同 WPF "Select a script"），选中回填裸路径（WPF 直接 Server 属性赋值，无引号）；
//  - 测试：后端复用 WPF 的 DisassembleOneLineScriptCmd 拆解并执行，回传
//    {file, arguments, exitCode, timedOut, output, error}——弹窗呈现命令/输出/退出码
//    （WPF 的 "We will run..." 提示 + 控制台窗口 + "The exit code..." 消息盒的 web 等价）。
const actionSelect = computed(() => Array.isArray(props.field.actions) && props.field.actions.includes('select'))
const actionTest = computed(() => Array.isArray(props.field.actions) && props.field.actions.includes('test'))
const scriptBusy = ref(false) // 两按钮互斥占用（选择请求寿命 = 用户开着对话框的时间）
// 脚本值为空（含纯空白）时禁用"测试"（空命令的测试必然无意义，
// 后端 DisassembleOneLineScriptCmd 拆不出可执行项）；"选择"不受影响——选择正是填值的入口
const scriptEmpty = computed(() => String(props.modelValue ?? '').trim() === '')
async function onScriptSelect() {
  if (scriptBusy.value) return
  scriptBusy.value = true
  try {
    const resp = await api.pickFile('script|*.bat;*.cmd;*.ps1;*.py|*|*.*', {
      path: String(props.modelValue ?? ''),
      title: t('editor.scriptPickTitle'),
    })
    if (resp?.path) emit('update:modelValue', resp.path)
  } catch (e) {
    if (e?.status !== 404) message.error(t('settings.r.pickFailed')) // 404=用户取消，静默
  } finally {
    scriptBusy.value = false
  }
}
async function onScriptTest() {
  if (scriptBusy.value) return
  const command = String(props.modelValue ?? '').trim()
  if (!command) return
  scriptBusy.value = true
  try {
    showScriptTestResult({ dialog, t, fieldKey: props.field.key, command, resp: await api.testScript(command) })
  } catch (e) {
    message.error(t('editor.scriptTestStartFailed') + ': ' + (e?.message || e))
  } finally {
    scriptBusy.value = false
  }
}

// ---- color：8 色固定色板 + 原始 hex 文本（WPF 为 ColorPickerWPF 全功能拾色器，
// web 端简化为色板；ColorHex 为 C# 的 #AARRGGBB 格式，默认 '#00000000'）。
// 色块预览需转 CSS 的 #RRGGBBAA 顺序；文本输入原样存取不做归一化（透传保真）。
// 当前色独立 swatch（不透明才有色，透明/无效 = 无色斜线示意），色板命中项带
// 选中环；选色即时联动图标预览 tint（EditorDrawer 的 iconTint）。
// 整体收进一个与 n-input 同观的边框容器（见模板/样式），语义（disabled /
// toCssColor / isSwatchActive / title 提示）不变。
const COLOR_SWATCHES = [
  '#00000000',
  '#FF565A63',
  '#FFEF6A6A',
  '#FFF0B25F',
  '#FF26A269',
  '#FF2C5AFF',
  '#FF8B5CF6',
  '#FFEC4899',
]
function toCssColor(hex) {
  return typeof hex === 'string' && /^#[0-9a-fA-F]{8}$/.test(hex) ? '#' + hex.slice(3) + hex.slice(1, 3) : hex
}
// 当前色（#RRGGBB）：全透明/格式非法 → ''（无色，swatch 用斜线示意；utils/color.js 同口径）
const currentColor = computed(() => opaqueHex(props.modelValue) || '')
const currentColorTitle = computed(() =>
  currentColor.value
    ? t('editor.colorCurrent') + ': ' + props.modelValue
    : t('editor.colorCurrent') + ': ' + t('editor.colorNone')
)
const isSwatchActive = (sw) => typeof props.modelValue === 'string' && sw === props.modelValue

const FIELD_TYPE = FIELD // 模板中使用类型常量做分发
</script>

<template>
  <div class="form-field" :class="'ff-' + field.type">
    <div class="ff-label" :title="label">
      <!-- switch 行标签列默认留空（控件列 [开关][文字] 自解释）；switchWithLabel 例外 -->
      <span v-if="showLabelInColumn" class="ff-label-text"
        >{{ label }}<span v-if="field.required" class="ff-required">*</span></span
      >
      <!-- 字段旁帮助链接：WPF 表单行 (?) 的 web 落点（如 mstsc 附加
           设置 → 文档 #additional-settings 锚点）；URL 照抄 WPF NavigateUri。
           普通开关行例外：标签列留空 → (?) 挂到开关文字后（SwitchItem 内渲染，
           如 mstsc 开关行的 "Enabled (?)" 形态） -->
      <HelpLink v-if="field.helpUrl && showLabelInColumn" :href="field.helpUrl" />
      <!-- MARKDOWN 的 编辑 ⇄ 预览 切换（标签列右侧；i18n 键沿用 MarkdownField 原有） -->
      <button
        v-if="field.type === FIELD_TYPE.MARKDOWN"
        class="ff-md-toggle"
        type="button"
        :disabled="disabled"
        :title="mdPreview ? t('editor.mdEdit') : t('editor.mdPreview')"
        @click="mdPreview = !mdPreview"
      >
        {{ mdPreview ? '✎' : '👁' }}
      </button>
    </div>

    <div class="ff-control">
      <!-- text（+ 可选 filePick"浏览…"按钮：占主列、按钮贴右，样式复用 ff-mini-btn） -->
      <div v-if="field.type === FIELD_TYPE.TEXT && field.filePick" class="ff-text-pick">
        <n-input
          class="ff-text-pick-input"
          size="small"
          :value="modelValue ?? ''"
          :placeholder="placeholder"
          :disabled="disabled"
          :input-props="{ spellcheck: false }"
          @update:value="emit('update:modelValue', $event)"
        />
        <button class="ff-mini-btn" type="button" :disabled="disabled || filePickBusy" @click="onFilePick">
          {{ t('settings.r.f.browse') }}
        </button>
      </div>
      <n-input
        v-else-if="field.type === FIELD_TYPE.TEXT"
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

      <!-- autocomplete（Serial 的端口/波特率）：可输入下拉——下拉始终显示全部建议
          （acOptions 不按输入过滤、get-show 恒 true）；选中与直接键入均为字符串值
           直通 json（建议只是候选，不约束取值） -->
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

      <!-- switch：控件在前、描述文字紧跟（标签列留空见上方 showLabelInColumn）；
           json 值可能为 null，组件内显示按 false、写回真实布尔 -->
      <SwitchItem
        v-else-if="field.type === FIELD_TYPE.SWITCH"
        :field="field"
        :model-value="modelValue"
        :disabled="disabled"
        @update:model-value="emit('update:modelValue', $event)"
      />

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
          >
            👁
          </button>
        </template>
      </n-input>

      <!-- tags：n-dynamic-tags（chips 添加/删除/回车确认由组件自带；值经 onTagsUpdate 归一回传）
           + 已有标签候选 chips（点击即加——对齐 WPF TagsEditor 的 TagsForSelect）。
           类名用 ff-tags-box 而非 ff-tags：根行已带 'ff-' + type 生成的 ff-tags，
           同名时下方 .ff-tags-box 的 display:flex 覆写根行 148px 网格（.form-field），
           正是标签行"标题在上/内容在下"错位的根因（与 COLOR 行 ff-color-box 同款教训） -->
      <div v-else-if="field.type === FIELD_TYPE.TAGS" class="ff-tags-box">
        <n-dynamic-tags
          size="small"
          :value="Array.isArray(modelValue) ? modelValue : []"
          :disabled="disabled"
          @update:value="onTagsUpdate"
        />
        <div v-if="tagSuggestions.length" class="ff-tag-sug">
          <button
            v-for="s in tagSuggestions"
            :key="s"
            type="button"
            class="ff-tag-sug-chip"
            :disabled="disabled"
            :title="s"
            @click="addTag(s)"
          >
            + {{ clipTag(s) }}
          </button>
        </div>
      </div>

      <!-- textarea：rows 字段描述符缺省 3；脚本字段 rows=1（默认单行输入框高）
           + 行内 [选择][测试] 按钮（actions 描述符） -->
      <div v-else-if="field.type === FIELD_TYPE.TEXTAREA" class="ff-ta" :class="{ onerow: (field.rows ?? 3) === 1 }">
        <n-input
          class="ff-ta-input"
          type="textarea"
          size="small"
          :rows="field.rows ?? 3"
          :value="modelValue ?? ''"
          :placeholder="placeholder"
          :disabled="disabled"
          @update:value="emit('update:modelValue', $event)"
        />
        <div v-if="actionSelect || actionTest" class="ff-ta-actions">
          <button
            v-if="actionSelect"
            class="ff-mini-btn"
            type="button"
            :disabled="disabled || scriptBusy"
            @click="onScriptSelect"
          >
            {{ t('editor.scriptSelect') }}
          </button>
          <button
            v-if="actionTest"
            class="ff-mini-btn"
            type="button"
            :disabled="disabled || scriptBusy || scriptEmpty"
            :title="scriptEmpty ? t('editor.scriptTestEmpty') : ''"
            @click="onScriptTest"
          >
            {{ t('editor.scriptTest') }}
          </button>
        </div>
      </div>

      <!-- markdown：编辑 ⇄ 预览（MarkdownField，值域同 textarea；受控：preview 状态由
           本组件持有，切换按钮在标签列右侧）；placeholder 透传编辑态 textarea（Note 挂
           editor.ph.note = WPF 输入区 Tag 的 markdown 示例文本，多行占位） -->
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

      <!-- color（单输入组）：与 n-input small 同观的边框容器内
           [当前色块 16×16][hex 文本（透明无边框）][竖分隔线][8 色板小点 14×14]；
           容器撑满控件列 → 与其他输入框左对齐同宽。
           类名用 ff-color-box 而非 ff-color：根行已有 'ff-' + type 的 ff-color，
           同名会经"同级特异性后者胜"把容器样式泄漏到根行（.ff-color 的
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

      <!-- key-value-lines：RDP 额外指令的行编辑器（KeyValueLines；候选 kvSuggestions
           由 schema 注入，placeholder 同 placeholderKey） -->
      <KeyValueLines
        v-else-if="field.type === FIELD_TYPE.KEY_VALUE_LINES"
        :field="field"
        :model-value="modelValue"
        :placeholder="placeholder"
        :disabled="disabled"
        @update:model-value="emit('update:modelValue', $event)"
      />

      <!-- kv-map：字符串字典行编辑器（KvMapField；{key:value} 对象 ↔ [key][value] 行，
           当前唯一消费方 AppArgument.Selections） -->
      <KvMapField
        v-else-if="field.type === FIELD_TYPE.KV_MAP"
        :field="field"
        :model-value="modelValue"
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

/* 标签列：flex 行（文字 + 可选的 MARKDOWN 切换按钮），文字省略、按钮恒右贴 */
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
  font-size: var(--fs-body);
  color: var(--text-2);
}

.ff-required {
  margin-left: 2px;
  color: var(--danger);
}

/* MARKDOWN 编辑 ⇄ 预览切换：低调图标文字按钮，标签列内右贴 */
.ff-md-toggle {
  margin-left: auto;
  flex: 0 0 auto;
  border: none;
  background: transparent;
  color: var(--text-4);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 2px;
  border-radius: var(--radius-xs);
  cursor: pointer;
}

.ff-md-toggle:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-1);
}

.ff-md-toggle:disabled {
  opacity: var(--opacity-disabled);
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

/* text + filePick"浏览…"按钮：输入框占主列、按钮贴右（同 ff-ta 布局）；
   容器由上方 100% 规则撑满控件列 */
.ff-text-pick {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  min-width: 0;
}

.ff-text-pick-input {
  flex: 1 1 auto;
  min-width: 0;
}

/* switch 行：[开关][6px][文字] 由 SwitchItem 渲染——其根节点被上方
   .ff-control > :deep(*) 的 100% 规则撑满控件列（文字 flex 填充），开关起点即
   其他输入框的左缘（148px 列起点）；EditorDrawer 的聚合行内同一组件按内容收缩
   换行（样式见 SwitchItem.vue） */

/* password 眼睛按钮 */
.ff-eye {
  border: none;
  background: transparent;
  color: var(--text-4);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 2px;
  cursor: pointer;
}

.ff-eye:hover {
  color: var(--text-1);
}

/* tags：输入 + 候选 chips 两行。容器由上方 .ff-control > :deep(*) 的
   100% 规则撑满控件列（form-field 两列网格不破：148px 标签列与其他行对齐，
   候选区只活在控件列内）；类名避开根行生成的 ff-tags（见模板注释）；
   n-dynamic-tags 的 chips 换行/删除/禁用态均组件自带，不自绘 chips 样式 */
.ff-tags-box {
  display: flex;
  flex-direction: column;
  gap: 4px;
  width: 100%;
  min-width: 0;
}

.ff-tags-box :deep(.n-dynamic-tags) {
  width: 100%;
}

/* 候选 chips：虚线小标签（视觉从属"可点选的候选"，与已选实心 chips 区分），最多 12 个
   （tagSuggestions 截断），无候选/全部已选时整行不渲染。
   候选区限高两行：行高 = 字号 caption 档 + 上下 padding 3px*2 +
   边框 1px*2，两行 + 一个 4px 行距——超出滚轮（overflow-y:auto），不足两行自适应 */
.ff-tag-sug {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  max-height: calc(2 * (var(--fs-caption) + 8px) + 4px);
  overflow-y: auto;
}

.ff-tag-sug-chip {
  border: 1px dashed var(--border);
  border-radius: var(--radius-xs);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1;
  padding: 3px 8px;
  cursor: pointer;
}

.ff-tag-sug-chip:hover:not(:disabled) {
  color: var(--accent-text);
  border-color: var(--accent);
}

.ff-tag-sug-chip:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}

/* textarea 行：textarea 占主列、行内按钮贴右。rows=1 的脚本字段默认
   单行输入框高（28px，与 n-input small 文本框同观），CSS 覆写允许纵向拉高——naive 的
   textarea 默认 resize:none，必须显式覆写；拉高后按钮保持顶部对齐（WPF 按钮随行拉伸，
   web 顶部对齐观感更稳，有意偏差） */
.ff-ta {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  width: 100%;
  min-width: 0;
}

.ff-ta-input {
  flex: 1 1 auto;
  min-width: 0;
}

.ff-ta.onerow :deep(textarea) {
  height: var(--ctrl-h-m);
  min-height: var(--ctrl-h-m);
  resize: vertical;
}

/* 行内 [选择][测试] 按钮：与 n-input small 同高的小按钮 */
.ff-ta-actions {
  flex: 0 0 auto;
  display: flex;
  gap: 4px;
}

.ff-mini-btn {
  height: var(--ctrl-h-m);
  padding: 0 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-xs);
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: var(--fs-caption);
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}

.ff-mini-btn:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}

.ff-mini-btn:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}

/* icon 选择器自带缩略图 + 按钮样式（IconPicker.vue），此处无需行内样式 */

/* color（单输入组）：与 n-input small 同观的边框容器——高 28px、1px 边框、3px 圆角、
   focus-within 亮边（naive 的 --n-* 变量不外泄到兄弟节点，用主题变量近似即可）；
   内部 [当前色块][hex 文本][竖分隔线][8 色板小点]。容器由上方
   .ff-control > :deep(*) 的 100% 规则撑满控件列 → 与其他输入框左对齐同宽。
   类名避开根行的 ff-color（见模板注释） */
.ff-color-box {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 28px;
  padding: 0 6px;
  border: 1px solid var(--border);
  border-radius: var(--radius-xs);
  background: var(--bg-elevated);
}

.ff-color-box:focus-within {
  border-color: var(--accent-focus);
}

/* 当前色块 16×16（常显边框，title 提示当前值/无色） */
.ff-cur {
  flex: 0 0 auto;
  width: 16px;
  height: 16px;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-xs);
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
  font-size: var(--fs-body);
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
  border-radius: var(--radius-xs);
  cursor: pointer;
  padding: 0;
}

.ff-sw.active {
  box-shadow: 0 0 0 2px var(--accent-focus);
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
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}

.ff-unknown {
  font-size: var(--fs-body);
  color: var(--text-4);
}
</style>
