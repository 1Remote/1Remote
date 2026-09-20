<script setup>
/**
 * 批量编辑表单（自 EditorDrawer 拆出，仅在抽屉 mode='bulk' 时挂载）——schema 驱动的
 * 协议感知视图（owner 需求，对齐 WPF 批量编辑语义）：
 *  - 勾选全为同一协议 → 完整渲染该协议的 PROTOCOLS[P].groups（与单机编辑同一份 schema、
 *    同 labelKey/placeholderKey/helpUrl，控件复用 FormField/SwitchItem——批量与非批量的
 *    输入方式/占位/帮助链接天然一致）；混合协议 → 组/字段级交集（规则见 editor/bulkSchema.js）。
 *  - SUBFORM 字段不渲染（后端 BatchPatchDeepFields 400 引导单机编辑）；凭据组按 credRole
 *    拍平（「手动 ⇄ 凭据库」二选一是单台状态机，批量域每字段独立保持不变/覆盖）；
 *    visibleWhen 忽略（N 台依赖值不同时显隐无法统一裁决，有意偏差，见 bulkSchema.js 头注释）。
 *  - 不抽共享 SchemaForm 组件的决策：单机渲染管线（blocksOf 开关聚合/cred-mode 伪块）是
 *    「单 json v-model」形态，批量是「逐字段保持不变/覆盖 + 共享值只读回显」形态，行结构
 *    不同（每行带切换按钮）；真正的共享点是 schema + FormField/SwitchItem/HelpLink 与组
 *    标题模板，强行抽组件需要大量条件槽位，两不像。组标题/描述/提示行的模板与样式按
 *    EditorDrawer 同款复制一份（scoped 样式不跨组件，.ed-fields/.ed-banner 同理）。
 *
 * 数据来源（两路合并，键均为 camelCase patch 键 = camelKey(schema key)）：
 *  - 列表 DTO（bulkServers prop）：BULK_DTO_KEYS 覆盖的 8 键（displayName/note/tags/
 *    colorHex/iconBase64/address/port/userName）共享值逐字段计算；
 *  - peek 回读（挂载时 POST /api/servers/batch/peek，≤50 台防大库风暴，失败静默退化）：
 *    其余全部非敏感键（后端从 BatchPatchFieldMap 同源派生，扣 3 个加密键与这 8 个 DTO 键）。
 *    password/privateKey/gatewayPassword 为敏感键：列表与 peek 均不回读（bulkSensitive 文案）。
 *
 * 每字段默认「保持不变」（不进 patch），点「覆盖」后从共享值（已知且全同且非 null）或
 * 类型默认值（schema defaults / 选项首项）起编辑；保存 = diffPatch(共享初值, 当前值) 仅取
 * 被覆盖字段 → POST /api/servers/batch（缺失 = 保持不变）。
 *
 * 与抽屉的接缝（保持既有事件序与按钮态）：
 *  - 保存入口统一在抽屉（底部保存按钮 / Ctrl+S）：抽屉经模板 ref 调本组件 save()，
 *    内部含 saving 防重入、必填校验与服务端错误内联展示；
 *  - 保存成功：message.success → emit('saved', { mode:'bulk', ids }) → 抽屉转发父级并
 *    执行关闭动画（列表刷新由 SSE reload 自动完成）；
 *  - saving / dirty（有字段处于覆盖态即脏）/ dsMixed（跨数据源勾选禁存）经 defineExpose
 *    暴露，抽屉的底部按钮禁用态与 Esc 关闭的脏确认经模板 ref 响应式读取。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import FormField from './FormField.vue'
import HelpLink from '../HelpLink.vue'
import { bulkSchemaView, camelKey, sharedValueOf, BULK_DTO_KEYS, BULK_SENSITIVE_KEYS } from '../../editor/bulkSchema.js'
import { FIELD } from '../../editor/fieldTypes.js'
import { diffPatch } from '../../editor/patch.js'
import { opaqueHex } from '../../utils/color.js'
import { api } from '../../api'

const props = defineProps({
  /** bulk 模式：目标服务器 id 列表（父级去重后的勾选集，批量 API 入参） */
  bulkIds: { type: Array, default: () => [] },
  /** bulk 模式：与 bulkIds 对应的列表 DTO（camelCase 域，共享值计算来源） */
  bulkServers: { type: Array, default: () => [] },
  /** 归属数据源（bulkServers[0] 缺 dataSourceName 时的兜底） */
  dataSourceName: { type: String, default: 'Local' },
})
const emit = defineEmits(['saved'])
const { t } = useI18n()
const message = useMessage()

