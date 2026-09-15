<script setup>
// 状态点（spec §3.4「状态」列，预留）：connectionState 是预留字段，后端功能就绪前恒 'disconnected'。
// connected → 绿点+光晕；connecting/reconnecting → 琥珀；disconnected/未知 → 灰空心圈 +「—」。
import { computed } from 'vue'

const props = defineProps({ state: { type: String, default: 'disconnected' } })

const cls = computed(() =>
  props.state === 'connected' ? 'ok' : props.state === 'connecting' || props.state === 'reconnecting' ? 'warn' : 'idle'
)
// 仅离线态补「—」文本，锁定预留列视觉（数据恒 disconnected → 恒显示「—」）
const showDash = computed(() => cls.value === 'idle')
</script>

<template>
  <span class="status-dot" :class="cls" :title="state">
    <span class="dot"></span>
    <span v-if="showDash" class="dash">—</span>
  </span>
</template>

<style scoped>
.status-dot {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex: 0 0 auto;
}
.ok .dot {
  background: var(--success);
  box-shadow: 0 0 6px var(--success); /* 已连接光晕 */
}
.warn .dot {
  background: var(--warning);
}
.idle .dot {
  border: 1.5px solid var(--text-4); /* 灰空心圈 */
}
.dash {
  color: var(--text-4);
  font-size: 11px;
  line-height: 1;
}
</style>
