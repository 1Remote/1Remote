import { createApp } from 'vue'
import App from './App.vue'
import { initTheme } from './themes'

// 挂载策略：立即 mount，不 await initTheme —— 后端不可达时 mount 最多被阻塞到 30s 超时是不可接受的。
// initTheme 内部在 await 前已同步 applyTheme()（默认 dark/系统值），首帧即有 data-theme；
// 后端返回的已存外观（如 light）随后异步覆盖，可能有一帧默认色闪变，暂可接受。
initTheme()
createApp(App).mount('#app')
