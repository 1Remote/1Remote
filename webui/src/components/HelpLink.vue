<script setup>
/**
 * 帮助元素统一形态（batch8 Task F #20：WPF 帮助链接系统补齐）。
 * WPF 侧对应物：XAML 里的 Hyperlink——外链形态（NavigateUri + IsOpenExternal）与
 * "?" / "(i)" 徽记两种用法；web 统一收敛为本组件：
 * - href：外链（target=_blank rel="noreferrer noopener"，URL 照抄 WPF NavigateUri）；
 * - tip：无 URL 的说明性提示 → ⓘ 徽标 + title 悬停（纯提示，无导航）；
 * - 徽标字符默认按用途推断（href→"?"，tip→"i"），badge="" 关闭徽标只留文字（默认插槽），
 *   承载 WPF 的文字型链接（"Can't find your language?" / "[More details]"）。
 * 视觉：--text-3 常态、hover accent（一眼可辨的帮助色语言，不与正文操作按钮混淆）。
 */
import { computed } from 'vue'

const props = defineProps({
  href: { type: String, default: '' },
  tip: { type: String, default: '' },
  /** 徽标字符：缺省按用途推断（href→? / tip→i）；空串 = 纯文字形态 */
  badge: { type: String, default: undefined },
})

const badgeChar = computed(() => {
  if (props.badge !== undefined) return props.badge
  if (props.href) return '?'
  if (props.tip) return 'i'
  return ''
})
// 无文字时的可访问名：徽标本身读不出目的，用 tip 补齐
const ariaLabel = computed(() => props.tip || undefined)
</script>

<template>
  <a
    v-if="href"
    class="help-link"
    :href="href"
    target="_blank"
    rel="noreferrer noopener"
    :title="tip || undefined"
    :aria-label="ariaLabel"
  >
    <span v-if="badgeChar" class="hl-badge" aria-hidden="true">{{ badgeChar }}</span>
    <span v-if="$slots.default" class="hl-text"><slot></slot></span>
  </a>
  <span v-else class="help-link" :title="tip || undefined">
    <span v-if="badgeChar" class="hl-badge" aria-hidden="true">{{ badgeChar }}</span>
    <span v-if="$slots.default" class="hl-text"><slot></slot></span>
  </span>
</template>

<style scoped>
.help-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--text-3);
  font-size: 0.8462rem;
  line-height: 1.4;
  text-decoration: none;
  cursor: pointer;
  vertical-align: middle;
}
.help-link:hover {
  color: var(--accent-text);
}
/* 有文字的形态：hover 才下划线（WPF Hyperlink 的 IsMouseOver→Underline 同款） */
.help-link:hover .hl-text {
  text-decoration: underline;
}
/* 徽标形态：小圆圈 ? / i —— 与周围表单控件拉开视觉身份（帮助，而非操作） */
.hl-badge {
  flex: 0 0 auto;
  width: 15px;
  height: 15px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--border-strong);
  border-radius: 50%;
  font-size: 10px;
  font-weight: 600;
  line-height: 1;
  font-style: italic;
  user-select: none;
}
.help-link:hover .hl-badge {
  border-color: var(--accent);
}
</style>
