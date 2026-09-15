<script setup>
/**
 * 连接编辑器抽屉（Plan 2 Task 8）：右侧滑入（clamp(560px, 68vw, 900px)，CSS 过渡），
 * schema 驱动表单——分组页签（未保存分组带小圆点）+ FormField 渲染 + 保存流。
 *
 * 数据纪律（计划全局约定 #3，两个 casing 域）：
 *  - 抽屉内的 json 是编辑器配置域：PascalCase 键原样直通（GET /config 回读、POST/PUT 回传），
 *    不做任何命名转换（后端 CreateFromJsonString 的 Protocol/ClassVersion 访问大小写敏感）；
 *  - 加载 = GET config → 整体深拷贝入响应式 json；schema 未列字段原样保留（透传保真），
 *    保存 = 整个 json 克隆回传（PUT 整体替换 / POST 新建）。
 *
 * 脏检测：加载完成时留 initialSnapshot（非响应式深拷贝）作基准；分组小圆点按「组内任一
 * 字段值与基准不一致」判定；整体 dirty 额外覆盖协议切换（Protocol/ClassVersion 不属于任何组）。
 * 隐藏字段（visibleWhen 不满足）只藏 UI 不删值，保存时随 json 原样回传。
 *
 * 快捷键（抽屉打开期间，window 级）：Ctrl/Cmd+S 保存；Esc 关闭（脏则先确认，naive dialog）。
 * ServerListView 的全局 Esc 链在抽屉打开时不消费 Esc（见其 onGlobalEsc 的 editor 守卫）。
 *
 * 复制（duplicateFrom）：create 语义 + 预填来源服务器 config；Id 为 [JsonIgnore] 本就不在
 * json 中（防御性 delete），后端 Create 路径也会清 Id 并生成新 ULID；TreeNodes（文件夹归属）
 * 随 json 携带 → 复制品与来源同文件夹（与 WPF 复制一致）。
 *
 * 批量模式（Plan 2 Task 10，mode='bulk'）：不加载单台 config——共享值由父级传入的列表 DTO
 * （camelCase 域，bulkServers）逐字段计算：全同 → 只读展示；不同/列表 DTO 无此字段 →
 * 「‹N 台各不相同›」/「未读取」占位。每字段默认「保持不变」（不进 patch），点「覆盖」后
 * 从共享值（已知且全同）或空值起编辑；保存 = diffPatch(共享初值, 当前值) 仅取被覆盖字段
 * → POST /api/servers/batch（patch 键 camelCase，缺失 = 保持不变）。表单字段限于后端
 * BatchPatchFieldMap 的 allow-list（schemas.js BULK_FIELDS），深层/子表单字段不参与批量。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import FormField from './FormField.vue'
import { PROTOCOLS, BULK_FIELDS } from '../../editor/schemas.js'
import { isVisible } from '../../editor/visibility.js'
import { switchProtocol } from '../../editor/protocolSwitch.js'
import { diffPatch } from '../../editor/patch.js'
import { api } from '../../api'

const props = defineProps({
  /** 'create' | 'edit'（create + duplicateFrom = 复制预填，保存走 POST 新建）| 'bulk'（Task 10 批量） */
  mode: { type: String, required: true },
  /** edit 模式目标服务器 id */
  serverId: { type: String, default: '' },
  /** 归属数据源（加载 config 与保存的 ds 参数；bulk 模式取 bulkServers[0] 所属） */
  dataSourceName: { type: String, default: 'Local' },
  /** create 模式初始协议（PROTOCOLS key），缺省 RDP */
  protocol: { type: String, default: '' },
  /** 列表 DTO 摘要（编辑头部展示名等兜底） */
  initialServer: { type: Object, default: null },
  /** 复制来源服务器 id（create 语义预填） */
  duplicateFrom: { type: String, default: '' },
  /** bulk 模式：目标服务器 id 列表（父级去重后的勾选集） */
  bulkIds: { type: Array, default: () => [] },
  /** bulk 模式：与 bulkIds 对应的列表 DTO（camelCase 域，共享值计算来源） */
  bulkServers: { type: Array, default: () => [] },
})
const emit = defineEmits(['close', 'saved'])
const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()

