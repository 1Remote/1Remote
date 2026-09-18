<script setup>
/**
 * 设置页骨架（Plan 3 Task 4，spec §6）：全屏视图 = 左侧 7 分组导航（~160px、34px 行、
 * 选中项 accent-container 底）+ 右侧内容区渲染当前分组组件。常规/外观/语言与关于本任务
 * 实装；启动器/数据源/凭据库/运行器为占位（Task 5/6 填充）。
 * Esc / 「← 返回」回服务器列表：设置页是路由而非浮层，Esc 直接 router.push('/')。
 * Esc 与下拉的链序：n-select 展开时首次 Esc 只应关闭下拉（naive 在组件层消化），
 * 通过 escShield 计数（provide/inject，@update:show 维护）避免"关下拉误退设置页"。
 * 计数的两处天真行为缺陷（键序按事件派发实际顺序）：
 * ① naive 在元素级 Esc handler 内**同步** emit update:show(false)（Select.mjs doUpdateShow
 *   直接 call）——等事件冒泡到 window 时计数已归零，原先的 `open === 0` 守卫护不住
 *   "关下拉"这一次按键，表现为关下拉的同时整页退出；
 * ② 只增不减的卡死路径（下拉开着时宿主模态/分组组件被直接卸载，update:show 不再发出），
 *   计数恒 >0 后 Esc 永远被吞（"按 Esc 无反应"）。
 * 对策：capture 阶段先快照本次按键**开始时**的计数（早于组件层任何处理），bubble 阶段按
 * 快照执行一次性语义：开始时有层开着（或计数已卡死）→ 本键视为已消费并把计数归零（自愈），
 * 不返回；快照与计数均为 0 才返回列表。任何卡死计数至多吞一次 Esc 即恢复。
 */
import { markRaw, onBeforeUnmount, onMounted, provide, reactive, ref } from 'vue'
import { isNavigationFailure, NavigationFailureType, useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import GeneralGroup from '../components/settings/GeneralGroup.vue'
import AppearanceGroup from '../components/settings/AppearanceGroup.vue'
import AboutGroup from '../components/settings/AboutGroup.vue'
import LauncherGroup from '../components/settings/LauncherGroup.vue'
import DataSourceGroup from '../components/settings/DataSourceGroup.vue'
import CredentialVaultGroup from '../components/settings/CredentialVaultGroup.vue'
import RunnerGroup from '../components/settings/RunnerGroup.vue'
import { useVersionInfo } from '../composables/useVersionInfo'
import { useUiLockWhileMounted } from '../composables/editorBus'

// 设置页存在期间持有通用 UI 锁：App.vue 顶栏（搜索/「+」/⚙）随之禁用（batch9 #2）——
// 设置页是路由整屏界面，与编辑抽屉同属"上下文切换中"状态
useUiLockWhileMounted()

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

// 分组清单：id 用于 ?#/settings 深链（初始 hash 查询参数 g=），组件 markRaw 防
// reactive 化警告；label 走 settings.nav.* 词条
const GROUPS = [
  { id: 'g-general', labelKey: 'settings.nav.general', component: markRaw(GeneralGroup) },
  { id: 'g-launcher', labelKey: 'settings.nav.launcher', component: markRaw(LauncherGroup) },
  { id: 'g-data', labelKey: 'settings.nav.data', component: markRaw(DataSourceGroup) },
  { id: 'g-credentials', labelKey: 'settings.nav.credentials', component: markRaw(CredentialVaultGroup) },
  { id: 'g-appearance', labelKey: 'settings.nav.appearance', component: markRaw(AppearanceGroup) },
  { id: 'g-runners', labelKey: 'settings.nav.runners', component: markRaw(RunnerGroup) },
  // 「语言与关于」已更名「关于」（语言选择行已并入常规组），
  // 分组 id 同步 g-langabout → g-about（深链 ?g= 引用仅在本文件）
  { id: 'g-about', labelKey: 'settings.nav.about', component: markRaw(AboutGroup) },
]

// 「关于」导航项红点：数据源 = 进设置页时自行拉一次 /api/version
//（简单方案：端点轻量读后端静态缓存，免去与 App.vue 共享状态的跨组件契约，见 useVersionInfo 头注释）
const { update: updateInfo } = useVersionInfo()

const activeId = ref('g-general')
{
  // 深链支持：#/settings?g=g-appearance（未知 id 忽略）
  const g = route.query.g
  if (typeof g === 'string' && GROUPS.some((x) => x.id === g)) activeId.value = g
}
const activeGroup = () => GROUPS.find((x) => x.id === activeId.value) || GROUPS[0]

function selectGroup(id) {
  activeId.value = id
  // 写回 hash 查询参数（replace 不留历史），刷新/分享保持分组
  router.replace({ query: { ...route.query, g: id } })
}

// Esc 返回（缺陷分析见文件头注释）：capture 阶段快照按键开始时的计数（早于 naive 组件层
// 的同步 update:show 归零），bubble 阶段按快照执行一次性语义并自愈卡死计数
const escShield = reactive({ open: 0 })
provide('settingsEscShield', escShield)
let escShieldAtStart = 0
function onEscCapture(e) {
  if (e.key === 'Escape') escShieldAtStart = escShield.open
}
function onKey(e) {
  if (e.key !== 'Escape' || e.defaultPrevented) return
  if (escShieldAtStart > 0 || escShield.open > 0) {
    escShield.open = 0 // 本键用于关（或刚关掉）下拉/浮层：消费一次，计数归零自愈
    return
  }
  leaveSettings()
}

// 「← 返回」/Esc 共用出口（重复导航自愈，机制见 App.vue openSettings 注释）：
// 同目标重复导航被 vue-router 去重丢弃时带 force 重发，重新触发 router-view 渲染。
function leaveSettings() {
  router.push('/').then(
    (failure) => {
      if (isNavigationFailure(failure, NavigationFailureType.duplicated)) {
        router.push({ path: '/', force: true }).catch(() => {})
      }
    },
    () => {}
  )
}
onMounted(() => {
  window.addEventListener('keydown', onEscCapture, true)
  window.addEventListener('keydown', onKey)
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onEscCapture, true)
  window.removeEventListener('keydown', onKey)
})
</script>

