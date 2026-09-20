<script setup>
/**
 * 外观分组（Plan 3 Task 4，spec §4/§6）：迁移 themes 模块的主题切换 UI 为正式入口
 * （此前靠 window.__theme 调试钩子，本任务已移除）。
 * - 基底三卡（dark/light/system 跟随系统）带迷你预览；
 * - 强调色圆点（ACCENTS 7 色，与 theme.css data-accent 一一对应）；
 * - 字号 S/M/L/XL 分段（点击即整站 font-size 生效）；
 * - 经典主题 9 预设 pills（CLASSIC_THEMES → setAppearance(themeMode+accent)）。
 * 全部经 setAppearance：本地 reactive 即时生效 + PUT 持久化（后端不可达时静默，纯预览可用）。
 * 界面字体选择器（themeState.font）WPF 侧依赖系统字体枚举，归后续任务，此处不暴露。
 */
import { useI18n } from 'vue-i18n'
import { ACCENTS, ACCENT_HEX, CLASSIC_THEMES, setAppearance, themeState } from '../../themes'

const { t } = useI18n()

const BASES = ['dark', 'light', 'system']
const FONT_SIZES = ['S', 'M', 'L', 'XL']
const CLASSIC_NAMES = Object.keys(CLASSIC_THEMES)

// 迷你预览的"当前选中态"高亮：文字色用 text-2，选中边框用 accent
function setBase(mode) {
  setAppearance({ themeMode: mode })
}
function setAccent(a) {
  setAppearance({ accent: a })
}
function setFontSize(s) {
  setAppearance({ fontSize: s })
}
function applyClassic(name) {
  setAppearance(CLASSIC_THEMES[name])
}
</script>

<template>
  <div class="group">
    <!-- 基底三卡 -->
    <h4 class="sub">{{ t('settings.appearance.base') }}</h4>
    <div class="bases">
      <button
        v-for="b in BASES"
        :key="b"
        type="button"
        class="base-card"
        :class="{ active: themeState.themeMode === b }"
        @click="setBase(b)"
      >
        <!-- 迷你预览：system 卡按 systemDark 解析后的实际基底着色 -->
        <span
          class="mini"
          :data-theme="b === 'system' ? (themeState.systemDark ? 'dark' : 'light') : b"
          :data-accent="themeState.accent"
        >
          <span class="mini-bar"></span>
          <span class="mini-body">
            <span class="mini-line"></span>
            <span class="mini-line short"></span>
          </span>
        </span>
        <span class="base-name">{{ t('settings.appearance.base.' + b) }}</span>
      </button>
    </div>

    <!-- 强调色圆点 -->
    <h4 class="sub">{{ t('settings.appearance.accent') }}</h4>
    <div class="dots">
      <button
        v-for="a in ACCENTS"
        :key="a"
        type="button"
        class="dot"
        :class="{ active: themeState.accent === a }"
        :style="{ background: ACCENT_HEX[a] }"
        :title="a"
        @click="setAccent(a)"
      ></button>
    </div>

    <!-- 字号分段 -->
    <h4 class="sub">{{ t('settings.appearance.fontSize') }}</h4>
    <div class="seg">
      <button
        v-for="s in FONT_SIZES"
        :key="s"
        type="button"
        class="seg-btn"
        :class="{ active: themeState.fontSize === s }"
        @click="setFontSize(s)"
      >
        {{ s }}
      </button>
    </div>

    <!-- 经典主题预设 -->
    <h4 class="sub">{{ t('settings.appearance.classic') }}</h4>
    <div class="pills">
      <button
        v-for="name in CLASSIC_NAMES"
        :key="name"
        type="button"
        class="pill"
        :class="{
          active:
            themeState.themeMode === CLASSIC_THEMES[name].themeMode &&
            themeState.accent === CLASSIC_THEMES[name].accent,
        }"
        @click="applyClassic(name)"
      >
        {{ name }}
      </button>
    </div>
    <p class="hint">{{ t('settings.appearance.classicHint') }}</p>
  </div>
</template>

<style scoped>
.group {
  /* 统一设置内容宽（SettingsView.s-body 的 --settings-content-w 穿透继承）；
     内部网格（三卡/圆点/分段/pills）不动 */
  width: min(100%, var(--settings-content-w));
}
.sub {
  margin: 16px 0 8px;
  font-size: var(--fs-body);
  font-weight: 600;
  color: var(--text-3);
}
.sub:first-child {
  margin-top: 0;
}
.hint {
  margin: 8px 0 0;
  font-size: var(--fs-body);
  color: var(--text-4);
}

/* 基底三卡 */
.bases {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}
.base-card {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px;
  border: 1px solid var(--border);
  border-radius: var(--radius-box);
  background: var(--bg-panel);
  cursor: pointer;
}
.base-card:hover {
  border-color: var(--border-strong);
}
.base-card.active {
  border-color: var(--accent);
  background: var(--accent-container);
}
.base-name {
  font-size: var(--fs-body);
  color: var(--text-2);
}
.base-card.active .base-name {
  color: var(--accent-text);
}
/* 迷你预览：贴 data-theme/data-accent 让 CSS 变量按预览基底解析（theme.css 的变量表复用） */
.mini {
  display: flex;
  width: 120px;
  height: 64px;
  border-radius: var(--radius-ctrl);
  overflow: hidden;
}
.mini[data-theme='dark'] {
  background: #0f1011;
}
.mini[data-theme='light'] {
  background: #fafafa;
}
.mini-bar {
  flex: 0 0 22px;
  background: var(--accent);
}
.mini-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 5px;
  padding: 0 10px;
}
.mini-line {
  height: 5px;
  border-radius: 2px;
  background: var(--text-3);
}
.mini[data-theme='dark'] .mini-line {
  background: #565a63;
}
.mini[data-theme='light'] .mini-line {
  background: #a1a1aa;
}
.mini-line.short {
  width: 55%;
}

/* 强调色圆点 */
.dots {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}
.dot {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  border: 2px solid transparent;
  padding: 0;
  cursor: pointer;
}
.dot:hover {
  outline: 1px solid var(--border-strong);
  outline-offset: 2px;
}
.dot.active {
  border-color: var(--bg);
  outline: 2px solid var(--text-2);
  outline-offset: 1px;
}

/* 字号分段：与 EditorDrawer .ed-seg 同源参数（容器 28 档 border-box + 未选 bg-elevated/
   text-3 + hover 提 text-1；此前 transparent 底/text-2/hover 仅加底三处漂移） */
.seg {
  display: inline-flex;
  height: var(--ctrl-h-m);
  box-sizing: border-box;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  overflow: hidden;
}
.seg-btn {
  min-width: 44px;
  border: none;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 0 14px;
  cursor: pointer;
}
.seg-btn + .seg-btn {
  border-left: 1px solid var(--border);
}
.seg-btn:hover:not(.active) {
  background: var(--bg-hover);
  color: var(--text-1);
}
.seg-btn.active {
  background: var(--accent-container);
  color: var(--accent-text);
}

/* 经典主题 pills */
.pills {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.pill {
  height: var(--ctrl-h-m);
  padding: 0 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  background: var(--bg-panel);
  color: var(--text-2);
  font-size: var(--fs-body);
  cursor: pointer;
}
.pill:hover {
  border-color: var(--border-strong);
  color: var(--text-1);
}
.pill.active {
  border-color: var(--accent);
  background: var(--accent-container);
  color: var(--accent-text);
}
</style>
