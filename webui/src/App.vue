<script setup>
import { useNaiveTheme } from './themes'
const naive = useNaiveTheme()
</script>

<template>
  <n-config-provider :theme="naive.theme" :theme-overrides="naive.overrides">
    <n-message-provider>
      <div class="shell">
        <header class="topbar">
          <div class="logo">1Remote</div>
          <!-- Task 13 先放只读搜索框占位；Task 17 接线防抖搜索与 Ctrl K -->
          <div class="searchbox">⌕ 搜索服务器、标签…</div>
          <div class="topbar-actions">
            <n-button quaternary size="small" title="Task 14">＋</n-button>
            <n-button quaternary size="small" @click="$router.push('/settings')">⚙</n-button>
          </div>
        </header>
        <div class="main">
          <router-view />
        </div>
      </div>
    </n-message-provider>
  </n-config-provider>
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-rows: 44px 1fr;
  height: 100vh;
  background: var(--bg);
  color: var(--text-1);
}
.topbar {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 12px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-panel);
}
.logo {
  font-weight: 600;
}
.searchbox {
  flex: 0 1 420px;
  color: var(--text-4);
  border: 1px solid var(--border-strong);
  border-radius: 7px;
  padding: 5px 10px;
  font-size: 12px;
}
.topbar-actions {
  margin-left: auto;
  display: flex;
  gap: 4px;
}
.main {
  display: flex;
  min-height: 0;
  min-width: 0; /* 防止内容区宽内容（Tasks 15-18）横向撑破外壳 */
}
</style>
