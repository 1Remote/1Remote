<script setup>
// 两栏布局（spec §3.1：边栏 216px + 内容区）。边栏承载 SideTree（数据源树+标签区）；
// 内容区 = 面包屑行 + 行列表（Task 16）。selection/tag 状态由本组件持有；
// tag 与搜索过滤在此取交集（Task 17）：基础列表 → 标签 → 搜索命中集 → 传 ServerTable。
// 连接动作与全局键盘流（spec §8.2，Task 18）也在此汇聚：所有连接入口（行双击/hover ▸/
// 右键菜单/树叶双击/批量条/Enter 光标行）emit 到本组件统一走 api.connect；
// 全局 Esc 链是唯一的 window 级 Esc 处理器（App.vue 搜索框与 ServerTable 均不本地拦截，
// 避免焦点位置不同导致链序漂移或双触发）。
// Task 20：内容区三态（骨架屏/空库引导/无匹配）+ 底部状态栏（数据源状态点/统计/SSE/语言切换）
// + <900px 自动收起边栏（spec §8.7）。
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { useWindowSize } from '@vueuse/core'
import SideTree from '../components/SideTree.vue'
import ServerTable from '../components/ServerTable.vue'
import EditorDrawer from '../components/editor/EditorDrawer.vue'
import { api } from '../api'
import { applyServerFilters, useServers } from '../composables/useServers'
import { useEditorBus } from '../composables/editorBus'
import { setLocale } from '../locales'

const { t, locale } = useI18n()
const message = useMessage()
const dialog = useDialog()
const selection = ref(null) // { dataSourceName, folderPath, serverId? } —— null=未选中（全部）
const activeTag = ref('') // ''=未按标签过滤
// 收起状态仅本地内存（持久化暂缓）。窄窗适配（spec §8.7）：<900px 自动收起，只收不展——
// 仅在跨过 900 阈值时收起（窄窗内用户手动展开后，同侧宽度微调不反复打回），≥900 不自动展开
const collapsed = ref(false)
const { width: winWidth } = useWindowSize()
watch(winWidth, (w, old) => {
  if (w < 900 && (old === undefined || old >= 900)) collapsed.value = true
}, { immediate: true })

const { servers, datasources, tags, loading, connected, searchQuery, searchedIds } = useServers()

// 传给 ServerTable 的收窄列表（其内部再应用树选中过滤 + 排序，交集自然复合）
const visibleServers = computed(() => applyServerFilters(servers.value, activeTag.value, searchedIds.value))
const searchActive = computed(() => searchedIds.value != null) // null=未启用；空 Set=搜了但零命中

// 面包屑（spec §3.2）：根=「数据源名 · 全部服务器」、文件夹=「数据源 / 路径」；右侧计数由 ServerTable 上报
const breadcrumb = computed(() => {
  const sel = selection.value
  if (!sel || !sel.dataSourceName) return t('crumb.allDataSources')
  return sel.folderPath ? `${sel.dataSourceName} / ${sel.folderPath}` : `${sel.dataSourceName} · ${t('crumb.allServers')}`
})
const tableCount = ref(0)
const table = ref(null) // ServerTable 实例引用：全局 Esc 链需调用其暴露的菜单/勾选/光标回退方法

