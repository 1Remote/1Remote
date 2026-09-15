import { createApp } from 'vue'
import naive from 'naive-ui'
import App from './App.vue'
import { router } from './router'
import { initTheme } from './themes'
import { i18n } from './locales'

// 挂载策略：立即 mount，不 await initTheme —— 后端不可达时 mount 最多被阻塞到 30s 超时是不可接受的。
// initTheme 内部在 await 前已同步 applyTheme()（默认 dark/系统值），首帧即有 data-theme；
// 后端返回的已存外观（如 light）随后异步覆盖，可能有一帧默认色闪变，暂可接受。
initTheme()
const app = createApp(App)
// naive-ui 全量注册：无 unplugin 自动按需导入脚手架，全局安装最简（spec 优先可维护性）。
// 实测 dist ~1.4MB 未压缩 / ~400kB gzip（naive 全量为主），本地 WebView2/localhost 加载可接受；
// 若 Task 21 实测冷启动慢，再考虑 unplugin 按需导入
app.use(naive)
// i18n（Task 19）：zh-CN/en-US 双语骨架，语言探测/切换见 locales/index.js；
// 其余 12 语言由 Plan 3 的 XAML→JSON 转换脚本补齐
app.use(i18n)
app.use(router)
app.mount('#app')
