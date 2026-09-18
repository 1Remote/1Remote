<script setup>
/**
 * 连接编辑器抽屉：右侧滑入（clamp(560px, 68vw, 900px)，CSS 过渡）。承载抽屉骨架
 *（头部/滚动体/底部按钮）与单机编辑（create/edit/复制）的 schema 驱动单页表单——
 * 全部分组垂直铺开在一个滚动区（分区标题 sticky），底部 取消/保存 恒贴底。头部展示
 * 拆分至 EditorHead，批量编辑（mode='bulk'）拆分至 BulkEditForm（接缝见两文件头）。
 *
 * 数据纪律（两个 casing 域）：抽屉内的 json 是编辑器配置域——PascalCase 键原样直通
 *（GET /config 回读、POST/PUT 回传，不做命名转换：后端 CreateFromJsonString 的
 * Protocol/ClassVersion 访问大小写敏感）；加载 = 深拷贝入响应式 json，schema 未列
 * 字段原样保留（透传保真），保存 = 整个 json 克隆回传。批量 patch 则是 camelCase 域。
 *
 * 脏检测：加载完成时留 initialSnapshot（非响应式深拷贝）作基准，dirty = 协议切换或
 * json 任一键与基准不一致（批量 = 有覆盖态字段，BulkEditForm 暴露）。隐藏字段
 *（visibleWhen 不满足）只藏 UI 不删值，保存时随 json 原样回传。
 *
 * 凭据组对齐 WPF CredentialView（分段细节见 groupBlocks）；新建模式可在头部改选数据
 * 源（EditorHead 持有，经 ds-change 镜像到本组件，保存与凭据库选项跟随），编辑/复制/
 * 批量仍用传入 ds。快捷键（window 级）：Ctrl/Cmd+S 保存；Esc 关闭（脏则先确认），
 * ServerListView 的全局 Esc 链在抽屉打开时不消费。复制（duplicateFrom）：create 语义
 * + 预填来源 config（Id 防御性 delete，TreeNodes 随 json 携带 → 同文件夹，与 WPF
 * 复制一致）。保存成功后列表刷新由 SSE reload 自动完成，无需手动拉取。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import FormField from './FormField.vue'
import SwitchItem from './SwitchItem.vue'
import EditorHead from './EditorHead.vue'
import BulkEditForm from './BulkEditForm.vue'
import HelpLink from '../HelpLink.vue'
import { PROTOCOLS } from '../../editor/schemas.js'
import { isVisible } from '../../editor/visibility.js'
import { switchProtocol } from '../../editor/protocolSwitch.js'
import { deriveCredentialMode } from '../../editor/credentialMode.js'
import { opaqueHex } from '../../utils/color.js'
import { api } from '../../api'

const props = defineProps({
  /** 'create' | 'edit'（create + duplicateFrom = 复制预填，保存走 POST 新建）| 'bulk' */
  mode: { type: String, required: true },
  /** edit 模式目标服务器 id */
  serverId: { type: String, default: '' },
  /** 归属数据源（加载 config 与保存的 ds 参数；bulk 模式取 bulkServers[0] 所属） */
  dataSourceName: { type: String, default: 'Local' },
  /** create 模式初始协议（PROTOCOLS key），缺省 RDP */
  protocol: { type: String, default: '' },
  /** create 模式初始文件夹路径（"a/b"，当前树选中）：注入 defaults.TreeNodes，
   *  新建服务器直接落在该文件夹内（batch9 #6；复制 duplicateFrom 不消费——随来源路径） */
  initialFolder: { type: String, default: '' },
  /** 列表 DTO 摘要（编辑头部展示名等兜底） */
  initialServer: { type: Object, default: null },
  /** 复制来源服务器 id（create 语义预填） */
  duplicateFrom: { type: String, default: '' },
  /** bulk 模式：目标服务器 id 列表（父级去重后的勾选集；透传 BulkEditForm） */
  bulkIds: { type: Array, default: () => [] },
  /** bulk 模式：与 bulkIds 对应的列表 DTO（camelCase 域；透传 BulkEditForm/EditorHead） */
  bulkServers: { type: Array, default: () => [] },
})
const emit = defineEmits(['close', 'saved'])
const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()

