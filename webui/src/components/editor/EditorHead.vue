<script setup>
/**
 * 编辑抽屉头部（自 EditorDrawer 拆出）：协议瓦片 + 标题 + [协议标签 + 协议切换下拉]
 * + [数据库标签 + 数据源（新建可改选的 n-select，或只读 pill）] + 关闭按钮。标签是
 * 下拉框前的可见小前缀（语义不靠 tooltip 承载）；标题 flex:1 省略让位，
 * 窄抽屉 560px 下拉不换行。
 *
 * 职责边界（与抽屉的接缝）：本组件只管头部展示与数据源选择器的选项拉取；编辑器状态机
 * 留在抽屉——
 *  - 协议切换（字段携带/凭据模式重派生）与关闭（脏确认弹窗）分别经 protocol-change /
 *    close 上抛，由抽屉执行；
 *  - 数据源值在本组件持有（n-select 直接 v-model），用户改选经 ds-change 上抛，抽屉
 *    镜像一份用于保存与表单字段（单一用户驱动路径，两侧不漂移）；仅新建（非复制）实际
 *    拉取可写数据源选项（loadDsOptions 内按 showDsSelect 守卫），编辑/复制/批量保持
 *    只读 pill；bulk：无协议切换，瓦片为批量符号 ≡；
 *  - 标题文案在本组件计算（defineExpose 暴露 title），抽屉 role=dialog 的 aria-label
 *    复用同一份，避免两处维护。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { PROTOCOLS } from '../../editor/schemas.js'
import HelpLink from '../HelpLink.vue'
import { opaqueHex } from '../../utils/color.js'
import { api } from '../../api'

const props = defineProps({
  /** 'create' | 'edit'（create + duplicateFrom = 复制预填）| 'bulk' */
  mode: { type: String, required: true },
  /** 复制来源标记：影响标题文案与「新建可改选数据源」的显隐 */
  duplicateFrom: { type: String, default: '' },
  /** create 模式初始协议（PROTOCOLS key）：schema 加载完成前标题协议名的回退 */
  protocol: { type: String, default: '' },
  /** 当前 json.Protocol（未知协议/未加载为 ''；瓦片字母与协议下拉值） */
  protocolKey: { type: String, default: '' },
  /** json.ColorHex（瓦片配色来源；透明/缺失回退中性瓦片） */
  colorHex: { type: String, default: '' },
  /** json.DisplayName（标题名称回退之一） */
  displayName: { type: String, default: '' },
  /** 列表 DTO 摘要（标题优先取其 displayName） */
  initialServer: { type: Object, default: null },
  /** edit 模式目标服务器 id（标题最终回退） */
  serverId: { type: String, default: '' },
  /** bulk 模式：列表 DTO（标题台数 N 与 pill 数据源名来源） */
  bulkServers: { type: Array, default: () => [] },
  /** config 加载中/加载失败：协议下拉禁用 */
  loading: { type: Boolean, default: false },
  loadError: { type: String, default: '' },
  /** 归属数据源（新建选择器的默认选中；其余模式 pill 展示值） */
  dataSourceName: { type: String, default: 'Local' },
})
const emit = defineEmits(['close', 'protocol-change', 'ds-change'])
const { t } = useI18n()

const isCreate = computed(() => props.mode === 'create')
const isDuplicate = computed(() => props.mode === 'create' && !!props.duplicateFrom)
const isBulk = computed(() => props.mode === 'bulk')

// ---- bulk 展示量：标题台数与 pill 的数据源名（后端 batch 端点单 ds 语义，取首台所属）----
const bulkCount = computed(() => props.bulkServers.length)
const bulkDs = computed(() => props.bulkServers[0]?.dataSourceName || props.dataSourceName || 'Local')

// ---- 标题 / 协议瓦片 ----
const title = computed(() => {
  if (isBulk.value) return t('editor.bulkTitle', { n: bulkCount.value })
  if (isDuplicate.value)
    return t('editor.title.duplicate', { name: props.initialServer?.displayName || props.displayName || '' })
  if (isCreate.value) return t('editor.title.create', { protocol: props.protocolKey || props.protocol || '?' })
  return t('editor.title.edit', { name: props.initialServer?.displayName || props.displayName || props.serverId })
})
// 协议字母瓦片配色：ColorHex（#AARRGGBB）不透明时低饱和底 + 同色字；透明/缺失 → null
// → 回退 .ed-tile 中性样式（--bg-elevated + 边框，暗色下可见；样式模式对齐 ServerRow 回退瓦片）
const tileStyle = computed(() => {
  const rgb = opaqueHex(props.colorHex)
  return rgb ? { background: rgb + '33', color: rgb } : null
})
const protocolOptions = Object.keys(PROTOCOLS).map((k) => ({ value: k, label: k }))
// 协议帮助链接：WPF ServerEditorPageView 协议页签旁的 "?"——仅
// HelpUrl 非空的协议显示（AppProtocol/RdpApp.GetHelpUrl，url 照抄），其余协议无此元素
const protoHelpUrl = computed(() => PROTOCOLS[props.protocolKey]?.helpUrl || '')