const saving = ref(false)
const saveErrors = ref([]) // 服务端 400 的 {errors} 列表（内联展示）
const missingRequired = ref([]) // 客户端必填快速校验（字段文案列表）

// ---- peek 回读：选中 ≤50 台时拉取非敏感字段，>50/失败静默退化 ----
// bulkDsMixed 的 ds 以 bulkServers 为准，与本表单保存口径一致；peek 也按单 ds 语义调用。
const PEEK_LIMIT = 50
const peekItems = ref(null) // null=未回读；Array<{id, <camelKey>: value}>（camelCase）
onMounted(() => {
  const count = props.bulkIds.length
  if (count === 0 || count > PEEK_LIMIT) return
  api
    .batchPeek(props.bulkIds, bulkDs.value)
    .then((items) => {
      peekItems.value = Array.isArray(items) ? items : null
    })
    .catch(() => {
      /* 静默退化：字段维持「未回读」占位与覆盖式设置，功能完整（只读展示是增强） */
    })
})
/** peek 数据按 id 建查；全部选中 id 都有记录才算就绪（否则退化，防半截数据误判共享值） */
const peekMap = computed(() => {
  if (!Array.isArray(peekItems.value)) return null
  const map = {}
  for (const it of peekItems.value) {
    if (it && typeof it.id === 'string') map[it.id] = it
  }
  return props.bulkIds.length && props.bulkIds.every((id) => map[id]) ? map : null
})

// ---- 深拷贝（JSON 往返：与共享值/patch 的序列化语义一致，reactive 代理与 schema
// defaults 对象脱钩——绝不让表单编辑原地改写 schemas.js 的共享常量）----
function deepClone(o) {
  return o == null ? o : JSON.parse(JSON.stringify(o))
}

// ---- schema 视图：全同协议 = 该协议完整分组；混合 = 组/字段级交集（bulkSchema.js）----
const schemaView = computed(() => bulkSchemaView(props.bulkServers.map((s) => s?.protocol)))
const bulkDefaults = computed(() => schemaView.value.defaults)
/** 渲染字段平铺（{field, key=camelCase patch 键}）：共享值/覆盖态/校验的遍历基 */
const bulkFields = computed(() => {
  const out = []
  for (const g of schemaView.value.groups) {
    for (const f of g.fields) out.push({ field: f, key: camelKey(f.key) })
  }
  return out
})

// ---- 共享值计算 + 逐字段「保持不变/覆盖」状态 ----
// bulkShared[key] = { known, same, value }（sharedValueOf 的三元组，bulkSchema.js）：
//  - BULK_DTO_KEYS 覆盖键 → known=true，value 为 N 台列表 DTO 值（same=false 时无意义）；
//  - 其余非敏感键 → known 由 peek 决定（回读成功时逐台比对，键名 = patch 键）；
//  - 敏感键（password/privateKey/gatewayPassword）或 peek 未就绪 → known=false，只提示、不展示值。
const bulkShared = computed(() => {
  const out = {}
  for (const { key } of bulkFields.value) {
    const dtoKey = BULK_DTO_KEYS[key]
    if (dtoKey) {
      out[key] = sharedValueOf(props.bulkServers.map((s) => s?.[dtoKey]))
      continue
    }
    if (!BULK_SENSITIVE_KEYS.has(key) && peekMap.value) {
      out[key] = sharedValueOf(props.bulkIds.map((id) => peekMap.value[id]?.[key]))
      continue
    }
    out[key] = { known: false, same: false, value: undefined }
  }
  return out
})
const bulkOverwrite = reactive({}) // key → true（已切到覆盖编辑）；缺省 = 保持不变
const bulkValues = reactive({}) // key → 覆盖态下的当前值（仅覆盖态有意义）

