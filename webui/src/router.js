import { createRouter, createWebHashHistory } from 'vue-router'
// hash 路由：WebView2 内嵌 + Kestrel 静态托管下刷新/深链零配置；
// ?token=xxx#/path 中 search 位于 hash 之前，token 提取不受影响（见 api/index.js）
import ServerListView from './views/ServerListView.vue'
import PlaceholderView from './views/PlaceholderView.vue'

export const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', name: 'servers', component: ServerListView },
    // titleKey → locales 的 page.* 词条（占位页标题 i18n；Plan 2/3 实装后替换为真实页面）
    { path: '/settings', name: 'settings', component: PlaceholderView, props: { titleKey: 'settings' } },
    { path: '/editor', name: 'editor', component: PlaceholderView, props: { titleKey: 'editor' } },
    { path: '/:pathMatch(.*)*', redirect: '/' }, // 兜底：未知路径回服务器列表
  ],
})