// ---- 内容区三态（spec §8.5 + 骨架屏，Task 20）：互斥地取代 ServerTable（表格隐藏时 ref 为 null，
// Esc 链的 tb?. 守卫天然兼容）。SSE 重载时列表已有数据，不闪骨架 ----
const showSkeleton = computed(() => loading.value && !servers.value.length) // 首载进行中
// 后端不可达（拉取失败且无任何数据）：优先于空库引导展示——引导卡的「新建/导入」会把用户带向
// 错误方向；恢复靠 30s 轮询（useServers 断连恢复时会补一次全量重载，此处自动切回正常内容）
const showOffline = computed(() => !connected.value && !loading.value && !servers.value.length)
const showGuide = computed(() => connected.value && !loading.value && !servers.value.length) // 已连通且整库为空 → 引导卡片
const showNoMatch = computed(() => servers.value.length > 0 && !visibleServers.value.length) // 标签/搜索交集为空
const tableHidden = computed(() => showSkeleton.value || showOffline.value || showGuide.value || showNoMatch.value)
// 表格卸载后 counted 不再上报，面包屑计数跟随空态归零（骨架期如实显示 0）
const listCount = computed(() => (tableHidden.value ? 0 : tableCount.value))
const noMatchDetail = computed(() => {
  if (searchActive.value) return t('empty.searchedFor', { q: searchQuery.value })
  if (activeTag.value) return t('empty.taggedNone', { tag: activeTag.value })
  return t('empty.filtered') // 兜底（视图层无匹配仅在搜索/标签生效时可达）
})
// 清除过滤（无匹配态按钮）：搜索 + 标签 + 树选中一并复位（三者均由本组件持有）
function clearFilters() {
  searchQuery.value = ''
  activeTag.value = ''
  selection.value = null
}

// ---- 状态栏（spec §3.1 内容区底部 26px）：左=数据源状态点+名称（最多 3 个，超出 +N），
// 右=统计 + SSE 可达性 + 语言切换。状态点语义与 SideTree 根节点一致（绿=connected /
// 红=reconnecting·title 带重连信息 / 灰=其余）----
const MAX_DS = 3
const dsShown = computed(() => datasources.value.slice(0, MAX_DS))
const dsHidden = computed(() => Math.max(0, datasources.value.length - MAX_DS))
const dsDotClass = (status) => (status === 'connected' ? 'ok' : status === 'reconnecting' ? 'bad' : 'idle')
const dsTitle = (ds) =>
  ds.status === 'reconnecting' ? ds.name + ' · ' + (ds.reconnectInfo || t('tree.reconnecting')) : ds.name
function toggleLocale() {
  setLocale(locale.value === 'zh-CN' ? 'en-US' : 'zh-CN')
}
// 按钮显示目标语言自称
// （「中/EN」为语言名，两语言环境下取值一致，经 i18n 键下发以保持代码内零硬编码文案）
const nextLang = computed(() => (locale.value === 'zh-CN' ? t('statusbar.langEn') : t('statusbar.langZh')))

// ---- 连接动作（spec §8.2）：api.connect → 后端触发 OnRequestServerConnect（fromView="WebUi"），
// 密码交互与会话窗口由桌面端既有管线处理（Web 侧不感知，spec 约定凭据留在本地）----
async function onConnect(id) {
  const name = servers.value.find(s => s.id === id)?.displayName || id
  try {
    await api.connect(id)
    message.success(t('toast.connectStarted', { name }))
  } catch (e) {
    console.warn('[ServerListView] connect failed:', e?.message || e)
    message.error(t('toast.connectFailed'))
  }
}

async function onBatchConnect(ids) {
  if (!ids?.length) return
  let ok = 0
  for (const id of ids) {
    // 逐个串行 await：批量并发轰炸后端/桌面端连接管线不友好
    try {
      await api.connect(id)
      ok++
    } catch (e) {
      console.warn('[ServerListView] batch connect failed:', id, e?.message || e)
    }
  }
  if (ok) message.success(t('toast.batchConnectStarted', { n: ok }))
  if (ok < ids.length) message.error(t('toast.batchConnectFailed', { n: ids.length - ok }))
}