const isBulk = computed(() => props.mode === 'bulk')

// ---- 状态 ----
const json = reactive({}) // 编辑中的配置（PascalCase 直通；见文件头数据纪律）
let initialSnapshot = {} // 加载完成时的深拷贝基准（脏检测；非响应式）
let loadedProtocol = '' // 加载时的 json.Protocol（协议切换脏判定）
const loading = ref(false)
const loadError = ref('')
const saving = ref(false) // 单机保存中（批量保存中在 BulkEditForm，经 bulkFormRef 同步）
const saveErrors = ref([]) // 服务端 400 的 {errors} 列表（内联展示）
const missingRequired = ref([]) // 客户端必填快速校验（字段文案列表）

// ---- 头部接缝：EditorHead 实例（title 暴露给 aria-label）与数据源镜像 ----
const headRef = ref(null)
// 数据源选择值由 EditorHead 持有（n-select），用户改选经 ds-change 上抛到本镜像——
// 保存（create 的 ds 参数）与表单字段（凭据库选项按 ds 隔离）读这份镜像
const ds = ref(props.dataSourceName || 'Local')
function onHeadDsChange(v) {
  ds.value = v
}

// ---- 派生：协议 schema ----
const protocolKey = computed(() => {
  const p = json.Protocol
  return p && PROTOCOLS[p] ? p : ''
})
const schema = computed(() => PROTOCOLS[protocolKey.value] || null)
const groups = computed(() => schema.value?.groups || [])

// ---- 凭据组二选一：模式初值派生 + 切换清引用 ----
const credentialMode = ref('manual') // 'manual' | 'vault'（load/协议切换时按 json 派生）
function isCredentialGroup(g) {
  return g.id === 'credential' && g.fields.some((f) => f.key === 'InheritedCredentialName')
}
function onCredModeSwitch(mode) {
  if (mode === credentialMode.value) return
  credentialMode.value = mode
  if (mode === 'manual') json.InheritedCredentialName = '' // 手动 = 清空库引用（有意语义）
}
/**
 * 组内可见字段 → 渲染块序列：visibleWhen 过滤后，连续 SWITCH 字段聚成一个 'switch-run'
 * 块（渲染为单个 form-field 行——标签列 + 控件列内所有开关项水平排列、flex-wrap 自动
 * 换行，RDP 高级组的 9 个 Enable* 同聚一行）；非 SWITCH 字段打断连续段、按单字段整行
 * 渲染（维持 148px 网格不变）。switchWithLabel 字段（IsPingBeforeConnect 可用性检测行）
 * 例外——它需要标签列文字的整行形态（对齐 WPF HostView.xaml:29-38），不进聚合行，一律
 * 按 'single' 整行渲染（也据此打断连续 switch 段）。
 * 行标题：块的 titleKey 取段首字段的 runTitleKey（schemas.js 挂在连续开关段第一个
 * 字段上）——有值时聚合行标签列渲染标题文字（RDP 资源重定向区对齐 WPF 的行标题列
 * server_editor_advantage_resources「共享到远程桌面」，RdpFormView.xaml:427-444）；
 * 无值（其余开关组 WPF 无行标题）标签列保持空占位。
 */
function blocksOf(fields) {
  const blocks = []
  for (const f of fields) {
    const asRun = f.type === 'switch' && !f.switchWithLabel
    const last = blocks[blocks.length - 1]
    if (asRun && last?.type === 'switch-run') last.fields.push(f)
    else
      blocks.push(asRun ? { type: 'switch-run', fields: [f], titleKey: f.runTitleKey } : { type: 'single', field: f })
  }
  return blocks
}

/**
 * 组 → 渲染块序列。非凭据组：整组可见字段走 blocksOf。
 * 凭据组（对齐 WPF CredentialView.xaml 的区段顺序）四段：
 *  ① 'pre'（RDP 的 Domain/LoadBalanceInfo）——二选一切换之前，两模式恒显；
 *  ② 'cred-mode' 伪块（手动 ⇄ 凭据库切换；vault 态后随 'cred-hint' 提示行）；
 *  ③ manual → 'identity'（UserName/Password/PrivateKey）；vault → 'picker'（库选择器）；
 *  ④ 'option'（AskPasswordWhenConnect 等）——两模式恒显收尾，WPF 中开关行不在 manual
 *    块内：vault 态下 Domain/LoadBalanceInfo/AskPasswordWhenConnect 依旧可见（owner
 *    验收核心诉求）。credRole 由 schemas.js credentialGroup 注入；子表单行内字段无
 *    credRole，不参与分段（localAppConnectionGroup 无 InheritedCredentialName，
 *    isCredentialGroup 已排除，不受影响）。
 */
