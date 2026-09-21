<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { isNavigationFailure, NavigationFailureType, useRouter } from 'vue-router'
import { enUS, zhCN } from 'naive-ui'
import { useNaiveTheme } from './themes'
import { useServers } from './composables/useServers'
import { useEditorBus } from './composables/editorBus'
import { useVersionInfo } from './composables/useVersionInfo'
import { handoffTableFocus } from './composables/tableBus'
const naive = useNaiveTheme()
const { t, locale } = useI18n()
const { requestNewServer, requestImport, editorOpen, uiLock } = useEditorBus()
// 顶栏占用态：编辑抽屉或任一整屏/模态界面（设置页/标签管理/导入，见 editorBus.uiLock）
// 打开时顶栏整体禁用——两层状态在此合并供模板与快捷键统一消费
const overlayActive = computed(() => editorOpen.value || uiLock.value)
// naive-ui 内建文案（弹窗按钮/分页等）跟随 i18n 语言（dateZhCN/dateEnUS 暂未用到日期组件，不引入）。
// Input/Select 的默认 placeholder（enUS "Please Input"/"Please Select"、zhCN "请输入"/"请选择"）
// 清空为 ''：WPF 表单无 Tag 的输入框不显示任何提示文本，web 未提供 placeholderKey 的字段
// 同样应为空才对齐；有键的字段由 FormField 显式传 :placeholder，不受该默认影响。
const naiveLocale = computed(() => {
  const base = locale.value === 'en-US' ? enUS : zhCN
  return {
    ...base,
    Input: { ...base.Input, placeholder: '' },
    Select: { ...base.Select, placeholder: '' },
  }
})
const { searchQuery, searching } = useServers()
const searchInput = ref(null)

// Ctrl+F / Cmd+F 全局聚焦搜索框（快捷键仅此一条，无 Ctrl+K）：
// keydown 于 window（冒泡），preventDefault 让位浏览器默认（如地址栏搜索 / 页内查找栏）；
// 再次按下全选已有内容，方便直接覆盖输入。
// Esc 不在此处理（输入框元素级 handler 焦点在表格时不触发，无法参与统一链序）——
// 全局 Esc 链（菜单→勾选→搜索→光标）由 ServerListView 的 window 级 handler 统一调度。
//
// 浏览器快捷键兜底（WPF 壳主修复在 MainWindowView 的
// AreBrowserAcceleratorKeysEnabled=false；这里是浏览器直开 dev 页的场景）：
//  - Ctrl/Cmd+P 打印：应用无打印功能，吞掉（否则弹浏览器打印预览）
//  - Alt+Left / Alt+Right 历史导航：单页壳无历史语义，keydown preventDefault 拦截。
//    可拦截性说明：本环境 CDP 合成按键不触发浏览器加速键、OS 级
//    注入到不了窗口，无法实证；按 Chromium/MDN 文档，Alt+←/→ 是 keydown 的默认
//    动作（可被 preventDefault 取消，保留例外仅 Ctrl+W/T/N 等浏览器保留键），
//    保留此兜底，硬保证由 WPF 侧 AreBrowserAcceleratorKeysEnabled 提供。
//  - F5/Ctrl+R 刷新不拦——刷新是开发需要
function onGlobalKey(e) {
  const key = e.key?.toLowerCase()
  if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && key === 'f') {
    if (overlayActive.value) return // 编辑抽屉/设置页/模态打开：搜索框已锁定，不抢焦点（也不吞浏览器默认行为）
    e.preventDefault()
    searchInput.value?.focus()
    searchInput.value?.select()
    return
  }
  // Ctrl/Cmd+P：preventDefault 即抑制浏览器打印
  if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && key === 'p') {
    e.preventDefault()
    return
  }
  // Alt+←/→：拦截浏览器历史后退/前进（单页应用无历史栈）
  if (e.altKey && !e.ctrlKey && !e.metaKey && !e.shiftKey && (key === 'arrowleft' || key === 'arrowright')) {
    e.preventDefault()
  }
}
onMounted(() => {
  window.addEventListener('keydown', onGlobalKey)
  // dev 控制台提醒（owner 原话保留）：排序/列显隐/列宽目前只落 localStorage（浏览器本地），
  // 与桌面端（WPF 侧 Sqlite/注册表）不共享——持久化策略分叉，后续统一时这里的条目要跟进。
  // 仅 DEV 构建打包（import.meta.env.DEV 生产构建常量折叠剔除）。
  if (import.meta.env.DEV)
    console.warn(
      '[1Remote WebUI] 排序/列显隐/列宽仅存 localStorage（本地），与桌面端不共享——持久化策略分叉，待后续统一'
    )
})
onBeforeUnmount(() => window.removeEventListener('keydown', onGlobalKey))