// ---- 全局 Esc 链（spec §8.2）：一次 Esc 只退一级，按 右键菜单 → 勾选 → 搜索 → 表格光标 逐级回退。
// 菜单/勾选/光标归 ServerTable（经 ref 暴露的 *IfOpen/*IfAny 方法，返回是否消费），
// 搜索归本组件（useServers 共享态）——三处状态在唯一的 window 级 handler 里按序裁决，
// 与焦点位置无关（搜索框元素级 handler 在焦点不在输入框时不会触发，无法参与统一链序）。
// 编辑抽屉打开时 Esc 归抽屉（关闭/未保存确认，EditorDrawer 自持 window 级 handler，注册在
// 本链之后，若此处不守卫会先消费掉 Esc），本链整体让位。----
function onGlobalEsc(e) {
  if (e.key !== 'Escape') return
  if (editor.value) return // 抽屉在开：Esc 由抽屉处理
  const tb = table.value // 命名避免遮蔽 i18n 的 t
  if (tb?.closeMenuIfOpen()) e.preventDefault()
  else if (tb?.clearCheckedIfAny()) e.preventDefault()
  else if (searchQuery.value) {
    searchQuery.value = ''
    e.preventDefault()
  } else if (tb?.clearCursorIfAny()) e.preventDefault()
}
onMounted(() => window.addEventListener('keydown', onGlobalEsc))
onBeforeUnmount(() => window.removeEventListener('keydown', onGlobalEsc))

// ---- 编辑抽屉（Plan 2 Task 8）：状态 + 全部入口汇聚于此 ----
// editor = { mode:'create', ds, protocol?, duplicateFrom?, initial? } | { mode:'edit', serverId, ds, initial } | null
const editor = ref(null)

// App.vue 顶栏「+」经 editorBus 请求新建（跨层：顶栏在 router-view 之外无法向本视图 emit）。
// 抽屉已开时忽略——替换状态会丢掉未保存编辑且绕过脏确认。
const { createRequest } = useEditorBus()
watch(createRequest, () => {
  if (!editor.value) openCreate()
})

function openCreate() {
  // 归属数据源 = 当前树选中（根/文件夹/叶）的数据源；未选 = Local
  editor.value = { mode: 'create', ds: selection.value?.dataSourceName || 'Local', protocol: 'RDP' }
}

function openEdit(server) {
  editor.value = { mode: 'edit', serverId: server.id, ds: server.dataSourceName || 'Local', initial: server }
}

// 复制 = create 语义 + 抽屉预填来源服务器 config（EditorDrawer duplicateFrom：加载→清 Id→POST 新建）
function openDuplicate(server) {
  editor.value = { mode: 'create', ds: server.dataSourceName || 'Local', duplicateFrom: server.id, initial: server }
}

