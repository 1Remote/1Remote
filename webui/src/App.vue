<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useNaiveTheme } from './themes'
import { useServers } from './composables/useServers'
const naive = useNaiveTheme()
const { searchQuery, searching } = useServers()
const searchInput = ref(null)

// Ctrl+K / Cmd+K 全局聚焦搜索框（spec §8）：keydown 于 window（冒泡），preventDefault 让位
// 浏览器默认（如地址栏搜索）。Esc 清空留在输入框本地（.stop 不外传，Task 18 再做全局 Esc 链）。
function onGlobalKey(e) {
  if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key?.toLowerCase() === 'k') {
    e.preventDefault()
    searchInput.value?.focus()
  }
}
onMounted(() => window.addEventListener('keydown', onGlobalKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onGlobalKey))
</script>

<template>
  <n-config-provider :theme="naive.theme" :theme-overrides="naive.overrides">
    <n-message-provider>
      <div class="shell">
        <header class="topbar">
          <div class="logo">1Remote</div>
          <!-- 顶栏搜索框（spec §3.1）：⌕ + 输入 + 搜索中 spinner；Ctrl K 聚焦 / Esc 清空（见 setup） -->
          <div class="searchbox" title="Ctrl+K 聚焦 · Esc 清空" @click="searchInput?.focus()">
            <span class="sb-icon">⌕</span>
            <input
              ref="searchInput"
              v-model="searchQuery"
              class="sb-input"
              type="text"
              placeholder="搜索服务器、标签…"
              @keydown.esc.stop.prevent="searchQuery = ''"
            />
            <span v-if="searching" class="sb-spin" title="搜索中…"></span>
          </div>
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
  display: flex;
  align-items: center;
  gap: 6px;
  height: 26px;
  padding: 0 9px;
  border: 1px solid var(--border-strong);
  border-radius: 7px;
  font-size: 12px;
  cursor: text;
}
.searchbox:focus-within {
  border-color: var(--accent);
}
.sb-icon {
  flex: 0 0 auto;
  color: var(--text-4);
  font-size: 13px;
  line-height: 1;
}
.sb-input {
  flex: 1;
  min-width: 0;
  border: none;
  outline: none;
  background: transparent;
  color: var(--text-1);
  font-size: 12px;
  font-family: inherit;
  padding: 0;
}
.sb-input::placeholder {
  color: var(--text-4);
}
.sb-spin {
  flex: 0 0 12px;
  width: 12px;
  height: 12px;
  border: 2px solid var(--border-strong);
  border-top-color: var(--accent);
  border-radius: 50%;
  animation: sb-rotate 0.7s linear infinite;
}
@keyframes sb-rotate {
  to {
    transform: rotate(360deg);
  }
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
