<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { enUS, zhCN } from 'naive-ui'
import { useNaiveTheme } from './themes'
import { useServers } from './composables/useServers'
import { useEditorBus } from './composables/editorBus'
const naive = useNaiveTheme()
const { t, locale } = useI18n()
const { requestNewServer, requestImport } = useEditorBus()
// naive-ui 内建文案（弹窗按钮/分页等）跟随 i18n 语言（dateZhCN/dateEnUS 暂未用到日期组件，不引入）
const naiveLocale = computed(() => (locale.value === 'en-US' ? enUS : zhCN))
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

// 「+」下拉（Plan 4 Task 3）：新建（原直达按钮）+ 导入（新模态入口）——服务器已存在时
// 导入是自然入口；两动作都经 editorBus 计数通知 ServerListView（跨层，见 editorBus.js）
const addOptions = computed(() => [
  { label: t('topbar.newServer'), key: 'new' },
  { label: t('import.title'), key: 'import' },
])
function onAddSelect(key) {
  if (key === 'new') requestNewServer()
  else if (key === 'import') requestImport()
}

// ===== 窗口控制（fix-batch Task 4）：仅 WebView2 宿主可见/生效 =====
// window.chrome.webview 只存在于 WebView2：普通浏览器打开时整套窗口控制隐藏，
// 且所有 postMessage 调用短路（守卫宿主存在），页面行为不变。
const isHosted = !!window.chrome?.webview?.postMessage
const winState = ref('normal') // 'normal' | 'maximized'
function postToHost(cmd) {
  try {
    window.chrome?.webview?.postMessage(JSON.stringify({ cmd }))
  } catch {
    /* 非 WebView2 宿主忽略 */
  }
}
// WPF 壳在最大化/还原（含窗口按钮/系统菜单）后回推状态（ExecuteScriptAsync 调用此全局函数）
window.__setWinState = (s) => {
  if (s === 'maximized' || s === 'normal') winState.value = s
}
onBeforeUnmount(() => {
  delete window.__setWinState
  dragCleanup?.()
})

function onWinBtn(kind) {
  if (kind === 'min') postToHost('window-minimize')
  else if (kind === 'max') postToHost(winState.value === 'maximized' ? 'window-restore' : 'window-maximize')
  else if (kind === 'close') postToHost('window-close')
}

// topbar 拖拽：CSS app-region 在 WebView2 不可用（那是 Electron），WPF 覆盖条也受 airspace
// 限制无法浮于 WebView2 之上——改为页面内判定“按住左键位移超阈值”后通知 WPF 壳 DragMove；
// 阈值避免吞掉普通单击（+/⚙/搜索框等），双击交给 dblclick 走最大化切换。
let dragCleanup = null
const NO_DRAG_SELECTOR = 'button,input,a,select,textarea,.searchbox,.wc-btn,[data-no-drag]'
function onTopbarMouseDown(e) {
  if (!isHosted || e.button !== 0 || e.detail >= 2) return
  if (e.target.closest?.(NO_DRAG_SELECTOR)) return
  const sx = e.clientX
  const sy = e.clientY
  const cleanup = () => {
    window.removeEventListener('mousemove', onMove)
    window.removeEventListener('mouseup', onUp)
    if (dragCleanup === cleanup) dragCleanup = null
  }
  const onMove = (ev) => {
    if (!(ev.buttons & 1)) {
      cleanup()
      return
    }
    if (Math.abs(ev.clientX - sx) > 3 || Math.abs(ev.clientY - sy) > 3) {
      cleanup()
      postToHost('window-drag')
    }
  }
  const onUp = () => cleanup()
  dragCleanup?.()
  dragCleanup = cleanup
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}
function onTopbarDblClick(e) {
  if (!isHosted) return
  if (e.target.closest?.(NO_DRAG_SELECTOR)) return
  postToHost(winState.value === 'maximized' ? 'window-restore' : 'window-maximize')
}
</script>