// 删除：确认对话框（naive dialog）→ DELETE → toast；列表经 SSE reload 自动刷新
function onDelete(server) {
  dialog.warning({
    title: t('editor.deleteTitle'),
    content: t('editor.deleteConfirm', { name: server.displayName }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    onPositiveClick: async () => {
      try {
        await api.deleteServer(server.id, server.dataSourceName)
        message.success(t('editor.deleteOk', { name: server.displayName }))
        // 选中态可能指向已删对象（树叶选中）：清理回退，避免高亮悬空
        if (selection.value?.serverId === server.id) selection.value = null
      } catch (e) {
        message.error(t('editor.deleteFailed') + (e?.message ? ` (${e.message})` : ''))
      }
    },
  })
}

// ---- 批量编辑（Plan 2 Task 10）：批量条按钮 → 抽屉 bulk 模式 ----
// 共享值计算需要列表 DTO：按勾选 id 从 servers 快照取（列表 DTO = camelCase 域，
// 与批量 patch 同域）；快照里找不到的 id（列表恰在勾选后变化）直接跳过，以能取到的为准。
function openBulkEdit(ids) {
  const list = (ids || [])
    .map((id) => servers.value.find((s) => s.id === id))
    .filter(Boolean)
  if (!list.length) return
  editor.value = {
    mode: 'bulk',
    ds: list[0].dataSourceName || 'Local', // 后端 batch 端点单 ds；跨源勾选由抽屉内提示拦下
    bulkIds: list.map((s) => s.id),
    bulkServers: list,
  }
}

// 保存成功：SSE 已自动刷新列表；这里收敛抽屉状态 + 清理指向旧行的选中态（名称/协议可能已变）
// bulk 模式目标是一个 id 集，不涉及树叶选中回退。
function onSaved({ id, mode }) {
  if (mode === 'edit' && selection.value?.serverId && selection.value.serverId !== id) {
    // 编辑目标的树叶选中态与保存对象不符（多选中残留）——保守回退，避免错误高亮
    selection.value = null
  }
  editor.value = null
}
</script>

<template>
  <div class="server-list">
    <aside class="sidebar" :class="{ collapsed }">
      <SideTree
        v-if="!collapsed"
        v-model:selection="selection"
        v-model:tag="activeTag"
        @update:collapsed="collapsed = $event"
        @connect="onConnect"
      />
      <button v-else class="expand-rail" :title="t('sidebar.expand')" @click="collapsed = false">»</button>
    </aside>
    <main class="content">
      <div class="crumb-row">
        <div class="crumb" :title="breadcrumb">{{ breadcrumb }}<span class="crumb-count">{{ t('crumb.count', { n: listCount }) }}</span></div>
        <!-- 搜索过滤 chip（Task 17）：命中数沿用右侧 crumb-count（同为过滤后计数，不重复展示） -->
        <span v-if="searchActive" class="search-chip" :title="t('crumb.searchChip')">
          <span class="sc-label">⌕ {{ searchQuery }}</span>
          <button class="sc-x" :title="t('crumb.clearSearch')" @click="searchQuery = ''">✕</button>
        </span>
      </div>

      <!-- 首载骨架屏（spec §8.4）：6 行灰块脉动（状态点 + 图标圆 + 名称/地址两横条，行高对齐真实行），
           数据到达后被表格原地替换（同布局高度，无跳动） -->
      <div v-if="showSkeleton" class="skeleton-host" aria-hidden="true">
        <div v-for="i in 6" :key="i" class="sk-row" :style="{ '--d': (i - 1) * 120 + 'ms' }">
          <span class="sk sk-dot"></span>
          <span class="sk sk-icon"></span>
          <span class="sk sk-bar sk-name"></span>
          <span class="sk sk-bar sk-addr"></span>
        </div>
      </div>

      <!-- 后端不可达（Task 21）：居中提示，取代空库引导卡——后端未运行时引导用户新建/导入会误导 -->
      <div v-else-if="showOffline" class="empty-offline">
        <div class="eo-title">{{ t('empty.offline') }}</div>
        <div class="eo-hint">{{ t('empty.offlineHint') }}</div>
      </div>

      <!-- 空库引导卡片（spec §8.5）：新建已接线（Plan 2 Task 8，打开编辑抽屉）；导入为 Plan 4 占位 -->
      <div v-else-if="showGuide" class="empty-guide">
        <div class="eg-title">{{ t('empty.none') }}</div>
        <div class="eg-actions">
          <button class="eg-btn eg-primary" @click="openCreate">+ {{ t('empty.newFirst') }}</button>
          <button class="eg-btn" disabled :title="t('common.comingSoon')">⤓ {{ t('empty.importMremote') }}</button>
        </div>
        <div class="eg-hint">{{ t('empty.launcherHint') }}</div>
      </div>

      <!-- 无匹配（库非空但标签/搜索交集为空）：轻提示（附搜索词或标签名）+ 清除过滤 -->
      <div v-else-if="showNoMatch" class="empty-nomatch">
        <div class="en-title">{{ t('empty.noMatch') }}</div>
        <div class="en-detail">{{ noMatchDetail }}</div>
        <button class="en-clear" @click="clearFilters">{{ t('empty.clearFilters') }}</button>
      </div>

      <ServerTable
        v-else
        ref="table"
        class="table-host"
        :servers="visibleServers"
        :selection="selection"
        @counted="tableCount = $event"
        @connect="onConnect"
        @batch-connect="onBatchConnect"
        @bulk-edit="openBulkEdit"
        @edit="openEdit"
        @duplicate="openDuplicate"
        @delete="onDelete"
      />

      <!-- 连接编辑抽屉（Plan 2 Task 8/10）：新建/编辑/复制/批量入口共用；fixed 覆盖层，不参与 flex 布局 -->
      <EditorDrawer
        v-if="editor"
        :mode="editor.mode"
        :server-id="editor.serverId || ''"
        :data-source-name="editor.ds"
        :protocol="editor.protocol || ''"
        :initial-server="editor.initial || null"
        :duplicate-from="editor.duplicateFrom || ''"
        :bulk-ids="editor.bulkIds || []"
        :bulk-servers="editor.bulkServers || []"
        @close="editor = null"
        @saved="onSaved"
      />

      <!-- 底部状态栏（spec §3.1）：数据源状态点 · 台数/标签数 · SSE 可达性 · 语言切换 -->
      <footer class="status-bar">
        <span v-for="ds in dsShown" :key="ds.name" class="sb-ds" :title="dsTitle(ds)">
          <span class="sb-dot" :class="dsDotClass(ds.status)"></span>
          <span class="sb-name">{{ ds.name }}</span>
        </span>
        <span v-if="dsHidden" class="sb-more" :title="t('statusbar.dsMore', { n: dsHidden })">+{{ dsHidden }}</span>
        <div class="sb-right">
          <!-- 计数用 vue-i18n 复数形式（en："{n} server | {n} servers"）；zh 无管道单形式同样兼容，
               {m} 需显式传命名参数 + 复数值（隐式仅绑定 n） -->
          <span>{{ t('statusbar.serverCount', servers.length) }} · {{ t('statusbar.tagCount', { m: tags.length }, tags.length) }}</span>
          <span class="sb-sse" :title="t('statusbar.sseTip')">
            <span class="sb-dot" :class="connected ? 'ok' : 'bad'"></span>
            {{ connected ? t('statusbar.sseOk') : t('statusbar.sseOff') }}
          </span>
          <button class="sb-lang" :title="t('statusbar.langSwitch')" @click="toggleLocale">{{ nextLang }}</button>
        </div>
      </footer>
    </main>
  </div>
</template>

<style scoped>
.server-list {
  flex: 1;
  min-width: 0;
  min-height: 0;
  display: flex;
}
.sidebar {
  flex: 0 0 216px;
  width: 216px;
  min-height: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--border);
  background: var(--bg-panel);
}
.sidebar.collapsed {
  flex-basis: 44px;
  width: 44px;
}
.expand-rail {
  flex: 1;
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: 14px;
  cursor: pointer;
}
.expand-rail:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.content {
  flex: 1;
  min-width: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--bg);
}
.crumb-row {
  flex: 0 0 34px;
  display: flex;
  align-items: center;
  padding: 0 14px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-panel);
}
.crumb {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12.5px;
  color: var(--text-2);
}
.crumb-count {
  color: var(--text-4);
}
.search-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  flex: 0 1 auto;
  min-width: 0;
  max-width: 280px;
  margin-left: 10px;
  padding: 2px 4px 2px 9px;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: 11.5px;
  line-height: 1.4;
}
.sc-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sc-x {
  flex: 0 0 auto;
  width: 18px;
  height: 18px;
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--text-4);
  font-size: 10px;
  line-height: 1;
  cursor: pointer;
}
.sc-x:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.table-host {
  flex: 1;
  min-height: 0;
}

