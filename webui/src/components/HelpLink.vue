<script setup>
/**
 * 帮助元素统一形态（对齐 WPF 的帮助链接系统）。
 * WPF 侧对应物：XAML 里的 Hyperlink——外链形态（NavigateUri + IsOpenExternal）与
 * "?" / "(i)" 徽记两种用法；web 统一收敛为本组件：
 * - href：外链 <a>（target=_blank rel="noreferrer noopener"，URL 照抄 WPF NavigateUri）；
 * - tip：无 URL 的说明性提示 → ⓘ 徽标 + title 悬停（纯提示，无导航）——根元素用
 *   <button type="button">（键盘可达：可 Tab 聚焦，焦点圈见 :focus-visible）；
 * - 徽标字符默认按用途推断（href→"?"，tip→"i"），badge="" 关闭徽标只留文字（默认插槽），
 *   承载 WPF 的文字型链接（"Can't find your language?" / "[More details]"）——
 *   即 badge 实际取值仅 ''/?/i（自定义字符兜底已随 G32 清理删除）。
 *
 * 徽标形态视觉（owner 反馈"(?) 不显眼、圆圈包不住问号"后的重做）：
 * - 18×18 圆钮（原 15×15 容器 + 10px 字体字符——字形比例失调）：改内绘 SVG（viewBox
 *   24），1.5 描边圆圈 + 笔画式 ? / i 字形——字形与容器的比例由路径决定，不依赖字体
 *   度量，任意缩放不失衡；
 * - 常态即清晰可辨：描边/字形用 --text-2（比原先 --text-3 提一档）；
 * - hover 强反馈：accent 实心圆底 + 反白字形 + scale(1.15) 微放大（120ms 过渡）；
 * - 行内不跳动：inline-flex + vertical-align:middle，18px 徽标在各落点行内
 *   （表单行 28px 控件高 / 标题行 / 工具栏）均低于宿主行高，不撑行；
 * - 键盘可达：href 形态 <a> 天然可聚焦；无文字形态的可访问名 aria-label = tip，
 *   缺省回落 common.help（"Help/帮助"——徽标字符对读屏无意义）。
 * 文字链形态（badge=""）维持：hover accent + 下划线，与徽标 hover 同一帮助色语言。
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({
  href: { type: String, default: '' },
  tip: { type: String, default: '' },
  /** 徽标字符：缺省按用途推断（href→? / tip→i）；空串 = 纯文字形态（有效值仅 ''/?/i） */
  badge: { type: String, default: undefined },
})

const { t } = useI18n()

const badgeChar = computed(() => {
  if (props.badge !== undefined) return props.badge
  if (props.href) return '?'
  if (props.tip) return 'i'
  return ''
})
// 无文字时的可访问名：徽标字符（?/i）读不出目的，用 tip 补齐，缺省回落通用"帮助"
const ariaLabel = computed(() => props.tip || t('common.help'))
</script>

<template>
  <a
    v-if="href"
    class="help-link"
    :href="href"
    target="_blank"
    rel="noreferrer noopener"
    :title="tip || undefined"
    :aria-label="$slots.default ? undefined : ariaLabel"
  >
    <span v-if="badgeChar" class="hl-badge" aria-hidden="true">
      <!-- "?" / "i" 用 SVG 笔画绘制（比例可控）。G32 清理：自定义字符字体兜底分支
           （hl-badge-char）全库无调用方已删——badge 实际取值只有 ''/?/i 三种 -->
      <svg viewBox="0 0 24 24" width="18" height="18">
        <circle class="hl-ring" cx="12" cy="12" r="9.25"></circle>
        <path
          v-if="badgeChar === '?'"
          class="hl-glyph"
          d="M8.8 8.9a3.2 3.2 0 1 1 4.8 2.6c-.9.55-1.6 1.1-1.6 2.3"
        ></path>
        <path v-else class="hl-glyph" d="M12 7.7v5.5"></path>
        <circle v-if="badgeChar === '?'" class="hl-dot" cx="12" cy="17" r="0.95"></circle>
        <circle v-else class="hl-dot" cx="12" cy="16.8" r="0.95"></circle>
      </svg>
    </span>
    <span v-if="$slots.default" class="hl-text"><slot></slot></span>
  </a>
  <button v-else class="help-link" type="button" :title="tip || undefined" :aria-label="ariaLabel">
    <span v-if="badgeChar" class="hl-badge" aria-hidden="true">
      <svg viewBox="0 0 24 24" width="18" height="18">
        <circle class="hl-ring" cx="12" cy="12" r="9.25"></circle>
        <path
          v-if="badgeChar === '?'"
          class="hl-glyph"
          d="M8.8 8.9a3.2 3.2 0 1 1 4.8 2.6c-.9.55-1.6 1.1-1.6 2.3"
        ></path>
        <path v-else class="hl-glyph" d="M12 7.7v5.5"></path>
        <circle v-if="badgeChar === '?'" class="hl-dot" cx="12" cy="17" r="0.95"></circle>
        <circle v-else class="hl-dot" cx="12" cy="16.8" r="0.95"></circle>
      </svg>
    </span>
    <span v-if="$slots.default" class="hl-text"><slot></slot></span>
  </button>
</template>

<style scoped>
.help-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1.4;
  text-decoration: none;
  cursor: pointer;
  vertical-align: middle;
  /* button 根元素（tip 形态）的表单控件缺省样式归零 */
  padding: 0;
  border: none;
  background: transparent;
  font-family: inherit;
}
.help-link:hover {
  color: var(--accent-text);
}
/* 键盘焦点圈（鼠标点击不出现）：与主题 accent 一致（light 基底自动取深变体） */
.help-link:focus-visible {
  outline: 2px solid var(--accent-focus);
  outline-offset: 2px;
  border-radius: var(--radius-xs);
}
/* 有文字的形态：hover 才下划线（WPF Hyperlink 的 IsMouseOver→Underline 同款） */
.help-link:hover .hl-text {
  text-decoration: underline;
}
/* 徽标形态：18×18 圆钮（SVG 内绘，见文件头）——与周围表单控件拉开视觉身份（帮助，
   而非操作）。常态 --text-2 描边可辨；hover accent 实心底 + 反白字形 + 微放大 */
.hl-badge {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--text-2);
  border-radius: 50%;
  transition:
    transform var(--dur-fast) ease,
    background-color var(--dur-fast) ease,
    color var(--dur-fast) ease;
}
.hl-badge svg {
  display: block;
}
.hl-ring {
  fill: none;
  stroke: currentColor;
  /* viewBox 24 渲染为 18px：2 单位 = 实际 1.5px 描边（owner 要求的圆圈线宽） */
  stroke-width: 2;
}
.hl-glyph {
  fill: none;
  stroke: currentColor;
  /* 同上比例：2.4 单位 = 实际 1.8px（字形略粗于圆圈，视觉重量平衡） */
  stroke-width: 2.4;
  stroke-linecap: round;
}
.hl-dot {
  fill: currentColor;
}
.help-link:hover .hl-badge {
  color: var(--text-on-accent);
  background: var(--accent-solid);
  transform: scale(1.15);
}
</style>