// 覆盖态的初值兜底：类型感知（int 数字/静态枚举用 schema defaults 或选项首值——空值无法
// 进 patch 域；runners 下拉的 '' 是真实取值=跟随全局，不拦）。
function emptyValueFor(field) {
  if (field.type === FIELD.TAGS) return []
  if (field.type === FIELD.SWITCH) return false
  if (field.type === FIELD.NUMBER && !field.asString) {
    const d = bulkDefaults.value?.[field.key]
    return d !== undefined ? deepClone(d) : null
  }
  if (field.type === FIELD.SELECT && !field.optionsSource) {
    const d = bulkDefaults.value?.[field.key]
    return d !== undefined ? deepClone(d) : (field.options?.[0]?.value ?? '')
  }
  return ''
}
function toggleOverwrite(field) {
  const key = camelKey(field.key)
  if (bulkOverwrite[key]) {
    bulkOverwrite[key] = false // 回到「保持不变」：该字段退出 patch
    return
  }
  const shared = bulkShared.value[key]
  // 共享值非 null 才作初值：null 无法写入 patch 域（后端 400），以类型默认值起编
  bulkValues[key] = shared.known && shared.same && shared.value != null ? deepClone(shared.value) : emptyValueFor(field)
  bulkOverwrite[key] = true
}
// 有字段处于覆盖态即脏（值变化不退出覆盖，无需更细）——抽屉的 Esc 关闭确认据此判断
const bulkDirty = computed(() => Object.values(bulkOverwrite).some(Boolean))
// 覆盖态字段的共享初值/当前值对 → diffPatch 仅产出真正变化的键（值与共享值相同的覆盖不产生写入）
function buildBulkPatch() {
  const initial = {}
  const current = {}
  for (const { key } of bulkFields.value) {
    if (!bulkOverwrite[key]) continue
    const shared = bulkShared.value[key]
    if (shared.known && shared.same) initial[key] = shared.value
    current[key] = bulkValues[key]
  }
  return diffPatch(initial, current)
}
function validateBulkRequired() {
  const missing = []
  for (const { field, key } of bulkFields.value) {
    if (!bulkOverwrite[key]) continue
    const v = bulkValues[key]
    const empty = v == null || (typeof v === 'string' && v.trim() === '')
    if (!empty) continue
    // 必填字段照旧；int 数字与静态枚举字段的空值无法进 patch 域（后端类型门/空值 400），
    // 与必填同栏提示；runners 下拉的 '' 是真实取值（跟随全局），不在此列
    if (field.required || field.type === FIELD.NUMBER || (field.type === FIELD.SELECT && !field.optionsSource)) {
      missing.push(field.labelKey ? t(field.labelKey) : key)
    }
  }
  return missing
}

// ---- 未回读占位文案：敏感键（加密字段，列表/peek 均不回读，与 WPF 哨兵机制同源）用
// bulkSensitiveHint；其余（peek 未就绪：>50 台或回读失败）用 bulkUnknown。两者都只引导
// 「覆盖」式设置。「各不相同」占位复用 WPF server_editor_different_options 译文。----
function bulkUnknownKey(field) {
  return BULK_SENSITIVE_KEYS.has(camelKey(field.key)) ? 'editor.bulkSensitiveHint' : 'editor.bulkUnknown'
}
const bulkCount = computed(() => props.bulkServers.length)
const bulkDsNames = computed(() => new Set(props.bulkServers.map((s) => s.dataSourceName || 'Local')))
// 后端 batch 端点单 ds 语义：跨数据源勾选无法一次落库 → 明确告知并禁存（不做静默裁剪）
const bulkDsMixed = computed(() => bulkDsNames.value.size > 1)
const bulkDs = computed(() => props.bulkServers[0]?.dataSourceName || props.dataSourceName || 'Local')

// 图标预览底色：共享 ColorHex（已知且全同）的低饱和 tint——IconPicker 只读/覆盖态即时联动
const iconTint = computed(() => {
  const shared = bulkShared.value[camelKey('ColorHex')]
  return shared?.known && shared.same ? opaqueHex(shared.value) || '' : ''
})