function groupBlocks(g) {
  const visible = g.fields.filter((f) => isVisible(f, json))
  if (!isCredentialGroup(g)) return blocksOf(visible)
  if (import.meta.env.DEV) {
    // 防御：凭据组内漏标 credRole 的字段会被下面的 byRole 分段静默丢弃（值仍随 json
    // 透传，仅 UI 缺行）——dev 构建下告警，便于 schema 变更时及早发现
    const untagged = visible.filter((f) => !f.credRole).map((f) => f.key)
    if (untagged.length) {
      console.warn('[EditorDrawer] credential group fields without credRole will not render:', untagged)
    }
  }
  const byRole = (role) => blocksOf(visible.filter((f) => f.credRole === role))
  return [
    ...byRole('pre'),
    { type: 'cred-mode' },
    ...(credentialMode.value === 'manual' ? byRole('identity') : [{ type: 'cred-hint' }, ...byRole('picker')]),
    ...byRole('option'),
  ]
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
  if (isBulk.value) return // 批量模式不加载单台 config（共享值来自 bulkServers prop，BulkEditForm 同步计算）
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
        // 文件夹内新建（batch9 #6）：TreeNodes 预置当前树选中文件夹路径——保存后服务器
        // 落在该文件夹（WPF 在文件夹上新建服务器同语义）；协议切换时 TreeNodes 由
        // PASSTHROUGH_KEEP 保留（protocolSwitch.js），不会因切换丢归属
        if (props.initialFolder) raw.TreeNodes = props.initialFolder.split('/')
      }
    } else {
      const cfg = await api.getServerConfig(props.serverId, props.dataSourceName)
      raw = cfg.json
    }
    replaceJson(raw)
    initialSnapshot = deepClone(raw)
    loadedProtocol = raw.Protocol || ''
    credentialMode.value = deriveCredentialMode(raw.InheritedCredentialName) // 模式初值派生
    if (!PROTOCOLS[loadedProtocol]) {
      // 未来版本新增协议（schema 未收录）：json 可透传但无法渲染表单，明确告知而非渲染空表单
      loadError.value = t('editor.unsupportedProtocol', { p: loadedProtocol || '?' })
    }
  } catch (e) {
    loadError.value = e?.message || String(e)
  } finally {
    loading.value = false
  }
}

// ---- 批量表单接缝：保存入口/按钮态/脏态经模板 ref 走 BulkEditForm 的 defineExpose ----
const bulkFormRef = ref(null)
const bulkSaving = computed(() => !!bulkFormRef.value?.saving) // 批量保存中（底部按钮禁用/文案）
const bulkDsMixed = computed(() => !!bulkFormRef.value?.dsMixed) // 跨数据源勾选禁存
function onBulkSaved(e) {
  emit('saved', e) // { mode:'bulk', ids } 原样转发父级（外部事件序与拆分前一致）
  doClose() // 关闭动画属本组件；列表刷新由 SSE reload 自动完成
}

// ---- 脏检测：整体 dirty = 协议切换 或 json 任一键与基准不一致 ----
function valueChanged(cur, init) {
  const a = cur === undefined ? undefined : JSON.stringify(cur)
  const b = init === undefined ? undefined : JSON.stringify(init)
  return a !== b
}
const dirty = computed(() => {
  if (isBulk.value) return !!bulkFormRef.value?.dirty // 覆盖态字段数即脏态（值变化不退出覆盖，无需更细）
  if (protocolKey.value !== loadedProtocol) return true
  return Object.keys(json).some((k) => valueChanged(json[k], initialSnapshot[k]))
})

