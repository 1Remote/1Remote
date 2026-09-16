<script setup>
// 占位分组（Plan 3 Task 6 实装：数据源卡片 + 添加向导 + 测试连接）
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