// ---- 保存（抽屉经模板 ref 调用；成功后由抽屉转发 saved 并执行关闭）----
async function save() {
  if (saving.value || bulkDsMixed.value) return
  missingRequired.value = validateBulkRequired()
  if (missingRequired.value.length) return
  const patch = buildBulkPatch()
  if (!Object.keys(patch).length) {
    // 后端空 patch 400（"patch must contain at least one field"）——前端先行提示
    message.warning(t('editor.bulkNoChanges'))
    return
  }
  saveErrors.value = []
  saving.value = true
  try {
    await api.batchUpdate(props.bulkIds, patch, bulkDs.value)
    message.success(t('editor.bulkUpdated', { n: bulkCount.value }))
    emit('saved', { mode: 'bulk', ids: props.bulkIds })
  } catch (e) {
    if (e?.status === 400) {
      const errs = e.body?.errors || (e.body?.error ? [e.body.error] : [])
      saveErrors.value = errs.length ? errs : [`${e.message}`]
    } else {
      message.error(t('editor.saveFailed') + (e?.message ? ` (${e.message})` : ''))
    }
  } finally {
    saving.value = false
  }
}

// 接缝暴露：save 供抽屉保存入口调用；saving/dirty/dsMixed 供抽屉按钮态与脏检测读取
defineExpose({ save, saving, dirty: bulkDirty, dsMixed: bulkDsMixed })
</script>

<template>
  <div class="ed-fields">
    <div v-if="bulkDsMixed" class="ed-banner">{{ t('editor.bulkMixedDs') }}</div>
    <!-- 混合协议提示：交集之外的选项不显示且各自保持不变（owner 需求文案） -->
    <div v-else-if="schemaView.mixed" class="bulk-info-banner">{{ t('editor.bulkMixedProtocols') }}</div>
    <div v-if="missingRequired.length" class="ed-banner ed-banner-required">
      {{ t('editor.missingRequired', { keys: missingRequired.join(', ') }) }}
    </div>
    <div v-if="saveErrors.length" class="ed-banner">
      <div v-for="(err, i) in saveErrors" :key="i">{{ err }}</div>
    </div>

    <!-- 分组铺开：与单机编辑同一份 schema（标题/描述/提示行/帮助链接同款模板；
         字段行 = FormField（覆盖态可编辑 / 共享值已知且全同时只读）或占位行 + 右侧
         「覆盖/保持不变」切换按钮 -->
    <section v-for="g in schemaView.groups" :key="g.id" class="ed-group">
      <h3 class="ed-group-title">
        {{ g.labelKey ? t(g.labelKey) : g.id }}
        <HelpLink v-if="g.helpUrl" :href="g.helpUrl" />
      </h3>
      <div v-if="g.descKey" class="ed-group-desc">{{ t(g.descKey) }}</div>
      <p v-if="g.note" class="ed-group-note">
        {{ g.note }}<HelpLink v-if="g.noteUrl" :href="g.noteUrl" badge="">{{ g.noteUrlLabel || '' }}</HelpLink>
      </p>

      <div v-for="f in g.fields" :key="f.key" class="bulk-field">
        <!-- 覆盖态：可编辑，值改动即时入 bulkValues -->
        <FormField
          v-if="bulkOverwrite[camelKey(f.key)]"
          class="bulk-control"
          :field="f"
          :model-value="bulkValues[camelKey(f.key)]"
          :data-source-name="bulkDs"
          :tint="iconTint"
          @update:model-value="(v) => (bulkValues[camelKey(f.key)] = v)"
        />
        <!-- 保持不变 + 共享值已知且全同：只读展示 N 台当前的共同值 -->
        <FormField
          v-else-if="bulkShared[camelKey(f.key)].known && bulkShared[camelKey(f.key)].same"
          class="bulk-control"
          :field="f"
          :model-value="bulkShared[camelKey(f.key)].value"
          :data-source-name="bulkDs"
          :tint="iconTint"
          disabled
        />
        <!-- 保持不变 + 各不相同/未回读/敏感：占位行（标签列对齐 FormField 的 148px） -->
        <div v-else class="bulk-keep bulk-control">
          <div class="bulk-keep-label" :title="f.labelKey ? t(f.labelKey) : f.key">
            {{ f.labelKey ? t(f.labelKey) : f.key }}<span v-if="f.required" class="ff-required-like">*</span>
          </div>
          <div
            class="bulk-hint"
            :title="
              bulkShared[camelKey(f.key)].known ? t('editor.differentValues', { n: bulkCount }) : t(bulkUnknownKey(f))
            "
          >
            {{
              bulkShared[camelKey(f.key)].known ? t('editor.differentValues', { n: bulkCount }) : t(bulkUnknownKey(f))
            }}
          </div>
        </div>
        <button
          class="bulk-toggle"
          :class="{ on: bulkOverwrite[camelKey(f.key)] }"
          type="button"
          :title="
            bulkOverwrite[camelKey(f.key)] ? t('editor.keepUnchangedTip') : t('editor.overwriteTip', { n: bulkCount })
          "
          @click="toggleOverwrite(f)"
        >
          {{ bulkOverwrite[camelKey(f.key)] ? t('editor.keepUnchanged') : t('editor.overwrite') }}
        </button>
      </div>
    </section>
  </div>