// 「+」下拉：新建 + 导入——服务器已存在时导入是自然入口；两动作都经 editorBus
// 计数通知 ServerListView（跨层，见 editorBus.js）
const addOptions = computed(() => [
  { label: t('topbar.newServer'), key: 'new' },
  { label: t('import.title'), key: 'import' },
])
function onAddSelect(key) {
  if (key === 'new') requestNewServer()
  else if (key === 'import') requestImport()
}

// ⚙ 设置按钮红点：检测到新版本时右上角 8px 红点。
// 数据源 = App 挂载即拉一次 /api/version（首检未完成时 30s 重拉至定论，见组合式函数头注释）；
// 简单方案，刻意不与设置页共享状态（后端读静态缓存，请求轻量）
const { update: updateInfo } = useVersionInfo()

// ⚙ 进设置（重复导航自愈）：router.push 同目标重复导航会被 vue-router 判为
// NAVIGATION_DUPLICATED 静默丢弃（isSameRouteLocation 去重，不重跑 finalizeNavigation，
// 不重新赋 currentRoute）——若一次语言切换等全站重渲染中 router-view 的渲染 effect
// 曾抛错死亡（URL/currentRoute 已推进而视图停在旧页），此后再点 ⚙ 全部落在去重分支，
// 表现为"永远进不了设置、刷新才恢复"。检测到 duplicated 失败时带 force 重发一次：
// force 跳过去重检查，finalizeNavigation 重新给 currentRoute 赋新对象，重新触发
// router-view 渲染——把"卡死直到手动刷新"变成"下一次点击自愈"。
const router = useRouter()
function openSettings() {
  router.push('/settings').then(
    (failure) => {
      if (isNavigationFailure(failure, NavigationFailureType.duplicated)) {
        router.push({ path: '/settings', force: true }).catch(() => {})
      }
    },
    () => {}
  )
}