// ---- 数据源：新建（非复制）可在头部改选，其余模式恒用传入 ds ----
const ds = ref(props.dataSourceName || 'Local')
const dsOptions = ref([]) // 可写数据源选项（新建模式拉取）
const showDsSelect = computed(() => isCreate.value && !props.duplicateFrom)
async function loadDsOptions() {
  if (!showDsSelect.value) return
  try {
    const list = await api.datasources()
    const names = (Array.isArray(list) ? list : []).filter((d) => d.writable !== false).map((d) => d.name)
    // 当前树选中的 ds 保持默认选中（即使只读也列出：默认值即现状，改选权在用户）
    if (!names.includes(ds.value)) names.unshift(ds.value)
    dsOptions.value = names.map((n) => ({ label: n, value: n }))
  } catch (e) {
    // 拉取失败不阻断表单：退化为只有当前 ds 的单选项（等价旧的静态 pill）
    dsOptions.value = [{ label: ds.value, value: ds.value }]
  }
}
// 改选上抛：抽屉镜像该值用于保存与表单字段（初值 seed 不触发，仅用户改选时发出）
watch(ds, (v) => emit('ds-change', v))
onMounted(loadDsOptions)

// 协议切换的编辑器状态机在抽屉（onProtocolSwitch：字段携带 + 派生凭据模式），此处只上抛
function onProtocolSwitch(next) {
  emit('protocol-change', next)
}

// 抽屉 section[aria-label] 复用标题（见文件头接缝说明）
defineExpose({ title })
</script>

<template>
  <header class="ed-head">
    <span class="ed-tile" :style="tileStyle">{{ isBulk ? '≡' : (protocolKey || '?').charAt(0) }}</span>
    <div class="ed-title" :title="title">{{ title }}</div>
    <!-- 可见小标签（下拉框前缀说明，不用 tooltip 承载语义）：
         紧贴各下拉框左侧的 11px/--text-3 短标签，与标题行的克制风格一致 -->
    <template v-if="!isBulk">
      <span class="ed-head-label">{{ t('editor.headProtocolLabel') }}</span>
      <n-select
        class="ed-proto"
        size="small"
        :value="protocolKey || undefined"
        :options="protocolOptions"
        :disabled="loading || !!loadError"
        :title="t('editor.headProtocolTip')"
        @update:value="onProtocolSwitch"
      />
      <!-- 协议帮助：跟随当前协议（WPF 页签 "?" 同款，仅 APP/RemoteApp 有） -->
      <HelpLink v-if="protoHelpUrl" :href="protoHelpUrl" />
    </template>
    <template v-if="showDsSelect && dsOptions.length > 1">
      <span class="ed-head-label">{{ t('editor.headDsLabel') }}</span>
      <n-select
        v-model:value="ds"
        class="ed-ds-select"
        size="small"
        :options="dsOptions"
        :title="t('editor.headDsTip')"
      />
    </template>
    <!-- 只读 pill（编辑/复制/批量）同样带「数据库」前缀标签 -->
    <template v-else>
      <span class="ed-head-label">{{ t('editor.headDsLabel') }}</span>
      <div class="ed-ds" :title="t('editor.headDsTip') + ': ' + (isBulk ? bulkDs : ds)">
        {{ isBulk ? bulkDs : ds }}
      </div>
    </template>
    <button class="ed-close" type="button" :title="t('editor.close')" @click="emit('close')">✕</button>
  </header>
</template>

<style scoped>
/* 头部：瓦片/标题/协议/数据源/关闭 五元素一行——标题 flex:1 占中段（超长省略，title
   属性悬浮全文），两下拉固定槽位不换行，窄抽屉（560px）由标题让位 */
.ed-head {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border);
}

.ed-tile {
  flex: 0 0 var(--ctrl-h-m);
  width: var(--ctrl-h-m);
  height: var(--ctrl-h-m);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-ctrl);
  border: 1px solid var(--border);
  /* 无色/透明色回退瓦片在暗色下也可见 */
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-title);
  font-weight: 600;
}

.ed-title {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--fs-title);
  font-weight: 600;
  color: var(--text-1);
}

/* 头部下拉框/只读 pill 的前缀小标签：11px/--text-3 短标签，紧贴其后
   的控件左侧（协议/数据库），不参与标题的弹性让位（flex 收缩为 0） */
.ed-head-label {
  flex: 0 0 auto;
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1.2;
  white-space: nowrap;
}

/* 数据源只读 pill（编辑/复制/批量）：行内元素（不堆叠于标题下方第二行）——固定 170px
   槽位与新建模式选择器对齐（border-box，padding 计入），超长 ds 名省略。
   caption+text-3 与 search-chip/tag-chip 的只读 pill 档统一 */
.ed-ds {
  flex: 0 0 170px;
  box-sizing: border-box;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  padding: 2px 10px;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1.4;
}

/* 新建模式的数据源选择器：与只读 pill 同一 170px 行内槽位 */
.ed-ds-select {
  flex: 0 0 170px;
}

.ed-proto {
  flex: 0 1 150px;
}

.ed-close {
  flex: 0 0 auto;
  border: none;
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  width: var(--ctrl-h-m);
  height: var(--ctrl-h-m);
  cursor: pointer;
}

.ed-close:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
</style>
