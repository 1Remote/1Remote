<script setup>
// 状态点（spec §3.4「状态」列）：connectionState 由后端从 SessionControlService 活动会话派生
// （Plan 4 Task 1 起随 /api/servers 下发，SSE reload 事件驱动前端自动刷新）。
// connected → 绿点+光晕；connecting/reconnecting → 琥珀；disconnected/未知 → 灰空心圈。
// 语义：仅反映「1Remote 托管会话是否活跃」——外部 mstsc.exe 等 Unhosted 会话不点亮。
// idle 态不在点旁附「—」文本：空心圈已表意、悬停 title 给精确状态，不加装饰性冗余。
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({ state: { type: String, default: 'disconnected' } })
const { t } = useI18n()

const cls = computed(() =>
  props.state === 'connected' ? 'ok' : props.state === 'connecting' || props.state === 'reconnecting' ? 'warn' : 'idle'
)
// title 用状态文案（i18n）而非后端英文枚举值：connected/connecting/reconnecting 各有词条，
// 未知/断开一律落 conn.disconnected（与点的灰色空心兜底同口径）
const title = computed(() =>
  props.state === 'connected'
    ? t('conn.connected')
    : props.state === 'connecting'
      ? t('conn.connecting')
      : props.state === 'reconnecting'
        ? t('conn.reconnecting')
        : t('conn.disconnected')
)
</script>

<template>
  <span class="status-dot" :class="cls" :title="title">
    <span class="dot"></span>
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
  box-shadow: 0 0 4px var(--success); /* 已连接光晕 4px */
}
.warn .dot {
  background: var(--warning);
}
.idle .dot {
  border: 1.5px solid var(--text-4); /* 灰空心圈 */
}
</style>