// ===== 窗口控制：仅 WebView2 宿主可见/生效 =====
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
// WPF 壳 Ctrl+F 转发入口（ExecuteScriptAsync 调用）：焦点不在 WebView2
//（如启动后未点进页面）时，WPF 窗口级 KeyBinding 抢先把命令派给 CommandFocusFilter，
// MainWindowView 转而调用本函数——聚焦并全选搜索框，与页面内 Ctrl+F handler
//（onGlobalKey）等效；焦点在网页内时网页自己的 handler 生效，不走此路径
window.__focusSearch = () => {
  if (overlayActive.value) return // 抽屉/设置页/模态打开时与页内 Ctrl+F 一致：不抢焦点
  searchInput.value?.focus()
  searchInput.value?.select()
}
onBeforeUnmount(() => {
  delete window.__setWinState
  delete window.__focusSearch
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
          <header
            class="topbar"
            :class="{ hosted: isHosted }"
            @mousedown="onTopbarMouseDown"
            @dblclick="onTopbarDblClick"
          >
            <!-- LOGO：程序真实图标（Ui/LOGO.ico 提取的 256px PNG，public/logo.png）；
                 文字包 .logo-text 供极窄窗收成纯图标（见样式区 media query） -->
            <div class="logo">
              <img class="logo-mark" src="/logo.png" width="16" height="16" alt="" />
              <span class="logo-text">1Remote</span>
            </div>
            <!-- 顶栏搜索框：⌕ + 输入 + 搜索中 spinner；Ctrl F 聚焦全选 / Esc 由全局链清空（见 setup）；
                 ↑/↓ 把键盘焦点移交给服务器列表（tableBus.handoffTableFocus：光标落首/末行，此后 ↑↓/Enter 归表格）。
                 编辑抽屉/设置页/模态打开时锁定（overlayActive）：容器弱化 + input disabled -->
            <div
              class="searchbox"
              :class="{ disabled: overlayActive }"
              :title="t('search.title')"
              @click="searchInput?.focus()"
            >
              <span class="sb-icon">⌕</span>
              <input
                ref="searchInput"
                v-model="searchQuery"
                class="sb-input"
                type="text"
                :disabled="overlayActive"
                :placeholder="t('search.placeholder')"
                @keydown.down.prevent="handoffTableFocus(1)"
                @keydown.up.prevent="handoffTableFocus(-1)"
              />
              <!-- 常驻占位仅切 visibility（不 v-if）：避免 spinner 出现/消失时输入框宽度跳动 -->
              <span class="sb-spin" :class="{ on: searching }" :title="t('search.searching')"></span>
            </div>
            <div class="topbar-actions">
              <!-- 「+」下拉：新建服务器 / 导入服务器（经 editorBus 通知 ServerListView）；
                   抽屉/设置页/模态打开时禁用——disabled 的原生 button 不派发
                   click，n-dropdown 不再弹出 -->
              <n-dropdown trigger="click" :options="addOptions" @select="onAddSelect">
                <n-button quaternary size="small" :disabled="overlayActive" :title="t('topbar.addServer')">+</n-button>
              </n-dropdown>
              <span class="gear-wrap">
                <n-button quaternary size="small" :disabled="overlayActive" @click="openSettings()">⚙</n-button>
                <!-- 更新红点：仅 updateAvailable -->
                <span v-if="updateInfo?.available" class="gear-dot"></span>
              </span>
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
              <button
                class="wc-btn wc-close"
                type="button"
                aria-label="Close"
                @mousedown.stop
                @click="onWinBtn('close')"
              >
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
  /* 顶栏高度变量：EditorDrawer 的 .ed-root（fixed 覆盖层）引用，
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
  position: relative; /* 搜索框绝对定位居中的定位基准 */
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
  gap: 6px;
  font-weight: 600;
}
.logo-mark {
  flex: 0 0 auto; /* 真实彩色图标，不随强调色着色 */
}
/* 视觉居中：绝对定位脱离 flex 流，logo 与右侧动作/窗口按钮布局不受影响。
   窄窗防遮挡（owner 反馈：拉窄窗口时 +/⚙/搜索框互相遮挡——旧公式
   max-width:min(420px, 100vw-360px) 的 360px 预算按浏览器直开估，宿主态右侧固定
   占位 ≈212px（窗口控制 3×46 + 8 间距 + 两个 28px 钮 + gap），~824px 起即溢出）：
   - 两锚点预算：--sb-left = logo 区；--sb-right = 右侧动作钮（浏览器态）/ 动作钮 +
     窗口控制（宿主态，.topbar.hosted 覆盖）；
   - 宽窗（宿主 ≥860px / 浏览器 ≥588px）：宽度吃满 420px，left 取「窗口几何居中位」，
     与旧行为逐像素一致；
   - 窄窗：宽度 = 两锚点间空隙，left 取「居中位」与「右锚点约束位」的较小值——
     min() 两式在 420px 满宽处相等（连续无跳变），收紧后自动左移贴住 logo 锚点，
     只缩不叠（宽度公式保证 left+width ≤ 100vw-右锚点）；
   - 极窄（<600px）logo 收成纯图标，左预算 96→48（见样式区末 media query）。 */
.searchbox {
  --sb-left: 96px;
  --sb-right: 84px;
  --sb-w: min(420px, calc(100vw - var(--sb-left) - var(--sb-right)));
  position: absolute;
  left: min(calc((100vw - var(--sb-w)) / 2), calc(100vw - var(--sb-right) - var(--sb-w)));
  width: var(--sb-w); /* absolute+left 定位下 width:auto 会 shrink-to-fit，须显式定宽 */
  display: flex;
  align-items: center;
  gap: 6px;
  height: var(--ctrl-h-m);
  padding: 0 8px;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-ctrl);
  font-size: var(--fs-body);
  cursor: text;
}
.searchbox:focus-within {
  border-color: var(--accent-focus);
}
/* 抽屉/设置页/模态打开时的锁定态：弱化 + 禁用光标（克制，不加边框变色等强提示） */
.searchbox.disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
.searchbox.disabled .sb-input {
  cursor: not-allowed; /* input 自身 cursor:text 需覆盖，整个框统一 not-allowed */
}
/* 宿主态（WebView2）右预算加窗口控制 3×46px + 8px 间距（≈212px，含余量取 220） */
.topbar.hosted .searchbox {
  --sb-right: 220px;
}
/* 极窄窗：logo 收成纯图标（1Remote 文字隐藏），左预算 96→48——搜索框相应变宽。
   断点 600px 处布局有一次约 48px 的跳变，属 logo 文字消失的自然档位切换 */
@media (max-width: 599px) {
  .logo-text {
    display: none;
  }
  .searchbox {
    --sb-left: 48px;
  }
}
.sb-icon {
  flex: 0 0 auto;
  color: var(--text-4);
  font-size: var(--fs-body);
  line-height: 1;
}
.sb-input {
  flex: 1;
  min-width: 0;
  border: none;
  outline: none;
  background: transparent;
  color: var(--text-1);
  font-size: var(--fs-body);
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
/* ⚙ 更新红点：8px 圆点绝对定位在按钮右上（与设置导航红点同形态） */
.gear-wrap {
  position: relative;
  display: inline-flex;
}
.gear-dot {
  position: absolute;
  top: 2px;
  right: 2px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--danger);
  pointer-events: none; /* 红点不吞点击，落点始终是 ⚙ 按钮 */
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
/* 10px 图标 1:1 渲染，1.5px = 控件级描边档（CSS 覆盖模板 stroke-width="1"） */
.wc-btn svg path,
.wc-btn svg rect {
  stroke-width: 1.5px;
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
  min-width: 0; /* 防止内容区宽内容横向撑破外壳 */
}
</style>