</template>

<style scoped>
/* 字段区容器与错误横幅：与 EditorDrawer 单机表单共用同名类（那边保留一份；本组件根节点
   会同时携带父组件 scope id，两份规则一致不冲突）。 */
.ed-fields {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 8px 18px 20px;
}

.ed-banner {
  border: 1px solid var(--danger);
  border-radius: var(--radius-ctrl);
  background: color-mix(in srgb, var(--danger) 10%, transparent);
  color: var(--danger);
  font-size: var(--fs-body);
  line-height: 1.6;
  padding: 8px 10px;
  word-break: break-word;
}

.ed-banner-required {
  border-color: var(--danger);
}

/* 混合协议提示：中性色（信息性提示，非错误——跨源禁存才用红色 ed-banner） */
.bulk-info-banner {
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1.6;
  padding: 8px 10px;
  word-break: break-word;
}

/* 分组区块与标题：与 EditorDrawer 单机表单同款（sticky 标题在滚动区贴顶） */
.ed-group {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.ed-group-title {
  position: sticky;
  top: 0;
  z-index: 1;
  margin: 0;
  padding: 6px 0 5px;
  background: var(--bg-panel);
  border-bottom: 1px solid var(--border);
  color: var(--text-2);
  font-size: var(--fs-body);
  font-weight: 600;
  line-height: 1.2;
}

.ed-group-desc {
  margin: -4px 0 0;
  color: var(--text-4);
  font-size: var(--fs-caption);
  line-height: 1.5;
}

.ed-group-note {
  margin: 8px 0 0;
  color: var(--accent-text);
  font-size: var(--fs-caption);
  line-height: 1.5;
}

/* 批量模式字段行：FormField（或占位行） + 右侧「覆盖/保持不变」切换 */
.bulk-field {
  display: flex;
  align-items: center;
  gap: 8px;
}

.bulk-control {
  flex: 1;
  min-width: 0;
}

/* 覆盖/保持切换：对齐 TableToolbar .bb-btn 参数（h24/0 10/fs-body/中性 hover）；
   on 态从"仅边框文字"改容器型（accent-container 底），与分段控件 on 语言一致（F5） */
.bulk-toggle {
  flex: 0 0 auto;
  height: var(--ctrl-h-s);
  padding: 0 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: var(--fs-body);
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}

.bulk-toggle:hover {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}

.bulk-toggle.on {
  border-color: var(--accent);
  background: var(--accent-container);
  color: var(--accent-text);
}

/* 「各不相同/未读取」占位行：布局对齐 FormField（148px 标签列 + 控件列） */
.bulk-keep {
  display: grid;
  grid-template-columns: 148px minmax(0, 1fr);
  gap: 4px 10px;
  align-items: center;
}

.bulk-keep-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--fs-body);
  color: var(--text-2);
}

.ff-required-like {
  margin-left: 2px;
  color: var(--danger);
}

.bulk-hint {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-4);
  font-size: var(--fs-body);
  font-style: italic;
}
</style>