<template>
  <div class="settings">
    <aside class="s-nav">
      <button class="s-back" type="button" :title="t('settings.backTitle')" @click="leaveSettings()">
        <svg class="s-back-arrow" viewBox="0 0 16 16" width="12" height="12" aria-hidden="true">
          <path
            d="M10 3 5 8l5 5"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
        {{ t('settings.back') }}
      </button>
      <nav class="s-groups">
        <button
          v-for="g in GROUPS"
          :key="g.id"
          type="button"
          class="s-item"
          :class="{ active: g.id === activeId }"
          @click="selectGroup(g.id)"
        >
          {{ t(g.labelKey) }}
          <!-- 「关于」项红点：仅 updateAvailable -->
          <span v-if="g.id === 'g-about' && updateInfo?.available" class="s-dot"></span>
        </button>
      </nav>
    </aside>
    <section class="s-main">
      <header class="s-header">{{ t(activeGroup().labelKey) }}</header>
      <div class="s-body">
        <component :is="activeGroup().component" />
      </div>
    </section>
  </div>
</template>

<style scoped>
.settings {
  flex: 1;
  min-width: 0;
  display: flex;
}
.s-nav {
  flex: 0 0 160px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 10px 8px;
  border-right: 1px solid var(--border);
  background: var(--bg-panel);
  overflow-y: auto;
}
.s-back {
  /* 返回按钮醒目化：accent 容器底 + accent 描边/文字 + ← 图标（与 .s-item.active
     同一视觉语系，暗/亮基底各自有低饱和容器变体，保持克制） */
  display: flex;
  align-items: center;
  gap: 5px;
  flex: none;
  height: 34px;
  padding: 0 10px;
  margin-bottom: 6px;
  border: 1px solid var(--accent);
  border-radius: 7px;
  background: var(--accent-container);
  color: var(--accent-text);
  font-size: 0.9615rem;
  font-weight: 600;
  text-align: left;
  cursor: pointer;
}
.s-back:hover {
  border-color: var(--accent-hover);
  filter: brightness(1.06);
}
.s-back-arrow {
  flex: 0 0 auto;
}
.s-groups {
  /* 一行一项由 flex column 承载；若无布局规则，button 默认 inline-block
     会横向平铺换行（表现为多项挤在一行） */
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.s-item {
  /* 一行一项由 .s-groups 的 flex column 承载（见上）；本规则的 nowrap/ellipsis 负责
     文本不折行、超宽省略。flex:none 在非 flex 父容器上无效，现父级已是 flex column，
     保留无害（占位语义：不被压缩）。160px 内 7 组中英标签均单行可容纳。
     position:relative 为「关于」项红点（.s-dot）的定位基准 */
  position: relative;
  flex: none;
  height: 34px;
  padding: 0 10px;
  border: none;
  border-radius: 7px;
  background: transparent;
  color: var(--text-2);
  font-size: 0.9615rem;
  text-align: left;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  cursor: pointer;
}
.s-item:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.s-item.active {
  background: var(--accent-container);
  color: var(--accent-text);
}
/* 「关于」导航项红点：8px 圆点绝对定位在文字右上，
   仅 updateAvailable 时渲染（与 App.vue ⚙ 红点同一形态） */
.s-dot {
  position: absolute;
  top: 6px;
  right: 8px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: red;
}
.s-main {
  flex: 1;
  min-width: 0;
  display: grid;
  grid-template-rows: auto 1fr;
}
.s-header {
  padding: 14px 24px 12px;
  border-bottom: 1px solid var(--border);
  font-size: 1.0769rem;
  font-weight: 600;
  color: var(--text-1);
}
.s-body {
  min-height: 0;
  overflow-y: auto;
  padding: 18px 24px 40px;
}
</style>