const isCreate = computed(() => props.mode === 'create')
const isDuplicate = computed(() => props.mode === 'create' && !!props.duplicateFrom)
const isBulk = computed(() => props.mode === 'bulk')

// ---- 状态 ----
const json = reactive({}) // 编辑中的配置（PascalCase 直通；见文件头数据纪律）
let initialSnapshot = {} // 加载完成时的深拷贝基准（脏检测；非响应式）
let loadedProtocol = '' // 加载时的 json.Protocol（协议切换脏判定）
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveErrors = ref([]) // 服务端 400 的 {errors} 列表（内联展示）
const missingRequired = ref([]) // 客户端必填快速校验（字段文案列表）
const activeGroup = ref('') // 当前页签 group.id

// ---- 派生：协议 schema ----
const protocolKey = computed(() => {
  const p = json.Protocol
  return p && PROTOCOLS[p] ? p : ''
})
const schema = computed(() => PROTOCOLS[protocolKey.value] || null)
const groups = computed(() => schema.value?.groups || [])
const activeFields = computed(() => {
  const g = groups.value.find((x) => x.id === activeGroup.value)
  return (g?.fields || []).filter((f) => isVisible(f, json))
})
const protocolOptions = Object.keys(PROTOCOLS).map((k) => ({ value: k, label: k }))

