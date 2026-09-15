<script setup>
// Task 15：两栏布局（spec §3.1：边栏 216px + 内容区）。边栏承载 SideTree（数据源树+标签区）；
// selection/tag 状态由本组件持有，Task 16（列表按树/标签过滤）与 Task 17（搜索取交集）消费，当前仅存储。
import { ref } from 'vue'
import SideTree from '../components/SideTree.vue'

const selection = ref(null) // { dataSourceName, folderPath, serverId? } —— null=未选中
const activeTag = ref('') // ''=未按标签过滤
// 收起状态仅本地内存：spec §8.7 的 <900px 自动收起与 44px 图标条（树/标签/设置入口）归 Task 20，持久化暂缓
const collapsed = ref(false)
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
    <main class="content">服务器列表 — Task 16 实现</main>
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
  padding: 16px;
  color: var(--text-4);
}
</style>
