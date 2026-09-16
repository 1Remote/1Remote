<script setup>
/**
 * 设置页骨架（Plan 3 Task 4，spec §6）：全屏视图 = 左侧 7 分组导航（~160px、34px 行、
 * 选中项 accent-container 底）+ 右侧内容区渲染当前分组组件。常规/外观/语言与关于本任务
 * 实装；启动器/数据源/凭据库/运行器为占位（Task 5/6 填充）。
 * Esc / 「← 返回」回服务器列表：设置页是路由而非浮层，Esc 直接 router.push('/')。
 * Esc 与下拉的链序：n-select 展开时首次 Esc 只应关闭下拉（naive 在组件层消化），
 * 通过 escShield 计数（provide/inject，@update:show 维护）避免"关下拉误退设置页"。
 */
import { markRaw, onBeforeUnmount, onMounted, provide, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import GeneralGroup from '../components/settings/GeneralGroup.vue'
import AppearanceGroup from '../components/settings/AppearanceGroup.vue'
import LanguageAboutGroup from '../components/settings/LanguageAboutGroup.vue'
import LauncherGroup from '../components/settings/LauncherGroup.vue'
import DataSourceGroup from '../components/settings/DataSourceGroup.vue'
import CredentialVaultGroup from '../components/settings/CredentialVaultGroup.vue'
import RunnerGroup from '../components/settings/RunnerGroup.vue'

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
  { id: 'g-langabout', labelKey: 'settings.nav.langAbout', component: markRaw(LanguageAboutGroup) },
]

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

// Esc 返回（见文件头注释）：shield>0 说明有下拉/浮层开着，让组件层先消化
const escShield = reactive({ open: 0 })
provide('settingsEscShield', escShield)
function onKey(e) {
  if (e.key === 'Escape' && !e.defaultPrevented && escShield.open === 0) router.push('/')
}
onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <div class="settings">
    <aside class="s-nav">
      <button class="s-back" type="button" :title="t('settings.backTitle')" @click="router.push('/')">
        « {{ t('settings.back') }}
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
  height: 34px;
  padding: 0 10px;
  margin-bottom: 6px;
  border: none;
  border-radius: 7px;
  background: transparent;
  color: var(--text-3);
  font-size: 12.5px;
  text-align: left;
  cursor: pointer;
}
.s-back:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.s-item {
  /* fix-batch1 #12：一行一项——nowrap/ellipsis 骨架期已有，补 flex:none 杜绝被压缩
     （flex 收缩曾致按钮变窄、配合换行表现为多项折行）；160px 内 7 组中英标签均单行可容纳 */
  flex: none;
  height: 34px;
  padding: 0 10px;
  border: none;
  border-radius: 7px;
  background: transparent;
  color: var(--text-2);
  font-size: 12.5px;
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
.s-main {
  flex: 1;
  min-width: 0;
  display: grid;
  grid-template-rows: auto 1fr;
}
.s-header {
  padding: 14px 24px 12px;
  border-bottom: 1px solid var(--border);
  font-size: 14px;
  font-weight: 600;
  color: var(--text-1);
}
.s-body {
  min-height: 0;
  overflow-y: auto;
  padding: 18px 24px 40px;
}
</style>
