<script setup>
// 协议徽章（spec §3.4「协议」列）：每协议固定配色——这是协议身份色，非主题变量，跨主题恒定。
// 实现取"简"：背景=hex+'22'（13% 透明度叠在任意主题底色上），文字=实心 hex；中间调 hex 在
// 深浅主题底上均可读（亮色下 FTP 黄稍浅，Task 21 视觉验收再定夺）。未知协议走中性灰兜底。
import { computed } from 'vue'

const PROTOCOL_COLORS = {
  RDP: '#2c5aff',
  SSH: '#26a269',
  SFTP: '#f97316',
  FTP: '#eab308',
  VNC: '#8b5cf6',
  Telnet: '#14b8a6',
  Serial: '#64748b',
  APP: '#6b7a99',
  RdpApp: '#2c5aff',
}

const props = defineProps({ protocol: { type: String, default: '' } })

const color = computed(() => PROTOCOL_COLORS[props.protocol] || '')
</script>

<template>
  <span v-if="color" class="badge" :style="{ background: color + '22', color: color, borderColor: color + '33' }">{{
    protocol
  }}</span>
  <span v-else class="badge badge-unknown">{{ protocol || '?' }}</span>
</template>

<style scoped>
.badge {
  display: inline-flex;
  align-items: center;
  border: 1px solid transparent;
  border-radius: 999px;
  font-size: 0.8077rem;
  line-height: 1;
  padding: 2.5px 9px;
  white-space: nowrap;
}
.badge-unknown {
  border-color: var(--border);
  background: var(--bg-elevated);
  color: var(--text-3);
}
</style>
