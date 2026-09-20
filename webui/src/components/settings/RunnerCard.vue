<script setup>
/**
 * 运行器卡片（自 RunnerGroup.vue 拆出）：单张 runner 卡 = 头行（名称/内外徽标/删除）+
 * 按形态渲染配置位——内置（PuTTY/KiTTY 按字段存在性探针渲染主题/字体/字号/字符集；
 * ExePath 只读——内置运行器随应用分发，路径由应用管理）或外部（ExePath+浏览、启动参数、
 * 环境变量/特殊字符行文本、RunWithHosting）。字段写值经 emit 上抛，保存节流（立即/debounce）
 * 与自动保存归 RunnerGroup；本组件无自有状态。
 * - 宏 chips：当前协议宏（GET 下发 [{name,description}]）渲染为可点胶囊（title=description），
 *   点击插入 textarea 光标位置（selectionStart 前插 + 光标移到宏尾 + 聚焦）。
 * - 主题/字体/字符集选项域来自 props.meta（后端同源 PuttyThemes.Themes /
 *   Fonts.SystemFontFamilies / PuttyRunner.CodePages），前端不硬编码。
 * - 环境变量/特殊字符以行文本 props 传入、改动 emit 回传（数组↔行文本的换算归 RunnerGroup）。
 */
import { computed, h, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingsEsc } from '../../composables/useSettingsEsc'
import HelpLink from '../HelpLink.vue'
import {
  hasArgsPrivateKey,
  hasCharset,
  hasExePath,
  hasFont,
  hasFontSize,
  hasInternalConfig,
  hasTheme,
  isExternal,
} from '../../editor/runnerPresets.js'

const props = defineProps({
  /** runner 对象（PascalCase 直通域，父列表内元素——写值直接落原对象） */
  runner: { type: Object, required: true },
  /** 当前协议的宏清单 [{name,description}]（启动参数宏 chips） */
  macros: { type: Array, default: () => [] },
  /** GET 下发的选项域 meta：{ puttyThemes, fonts, codePages }（缺省=空选项，值仍可保存） */
  meta: { type: Object, default: null },
  /** 自动保存进行中（开关 loading 态） */
  saving: { type: Boolean, default: false },
  /** exe 原生文件选择器在途（浏览按钮禁用，全局单飞） */
  browsing: { type: Boolean, default: false },
  /** 环境变量行文本（'KEY=VALUE' 每行一条） */
  envText: { type: String, default: '' },
  /** 特殊字符行文本（同上） */
  specialText: { type: String, default: '' },
})
const emit = defineEmits([
  'exe-path', // (value) ExePath 手输变化（父触发预设自动填充 + debounce 保存）
  'browse', // () 浏览按钮：父弹原生文件选择器
  'arg', // (field, value) Arguments / ArgumentsForPrivateKey 变化
  'env-text', // (value) 环境变量行文本变化
  'special-text', // (value) 特殊字符行文本变化
  'hosting', // (value) RunWithHosting 开关
  'select-field', // (key, value) 内置运行器下拉位（PuttyThemeName/PuttyFont/LineCodePage）
  'font-size', // (n) 字号（已校验为 ≥1 整数）
  'delete', // () 删除按钮（仅外部运行器渲染）
])
const { t } = useI18n()

// 下拉展开计数（SettingsView 的 Esc 返回链序，见 SettingsView/useSettingsEsc 文件头注释）
const { shield } = useSettingsEsc()

// ---- 选项域（meta 提供；缺 meta（旧后端/请求失败）时下拉为空，值仍可显示与保存）----
const themeOptions = computed(() =>
  (props.meta?.puttyThemes || []).map((x) => ({ value: x.name, label: x.name, theme: x }))
)
const fontOptions = computed(() => (props.meta?.fonts || []).map((f) => ({ value: f, label: f })))
const codePageOptions = computed(() => (props.meta?.codePages || []).map((c) => ({ value: c, label: c })))

function themeColors(name) {
  return (props.meta?.puttyThemes || []).find((x) => x.name === name)?.colors || null
}

// 主题下拉选项：色点行（bg/fg/绿/红 四点，与预览条同键位）——WPF 无对应物，web 简化预览
function renderThemeLabel(option) {
  const c = option.theme?.colors || {}
  const dots = [c.bg || '#000', c.fg || '#bbb', c.green || '#55ff55', c.red || '#ff5555']
  return h('span', { class: 'theme-opt' }, [
    ...dots.map((d) => h('i', { class: 'theme-dot', style: { background: d } })),
    h('span', null, option.label),
  ])
}

