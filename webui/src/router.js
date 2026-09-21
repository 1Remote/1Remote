import { createRouter, createWebHashHistory } from 'vue-router'
// hash 路由：WebView2 内嵌 + Kestrel 静态托管下刷新/深链零配置；
// ?token=xxx#/path 中 search 位于 hash 之前，token 提取不受影响（见 api/index.js）
import ServerListView from './views/ServerListView.vue'
import SettingsView from './views/SettingsView.vue'

export const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', name: 'servers', component: ServerListView },
    // 设置中心（Plan 3 Task 4 实装，7 分组骨架 + 常规/外观/语言与关于）
    { path: '/settings', name: 'settings', component: SettingsView },
    { path: '/:pathMatch(.*)*', redirect: '/' }, // 兜底：未知路径回服务器列表
  ],
})
