<script setup>
/**
 * 关于分组（web 单列自适应 + 排版紧凑——内容 12 节零增删，仅布局压缩）：
 * 内容对齐 WPF AboutPageView.xaml（12 节零遗漏）——
 * hero（logo/应用名/标语/版本/构建日期）、Update 行（有新版本时显示新版本号链接 + 红点，
 * 破坏性更新在 tooltip 里标记）、Author 卡（头像 + Shawn(github) + 邮箱）、Support
 * （使用文档）、做出贡献（说明 + 三按钮）、包含组件（10 个链接照抄 WPF 列表）。
 * 紧凑布局：hero 与 Author 卡同行左右分布；Support 标题与文档链接同行；
 * 贡献按钮组保持横排；组件清单两列网格。克制风格：分组小标题 + 主题变量取色，
 * 不克隆 WPF 的三色按钮/双栏形态；词条与链接零增删。
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

// 贡献三按钮（WPF AboutPageView.xaml:199-236；文案键从 WPF 移植，视觉统一为克制 outline 风格）
const CONTRIBUTE = [
  {
    key: 'suggestions',
    label: 'about.giveSuggestions',
    url: 'https://github.com/1Remote/1Remote/issues/',
  },
  { key: 'coffee', label: 'about.buyCoffee', url: 'https://1remote.github.io/about/' },
  // ms-windows-store: 协议链接直接 href（任务约定；浏览器/WebView2 交由系统处理）
  {
    key: 'review',
    label: 'about.giveReview',
    url: 'ms-windows-store://review/?productid=9PNMNF92JNFP',
  },
]
</script>

<template>
  <div class="about">
    <!-- hero 行（紧凑布局）：logo+名称/标语/版本（左）与 Author 卡（右）同行左右分布 -->
    <div class="hero-row">
      <!-- hero：logo + 应用名 + 标语 + 版本/构建日期（版本徽章 + 日期弱化，tooltip 均为 BuildDate 全文） -->
      <div class="hero">
        <img class="hero-logo" src="/logo.png" width="72" height="72" alt="" />
        <div class="hero-body">
          <div class="hero-name">1Remote</div>
          <div class="hero-tagline">{{ t('about.tagline') }}</div>
          <div class="hero-meta">
            <span v-if="version" class="ver-badge">{{ version }}</span>
            <span v-else class="ver-badge soon">{{ t('common.comingSoon') }}</span>
            <span v-if="buildDateDisplay" class="hero-build" :title="buildDate">{{ buildDateDisplay }}</span>
          </div>
        </div>
      </div>

      <!-- Author 卡（WPF:201-233：头像 + Shawn(github) + 邮箱），紧随 hero 右侧 -->
      <div class="author-block">
        <h3 class="sec-title">{{ t('about.author') }}</h3>
        <div class="author-card">
          <img class="author-avatar" src="/author-avatar.jpg" width="36" height="36" alt="" />
          <div class="author-links">
            <a href="https://github.com/VShawn" target="_blank" rel="noreferrer noopener">Shawn</a>
            <a href="mailto:veckshawn@gmail.com?subject=1Remote">(veckshawn@gmail.com)</a>
          </div>
        </div>
      </div>
    </div>

    <!-- Update 行：无新版本时整行隐藏（WPF DataTrigger Text="" → Collapsed 同语义） -->
    <div v-if="updateAvailable" class="update-line">
      <span class="update-label">{{ t('about.update') }}</span>
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
    </div>

    <!-- Support（WPF:168-180：使用文档链接，文案 = WPF about_page_how_to_use 词条）：
         标题与链接同行-->
    <div class="sec-row">
      <h3 class="sec-title">{{ t('about.support') }}</h3>
      <a class="link-btn" href="https://1remote.github.io/usage/quick-start/" target="_blank" rel="noreferrer noopener">
        {{ t('about.howToUse') }}
      </a>
    </div>

    <!-- Make contributions（WPF:183-238：说明文案 + 三按钮横排） -->
    <h3 class="sec-title">{{ t('about.makeContributions') }}</h3>
    <p class="contribute-text">{{ t('about.contributeText') }}</p>
    <div class="contribute-actions">
      <a v-for="c in CONTRIBUTE" :key="c.key" class="link-btn" :href="c.url" target="_blank" rel="noreferrer noopener">
        {{ t(c.label) }}
      </a>
    </div>

    <!-- Included Components（WPF:241-293：10 个组件链接照抄；两列网格 #18） -->
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
  /* 统一设置内容宽（SettingsView.s-body 的 --settings-content-w 穿透继承） */
  width: min(100%, var(--settings-content-w));
}

