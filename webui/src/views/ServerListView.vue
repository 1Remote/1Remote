<script setup>
// 两栏布局（spec §3.1：边栏 216px + 内容区）。边栏承载 SideTree（数据源树+标签区）；
// 内容区 = 面包屑行 + 行列表（Task 16）。selection/tag 状态由本组件持有；
// tag 与搜索过滤在此取交集（Task 17）：基础列表 → 标签 → 搜索命中集 → 传 ServerTable。
// 连接动作与全局键盘流（spec §8.2，Task 18）也在此汇聚：所有连接入口（行双击/hover ▸/
// 右键菜单/树叶双击/批量条/Enter 光标行）emit 到本组件统一走 api.connect；
// 全局 Esc 链是唯一的 window 级 Esc 处理器（App.vue 搜索框与 ServerTable 均不本地拦截，
// 避免焦点位置不同导致链序漂移或双触发）。
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useMessage } from 'naive-ui'
import SideTree from '../components/SideTree.vue'
import ServerTable from '../components/ServerTable.vue'
import { api } from '../api'
import { applyServerFilters, useServers } from '../composables/useServers'

const message = useMessage()
const selection = ref(null) // { dataSourceName, folderPath, serverId? } —— null=未选中（全部）
const activeTag = ref('') // ''=未按标签过滤
// 收起状态仅本地内存：spec §8.7 的 <900px 自动收起与 44px 图标条（树/标签/设置入口）归 Task 20，持久化暂缓
const collapsed = ref(false)

const { servers, searchQuery, searchedIds } = useServers()

// 传给 ServerTable 的收窄列表（其内部再应用树选中过滤 + 排序，交集自然复合）
const visibleServers = computed(() => applyServerFilters(servers.value, activeTag.value, searchedIds.value))
const searchActive = computed(() => searchedIds.value != null) // null=未启用；空 Set=搜了但零命中

// 面包屑（spec §3.2）：根=「数据源名 · 全部服务器」、文件夹=「数据源 / 路径」；右侧计数由 ServerTable 上报
const breadcrumb = computed(() => {
  const sel = selection.value
  if (!sel || !sel.dataSourceName) return '全部数据源'
  return sel.folderPath ? `${sel.dataSourceName} / ${sel.folderPath}` : `${sel.dataSourceName} · 全部服务器`
})
const tableCount = ref(0)
const table = ref(null) // ServerTable 实例引用：全局 Esc 链需调用其暴露的菜单/勾选/光标回退方法

// ---- 连接动作（spec §8.2）：api.connect → 后端触发 OnRequestServerConnect（fromView="WebUi"），
// 密码交互与会话窗口由桌面端既有管线处理（Web 侧不感知，spec 约定凭据留在本地）----
async function onConnect(id) {
  const name = servers.value.find(s => s.id === id)?.displayName || id
  try {
    await api.connect(id)
    message.success(`已发起连接：${name}`)
  } catch (e) {
    console.warn('[ServerListView] connect failed:', e?.message || e)
    message.error('连接发起失败')
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
  if (ok) message.success(`已发起 ${ok} 个连接`)
  if (ok < ids.length) message.error(`${ids.length - ok} 个连接发起失败`)
}

// ---- 全局 Esc 链（spec §8.2）：一次 Esc 只退一级，按 右键菜单 → 勾选 → 搜索 → 表格光标 逐级回退。
// 菜单/勾选/光标归 ServerTable（经 ref 暴露的 *IfOpen/*IfAny 方法，返回是否消费），
// 搜索归本组件（useServers 共享态）——三处状态在唯一的 window 级 handler 里按序裁决，
// 与焦点位置无关（搜索框元素级 handler 在焦点不在输入框时不会触发，无法参与统一链序）。----
function onGlobalEsc(e) {
  if (e.key !== 'Escape') return
  const t = table.value
  if (t?.closeMenuIfOpen()) e.preventDefault()
  else if (t?.clearCheckedIfAny()) e.preventDefault()
  else if (searchQuery.value) {
    searchQuery.value = ''
    e.preventDefault()
  } else if (t?.clearCursorIfAny()) e.preventDefault()
}
onMounted(() => window.addEventListener('keydown', onGlobalEsc))
onBeforeUnmount(() => window.removeEventListener('keydown', onGlobalEsc))

// 编辑抽屉（Plan 2）——ServerTable 的 edit emit 在此忽略
function onEdit() {}
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
      <button v-else class="expand-rail" title="展开边栏" @click="collapsed = false">»</button>
    </aside>
    <main class="content">
      <div class="crumb-row">
        <div class="crumb" :title="breadcrumb">{{ breadcrumb }}<span class="crumb-count"> · {{ tableCount }} 台</span></div>
        <!-- 搜索过滤 chip（Task 17）：命中数沿用右侧 crumb-count（同为过滤后计数，不重复展示） -->
        <span v-if="searchActive" class="search-chip" title="搜索过滤中">
          <span class="sc-label">⌕ {{ searchQuery }}</span>
          <button class="sc-x" title="清除搜索（Esc）" @click="searchQuery = ''">✕</button>
        </span>
      </div>
      <ServerTable
        ref="table"
        class="table-host"
        :servers="visibleServers"
        :selection="selection"
        @counted="tableCount = $event"
        @connect="onConnect"
        @batch-connect="onBatchConnect"
        @edit="onEdit"
      />
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
</style>
