<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useNaiveTheme } from './themes'
import { useServers } from './composables/useServers'
const naive = useNaiveTheme()
const { t } = useI18n()
const { searchQuery, searching } = useServers()
const searchInput = ref(null)

// Ctrl+K / Cmd+K 全局聚焦搜索框（spec §8）：keydown 于 window（冒泡），preventDefault 让位
// 浏览器默认（如地址栏搜索）；再次按下全选已有内容，方便直接覆盖输入。
// Esc 不在此处理（输入框元素级 handler 焦点在表格时不触发，无法参与统一链序）——
// 全局 Esc 链（菜单→勾选→搜索→光标）由 ServerListView 的 window 级 handler 统一调度（Task 18）。
function onGlobalKey(e) {
  if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key?.toLowerCase() === 'k') {
    e.preventDefault()
    searchInput.value?.focus()
    searchInput.value?.select()
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
          <!-- 顶栏搜索框（spec §3.1）：⌕ + 输入 + 搜索中 spinner；Ctrl K 聚焦全选 / Esc 由全局链清空（见 setup） -->
          <div class="searchbox" :title="t('search.title')" @click="searchInput?.focus()">
            <span class="sb-icon">⌕</span>
            <input
              ref="searchInput"
              v-model="searchQuery"
              class="sb-input"
              type="text"
              :placeholder="t('search.placeholder')"
            />
            <!-- 常驻占位仅切 visibility（不 v-if）：避免 spinner 出现/消失时输入框宽度跳动 -->
            <span class="sb-spin" :class="{ on: searching }" :title="t('search.searching')"></span>
          </div>
          <div class="topbar-actions">
            <!-- 新建服务器（Task 14 占位）：内部任务号不入 UI，统一「即将推出」文案 -->
            <n-button quaternary size="small" :title="t('common.comingSoon')">＋</n-button>
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
  visibility: hidden; /* 常驻占位防宽度跳动（见模板注释） */
}
.sb-spin.on {
  visibility: visible;
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