/* ---- hero 行：hero（左）与 Author 块（右）同行左右分布；窄屏折行 ---- */
.hero-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 24px;
  flex-wrap: wrap;
  padding: 4px 0 16px;
}
.author-block {
  flex: 0 0 auto;
  margin-left: auto;
}
.author-block .sec-title {
  margin: 0 0 8px;
}

/* ---- hero：logo + 名称/标语/版本元信息 ---- */
.hero {
  display: flex;
  align-items: center;
  gap: 16px;
}
.hero-logo {
  flex: 0 0 auto;
  border-radius: var(--radius-box);
}
.hero-name {
  font-size: var(--fs-display);
  font-weight: 700;
  color: var(--text-1);
}
.hero-tagline {
  margin-top: 2px;
  font-size: var(--fs-body);
  color: var(--text-2);
}
.hero-meta {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 6px;
}
.ver-badge {
  display: inline-block;
  padding: 1px 8px;
  border: 1px solid var(--accent);
  border-radius: var(--radius-pill);
  color: var(--accent-text);
  font-size: var(--fs-caption);
  line-height: 1.5;
}
.ver-badge.soon {
  border-color: var(--border-strong);
  color: var(--text-4);
}
.hero-build {
  font-size: var(--fs-caption);
  color: var(--text-4);
}

/* ---- Update 行 ---- */
.update-line {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border: 1px solid var(--accent);
  border-radius: var(--radius-box);
  background: var(--accent-container);
  margin-bottom: 4px;
}
.update-label {
  flex: 0 0 auto;
  font-size: var(--fs-body);
  font-weight: 600;
  color: var(--accent-text);
}
.upd-link {
  position: relative; /* 红点（.dot）的定位基准 */
  color: var(--accent-text);
  font-size: var(--fs-body);
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
  background: var(--danger); /* 更新红点：--danger 主题化（V5，原 WPF Fill="Red" 遗产） */
}

/* ---- 分组小标题：克制样式（text-2 弱化 + 留白，不再克隆 WPF accent 色标题）；
       #18 紧凑化：上下留白收紧 ---- */
.sec-title {
  margin: 16px 0 8px;
  font-size: var(--fs-body);
  font-weight: 600;
  color: var(--text-3);
}
.about a:not(.link-btn) {
  color: var(--accent-text);
}

/* ---- Support 行：标题与文档链接同行横排 ---- */
.sec-row {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}
.sec-row .sec-title {
  margin: 16px 0 8px;
}
.sec-row .link-btn {
  margin-bottom: 8px; /* 与标题基线对齐（标题自带 8px 下留白） */
}

/* ---- Author 卡 ---- */
.author-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border: 1px solid var(--border);
  border-radius: var(--radius-box);
  background: var(--bg-elevated);
  width: fit-content;
}
.author-avatar {
  flex: 0 0 auto;
  border-radius: var(--radius-ctrl);
}
.author-links {
  display: flex;
  flex-direction: column;
  gap: 2px;
  font-size: var(--fs-body);
}

/* ---- Support / 贡献按钮：统一克制 outline 风格（替代 WPF 三色实心按钮） ---- */
.link-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: var(--ctrl-h-m);
  box-sizing: border-box; /* <a> 非 UA border-box，显式声明使 28 档含边框 */
  padding: 0 14px;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  color: var(--text-1);
  font-size: var(--fs-body);
  text-decoration: none;
  white-space: nowrap;
}
.link-btn:hover {
  border-color: var(--accent);
  background: var(--accent-container);
  color: var(--accent-text);
}
.contribute-text {
  margin: 0 0 10px;
  font-size: var(--fs-body);
  line-height: 1.5;
  color: var(--text-2);
  max-width: 56ch;
}
.contribute-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

/* ---- Included Components（自适应列网格：宽容器多列、960px 内约 2 列） ---- */
.components {
  margin: 0;
  padding: 0;
  list-style: none;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 3px 28px;
}
.components li {
  padding: 3px 0;
}
.components a {
  font-size: var(--fs-body);
  color: var(--accent-text);
  text-decoration: none;
  overflow-wrap: anywhere;
}
.components a:hover {
  text-decoration: underline;
}
</style>
