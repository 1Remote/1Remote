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
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import FormField from './FormField.vue'
import { PROTOCOLS } from '../../editor/schemas.js'
import { isVisible } from '../../editor/visibility.js'
import { switchProtocol } from '../../editor/protocolSwitch.js'
import { api } from '../../api'

const props = defineProps({
  /** 'create' | 'edit'（create + duplicateFrom = 复制预填，保存走 POST 新建） */
  mode: { type: String, required: true },
  /** edit 模式目标服务器 id */
  serverId: { type: String, default: '' },
  /** 归属数据源（加载 config 与保存的 ds 参数） */
  dataSourceName: { type: String, default: 'Local' },
  /** create 模式初始协议（PROTOCOLS key），缺省 RDP */
  protocol: { type: String, default: '' },
  /** 列表 DTO 摘要（编辑头部展示名等兜底） */
  initialServer: { type: Object, default: null },
  /** 复制来源服务器 id（create 语义预填） */
  duplicateFrom: { type: String, default: '' },
})
const emit = defineEmits(['close', 'saved'])
const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()

const isCreate = computed(() => props.mode === 'create')
const isDuplicate = computed(() => props.mode === 'create' && !!props.duplicateFrom)

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
const dirty = computed(() => protocolKey.value !== loadedProtocol || Object.values(groupDirty.value).some(Boolean))

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
      <!-- 头部：协议瓦片 + 标题/归属 + 协议切换 + 关闭 -->
      <header class="ed-head">
        <span class="ed-tile" :style="tileStyle">{{ (protocolKey || '?').charAt(0) }}</span>
        <div class="ed-head-main">
          <div class="ed-title" :title="title">{{ title }}</div>
          <div class="ed-ds" :title="t('editor.dataSource') + ': ' + dataSourceName">{{ dataSourceName }}</div>
        </div>
        <n-select
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
      </div>

      <!-- 底部：快捷键提示 + 取消/保存 -->
      <footer class="ed-foot">
        <span class="ed-hint">{{ t('editor.saveHint') }}</span>
        <div class="ed-foot-btns">
          <button class="ed-btn" type="button" :disabled="saving" @click="requestClose">{{ t('editor.cancel') }}</button>
          <button
            class="ed-btn ed-primary"
            type="button"
            :disabled="saving || loading || !!loadError"
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