// ---- 宏 chips：点击插入光标位置（textarea selectionStart 前插、光标移宏尾、聚焦） ----
// n-input 组件实例按字段名收集（v-for 函数 ref）；$el 是外层 div，textarea 在其内。
// 取不到 DOM（理论不可达）时退化为尾部追加。
const argAreaRefs = {}
function setArgRef(comp, key) {
  if (comp) argAreaRefs[key] = comp
  else delete argAreaRefs[key]
}
function insertMacro(field, macro) {
  const cur = String(props.runner[field] ?? '')
  const comp = argAreaRefs[field]
  const el = comp?.$el?.querySelector?.('textarea')
  if (!el) {
    emit('arg', field, cur + macro.name)
    return
  }
  const s = el.selectionStart ?? cur.length
  const e = el.selectionEnd ?? s
  emit('arg', field, cur.slice(0, s) + macro.name + cur.slice(e))
  nextTick(() => {
    el.focus()
    const pos = s + macro.name.length
    el.setSelectionRange(pos, pos)
  })
}

// 字号（数字输入）：合法值才上抛；清空/null 跳过（保持原值，不发送非法）
function onFontSize(v) {
  const n = Number(v)
  if (!Number.isFinite(n) || n < 1) return
  emit('font-size', Math.round(n))
}
</script>