/* ---- 首载骨架屏：行结构对齐 ServerRow（状态点 + 图标圆 + 名称/地址横条），纯 CSS 透明度脉动；
   灰块用 --bg-hover 浮于 --bg，逐行错峰（--d 由内联注入），36px 行高 + 分隔线与真实行一致 ---- */
.skeleton-host {
  flex: 1;
  min-height: 0;
  overflow: auto;
}
.sk-row {
  display: flex;
  align-items: center;
  height: 36px;
  padding: 0 10px 0 34px;
  border-bottom: 1px solid var(--border);
}
.sk {
  background: var(--bg-hover);
  animation: sk-pulse 1.4s ease-in-out infinite;
  animation-delay: var(--d, 0s);
}
.sk-dot {
  flex: 0 0 8px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.sk-icon {
  flex: 0 0 22px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  margin: 0 10px 0 30px;
}
.sk-bar {
  height: 10px;
  border-radius: 5px;
}
.sk-name {
  flex: 0 0 26%;
}
.sk-addr {
  flex: 0 0 16%;
  margin-left: 28px;
}
@keyframes sk-pulse {
  0%, 100% { opacity: 0.5; }
  50% { opacity: 1; }
}
@media (prefers-reduced-motion: reduce) {
  .sk { animation: none; opacity: 0.7; }
}

/* ---- 空库引导卡片（spec §8.5）：居中；新建/导入为占位禁用，提示行指向桌面启动器热键 ---- */
.empty-guide {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 14px;
  padding: 24px;
}
.eg-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-2);
}
.eg-actions {
  display: flex;
  gap: 10px;
}
.eg-btn {
  border: 1px solid var(--border);
  border-radius: 7px;
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: 12.5px;
  line-height: 1;
  padding: 8px 14px;
  cursor: pointer;
}
.eg-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.eg-primary {
  border-color: var(--accent);
  color: var(--accent-text);
}
.eg-hint {
  font-size: 12px;
  color: var(--text-4);
}

