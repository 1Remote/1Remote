<script setup>
// 协议徽章（spec §3.4「协议」列）：每协议固定配色——这是协议身份色，非主题变量，跨主题恒定。
// 实现：背景/边框 = hex+'22'/'33'（13%/20% 透明度叠在行底色上，恒定）；文字按基底深浅
// 取双值——中间调 hex 直接作小字在浅色底上不可读（light+FTP 1.70:1），
// light 侧换深变体、dark 侧对过深的 4 协议（RDP/VNC/Serial/APP）换浅变体，全部组合
// （含 13% 底色叠加、hover 行底、勾选行 accent-container 底）≥4.5 AA。未知协议走中性灰兜底。
import { computed } from 'vue'
import { isDarkMode } from '../themes'

// 身份色（底色/边框来源，双基底恒定）。键 = 列表 protocol 字段值 = 后端 ProtocolName
//（RdpApp.cs:13 为 "RemoteApp"，非类名）——第三轮 G4：旧键 'RdpApp' 从不匹配任何
// 行，RemoteApp 徽章一直走 badge-unknown 灰兜底；RemoteApp 属 RDP 家族，配色沿用 RDP 蓝
const PROTOCOL_COLORS = {
  RDP: '#2c5aff',
  SSH: '#26a269',
  SFTP: '#f97316',
  FTP: '#eab308',
  VNC: '#8b5cf6',
  Telnet: '#14b8a6',
  Serial: '#64748b',
  APP: '#6b7a99',
  RemoteApp: '#2c5aff',
}

// 文字色·暗色基底（身份色过深的换浅一档，其余保持身份色）
const PROTOCOL_TEXT_DARK = {
  RDP: '#7c9bff',
  SSH: '#3cba7f',
  SFTP: '#f97316',
  FTP: '#eab308',
  VNC: '#b8a6f7',
  Telnet: '#14b8a6',
  Serial: '#9aa8bb',
  APP: '#94a3b8',
  RemoteApp: '#7c9bff',
}

// 文字色·亮色基底（身份色过浅的换深一档）
const PROTOCOL_TEXT_LIGHT = {
  RDP: '#2148c8',
  SSH: '#116631',
  SFTP: '#a03609',
  FTP: '#8f5606',
  VNC: '#6d28d9',
  Telnet: '#0c6a63',
  Serial: '#475569',
  APP: '#46536e',
  RemoteApp: '#2148c8',
}

const props = defineProps({ protocol: { type: String, default: '' } })

const color = computed(() => PROTOCOL_COLORS[props.protocol] || '')
const textColor = computed(() => (isDarkMode() ? PROTOCOL_TEXT_DARK : PROTOCOL_TEXT_LIGHT)[props.protocol] || '')
</script>

<template>
  <span v-if="color" class="badge" :style="{ background: color + '22', color: textColor, borderColor: color + '33' }">{{
    protocol
  }}</span>
  <span v-else class="badge badge-unknown">{{ protocol || '?' }}</span>
</template>

<style scoped>
.badge {
  display: inline-flex;
  align-items: center;
  border: 1px solid transparent;
  border-radius: var(--radius-pill);
  font-size: var(--fs-micro);
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