<template>
  <div class="r-card">
    <div class="r-head">
      <span class="r-name" :title="runner.Name">{{ runner.Name }}</span>
      <span
        class="r-badge"
        :class="{ ext: isExternal(runner) }"
        :title="isExternal(runner) ? '' : t('settings.r.internalNoDelete')"
      >
        {{ isExternal(runner) ? t('settings.r.external') : t('settings.r.internal') }}
      </span>
      <!-- 删除：仅外部运行器；内置无删除钮（徽标 title 提示不可删） -->
      <button
        v-if="isExternal(runner)"
        class="del"
        type="button"
        :title="t('settings.r.deleteTitle')"
        @click="emit('delete')"
      >
        ×
      </button>
    </div>

    <!-- 内置运行器：按字段存在性渲染配置位（PuTTY/KiTTY），无可配置位则只读说明 -->
    <template v-if="!isExternal(runner)">
      <template v-if="hasInternalConfig(runner)">
        <!-- 内置运行器 ExePath 只读：路径由应用管理（随应用分发/部署），WPF 侧可改是
             便携部署的历史遗留，web 端收为只读展示（title 说明悬停可见） -->
        <div v-if="hasExePath(runner)" class="f-row exe-readonly" :title="t('settings.r.internalExeManaged')">
          <label>{{ t('editor.f.ExePath') }}</label>
          <n-input size="small" :value="runner.ExePath" disabled :input-props="{ spellcheck: false }" />
        </div>
        <div v-if="hasTheme(runner)" class="f-row">
          <label>{{ t('settings.r.f.theme') }}</label>
          <div class="theme-wrap">
            <div class="theme-select-row">
              <n-select
                size="small"
                :value="runner.PuttyThemeName"
                :options="themeOptions"
                :render-label="renderThemeLabel"
                @update:show="shield"
                @update:value="emit('select-field', 'PuttyThemeName', $event)"
              />
              <!-- 主题行 (?)：WPF PuttyRunnerSettings.xaml:101-105 / KittyRunnerSettings
                   :88 → 主题站（url 照抄） -->
              <HelpLink href="https://putty.org.ru/themes/" />
            </div>
            <!-- 主题预览：与 WPF 预览同键位的色块文本行（Colour2 底 / Colour11·15·9·0 前景） -->
            <div
              v-if="themeColors(runner.PuttyThemeName)"
              class="theme-preview"
              :style="{ background: themeColors(runner.PuttyThemeName).bg || '#000' }"
            >
              <span :style="{ color: themeColors(runner.PuttyThemeName).green || '#55ff55' }">1Remote</span>
              <span :style="{ color: themeColors(runner.PuttyThemeName).white || '#ffffff' }">version.cpp</span>
              <span :style="{ color: themeColors(runner.PuttyThemeName).red || '#ff5555' }">data.zip</span>
              <span :style="{ color: themeColors(runner.PuttyThemeName).fg || '#bbbbbb' }">root@remote:~$</span>
            </div>
          </div>
        </div>
        <div v-if="hasFont(runner)" class="f-row">
          <label>{{ t('settings.r.f.font') }}</label>
          <n-select
            size="small"
            filterable
            :value="runner.PuttyFont"
            :options="fontOptions"
            @update:show="shield"
            @update:value="emit('select-field', 'PuttyFont', $event)"
          />
        </div>
        <div v-if="hasFontSize(runner)" class="f-row">
          <label>{{ t('settings.r.f.fontSize') }}</label>
          <n-input-number
            class="num-input"
            size="small"
            :min="1"
            :value="runner.PuttyFontSize"
            :show-button="false"
            @update:value="onFontSize"
          />
        </div>
        <div v-if="hasCharset(runner)" class="f-row">
          <label>{{ t('settings.r.f.charset') }}</label>
          <div class="charset-wrap">
            <n-select
              size="small"
              filterable
              :value="runner.LineCodePage"
              :options="codePageOptions"
              @update:show="shield"
              @update:value="emit('select-field', 'LineCodePage', $event)"
            />
            <!-- 字符集行 (?)：WPF Putty/KittyRunnerSettings Character set 行 (?)
                 ——WPF 该链接同样指向主题站 URL（原样照抄，不代为修正） -->
            <HelpLink href="https://putty.org.ru/themes/" />
          </div>
        </div>
      </template>
      <p v-else class="r-internal-hint">{{ t('settings.r.internalHint') }}</p>
    </template>

    <!-- 外部运行器：已知字段编辑（PascalCase 直通） -->
    <template v-else>
      <div class="f-row">
        <label>{{ t('editor.f.ExePath') }}</label>
        <div class="exe-wrap">
          <n-input
            size="small"
            :value="runner.ExePath"
            :input-props="{ spellcheck: false }"
            @update:value="emit('exe-path', $event)"
          />
          <!-- 原生文件选择器：后端 WPF OpenFileDialog（Filter=exe），WPF
               CmdSelectExePath 同款交互；选中后触发预设自动填充 -->
          <button class="act-btn" type="button" :disabled="browsing" @click="emit('browse')">
            {{ t('settings.r.f.browse') }}
          </button>
        </div>
      </div>
      <div class="f-row">
        <label>
          <!-- 参数行 label：SSH 族（有 ArgumentsForPrivateKey 字段）区分
               "通过密码/通过私钥"两个参数位；其余协议（VNC/FTP 等外部运行器无私钥概念）用
               通用 label（WPF ExternalRunnerSettings 同行标签 'Cmd parameter'，非 SSH 弹窗
               从不带"通过密码"后缀） -->
          {{ hasArgsPrivateKey(runner) ? t('settings.r.f.arguments') : t('settings.r.f.argumentsGeneric') }}
          <!-- 参数行 (?)：WPF External(SSH)RunnerSettings Arguments 行 (?) → 运行器文档
               （url 照抄）；WPF 同行的 (i) 宏说明弹窗由宏 chips 的 title=描述承载，不再重复 -->
          <HelpLink href="https://1remote.github.io/usage/protocol/runner/" />
        </label>
        <div class="arg-wrap">
          <n-input
            size="small"
            type="textarea"
            :rows="2"
            :value="runner.Arguments"
            :input-props="{ spellcheck: false }"
            :ref="(c) => setArgRef(c, 'Arguments')"
            @update:value="emit('arg', 'Arguments', $event)"
          />
          <!-- 宏 chips：点击插入光标位置，title=宏描述 -->
          <div v-if="macros.length" class="macro-row">
            <span class="macro-row-label">{{ t('settings.r.f.macroHint') }}</span>
            <button
              v-for="m in macros"
              :key="m.name"
              class="macro-pill"
              type="button"
              :title="m.description"
              @click="insertMacro('Arguments', m)"
            >
              {{ m.name }}
            </button>
          </div>
        </div>
      </div>
      <!-- SSH 族外部运行器：私钥登录参数（WPF ExternalSshRunnerSettings 审计补齐） -->
      <div v-if="hasArgsPrivateKey(runner)" class="f-row">
        <label>
          {{ t('settings.r.f.argsPrivateKey') }}
          <!-- 私钥参数行 (?)：WPF ExternalSshRunnerSettings:216 同款（url 照抄） -->
          <HelpLink href="https://1remote.github.io/usage/protocol/runner/" />
        </label>
        <div class="arg-wrap">
          <n-input
            size="small"
            type="textarea"
            :rows="2"
            :value="runner.ArgumentsForPrivateKey"
            :input-props="{ spellcheck: false }"
            :ref="(c) => setArgRef(c, 'ArgumentsForPrivateKey')"
            @update:value="emit('arg', 'ArgumentsForPrivateKey', $event)"
          />
          <div v-if="macros.length" class="macro-row">
            <span class="macro-row-label">{{ t('settings.r.f.macroHint') }}</span>
            <button
              v-for="m in macros"
              :key="m.name"
              class="macro-pill"
              type="button"
              :title="m.description"
              @click="insertMacro('ArgumentsForPrivateKey', m)"
            >
              {{ m.name }}
            </button>
          </div>
        </div>
      </div>
      <div class="f-row">
        <label>{{ t('settings.r.f.env') }}</label>
        <div class="env-wrap">
          <n-input
            size="small"
            type="textarea"
            :rows="2"
            :value="envText"
            :input-props="{ spellcheck: false }"
            :placeholder="t('settings.r.f.envHint')"
            @update:value="emit('env-text', $event)"
          />
          <p class="f-hint">{{ t('settings.r.f.envHint') }}</p>
        </div>
      </div>
      <!-- 特殊字符（%XX 转义，WPF ExternalRunnerSettings 审计补齐） -->
      <div class="f-row">
        <label>{{ t('settings.r.f.special') }}</label>
        <div class="env-wrap">
          <n-input
            size="small"
            type="textarea"
            :rows="2"
            :value="specialText"
            :input-props="{ spellcheck: false }"
            :placeholder="t('settings.r.f.envHint')"
            @update:value="emit('special-text', $event)"
          />
          <p class="f-hint">
            {{ t('settings.r.f.specialHint') }}
            <!-- 特殊字符行 (?)：WPF External*RunnerSettings 转义说明旁 (?) → 运行器文档
                 （url 照抄） -->
            <HelpLink href="https://1remote.github.io/usage/protocol/runner/" />
          </p>
        </div>
      </div>
      <!-- 集成到标签页：开关与输入框同列对齐；解释文本移植 WPF
           ExternalRunnerSettings.xaml 的 Caution 词条（14 语言），行 ToolTip=WPF 同名词条 -->
      <div class="f-row" :title="t('settings.r.f.hostingTitle')">
        <label>{{ t('editor.f.RunWithHosting') }}</label>
        <div class="hosting-wrap">
          <n-switch
            size="small"
            :value="!!runner.RunWithHosting"
            :loading="saving"
            @update:value="emit('hosting', $event)"
          />
          <span class="hosting-hint">{{ t('settings.r.f.hostingHint') }}</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.r-card {
  border: 1px solid var(--border);
  border-radius: var(--radius-box);
  background: var(--bg-elevated);
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: border-color 0.4s;
}
/* 新建卡短暂高亮（class 由 RunnerGroup 传入：创建并选中新卡）——高亮 3s 后熄灭，transition 平滑回落 */
.r-card.flash {
  border-color: var(--accent);
}
.r-head {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.r-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--fs-body);
  color: var(--text-1);
}
.r-badge {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: var(--radius-xs);
  padding: 1px 5px;
  font-size: var(--fs-micro);
  color: var(--text-4);
}
.r-badge.ext {
  border-color: var(--accent);
  color: var(--accent-text);
}
.r-head .del {
  margin-left: auto;
  flex: 0 0 auto;
  width: 22px;
  height: 22px;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  cursor: pointer;
}
.r-head .del:hover {
  border-color: var(--danger);
  background: var(--bg-hover);
  color: var(--danger);
}
.r-internal-hint {
  margin: 0;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
.f-row {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}
.f-row label {
  font-size: var(--fs-body);
  color: var(--text-2);
}
.f-hint {
  margin: 4px 0 0;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
.num-input {
  width: 120px;
}
.env-wrap {
  min-width: 0;
}

/* ---- exe 路径行：输入框 + 浏览按钮 ---- */
.exe-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}
.exe-wrap .n-input {
  flex: 1 1 auto;
  min-width: 0;
}
.act-btn {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 7px 10px;
  cursor: pointer;
}
.act-btn:hover:not(:disabled) {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}
.act-btn:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}

