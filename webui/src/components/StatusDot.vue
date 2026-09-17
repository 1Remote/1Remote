<script setup>
// 状态点（spec §3.4「状态」列）：connectionState 由后端从 SessionControlService 活动会话派生
// （Plan 4 Task 1 起随 /api/servers 下发，SSE reload 事件驱动前端自动刷新）。
// connected → 绿点+光晕；connecting/reconnecting → 琥珀；disconnected/未知 → 灰空心圈 +「—」。
// 语义：仅反映「1Remote 托管会话是否活跃」——外部 mstsc.exe 等 Unhosted 会话不点亮。
import { computed } from 'vue'

const props = defineProps({ state: { type: String, default: 'disconnected' } })

const cls = computed(() =>
  props.state === 'connected' ? 'ok' : props.state === 'connecting' || props.state === 'reconnecting' ? 'warn' : 'idle'
)
// 仅离线态补「—」文本（无活动会话是常态基线）
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
  font-size: 0.8462rem;
  line-height: 1;
}
</style>
