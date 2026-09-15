<script setup>
import { NButton } from 'naive-ui'
</script>

<template>
  <div
    :style="{
      background: 'var(--bg)',
      color: 'var(--text-1)',
      padding: '20px',
      minHeight: '100vh',
      transition: 'background 0.2s',
    }"
  >
    <p>1Remote Web UI — theme smoke</p>
    <NButton size="small" type="primary">naive-ui smoke</NButton>
  </div>
</template>