/* ---- 启动参数行：textarea + 宏 chips ---- */
.arg-wrap {
  min-width: 0;
}
.macro-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  margin-top: 5px;
}
.macro-row-label {
  font-size: var(--fs-micro);
  color: var(--text-4);
  margin-right: 2px;
}
.macro-pill {
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-micro);
  line-height: 1.5;
  padding: 1px 8px;
  cursor: pointer;
}
.macro-pill:hover {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}

/* ---- 集成到标签页行：开关与解释文本同列 ---- */
.hosting-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}
.hosting-hint {
  font-size: var(--fs-caption);
  color: var(--text-4);
}

/* ---- 主题下拉选项色点 + 预览条 ---- */
.theme-opt {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.theme-opt .theme-dot {
  width: 10px;
  height: 10px;
  border-radius: var(--radius-xs);
  border: 1px solid var(--border);
  flex: 0 0 auto;
}
.theme-wrap {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
/* 主题下拉 + (?) 帮助并排 */
.theme-select-row {
  display: flex;
  align-items: center;
  gap: 6px;
}
.theme-select-row .n-select {
  flex: 1 1 auto;
  min-width: 0;
}
/* 字符集下拉 + (?) 帮助并排 */
.charset-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}
.charset-wrap .n-select {
  flex: 1 1 auto;
  min-width: 0;
}
.theme-preview {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  padding: 8px 10px;
  border-radius: var(--radius-ctrl);
  font-family: Consolas, 'Courier New', monospace;
  font-size: var(--fs-caption);
  line-height: 1.5;
}
</style>
