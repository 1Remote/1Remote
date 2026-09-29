<script setup>
// 状态点（spec §3.4「状态」列）：connectionState 由后端从 SessionControlService 活动会话派生
// （Plan 4 Task 1 起随 /api/servers 下发，SSE reload 事件驱动前端自动刷新）。
// connected → 绿点+光晕；connecting/reconnecting → 琥珀；disconnected/未知 → 隐藏。
// 语义：仅反映「1Remote 托管会话是否活跃」——外部 mstsc.exe 等 Unhosted 会话不点亮。
// 新需求（2026-09-25，写入 spec §3.4 与测试指南）：未连接时指示器整体隐藏不可见——
// 只有用户发起过连接（连接中/重连中/已连接）才显示，连接断开后回到隐藏。列位保留
//（--c-status 列不随会话增删塌缩/弹出，行布局稳定），隐藏的只是行内指示点。
// 调整（2026-09-26 owner 定案）：状态列表头不再显示标题，列宽 12px = 指示器 8px + 4px。
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({ state: { type: String, default: 'disconnected' } })
const { t } = useI18n()

// 是否显示：仅活动会话态（连接中/重连中/已连接）显示；断开/未知隐藏（新需求）
const visible = computed(
  () => props.state === 'connected' || props.state === 'connecting' || props.state === 'reconnecting'
)
const cls = computed(() =>
  props.state === 'connected' ? 'ok' : props.state === 'connecting' || props.state === 'reconnecting' ? 'warn' : 'idle'
)
// title 用状态文案（i18n）而非后端英文枚举值：connected/connecting/reconnecting 各有词条，
// 未知/断开一律落 conn.disconnected（隐藏态不可悬停，title 仅活动态可达）
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
  <span v-if="visible" class="status-dot" :class="cls" :title="title">
    <span class="dot"></span>
  </span>
</template>

<style scoped>
.status-dot {
  display: inline-flex;
  align-items: center; /* 单子元素无 gap（G32：原 gap 5px 无效声明删除） */
}
.dot {
  /* 服务器状态点 8px 档（G19）：宿主为 36px 行列表，比数据源状态点（6px，紧凑行/
     状态栏语境）大一档；骨架屏占位点同 8px 对齐真实行视觉 */
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
/* idle 态样式已删（2026-09-25 需求）：未连接时指示器整体隐藏（v-if），不再渲染灰空心圈 */
</style>