// ---- 协议切换（编辑/新建/复制均可；字段携带规则见 protocolSwitch.js）----
function onProtocolSwitch(next) {
  if (next === protocolKey.value) return
  const from = PROTOCOLS[protocolKey.value]
  const to = PROTOCOLS[next]
  if (!from || !to) return
  replaceJson(switchProtocol(json, from, to))
  credentialMode.value = deriveCredentialMode(json.InheritedCredentialName) // 携带后的值重派生
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
    bulkFormRef.value?.save() // 批量保存流在 BulkEditForm（含 saving 防重入/校验/错误展示）
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
      const resp = await api.createServer(payload, ds.value) // 新建可改选数据源（头部镜像值）
      savedId = resp?.id || ''
      message.success(t('editor.created', { name: displayName() }))
    } else {
      await api.updateServer(props.serverId, payload, ds.value)
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

// 图标预览底色：当前 ColorHex 的低饱和 tint，即时联动基本信息组的图标缩略图
const iconTint = computed(() => opaqueHex(json.ColorHex) || '')

onMounted(() => {
  requestAnimationFrame(() => (show.value = true)) // 首帧后再置开 → 进场过渡生效
  load() // 数据源选项由 EditorHead 自行拉取（其 onMounted 内按 showDsSelect 守卫）
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
    <section class="ed-panel" role="dialog" aria-modal="true" :aria-label="headRef?.title">
      <!-- 头部（EditorHead）：瓦片/标题/协议切换/数据源/关闭；协议切换与关闭确认的
           状态机在本组件（经 protocol-change / close 上抛执行） -->
      <EditorHead
        ref="headRef"
        :mode="mode"
        :duplicate-from="duplicateFrom"
        :protocol="protocol"
        :protocol-key="protocolKey"
        :color-hex="json.ColorHex || ''"
        :display-name="json.DisplayName || ''"
        :initial-server="initialServer"
        :server-id="serverId"
        :bulk-servers="bulkServers"
        :loading="loading"
        :load-error="loadError"
        :data-source-name="dataSourceName"
        @close="requestClose"
        @protocol-change="onProtocolSwitch"
        @ds-change="onHeadDsChange"
      />

      <!-- 主体：加载/错误态 或 表单（批量=BulkEditForm 覆盖列表；单机=单页分区滚动） -->
      <div class="ed-body">
        <div v-if="loading" class="ed-state">{{ t('editor.loading') }}</div>
        <div v-else-if="loadError" class="ed-state">
          <div class="ed-state-title">{{ t('editor.loadFailed') }}</div>
          <div class="ed-state-detail">{{ loadError }}</div>
        </div>
        <template v-else>
          <!-- 批量模式：BULK_FIELDS 覆盖列表 + 保存流（接缝见 BulkEditForm 文件头） -->
          <BulkEditForm
            v-if="isBulk"
            ref="bulkFormRef"
            :bulk-ids="bulkIds"
            :bulk-servers="bulkServers"
            :data-source-name="dataSourceName"
            @saved="onBulkSaved"
          />

          <!-- 单机模式：全部分组垂直铺开 + 分区标题（sticky），整体一个滚动区 -->
          <template v-else>
            <div class="ed-fields">
              <div v-if="missingRequired.length" class="ed-banner ed-banner-required">
                {{ t('editor.missingRequired', { keys: missingRequired.join(', ') }) }}
              </div>
              <div v-if="saveErrors.length" class="ed-banner">
                <div v-for="(err, i) in saveErrors" :key="i">{{ err }}</div>
              </div>
              <section v-for="g in groups" :key="g.id" class="ed-group">
                <h3 class="ed-group-title">
                  {{ g.labelKey ? t(g.labelKey) : g.id }}
                  <!-- 组标题帮助链接：WPF 表单组标题旁 (?)/说明的
                       web 落点之一（如 RDP mstsc 组 → mstsc 模式文档） -->
                  <HelpLink v-if="g.helpUrl" :href="g.helpUrl" />
                </h3>
                <div v-if="g.descKey" class="ed-group-desc">{{ t(g.descKey) }}</div>
                <!-- 组内提示行：WPF 表单首行说明文字 + 链接（VNC 的 RFB 专有协议
                     警告 + [More details]）；文字与 URL 由 schema 硬编码（WPF 同为字面量） -->
                <p v-if="g.note" class="ed-group-note">
                  {{ g.note
                  }}<HelpLink v-if="g.noteUrl" :href="g.noteUrl" badge="">{{ g.noteUrlLabel || '' }}</HelpLink>
                </p>

                <!-- 渲染块循环：switch-run 聚合行 / 整行字段 + 凭据组的 cred-mode /
                     cred-hint 伪块（分段顺序见 groupBlocks） -->
                <template v-for="(b, bi) in groupBlocks(g)" :key="bi">
                  <!-- 凭据组二选一：标签列对齐 FormField 的 148px 网格 -->
                  <div v-if="b.type === 'cred-mode'" class="ed-cred-mode">
                    <span class="ed-cred-mode-label">{{ t('editor.credMode.label') }}</span>
                    <div class="ed-seg" role="tablist">
                      <button
                        type="button"
                        role="tab"
                        :aria-selected="credentialMode === 'manual'"
                        :class="{ on: credentialMode === 'manual' }"
                        @click="onCredModeSwitch('manual')"
                      >
                        {{ t('editor.credMode.manual') }}
                      </button>
                      <button
                        type="button"
                        role="tab"
                        :aria-selected="credentialMode === 'vault'"
                        :class="{ on: credentialMode === 'vault' }"
                        @click="onCredModeSwitch('vault')"
                      >
                        {{ t('editor.credMode.vault') }}
                      </button>
                    </div>
                  </div>
                  <!-- vault 模式提示行（紧贴切换下方，现状语义保留） -->
                  <div v-else-if="b.type === 'cred-hint'" class="ed-cred-hint-row">
                    <span></span>
                    <span class="ed-cred-hint">{{ t('editor.credMode.vaultHint') }}</span>
                  </div>
                  <!-- 连续 SWITCH 聚合行：一个 form-field 行 = 标签列（148px，段首字段带
                       runTitleKey 时渲染行标题，否则空占位）+ 控件列，所有开关项
                       （SwitchItem，与单字段开关行同款渲染）在控件列水平排列、flex-wrap
                       自动换行（RDP 高级组 9 个 Enable* 带行标题「共享到远程桌面」；
                       凭据组 option 开关 / 显示组附属开关无行标题，标签列留空） -->
                  <div v-else-if="b.type === 'switch-run'" class="form-field ed-switch-row">
                    <span v-if="b.titleKey" class="ed-switch-row-title" :title="t(b.titleKey)">
                      {{ t(b.titleKey) }}
                    </span>
                    <span v-else></span>
                    <div class="ed-switch-row-control">
                      <SwitchItem
                        v-for="f in b.fields"
                        :key="f.key"
                        :field="f"
                        :model-value="json[f.key]"
                        @update:model-value="(v) => setField(f.key, v)"
                      />
                    </div>
                  </div>
                  <FormField
                    v-else
                    :field="b.field"
                    :model-value="json[b.field.key]"
                    :data-source-name="ds"
                    :tint="iconTint"
                    @update:model-value="(v) => setField(b.field.key, v)"
                  />
                </template>
              </section>
            </div>
          </template>
        </template>
      </div>

      <!-- 底部：快捷键提示 + 取消/保存（批量保存中/跨源禁存态经 bulkFormRef 同步） -->
      <footer class="ed-foot">
        <span class="ed-hint">{{ t('editor.saveHint') }}</span>
        <div class="ed-foot-btns">
          <button class="ed-btn" type="button" :disabled="saving || bulkSaving" @click="requestClose">
            {{ t('editor.cancel') }}
          </button>
          <button
            class="ed-btn ed-primary"
            type="button"
            :disabled="saving || bulkSaving || loading || !!loadError || bulkDsMixed"
            @click="save"
          >
            {{ saving || bulkSaving ? t('editor.saving') : t('editor.save') }}
          </button>
        </div>
      </footer>
    </section>
  </div>
</template>

<style scoped>
/* 蒙层 + 右滑面板：width clamp(560px, 68vw, 900px)。覆盖范围从顶栏下沿开始
   （top: var(--topbar-h)）而非 inset:0——抽屉打开时顶栏（窗口拖拽区/最小化-最大化-
   关闭）不再被蒙层盖住、保持可交互。--topbar-h 定义于 App.vue 的 .shell（44px）；
   .ed-root 是 .shell 的 DOM 后代（ServerListView 内），自定义属性沿 DOM 树继承；
   回退值与 .shell 保持一致。滑入过渡/蒙层点击关闭/sticky 分组标题均为 .ed-root
   内部相对定位，不受影响。 */
.ed-root {
  position: fixed;
  top: var(--topbar-h, 44px);
  right: 0;
  bottom: 0;
  left: 0;
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
  font-size: 1rem;
  padding: 24px;
}

.ed-state-detail {
  max-width: 80%;
  color: var(--text-4);
  font-size: 0.9231rem;
  word-break: break-all;
  text-align: center;
}

/* 字段区：唯一滚动容器，全部分组垂直铺开（批量分支的同名容器/横幅样式由
   BulkEditForm 自持一份） */
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

/* 分组区块：分区标题 sticky 于滚动区顶部（滚动时贴顶，不遮字段） */
.ed-group {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.ed-group-title {
  position: sticky;
  top: 0;
  /* 相对滚动口贴顶（sticky 参照 scrollport，容器 padding 不影响偏移） */
  z-index: 1;
  margin: 0;
  padding: 6px 0 5px;
  background: var(--bg-panel);
  /* 滚动内容从标题下穿过时不透底 */
  border-bottom: 1px solid var(--border);
  color: var(--text-2);
  font-size: 0.9615rem;
  font-weight: 600;
  line-height: 1.2;
}

.ed-group-desc {
  margin: -4px 0 0;
  color: var(--text-4);
  font-size: 0.8846rem;
  line-height: 1.5;
}
/* 组内提示行：WPF VncFormView 的 RFB 警告行——强调色文字（WPF AccentMidBrush 同语义） */
.ed-group-note {
  margin: 8px 0 0;
  color: var(--accent-text);
  font-size: 0.8846rem;
  line-height: 1.5;
}

/* 凭据组二选一：标签列对齐 FormField 的 148px 网格 */
.ed-cred-mode {
  display: grid;
  grid-template-columns: 148px minmax(0, 1fr);
  gap: 4px 10px;
  align-items: center;
}

.ed-cred-mode-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.9615rem;
  color: var(--text-2);
}

.ed-seg {
  display: inline-flex;
  align-self: start;
  border: 1px solid var(--border);
  border-radius: 6px;
  overflow: hidden;
}

.ed-seg button {
  border: none;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 0.9231rem;
  line-height: 1;
  padding: 6px 12px;
  cursor: pointer;
  white-space: nowrap;
}

.ed-seg button + button {
  border-left: 1px solid var(--border);
}

.ed-seg button:hover:not(.on) {
  background: var(--bg-hover);
  color: var(--text-1);
}

.ed-seg button.on {
  background: var(--accent-container);
  color: var(--accent-text);
}

.ed-cred-hint-row {
  display: grid;
  grid-template-columns: 148px minmax(0, 1fr);
  gap: 4px 10px;
  align-items: center;
  margin-top: -6px;
}

.ed-cred-hint {
  color: var(--text-4);
  font-size: 0.8846rem;
  line-height: 1.5;
}

/* 连续 SWITCH 聚合行：单个 form-field 形态——标签列占位 148px（段首字段带 runTitleKey
   时渲染行标题，样式对齐 FormField 的 .ff-label-text）+ 控件列（.ff-control 同款右列）
   内所有开关项水平排列、flex-wrap 自动换行（1280 宽 RDP 高级组 9 个 Enable* 约 3-4 项
   一行）；项内 [开关][6px][文字] 由 SwitchItem 自带（与单字段开关行共用同一渲染） */
.ed-switch-row {
  display: grid;
  grid-template-columns: 148px minmax(0, 1fr);
  gap: 4px 10px;
  align-items: start;
}

/* 聚合行行标题：与 .ff-label-text 同款排版（12.5px/--text-2/超长省略，title 属性悬浮
   全文）；对齐 WPF 资源重定向区的行标题列（server_editor_advantage_resources） */
.ed-switch-row-title {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.9615rem;
  color: var(--text-2);
}

.ed-switch-row-control {
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px 16px;
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
  font-size: 0.8846rem;
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
  font-size: 0.9615rem;
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