// ---- 批量模式（Task 10）：共享值计算 + 逐字段「保持不变/覆盖」状态 ----
// bulkServers 是列表 DTO（camelCase）；bulkShared[key] = { known, same, value }：
//  - dtoKey 有值 → known=true，value 为 N 台的共享值（same=false 时无意义，仅 same 参与 UI）；
//  - dtoKey=null（note/password 等列表 DTO 不携带）→ known=false，只提示、不展示值。
// 相等判定与 patch.js 同口径（JSON.stringify 严格比对，数组整体比较）。
const bulkFields = computed(() =>
  BULK_FIELDS.filter((f) => !f.protocols || props.bulkServers.every((s) => f.protocols.includes(s.protocol))),
)
const bulkShared = computed(() => {
  const out = {}
  for (const f of BULK_FIELDS) {
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
const bulkCount = computed(() => props.bulkServers.length)
const bulkDsNames = computed(() => new Set(props.bulkServers.map((s) => s.dataSourceName || 'Local')))
// 后端 batch 端点单 ds 语义：跨数据源勾选无法一次落库 → 明确告知并禁存（不做静默裁剪）
const bulkDsMixed = computed(() => isBulk.value && bulkDsNames.value.size > 1)
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
async function saveBulk() {
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
    doClose() // 列表刷新由 SSE reload 自动完成
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

// ---- 深拷贝（JSON 往返：与 snapshot 基准的序列化语义一致， reactive 代理脱钩）----
function deepClone(o) {
  return JSON.parse(JSON.stringify(o ?? {}))
}
function replaceJson(raw) {
  for (const k of Object.keys(json)) delete json[k]
  Object.assign(json, deepClone(raw))
}
function setField(key, v) {
  json[key] = v
}

// ---- 加载 ----
async function load() {
  if (isBulk.value) return // 批量模式不加载单台 config（共享值来自 bulkServers prop，同步计算）
  loading.value = true
  loadError.value = ''
  try {
    let raw
    if (props.mode === 'create') {
      if (props.duplicateFrom) {
        const cfg = await api.getServerConfig(props.duplicateFrom, props.dataSourceName)
        raw = cfg.json
        delete raw.Id // Id 为 [JsonIgnore] 本就不在文中；防御性清理（复制=新建语义）
      } else {
        const s = PROTOCOLS[props.protocol] || PROTOCOLS.RDP
        raw = { Protocol: s.protocol, ClassVersion: s.classVersion, ...deepClone(s.defaults) }
      }
    } else {
      const cfg = await api.getServerConfig(props.serverId, props.dataSourceName)
      raw = cfg.json
    }
    replaceJson(raw)
    initialSnapshot = deepClone(raw)
    loadedProtocol = raw.Protocol || ''
    if (!PROTOCOLS[loadedProtocol]) {
      // 未来版本新增协议（schema 未收录）：json 可透传但无法渲染表单，明确告知而非渲染空表单
      loadError.value = t('editor.unsupportedProtocol', { p: loadedProtocol || '?' })
    }
    activeGroup.value = groups.value[0]?.id || ''
  } catch (e) {
    loadError.value = e?.message || String(e)
  } finally {
    loading.value = false
  }
}

// ---- 脏检测：分组小圆点 + 整体 dirty ----
function valueChanged(cur, init) {
  const a = cur === undefined ? undefined : JSON.stringify(cur)
  const b = init === undefined ? undefined : JSON.stringify(init)
  return a !== b
}
const groupDirty = computed(() => {
  const map = {}
  for (const g of groups.value) {
    map[g.id] = g.fields.some((f) => valueChanged(json[f.key], initialSnapshot[f.key]))
  }
  return map
})
const dirty = computed(() => {
  if (isBulk.value) return bulkDirty.value // 覆盖态字段数即脏态（值变化不退出覆盖，无需更细）
  return protocolKey.value !== loadedProtocol || Object.values(groupDirty.value).some(Boolean)
})

// ---- 协议切换（编辑/新建/复制均可；字段携带规则见 protocolSwitch.js）----
function onProtocolSwitch(next) {
  if (next === protocolKey.value) return
  const from = PROTOCOLS[protocolKey.value]
  const to = PROTOCOLS[next]
  if (!from || !to) return
  replaceJson(switchProtocol(json, from, to))
  activeGroup.value = groups.value[0]?.id || '' // 目标分组序列可能不同，回到首个页签
  saveErrors.value = []
  missingRequired.value = []
}

// ---- 进出场过渡与关闭（脏则确认）----
const show = ref(false)
let closing = false // 确认弹窗打开期间忽略重复 Esc/点击，避免叠弹窗
function requestClose() {
  if (closing) return
  if (!dirty.value) {
    doClose()
    return
  }
  closing = true
  dialog.warning({
    title: t('editor.unsavedTitle'),
    content: t('editor.unsavedText'),
    positiveText: t('editor.btnDiscard'),
    negativeText: t('editor.btnKeepEditing'),
    onPositiveClick: () => {
      closing = false
      doClose()
    },
    onNegativeClick: () => {
      closing = false
    },
    onClose: () => {
      closing = false // 点遮罩/右上角关闭 = 继续编辑
    },
  })
}
function doClose() {
  show.value = false
  closeTimer = setTimeout(() => emit('close'), 170) // 等滑出过渡结束再卸载
}
let closeTimer = 0

// ---- 快捷键（window 级；ServerListView 的 Esc 链在抽屉打开时不消费）----
function onKey(e) {
  if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key?.toLowerCase() === 's') {
    e.preventDefault() // 抢在浏览器「保存网页」前
    save()
  } else if (e.key === 'Escape') {
    // 字段内下拉（协议切换/凭据选择等 n-select 菜单）打开时，Esc 只关下拉：naive 只标记
    // 事件不阻断冒泡（Select.mjs 的 markEventEffectPerformed），到达此处时菜单可能已被其
    // 关闭但 DOM 要到本事件结束后才卸下——存在性即「刚刚有下拉在开」，此时不消费 Esc。
    // IconPicker 弹窗的 Esc 由其自身捕获阶段拦截（见 IconPicker.vue），不会走到这里。
    if (document.querySelector('.n-base-select-menu')) return
    e.preventDefault()
    requestClose()
  }
}

// ---- 保存 ----
function validateRequired() {
  const missing = []
  for (const g of groups.value) {
    for (const f of g.fields) {
      // 只查顶层必填（subform 行内必填如备用凭据 Name 由后端保存校验把关，schemas.js 注释同述）
      if (!f.required) continue
      const v = json[f.key]
      if (v == null || (typeof v === 'string' && v.trim() === '')) {
        missing.push(f.labelKey ? t(f.labelKey) : f.key)
      }
    }
  }
  return missing
}
function displayName() {
  return json.DisplayName || props.initialServer?.displayName || props.serverId || ''
}
async function save() {
  if (isBulk.value) {
    await saveBulk()
    return
  }
  if (saving.value || loading.value || loadError.value) return
  missingRequired.value = validateRequired()
  if (missingRequired.value.length) return
  saveErrors.value = []
  saving.value = true
  try {
    const payload = deepClone(json) // schema 字段 + 加载时的透传字段整体回传
    if (schema.value) {
      // 鉴别字段兜底：新建 defaults 不含 Protocol/ClassVersion（组装时注入）；编辑/切换由 schema 覆写
      payload.Protocol = schema.value.protocol
      payload.ClassVersion = schema.value.classVersion
    }
    let savedId = props.serverId
    if (props.mode === 'create') {
      const resp = await api.createServer(payload, props.dataSourceName)
      savedId = resp?.id || ''
      message.success(t('editor.created', { name: displayName() }))
    } else {
      await api.updateServer(props.serverId, payload, props.dataSourceName)
      message.success(t('editor.saved', { name: displayName() }))
    }
    emit('saved', { id: savedId, mode: props.mode, protocol: payload.Protocol })
    doClose() // 列表刷新由 SSE reload 自动完成，无需手动拉取
  } catch (e) {
    if (e?.status === 400) {
      const errs = e.body?.errors || (e.body?.error ? [e.body.error] : [])
      saveErrors.value = errs.length ? errs : [`${e.message}`]
    } else if (e?.status === 500 && e.body?.error) {
      saveErrors.value = [e.body.error]
    } else {
      message.error(t('editor.saveFailed') + (e?.message ? ` (${e.message})` : ''))
    }
  } finally {
    saving.value = false
  }
}

// ---- 头部 ----
const title = computed(() => {
  if (isBulk.value) return t('editor.bulkTitle', { n: bulkCount.value })
  if (isDuplicate.value) return t('editor.title.duplicate', { name: props.initialServer?.displayName || json.DisplayName || '' })
  if (isCreate.value) return t('editor.title.create', { protocol: protocolKey.value || props.protocol || '?' })
  return t('editor.title.edit', { name: props.initialServer?.displayName || json.DisplayName || props.serverId })
})
// 协议字母瓦片配色：ColorHex（#AARRGGBB）有效时低饱和底 + 同色字（样式模式对齐 ServerRow 回退瓦片）
const tileStyle = computed(() => {
  const hex = typeof json.ColorHex === 'string' && /^#[0-9a-fA-F]{8}$/.test(json.ColorHex) ? json.ColorHex : ''
  if (!hex) return null
  const rgb = '#' + hex.slice(3)
  return { background: rgb + '33', color: rgb }
})

onMounted(() => {
  requestAnimationFrame(() => (show.value = true)) // 首帧后再置开 → 进场过渡生效
  load()
  window.addEventListener('keydown', onKey)
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKey)
  clearTimeout(closeTimer) // 保存成功路径：父级已随 @saved 收敛状态并卸载，防迟到 emit
})
</script>

<template>
  <div class="ed-root" :class="{ open: show }">
    <div class="ed-scrim" @click="requestClose"></div>
    <section class="ed-panel" role="dialog" aria-modal="true" :aria-label="title">
      <!-- 头部：协议瓦片 + 标题/归属 + 协议切换 + 关闭（bulk：无协议切换，瓦片为批量符号） -->
      <header class="ed-head">
        <span class="ed-tile" :style="tileStyle">{{ isBulk ? '≡' : (protocolKey || '?').charAt(0) }}</span>
        <div class="ed-head-main">
          <div class="ed-title" :title="title">{{ title }}</div>
          <div class="ed-ds" :title="t('editor.dataSource') + ': ' + (isBulk ? bulkDs : dataSourceName)">{{ isBulk ? bulkDs : dataSourceName }}</div>
        </div>
        <n-select
          v-if="!isBulk"
          class="ed-proto"
          size="small"
          :value="protocolKey || undefined"
          :options="protocolOptions"
          :disabled="loading || !!loadError"
          :title="t('editor.protocol')"
          @update:value="onProtocolSwitch"
        />
        <button class="ed-close" type="button" :title="t('editor.close')" @click="requestClose">✕</button>
      </header>

      <!-- 主体：加载/错误态 或 分组页签 + 字段 -->
      <div class="ed-body">
        <div v-if="loading" class="ed-state">{{ t('editor.loading') }}</div>
        <div v-else-if="loadError" class="ed-state">
          <div class="ed-state-title">{{ t('editor.loadFailed') }}</div>
          <div class="ed-state-detail">{{ loadError }}</div>
        </div>
        <template v-else>
          <!-- 批量模式（Task 10）：无分组页签，BULK_FIELDS 扁平列表 + 逐字段「保持不变/覆盖」 -->
          <div v-if="isBulk" class="ed-fields">
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
              <!-- 保持不变 + 各不相同/未读取：占位行（标签列对齐 FormField 的 148px） -->
              <div v-else class="bulk-keep bulk-control">
                <div class="bulk-keep-label" :title="f.labelKey ? t(f.labelKey) : f.key">
                  {{ f.labelKey ? t(f.labelKey) : f.key }}<span v-if="f.required" class="ff-required-like">*</span>
                </div>
                <div
                  class="bulk-hint"
                  :title="bulkShared[f.key].known ? t('editor.differentValues', { n: bulkCount }) : t('editor.bulkUnknown')"
                >
                  {{ bulkShared[f.key].known ? t('editor.differentValues', { n: bulkCount }) : t('editor.bulkUnknown') }}
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

          <!-- 单机模式：分组页签 + 字段 -->
          <template v-else>
            <nav class="ed-tabs">
              <button
                v-for="g in groups"
                :key="g.id"
                class="ed-tab"
                :class="{ active: g.id === activeGroup }"
                type="button"
                @click="activeGroup = g.id"
              >
                {{ g.labelKey ? t(g.labelKey) : g.id }}
                <span v-if="groupDirty[g.id]" class="ed-dot" :title="t('editor.unsavedTab')"></span>
              </button>
            </nav>
            <div class="ed-fields">
              <div v-if="missingRequired.length" class="ed-banner ed-banner-required">
                {{ t('editor.missingRequired', { keys: missingRequired.join(', ') }) }}
              </div>
              <div v-if="saveErrors.length" class="ed-banner">
                <div v-for="(err, i) in saveErrors" :key="i">{{ err }}</div>
              </div>
              <FormField
                v-for="f in activeFields"
                :key="f.key"
                :field="f"
                :model-value="json[f.key]"
                :data-source-name="dataSourceName"
                @update:model-value="(v) => setField(f.key, v)"
              />
            </div>
          </template>
        </template>
      </div>

      <!-- 底部：快捷键提示 + 取消/保存 -->
      <footer class="ed-foot">
        <span class="ed-hint">{{ t('editor.saveHint') }}</span>
        <div class="ed-foot-btns">
          <button class="ed-btn" type="button" :disabled="saving" @click="requestClose">{{ t('editor.cancel') }}</button>
          <button
            class="ed-btn ed-primary"
            type="button"
            :disabled="saving || loading || !!loadError || bulkDsMixed"
            @click="save"
          >{{ saving ? t('editor.saving') : t('editor.save') }}</button>
        </div>
      </footer>
    </section>
  </div>
</template>

<style scoped>
/* 蒙层 + 右滑面板：width clamp(560px, 68vw, 900px)（Plan 2 Task 8 约定） */
.ed-root {
  position: fixed;
  inset: 0;
  z-index: 60;
}
.ed-scrim {
  position: absolute;
  inset: 0;
  background: rgb(0 0 0 / 40%);
  opacity: 0;
  transition: opacity 0.17s ease;
}
.ed-panel {
  position: absolute;
  top: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  width: clamp(560px, 68vw, 900px);
  background: var(--bg-panel);
  border-left: 1px solid var(--border-strong);
  box-shadow: -10px 0 28px rgb(0 0 0 / 22%);
  transform: translateX(100%);
  transition: transform 0.17s ease;
}
.ed-root.open .ed-scrim {
  opacity: 1;
}
.ed-root.open .ed-panel {
  transform: none;
}
@media (prefers-reduced-motion: reduce) {
  .ed-scrim,
  .ed-panel {
    transition: none;
  }
}

/* 头部 */
.ed-head {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border);
}
.ed-tile {
  flex: 0 0 30px;
  width: 30px;
  height: 30px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 7px;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 14px;
  font-weight: 600;
}
.ed-head-main {
  flex: 1 1 auto;
  min-width: 0;
}
.ed-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-1);
}
.ed-ds {
  display: inline-block;
  margin-top: 2px;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 1px 8px;
  background: var(--bg-elevated);
  color: var(--text-4);
  font-size: 10.5px;
}
.ed-proto {
  flex: 0 1 150px;
}
.ed-close {
  flex: 0 0 auto;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--text-3);
  font-size: 13px;
  width: 28px;
  height: 28px;
  cursor: pointer;
}
.ed-close:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}