/* ---- 后端不可达：居中提示（无操作——恢复自动进行，不做手动重试按钮） ---- */
.empty-offline {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
}
.eo-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-2);
}
.eo-hint {
  font-size: 12px;
  color: var(--text-4);
}

/* ---- 无匹配（过滤后为空）：轻提示 + 清除过滤按钮 ---- */
.empty-nomatch {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
}
.en-title {
  font-size: 13px;
  color: var(--text-3);
}
.en-detail {
  font-size: 12px;
  color: var(--text-4);
}
.en-clear {
  margin-top: 6px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: transparent;
  color: var(--text-2);
  font-size: 12px;
  line-height: 1;
  padding: 6px 12px;
  cursor: pointer;
}
.en-clear:hover {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}

/* ---- 底部状态栏（spec §3.1）：26px 单行，左=数据源状态点（≤3 个 + 溢出 +N），右=统计/SSE/语言 ---- */
.status-bar {
  flex: 0 0 26px;
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 0 12px;
  border-top: 1px solid var(--border);
  background: var(--bg-panel);
  color: var(--text-3);
  font-size: 11.5px;
  white-space: nowrap;
  overflow: hidden; /* 数据源名过长时截断而非把右侧统计挤出可视区 */
}
.sb-ds {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  min-width: 0;
  max-width: 130px;
}
.sb-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}
.sb-dot {
  flex: 0 0 6px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
}
.sb-dot.ok {
  background: var(--success);
}
.sb-dot.bad {
  background: var(--danger);
}
.sb-dot.idle {
  background: var(--text-4);
}
.sb-more {
  color: var(--text-4);
}
.sb-right {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  overflow: hidden; /* 左侧数据源名撑满时不被顶出，内部整体截断 */
}
.sb-sse {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  cursor: help; /* title 说明其语义为后端可达性而非连接会话状态 */
}
.sb-lang {
  border: 1px solid var(--border);
  border-radius: 5px;
  background: transparent;
  color: var(--text-3);
  font-size: 11px;
  line-height: 1;
  padding: 3px 7px;
  cursor: pointer;
}
.sb-lang:hover {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
</style>
