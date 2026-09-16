<script setup>
/**
 * Markdown 备注字段（fix-batch2 Task C #4，owner 确认 marked 库）：
 * 「编辑 ⇄ 预览」二选一——编辑态 = 等宽 textarea（值原样存取，存储格式不因预览改变，
 * json 里仍是纯 Markdown 文本）；预览态 = marked.parse 渲染（gfm + breaks）。
 *
 * XSS 处理（有意轻量，注意局限）：渲染后做一次正则清洗——剥 <script> 块、危险嵌资源
 * 标签（iframe/object/embed/...）、on* 事件属性、javascript: URL。这不是完整净化器
 * （如 <img src=x onerror=… 的属性拆分混淆、data: URL、CSS 注入等极端构造不保证覆盖）；
 * 编辑器内容来自用户自己的服务器配置（单机自托管场景，无多租户互攻击面），加上后端
 * 保存的也是这份纯文本，故轻量清洗已覆盖常见意外注入面。若未来引入多用户共享库，
 * 应换 DOMPurify 等完整净化器（v-html + 本组件 sanitize 的替换点集中在此）。
 *
 * 组件形状对齐 FormField 约定：modelValue = 字符串（null 容忍），emit update:modelValue；
 * 由 FormField 按 field.type === 'markdown' 分发（schemas.js 的 basic 组 Note 字段）。
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { marked } from 'marked'

marked.setOptions({ breaks: true, gfm: true })

const props = defineProps({
  /** 当前值（Markdown 源文本；null/undefined 容忍按空串处理） */
  modelValue: { type: String, default: '' },
  disabled: { type: Boolean, default: false },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()

const mode = ref('edit') // 'edit' | 'preview'（切换仅影响展示，不改值）

/** 轻量净化（局限见文件头注释）：剥 script 块 / 危险标签 / on* 属性 / javascript: URL */
function sanitize(html) {
  return String(html)
    .replace(/<script[\s\S]*?<\/script\s*>/gi, '')
    .replace(/<\/?(iframe|object|embed|style|link|meta|base|form)\b[^>]*>/gi, '')
    .replace(/\son\w+\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+)/gi, '')
    .replace(/((?:href|src|xlink:href)\s*=\s*)(?:"\s*javascript:[^"]*"|'\s*javascript:[^']*'|javascript:\S+)/gi, '$1""')
}

const rendered = computed(() => sanitize(marked.parse(props.modelValue ?? '')))
const isEmpty = computed(() => !String(props.modelValue ?? '').trim())
</script>

<template>
  <div class="md-field">
    <!-- 工具条：右侧「编辑 ⇄ 预览」切换（按钮文案 = 点击后进入的模式） -->
    <div class="md-toolbar">
      <button
        class="md-toggle"
        type="button"
        :disabled="disabled"
        :title="mode === 'edit' ? t('editor.mdPreview') : t('editor.mdEdit')"
        @click="mode = mode === 'edit' ? 'preview' : 'edit'"
      >{{ mode === 'edit' ? '👁 ' + t('editor.mdPreview') : '✎ ' + t('editor.mdEdit') }}</button>
    </div>

    <!-- 编辑态：等宽 textarea -->
    <n-input
      v-if="mode === 'edit'"
      class="md-editor"
      type="textarea"
      size="small"
      :rows="5"
      :value="modelValue ?? ''"
      :disabled="disabled"
      :input-props="{ spellcheck: false }"
      @update:value="emit('update:modelValue', $event)"
    />

    <!-- 预览态：渲染 HTML（已过 sanitize；高度与编辑态对齐，超高内滚） -->
    <div v-else class="md-preview" :class="{ empty: isEmpty }">
      <div v-if="isEmpty" class="md-preview-empty">{{ t('editor.mdEmptyPreview') }}</div>
      <!-- eslint-disable-next-line vue/no-v-html — 输入为用户本人配置，已过轻量净化（见文件头） -->
      <div v-else class="md-body" v-html="rendered"></div>
    </div>
  </div>
</template>

<style scoped>
.md-field {
  width: 100%;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.md-toolbar {
  display: flex;
  justify-content: flex-end; /* 切换按钮恒居字段容器右上 */
}
.md-toggle {
  border: 1px solid var(--border);
  border-radius: 5px;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 11px;
  line-height: 1;
  padding: 4px 10px;
  cursor: pointer;
  white-space: nowrap;
}
.md-toggle:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.md-toggle:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

/* 编辑态：等宽字体（Markdown 源文本） */
.md-editor :deep(textarea) {
  font-family: ui-monospace, 'Cascadia Mono', Consolas, 'Courier New', monospace;
  font-size: 12px;
}

/* 预览态：块级排版（h/p/ul/code 的主题变量配色），高度与 5 行编辑框近似对齐 */
.md-preview {
  min-height: 96px;
  max-height: 260px;
  overflow-y: auto;
  border: 1px solid var(--border);
  border-radius: 7px;
  background: var(--bg-elevated);
  padding: 8px 12px;
}
.md-preview-empty {
  color: var(--text-4);
  font-size: 12px;
  font-style: italic;
}
.md-body {
  font-size: 12.5px;
  line-height: 1.6;
  color: var(--text-1);
  word-break: break-word;
}
.md-body :deep(h1),
.md-body :deep(h2),
.md-body :deep(h3),
.md-body :deep(h4) {
  margin: 0.5em 0 0.3em;
  color: var(--text-1);
  line-height: 1.3;
}
.md-body :deep(h1:first-child),
.md-body :deep(h2:first-child),
.md-body :deep(h3:first-child) {
  margin-top: 0;
}
.md-body :deep(p) {
  margin: 0.3em 0;
}
.md-body :deep(ul),
.md-body :deep(ol) {
  margin: 0.3em 0;
  padding-left: 1.4em;
}
.md-body :deep(code) {
  border: 1px solid var(--border);
  border-radius: 3px;
  background: var(--bg-hover);
  padding: 0 3px;
  font-family: ui-monospace, Consolas, monospace;
  font-size: 11.5px;
}
.md-body :deep(pre) {
  overflow-x: auto;
  border: 1px solid var(--border);
  border-radius: 5px;
  background: var(--bg-hover);
  padding: 6px 8px;
}
.md-body :deep(pre code) {
  border: none;
  background: transparent;
  padding: 0;
}
.md-body :deep(blockquote) {
  margin: 0.4em 0;
  border-left: 3px solid var(--border-strong);
  padding-left: 8px;
  color: var(--text-3);
}
.md-body :deep(a) {
  color: var(--accent-text);
}
.md-body :deep(img) {
  max-width: 100%;
}
.md-body :deep(table) {
  border-collapse: collapse;
}
.md-body :deep(th),
.md-body :deep(td) {
  border: 1px solid var(--border);
  padding: 2px 8px;
}
.md-body :deep(hr) {
  border: none;
  border-top: 1px solid var(--border);
  margin: 0.6em 0;
}
</style>
