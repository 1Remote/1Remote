<script setup>
// 通用占位页：Plan 2/3 阶段对应路由（settings / editor）暂未实现；
// 文案走 i18n（titleKey → page.* 词条），内部计划号不入 UI
import { useI18n } from 'vue-i18n'

defineProps({ titleKey: { type: String, default: '' } })
const { t } = useI18n()
</script>

<template>
  <div class="placeholder">
    <p class="placeholder-title">{{ t('placeholder.comingSoon', { title: titleKey ? t('page.' + titleKey) : '' }) }}</p>
  </div>
</template>

<style scoped>
.placeholder {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}
.placeholder-title {
  color: var(--text-3);
  font-size: var(--fs-body);
}
</style>
