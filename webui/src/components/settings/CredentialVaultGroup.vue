<script setup>
// 占位分组（Plan 3 Task 5 实装：凭据库表格 + reveal 二次验证）：仅保持导航可点
import { useI18n } from 'vue-i18n'
const { t } = useI18n()
</script>

<template>
  <div class="placeholder-group">
    <p>{{ t('settings.placeholder') }}</p>
  </div>
</template>

<style scoped>
.placeholder-group {
  color: var(--text-3);
  font-size: 12.5px;
}
</style>
