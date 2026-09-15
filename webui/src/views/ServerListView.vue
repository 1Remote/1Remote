<script setup>
// 数据层冒烟占位：验证 useServers（加载 / SSE 刷新 / 轮询 / 树构建）可用。
// Task 15（边栏树）与 Task 16（列表工具栏）会以真实布局整体重写本文件
import { useServers } from '../composables/useServers'
const { servers, tags, loading, connected } = useServers()
</script>

<template>
  <div class="server-list-smoke">
    <p class="status">
      {{ servers.length }} 台服务器 · {{ tags.length }} 个标签 · SSE {{ connected ? '已连接' : '未连接' }}<span
        v-if="loading"
        class="loading"
      >（加载中…）</span>
    </p>
    <ul class="names">
      <li v-for="s in servers.slice(0, 10)" :key="s.id">{{ s.displayName }}</li>
    </ul>
    <p v-if="!loading && !servers.length" class="hint">（服务器列表为空——后端调试数据库可能没有数据）</p>
  </div>
</template>

<style scoped>
.server-list-smoke {
  flex: 1;
  min-width: 0;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  overflow: auto;
}
.status {
  color: var(--text-2);
  font-size: 13px;
}
.loading {
  color: var(--text-4);
}
.names {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.names li {
  color: var(--text-1);
  font-size: 13px;
  padding: 3px 6px;
  border-radius: 5px;
}
.names li:hover {
  background: var(--bg-hover);
}
.hint {
  color: var(--text-4);
  font-size: 12px;
}
</style>