<template>
  <n-config-provider :theme="naive.theme" :theme-overrides="naive.overrides" :locale="naiveLocale">
    <n-message-provider>
      <!-- n-dialog-provider：编辑抽屉的未保存确认/删除确认（useDialog）与全局 toast 同层提供 -->
      <n-dialog-provider>
        <div class="shell">
          <header class="topbar" @mousedown="onTopbarMouseDown" @dblclick="onTopbarDblClick">
            <!-- LOGO：显示器+播放三角（远程连接），着色随强调色 -->
            <div class="logo">
              <svg class="logo-mark" viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
                <rect x="2.5" y="4" width="19" height="13" rx="2" fill="none" stroke="currentColor" stroke-width="1.6" />
                <path d="M9 20.5h6M12 17.2v3.3" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" />
                <path d="M10 7.7 14.9 10.5 10 13.3Z" fill="currentColor" />
              </svg>
              1Remote
            </div>
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
              <!-- 「+」下拉（Plan 4 Task 3）：新建服务器 / 导入服务器（经 editorBus 通知 ServerListView） -->
              <n-dropdown trigger="click" :options="addOptions" @select="onAddSelect">
                <n-button quaternary size="small" :title="t('topbar.addServer')">+</n-button>
              </n-dropdown>
              <n-button quaternary size="small" @click="$router.push('/settings')">⚙</n-button>
            </div>
            <!-- 窗口控制（仅 WebView2 宿主）：Windows 风格 46×44，close hover 红。消息见 postToHost -->
            <div v-if="isHosted" class="win-controls">
              <button class="wc-btn" type="button" aria-label="Minimize" @mousedown.stop @click="onWinBtn('min')">
                <svg viewBox="0 0 10 10" width="10" height="10" aria-hidden="true">
                  <path d="M0 5h10" stroke="currentColor" stroke-width="1" />
                </svg>
              </button>
              <button class="wc-btn" type="button" aria-label="Maximize" @mousedown.stop @click="onWinBtn('max')">
                <svg v-if="winState !== 'maximized'" viewBox="0 0 10 10" width="10" height="10" aria-hidden="true">
                  <rect x="0.5" y="0.5" width="9" height="9" fill="none" stroke="currentColor" stroke-width="1" />
                </svg>
                <svg v-else viewBox="0 0 10 10" width="10" height="10" aria-hidden="true">
                  <rect x="0.5" y="2.5" width="7" height="7" fill="none" stroke="currentColor" stroke-width="1" />
                  <path d="M2.5 2.5v-2h7v7h-2" fill="none" stroke="currentColor" stroke-width="1" />
                </svg>
              </button>
              <button class="wc-btn wc-close" type="button" aria-label="Close" @mousedown.stop @click="onWinBtn('close')">
                <svg viewBox="0 0 10 10" width="10" height="10" aria-hidden="true">
                  <path d="M0 0l10 10M10 0L0 10" stroke="currentColor" stroke-width="1" />
                </svg>
              </button>
            </div>
          </header>
          <div class="main">
            <router-view />
          </div>
        </div>
      </n-dialog-provider>
    </n-message-provider>
  </n-config-provider>
</template>

<style scoped>
.shell {
  /* 顶栏高度变量（fix-batch3 Task A #2）：EditorDrawer 的 .ed-root（fixed 覆盖层）引用，
     使编辑抽屉的蒙层/面板从顶栏下沿开始、顶栏（窗口拖拽区/最小化-最大化-关闭）保持可交互。
     .ed-root 是 .shell 的 DOM 后代（ServerListView 内），自定义属性沿 DOM 树继承可达。 */
  --topbar-h: 44px;
  display: grid;
  grid-template-rows: var(--topbar-h) 1fr;
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
  user-select: none; /* 拖拽窗口时避免误选文本 */
}
.logo {
  display: flex;
  align-items: center;
  gap: 7px;
  font-weight: 600;
}
.logo-mark {
  color: var(--accent); /* LOGO 着色随强调色 */
  flex: 0 0 auto;
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
/* 窗口控制（Windows 风格）：46×44、hover --bg-hover、close hover #e81123 白图标。
 * margin-right 负值抵消 topbar 右内边距，按钮贴到窗口右缘（Windows 惯例）。 */
.win-controls {
  display: flex;
  align-self: stretch;
  margin-left: 8px;
  margin-right: -12px;
}
.wc-btn {
  width: 46px;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: none;
  border-radius: 0;
  background: transparent;
  color: var(--text-2);
  cursor: default; /* 窗口控制不显示手型（Windows 惯例） */
}
.wc-btn:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.wc-btn:active {
  opacity: 0.8;
}
.wc-close:hover {
  background: #e81123;
  color: #fff;
}
.wc-close:active {
  background: #f1707a;
  color: #fff;
  opacity: 1;
}
.main {
  display: flex;
  min-height: 0;
  min-width: 0; /* 防止内容区宽内容（Tasks 15-18）横向撑破外壳 */
}
</style>
