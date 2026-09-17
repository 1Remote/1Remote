<script setup>
/**
 * 关于分组（fix batch6 Task D #7）：内容对齐 WPF AboutPageView.xaml（不漏项）——
 * logo/应用名/标语、版本 + 构建日期、Update 行（有新版本时显示新版本号链接 + 红点，
 * 同 WPF Hyperlink 红点形态；破坏性更新在 tooltip 里标记）、作者、支持（使用文档）、
 * 做出贡献（说明 + 三按钮）、包含组件（10 个链接照抄 WPF 列表）。
 * 语言选择行已删除——常规组（GeneralGroup）已有语言下拉，此处不再重复。
 * 纯技术标签（Author/Support/Make contributions/Included Components/Update/Version/标语）
 * 与 WPF 一致为硬编码英文（locale 中 14 语言同值）；howToUse/贡献说明/三按钮文案
 * 从 WPF 14 语言词条移植（见 scripts/convert-locales.mjs MAPPING）。
 * 数据：useVersionInfo 各自拉取 /api/version（端点轻量读静态缓存，见组合式函数头注释）。
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVersionInfo } from '../../composables/useVersionInfo'

const { t } = useI18n()
const { version, buildDate, update } = useVersionInfo()

// WPF AboutPageViewModel.CurrentVersion(:98) 语义：构建日期去掉时区后缀（"2024-01-01T00:00:00+08:00"
// → "2024-01-01T00:00:00"）作显示值，完整原文进 tooltip。CI 未注入时为空串 → 整行隐藏（dev 形态）。
const buildDateDisplay = computed(() => {
  const s = buildDate.value
  const i = s.indexOf('+')
  return i > 0 ? s.slice(0, i) : s
})

const updateAvailable = computed(() => update.value?.available === true)

// WPF AboutPageView.xaml:243-292 的组件清单（照抄，含顺序）
const COMPONENTS = [
  'https://www.chiark.greenend.org.uk/~sgtatham/putty/',
  'https://github.com/ButchersBoy/Dragablz',
  'https://github.com/drogoganor/ColorPickerWPF',
  'https://github.com/VShawn/VariableKeywordMatcher',
  'https://github.com/praeclarum/sqlite-net',
  'https://github.com/AlexAkulov/putty-color-themes',
  'https://github.com/xiangyuecn/RSA-csharp',
  'https://github.com/robinrodricks/FluentFTP',
  'https://github.com/sshnet/SSH.NET',
  'https://github.com/icsharpcode/AvalonEdit',
]

// 贡献三按钮（WPF AboutPageView.xaml:199-236：红/绿/蓝三色链接按钮；文案键从 WPF 移植）
const CONTRIBUTE = [
  {
    key: 'suggestions',
    cls: 'c-suggest',
    label: 'about.giveSuggestions',
    url: 'https://github.com/1Remote/1Remote/issues/',
  },
  { key: 'coffee', cls: 'c-coffee', label: 'about.buyCoffee', url: 'https://1remote.github.io/about/' },
  // ms-windows-store: 协议链接直接 href（任务约定；浏览器/WebView2 交由系统处理）
  {
    key: 'review',
    cls: 'c-review',
    label: 'about.giveReview',
    url: 'ms-windows-store://review/?productid=9PNMNF92JNFP',
  },
]
</script>

<template>
  <div class="about">
    <!-- 顶部：logo + 应用名 + 标语（WPF 左栏顶部块；应用名与 App.vue 顶栏一致硬编码 1Remote） -->
    <div class="hero">
      <img class="hero-logo" src="/logo.png" width="96" height="96" alt="" />
      <div class="hero-text">
        <div class="hero-name">1Remote</div>
        <div class="hero-tagline">{{ t('about.tagline') }}</div>
      </div>
    </div>

    <!-- 版本 + 构建日期（WPF: Version 行 + CurrentVersionDate 行，tooltip 均为 BuildDate 全文） -->
    <div class="row">
      <span class="row-label">{{ t('about.version') }}</span>
      <span class="row-value">{{ version || t('common.comingSoon') }}</span>
    </div>
    <div v-if="buildDateDisplay" class="row">
      <span class="row-label"></span>
      <span class="row-value hint" :title="buildDate">{{ buildDateDisplay }}</span>
    </div>

    <!-- Update 行：无新版本时整行隐藏（WPF DataTrigger Text="" → Collapsed 同语义） -->
    <div v-if="updateAvailable" class="row">
      <span class="row-label">{{ t('about.update') }}</span>
      <span class="row-value">
        <a
          class="upd-link"
          :href="update.newVersionUrl || '#'"
          target="_blank"
          rel="noreferrer noopener"
          :title="update.breaking ? t('about.update') + ' (breaking change!)' : t('about.update')"
        >
          {{ update.newVersion }}
          <!-- 红点：同 WPF Hyperlink 右上角红点形态（破坏性更新标记见上行 tooltip） -->
          <span class="dot"></span>
        </a>
      </span>
    </div>

    <!-- Author（WPF:201-233：头像 + Shawn(github) + 邮箱） -->
    <h3 class="sec-title">{{ t('about.author') }}</h3>
    <div class="author">
      <img class="author-avatar" src="/author-avatar.jpg" width="32" height="32" alt="" />
      <div class="author-links">
        <a href="https://github.com/VShawn" target="_blank" rel="noreferrer noopener">Shawn</a>
        <a href="mailto:veckshawn@gmail.com?subject=1Remote">(veckshawn@gmail.com)</a>
      </div>
    </div>

    <!-- Support（WPF:168-180：使用文档链接，文案 = WPF about_page_how_to_use 词条） -->
    <h3 class="sec-title">{{ t('about.support') }}</h3>
    <a href="https://1remote.github.io/usage/quick-start/" target="_blank" rel="noreferrer noopener">
      {{ t('about.howToUse') }}
    </a>

    <!-- Make contributions（WPF:183-238：说明文案 + 三色按钮） -->
    <h3 class="sec-title">{{ t('about.makeContributions') }}</h3>
    <p class="contribute-text">{{ t('about.contributeText') }}</p>
    <div class="contribute-actions">
      <a
        v-for="c in CONTRIBUTE"
        :key="c.key"
        :class="['c-btn', c.cls]"
        :href="c.url"
        target="_blank"
        rel="noreferrer noopener"
      >
        {{ t(c.label) }}
      </a>
    </div>

    <!-- Included Components（WPF:241-293：10 个组件链接照抄） -->
    <h3 class="sec-title">{{ t('about.includedComponents') }}</h3>
    <ul class="components">
      <li v-for="u in COMPONENTS" :key="u">
        <a :href="u" target="_blank" rel="noreferrer noopener">{{ u }}</a>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.about {
  max-width: 640px;
}
.hero {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 8px 0 16px;
}
.hero-logo {
  flex: 0 0 auto;
}
.hero-name {
  font-size: 1.3846rem;
  font-weight: 700;
  color: var(--text-1);
}
.hero-tagline {
  margin-top: 2px;
  font-size: 0.9615rem;
  color: var(--text-2);
}
.row {
  display: grid;
  grid-template-columns: 120px minmax(0, 1fr);
  gap: 6px 12px;
  align-items: center;
  padding: 4px 0;
}
.row-label {
  font-size: 0.9615rem;
  color: var(--text-2);
}
.row-value {
  font-size: 0.9615rem;
  color: var(--text-1);
}
.row-value.hint {
  color: var(--text-4);
}
/* Update 行链接：占位 underline 去除、hover 出现（对齐 WPF Hyperlink 样式触发器） */
.upd-link {
  position: relative; /* 红点（.dot）的定位基准 */
  color: var(--accent-text);
  text-decoration: none;
}
.upd-link:hover {
  text-decoration: underline;
}
.dot {
  position: absolute;
  top: -4px;
  right: -11px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: red; /* WPF Path Fill="Red" 同值 */
}
.sec-title {
  margin: 18px 0 8px;
  font-size: 1.0769rem;
  font-weight: 600;
  color: var(--accent-text); /* 对齐 WPF EditorGroupTextBlockTitle 的 accent 色小节标题 */
}
/* 通用链接配色排除贡献按钮（.c-btn 白字）——否则 .about a 的类+元素优先级会盖掉按钮白字 */
.about a:not(.c-btn) {
  color: var(--accent-text);
}
.author {
  display: flex;
  align-items: center;
  gap: 10px;
}
.author-avatar {
  flex: 0 0 auto;
  border-radius: 4px;
}
.author-links {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.contribute-text {
  margin: 0 0 12px;
  font-size: 0.9615rem;
  line-height: 1.5;
  color: var(--text-2);
  opacity: 0.85; /* WPF Opacity 0.7 的克制近似 */
}
.contribute-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
/* 三色贡献按钮：WPF Border 背景 #e53e3e/#2f855a/#3182ce + 白字圆角（WPF:199/212/225） */
.c-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 130px; /* WPF MinWidth=130 */
  padding: 7px 14px;
  border-radius: 4px;
  color: #fff;
  font-size: 0.9615rem;
  font-weight: 600;
  text-decoration: none;
}
.c-btn:hover {
  opacity: 0.85; /* WPF IsMouseOver Opacity 0.6 的克制近似 */
}
.c-suggest {
  background: #e53e3e;
}
.c-coffee {
  background: #2f855a;
}
.c-review {
  background: #3182ce;
}
.components {
  margin: 0;
  padding: 0;
  list-style: none;
}
.components li {
  padding: 3px 0;
}
.components a {
  font-size: 0.9615rem;
  text-decoration: none;
}
.components a:hover {
  text-decoration: underline;
}
</style>
