<script setup>
// 通用占位页：Plan 2/3 阶段对应路由（settings / editor）暂未实现
defineProps({ title: { type: String, default: '' } })
</script>

<template>
  <div class="placeholder">
    <p class="placeholder-title">敬请期待 / Coming soon — {{ title }} (Plan 2/3)</p>
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
  font-size: 13px;
}
</style>
