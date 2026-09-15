<script setup>
// 两栏布局（spec §3.1：边栏 216px + 内容区）。边栏承载 SideTree（数据源树+标签区）；
// 内容区 = 面包屑行 + 行列表（Task 16）。selection/tag 状态由本组件持有；
// tag 与搜索过滤在此取交集（Task 17）：基础列表 → 标签 → 搜索命中集 → 传 ServerTable。
import { computed, ref } from 'vue'
import SideTree from '../components/SideTree.vue'
import ServerTable from '../components/ServerTable.vue'
import { applyServerFilters, useServers } from '../composables/useServers'

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

// ---- 连接动作（Task 18 接线：双击行 / hover ▸ / 右键菜单「连接」/ 批量条）当前仅打点占位 ----
function onConnect(id) {
  console.debug('[ServerListView] connect (Task 18):', id)
}
function onBatchConnect(ids) {
  console.debug('[ServerListView] batch connect (Task 18):', ids)
}
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
      />
      <!-- connect 事件（双击服务器叶）此处暂不处理：连接动作 Task 18 接线 -->
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
