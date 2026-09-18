<script setup>
/**
 * 批量编辑表单（自 EditorDrawer 拆出，仅在抽屉 mode='bulk' 时挂载）：BULK_FIELDS
 * 扁平列表 + 逐字段「保持不变 / 覆盖」切换，不含抽屉骨架/头部/底部按钮（留在 EditorDrawer）。
 *
 * 数据来源（两路合并）：
 *  - 列表 DTO（camelCase 域，bulkServers prop）：dtoKey 字段的共享值逐字段计算——全同 →
 *    只读展示；不同 →「N 台各不相同」；
 *  - peek 回读（挂载时 POST /api/servers/batch/peek）：dtoKey=null 且非敏感的
 *    五键（inheritedCredentialName/askPasswordWhenConnect/startupAutoCommand/startupPath/
 *    rdpFileAdditionalSettings）由 peek 补齐 known——同样参与共享值展示与「覆盖」初值；
 *    勾选 ≤50 台才回读（防大库风暴，>50 维持「未回读」提示），失败静默退化（字段回到
 *    未回读占位，不阻断表单）。password 例外：敏感字段永不回读（bulkSensitive 文案不变）。
 *
 * 每字段默认「保持不变」（不进 patch），点「覆盖」后从共享值（已知且全同）或空值起编辑；
 * 保存 = diffPatch(共享初值, 当前值) 仅取被覆盖字段 → POST /api/servers/batch
 *（patch 键 camelCase，缺失 = 保持不变）。表单字段限于后端 BatchPatchFieldMap 的
 * allow-list（schemas.js BULK_FIELDS），深层/子表单字段不参与批量。
 *
 * 与抽屉的接缝（保持拆分前的事件序与按钮态）：
 *  - 保存入口统一在抽屉（底部保存按钮 / Ctrl+S）：抽屉经模板 ref 调本组件 save()，
 *    内部含 saving 防重入、必填校验与服务端错误内联展示；
 *  - 保存成功：message.success → emit('saved', { mode:'bulk', ids }) → 抽屉转发父级并
 *    执行关闭动画（doClose 属抽屉的进出场职责；列表刷新由 SSE reload 自动完成）；
 *  - saving / dirty（有字段处于覆盖态即脏）/ dsMixed（跨数据源勾选禁存）经 defineExpose
 *    暴露，抽屉的底部按钮禁用态与 Esc 关闭的脏确认经模板 ref 响应式读取。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import FormField from './FormField.vue'
import { BULK_FIELDS } from '../../editor/schemas.js'
import { diffPatch } from '../../editor/patch.js'
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

// ---- peek 回读：选中 ≤50 台时拉取五键非敏感字段，>50/失败静默退化 ----
// bulkDsMixed 的 ds 以 bulkServers 为准，与本表单保存口径一致；peek 也按单 ds 语义调用。
const PEEK_LIMIT = 50
const peekItems = ref(null) // null=未回读；Array<{id, askPasswordWhenConnect, ...}>（camelCase）
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

// ---- 深拷贝（JSON 往返：与共享值/patch 的序列化语义一致，reactive 代理脱钩；
// null/undefined 原样保留——peek 的协议不适用字段以 null 占位参与共享值比较）----
function deepClone(o) {
  return o == null ? o : JSON.parse(JSON.stringify(o))
}

// ---- 共享值计算 + 逐字段「保持不变/覆盖」状态 ----
// bulkServers 是列表 DTO（camelCase）；bulkShared[key] = { known, same, value }：
//  - dtoKey 有值 → known=true，value 为 N 台的共享值（same=false 时无意义，仅 same 参与 UI）；
//  - dtoKey=null 且非敏感 → known 由 peek 决定（回读成功时逐台比对，键名与 patch 键一致）；
//  - dtoKey=null 且敏感（password）或 peek 未就绪 → known=false，只提示、不展示值。
// 相等判定与 patch.js 同口径（JSON.stringify 严格比对，数组整体比较）。
const bulkFields = computed(() =>
  BULK_FIELDS.filter((f) => !f.protocols || props.bulkServers.every((s) => f.protocols.includes(s.protocol)))
)
const bulkShared = computed(() => {
  const out = {}
  for (const f of BULK_FIELDS) {
    // peek 回读键名 = BULK_FIELDS 的 key（camelCase patch 键，与 BatchPeekItem 序列一致）
    if (!f.dtoKey && !f.bulkSensitive && peekMap.value) {
      const vals = props.bulkIds.map((id) => peekMap.value[id]?.[f.key])
      const first = JSON.stringify(vals[0])
      const same = vals.every((v) => JSON.stringify(v) === first)
      out[f.key] = { known: true, same, value: same ? deepClone(vals[0]) : undefined }
      continue
    }
    if (!f.dtoKey) {
      out[f.key] = { known: false, same: false, value: undefined }
      continue
    }
    const vals = props.bulkServers.map((s) => s?.[f.dtoKey])
    const first = JSON.stringify(vals[0])
    const same = vals.every((v) => JSON.stringify(v) === first)
    out[f.key] = { known: true, same, value: same ? deepClone(vals[0]) : undefined }
  }
  return out
})
const bulkOverwrite = reactive({}) // key → true（已切到覆盖编辑）；缺省 = 保持不变
const bulkValues = reactive({}) // key → 覆盖态下的当前值（仅覆盖态有意义）

// 未回读字段（known=false）的占位文案分两类：敏感字段（bulkSensitive，如 password——
// 列表接口不回读明文，安全设计，与 WPF 哨兵机制同源；peek 也不回读）用
// bulkSensitiveHint；其余（继承凭据/连接时询问密码/协议专属键——peek 未就绪时：
// 勾选 >50 台或回读失败）用 bulkUnknown。两者都只引导「覆盖」式设置。
function bulkUnknownKey(f) {
  return f.bulkSensitive ? 'editor.bulkSensitiveHint' : 'editor.bulkUnknown'
}
const bulkCount = computed(() => props.bulkServers.length)
const bulkDsNames = computed(() => new Set(props.bulkServers.map((s) => s.dataSourceName || 'Local')))
// 后端 batch 端点单 ds 语义：跨数据源勾选无法一次落库 → 明确告知并禁存（不做静默裁剪）
const bulkDsMixed = computed(() => bulkDsNames.value.size > 1)
const bulkDs = computed(() => props.bulkServers[0]?.dataSourceName || props.dataSourceName || 'Local')

function emptyValueFor(field) {
  if (field.type === 'tags') return []
  if (field.type === 'switch') return false
  return ''
}
function toggleOverwrite(field) {
  const key = field.key
  if (bulkOverwrite[key]) {
    bulkOverwrite[key] = false // 回到「保持不变」：该字段退出 patch
    return
  }
  const shared = bulkShared.value[key]
  bulkValues[key] = shared.known && shared.same ? deepClone(shared.value) : emptyValueFor(field)
  bulkOverwrite[key] = true
}
// 有字段处于覆盖态即脏（值变化不退出覆盖，无需更细）——抽屉的 Esc 关闭确认据此判断
const bulkDirty = computed(() => Object.values(bulkOverwrite).some(Boolean))
// 覆盖态字段的共享初值/当前值对 → diffPatch 仅产出真正变化的键（值与共享值相同的覆盖不产生写入）
function buildBulkPatch() {
  const initial = {}
  const current = {}
  for (const f of bulkFields.value) {
    if (!bulkOverwrite[f.key]) continue
    const shared = bulkShared.value[f.key]
    if (shared.known && shared.same) initial[f.key] = shared.value
    current[f.key] = bulkValues[f.key]
  }
  return diffPatch(initial, current)
}
function validateBulkRequired() {
  const missing = []
  for (const f of bulkFields.value) {
    if (!f.required || !bulkOverwrite[f.key]) continue
    const v = bulkValues[f.key]
    if (v == null || (typeof v === 'string' && v.trim() === '')) {
      missing.push(f.labelKey ? t(f.labelKey) : f.key)
    }
  }
  return missing
}

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
    <div v-if="missingRequired.length" class="ed-banner ed-banner-required">
      {{ t('editor.missingRequired', { keys: missingRequired.join(', ') }) }}
    </div>
    <div v-if="saveErrors.length" class="ed-banner">
      <div v-for="(err, i) in saveErrors" :key="i">{{ err }}</div>
    </div>
    <div v-for="f in bulkFields" :key="f.key" class="bulk-field">
      <!-- 覆盖态：可编辑，值改动即时入 bulkValues -->
      <FormField
        v-if="bulkOverwrite[f.key]"
        class="bulk-control"
        :field="f"
        :model-value="bulkValues[f.key]"
        :data-source-name="bulkDs"
        @update:model-value="(v) => (bulkValues[f.key] = v)"
      />
      <!-- 保持不变 + 共享值已知且全同：只读展示 N 台当前的共同值 -->
      <FormField
        v-else-if="bulkShared[f.key].known && bulkShared[f.key].same"
        class="bulk-control"
        :field="f"
        :model-value="bulkShared[f.key].value"
        disabled
      />
      <!-- 保持不变 + 各不相同/未回读：占位行（标签列对齐 FormField 的 148px） -->
      <div v-else class="bulk-keep bulk-control">
        <div class="bulk-keep-label" :title="f.labelKey ? t(f.labelKey) : f.key">
          {{ f.labelKey ? t(f.labelKey) : f.key }}<span v-if="f.required" class="ff-required-like">*</span>
        </div>
        <div
          class="bulk-hint"
          :title="bulkShared[f.key].known ? t('editor.differentValues', { n: bulkCount }) : t(bulkUnknownKey(f))"
        >
          {{ bulkShared[f.key].known ? t('editor.differentValues', { n: bulkCount }) : t(bulkUnknownKey(f)) }}
        </div>
      </div>
      <button
        class="bulk-toggle"
        :class="{ on: bulkOverwrite[f.key] }"
        type="button"
        :title="bulkOverwrite[f.key] ? t('editor.keepUnchangedTip') : t('editor.overwriteTip', { n: bulkCount })"
        @click="toggleOverwrite(f)"
      >
        {{ bulkOverwrite[f.key] ? t('editor.keepUnchanged') : t('editor.overwrite') }}
      </button>
    </div>
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
  border-radius: 6px;
  background: color-mix(in srgb, var(--danger) 10%, transparent);
  color: var(--danger);
  font-size: 0.9231rem;
  line-height: 1.6;
  padding: 8px 10px;
  word-break: break-word;
}

.ed-banner-required {
  border-color: var(--danger);
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

.bulk-toggle {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: 5px;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 0.8846rem;
  line-height: 1;
  padding: 5px 9px;
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
  font-size: 0.9615rem;
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
  font-size: 0.9231rem;
  font-style: italic;
}
</style>
