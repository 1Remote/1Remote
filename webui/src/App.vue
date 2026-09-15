<script setup>
import { onMounted } from 'vue'
import { NButton } from 'naive-ui'
import { setAppearance, themeState, CLASSIC_THEMES, ACCENTS } from './themes'

// 临时调试钩子（Task 13 接入 n-config-provider 后移除）：
// 控制台执行 window.__theme.setAppearance({ themeMode: 'light', accent: 'green' }) 可实时验证 CSS 变量切换与持久化
onMounted(() => {
  window.__theme = { setAppearance, themeState, CLASSIC_THEMES, ACCENTS }
})
</script>

<template>
  <div
    :style="{
      background: 'var(--bg)',
      color: 'var(--text-1)',
      padding: '20px',
      minHeight: '100vh',
      transition: 'background 0.2s',
    }"
  >
    <p>1Remote Web UI — theme smoke</p>
    <NButton size="small" type="primary">naive-ui smoke</NButton>
  </div>
</template>