/* 主体 */
.ed-body {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.ed-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  color: var(--text-3);
  font-size: 13px;
  padding: 24px;
}
.ed-state-detail {
  max-width: 80%;
  color: var(--text-4);
  font-size: 12px;
  word-break: break-all;
  text-align: center;
}

/* 分组页签（水平滚动）：未保存分组带小圆点 */
.ed-tabs {
  flex: 0 0 auto;
  display: flex;
  gap: 2px;
  padding: 6px 12px 0;
  border-bottom: 1px solid var(--border);
  overflow-x: auto;
}
.ed-tab {
  position: relative;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border: none;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--text-3);
  font-size: 12.5px;
  line-height: 1;
  padding: 8px 10px;
  cursor: pointer;
  white-space: nowrap;
}
.ed-tab:hover {
  color: var(--text-1);
}
.ed-tab.active {
  color: var(--accent-text);
  border-bottom-color: var(--accent);
}
.ed-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--accent);
}

/* 字段区 */
.ed-fields {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px 18px 20px;
}
.ed-banner {
  border: 1px solid var(--danger);
  border-radius: 6px;
  background: color-mix(in srgb, var(--danger) 10%, transparent);
  color: var(--danger);
  font-size: 12px;
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
  font-size: 11.5px;
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
  font-size: 12.5px;
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
  font-size: 12px;
  font-style: italic;
}

/* 底部 */
.ed-foot {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px;
  border-top: 1px solid var(--border);
  background: var(--bg-panel);
}
.ed-hint {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-4);
  font-size: 11.5px;
}
.ed-foot-btns {
  display: flex;
  gap: 8px;
}
.ed-btn {
  border: 1px solid var(--border);
  border-radius: 6px;
  background: transparent;
  color: var(--text-2);
  font-size: 12.5px;
  line-height: 1;
  padding: 7px 14px;
  cursor: pointer;
}
.ed-btn:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.ed-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.ed-primary {
  border-color: var(--accent);
  color: var(--accent-text);
}
.ed-primary:hover:not(:disabled) {
  background: var(--bg-hover);
  border-color: var(--accent);
  color: var(--accent-text);
}
</style>
