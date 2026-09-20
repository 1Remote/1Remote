<script setup>
// 单行：36px flex 行，列宽不自持——由父级 ServerTable 经 CSS
// 变量（--c-*）下发，表头与行严格对齐；本组件只管渲染与交互 emit。
// 交互：单击=纯光标（父级据 event 修饰键做 Ctrl/Shift 勾选，裸点击不改勾选集）、
// 双击=连接、复选框=切换勾选、右键/hover ⋯=菜单、▸=连接、✎=编辑（与菜单「编辑」同链路）。
// 操作列按钮常显（不随 hover 浮现）——行 hover 变色承担"当前行"提示。
// 备注列：文本直显（一行 ellipsis），整格悬停弹 Markdown 预览（对齐 WPF 悬停备注弹层）。
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'
import StatusDot from './StatusDot.vue'
import ProtocolBadge from './ProtocolBadge.vue'
import { formatRelativeTime, formatAbsoluteTime } from '../utils/time'
import { splitHighlight } from '../utils/highlight'
import { opaqueHex } from '../utils/color'
import { renderMarkdown } from '../utils/markdown'

const props = defineProps({
  server: { type: Object, required: true },
  selected: { type: Boolean, default: false }, // 复选框勾选态（批量操作）
  highlighted: { type: Boolean, default: false }, // 边栏树叶选中对应行的高亮
  cursor: { type: Boolean, default: false }, // 键盘导航光标行（↑↓ 移动 / Enter 连接）
  showFolder: { type: Boolean, default: false }, // 仅根视图显示「文件夹」列
  showDs: { type: Boolean, default: false }, // 「全部数据」根视图：文件夹列前缀数据源名
  hiddenCols: { type: Object, default: null }, // 列显隐：{name/addr/proto/note/folder/time: bool}
  query: { type: String, default: '' }, // 搜索过滤词：非空时名称/地址单元格做命中高亮
})
const emit = defineEmits(['toggle-select', 'row-click', 'connect', 'edit', 'context-menu'])
const { t, locale } = useI18n()

const iconSrc = (s) => (s.iconBase64 ? 'data:image/png;base64,' + s.iconBase64 : '')
const initial = (p) => (p || '?').charAt(0).toUpperCase()
// 回退瓦片配色：列表 DTO 的 color 是 C# ColorHex（#AARRGGBB，'#00000000'=无色）——
// 先经 opaqueHex 归一为 #RRGGBB（alpha 在前直接当 CSS 用会得到非法值/全透明，暗色下不可见），
// 无色/非法 → null → 走 .icon-fb 中性样式
const tileStyle = (s) => {
  const rgb = opaqueHex(s.color)
  return rgb ? { background: rgb + '26', color: rgb } : null
}
// 地址列：有地址拼 ':port'；无地址协议回退 SubTitle（WPF 列表地址列绑定 Server.SubTitle，
// Serial 即 "COM1(9600)"——串口号(波特率)，搜索命中高亮 addrSegs 对该文本自动生效），
// 旧后端无该字段时再退协议名
const addressText = (s) => (s.address ? s.address + (s.port ? ':' + s.port : '') : s.subTitle || s.protocol)
// 文件夹列：「全部数据」根视图无文件夹行，此列是
// 唯一来源上下文——「数据源 / 路径」定位信息（额外前缀数据源名，同名路径跨数据源区分）；
// 进入文件夹后列隐藏（面包屑承载路径）
const folderText = (s) =>
  props.showDs ? s.dataSourceName + (s.folderPath ? ' / ' + s.folderPath : '') : s.folderPath || '—'
// 标签胶囊：最多 2 个 + 溢出计数（完整列表见 title）
const overflow = (s) => Math.max(0, s.tags.length - 2)
// 从未连接 = formatRelativeTime 返回 null 时的占位文案；相对时间显式注入当前 i18n locale
// （渲染期读 locale.value，语言切换即时重格式化，不再依赖 navigator.language）
const relTime = (s) => formatRelativeTime(s.lastConnectTime, Date.now(), locale.value) || t('status.never')
// 悬停 title 用绝对时间（精确到秒）：相对时间适合扫读，确切时刻需要完整时间戳；
// 从未连接（0）返回 null 时回退到与显示文本相同的「从未连接」文案
const absTime = (s) => formatAbsoluteTime(s.lastConnectTime, locale.value) || t('status.never')

// 搜索命中高亮分段：查询非空时名称/地址同时高亮；拼音等无法定位原文的
// 命中不高亮（splitHighlight 内处理，见其文件头注释）
const nameSegs = computed(() => splitHighlight(props.server.displayName, props.query))
const addrSegs = computed(() => splitHighlight(addressText(props.server), props.query))
// 行左色条：服务器自定义色（C# #AARRGGBB → opaqueHex 归一为不透明 #RRGGBB）
// 的实色竖条，对齐 WPF 列表行色条——列表中颜色直接可见；无色/全透明 → null 不渲染（无占位）
const barColor = computed(() => opaqueHex(props.server.color))
</script>

<template>
  <div
    class="row"
    :class="{ selected: selected, highlighted: highlighted, cursor: cursor }"
    @click="emit('row-click', $event)"
    @dblclick="emit('connect')"
    @contextmenu.prevent="emit('context-menu', { server, x: $event.clientX, y: $event.clientY })"
  >
    <!-- 左侧颜色条：absolute 定位不占 flex 布局，列对齐零位移 -->
    <span v-if="barColor" class="cbar" :style="{ background: barColor }"></span>
    <div class="cell cell-check">
      <input
        type="checkbox"
        class="cb"
        :checked="selected"
        :title="t('row.select')"
        @click.stop
        @change="emit('toggle-select')"
      />
    </div>
    <div class="cell cell-status"><StatusDot :state="server.connectionState" /></div>
    <div v-if="!hiddenCols || !hiddenCols.name" class="cell cell-name" :title="server.displayName">
      <img v-if="server.iconBase64" class="icon" :src="iconSrc(server)" alt="" />
      <span v-else class="icon icon-fb" :style="tileStyle(server)">{{ initial(server.protocol) }}</span>
      <span class="name"
        ><template v-for="(seg, i) in nameSegs" :key="i"
          ><span v-if="seg.hit" class="hl">{{ seg.text }}</span
          ><template v-else>{{ seg.text }}</template></template
        ></span
      >
    </div>
    <div v-if="!hiddenCols || !hiddenCols.addr" class="cell cell-addr" :title="addressText(server)">
      <template v-for="(seg, i) in addrSegs" :key="i"
        ><span v-if="seg.hit" class="hl">{{ seg.text }}</span
        ><template v-else>{{ seg.text }}</template></template
      >
    </div>
    <div v-if="!hiddenCols || !hiddenCols.proto" class="cell cell-proto">
      <ProtocolBadge :protocol="server.protocol" />
    </div>
    <div class="cell cell-tags" :title="server.tags.join(t('row.tagSep'))">
      <!-- 循环变量命名 tag：避免遮蔽 i18n 的 t（title 属性在循环外也用到 t） -->
      <span v-for="tag in server.tags.slice(0, 2)" :key="tag" class="tag">{{ tag }}</span>
      <span v-if="overflow(server)" class="tag tag-more">+{{ overflow(server) }}</span>
    </div>
    <!-- 备注列：note 非空 = 纯文本一行直显（title 原文），整格作为
         n-popover 的 trigger（naive 不加包装 DOM）悬停弹 Markdown 预览；空 note = 空单元格
         （与 tags 列空态一致，不用「—」占位） -->
    <template v-if="!hiddenCols || !hiddenCols.note">
      <n-popover
        v-if="server.note"
        trigger="hover"
        placement="top-start"
        :content-style="{ maxWidth: '380px', maxHeight: '280px', overflow: 'auto' }"
      >
        <template #trigger>
          <div class="cell cell-note">
            <span class="note-text" :title="server.note">{{ server.note }}</span>
          </div>
        </template>
        <!-- eslint-disable-next-line vue/no-v-html — Note 为用户本人配置的 Markdown，已过轻量净化（utils/markdown.js） -->
        <div class="note-md" v-html="renderMarkdown(server.note)"></div>
      </n-popover>
      <div v-else class="cell cell-note"></div>
    </template>
    <div v-if="showFolder && (!hiddenCols || !hiddenCols.folder)" class="cell cell-folder" :title="folderText(server)">
      {{ folderText(server) }}
    </div>
    <!-- 时间列：显示保持相对时间，悬停 title 为绝对时间（精确到秒，随语言本地化） -->
    <div v-if="!hiddenCols || !hiddenCols.time" class="cell cell-time" :title="absTime(server)">
      {{ relTime(server) }}
    </div>
    <div class="cell cell-act" @click.stop>
      <button class="act" :title="t('row.connect')" @click="emit('connect')">▸</button>
      <!-- 编辑按钮：与右键菜单「编辑」同一 emit 链路，经 ServerTable 转发 server 对象 -->
      <button class="act" :title="t('row.edit')" @click="emit('edit')">✎</button>
      <button
        class="act"
        :title="t('row.more')"
        @click="emit('context-menu', { server, x: $event.clientX, y: $event.clientY })"
      >
        ⋯
      </button>
    </div>
  </div>
</template>

<style scoped>
/* 列宽消费父级下发的 --c-*（见 ServerTable colVars），带独立使用时的兜底值 */
.row {
  position: relative; /* 行左色条 absolute 定位基准 */
  display: flex;
  align-items: center;
  height: 36px;
  padding: 0 10px 0 0;
  border-bottom: 1px solid var(--border);
  user-select: none;
}
/* hover 只作用于未勾选行——勾选行常亮 accent-container（与 hover 叠加语义：
   selected 优先），结构上排除而非依赖规则顺序 */
.row:not(.selected):hover {
  background: var(--bg-hover);
}
.row.selected {
  background: var(--accent-container);
}
.row.highlighted {
  box-shadow: inset 2px 0 0 var(--accent); /* 树叶选中行：左侧强调色细条 */
}
.row.cursor {
  /* 键盘光标行：accent 色外框（不占布局）。单击=光标是核心交互，其落点必须可感知——
     此前 1px --border-strong 过淡，用户无法判断 Enter 将作用于哪行。与另两类行态可区分：
     勾选=.selected 的 accent-container 底色、树叶选中=.highlighted 的左缘 2px 细条 */
  outline: 1px solid var(--accent-focus);
  outline-offset: -1px;
}

.cell {
  display: flex;
  align-items: center;
  min-width: 0;
  padding-right: 10px;
  font-size: var(--fs-body);
  color: var(--text-2);
}
.cell-check {
  flex: 0 0 var(--c-check, 30px);
  justify-content: center;
  padding-right: 0;
}
.cell-status {
  flex: 0 0 var(--c-status, 42px);
}
.cell-name {
  flex: var(--c-name, 2.3) var(--c-name-grow, 1) 0;
  gap: 8px;
}
.cell-addr {
  flex: var(--c-addr, 1.6) var(--c-addr-grow, 1) 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  /* 地址与协议徽章间舒适间距（.cell 通用 10px 视觉上仍贴住徽章，
     提到 16px；.h-addr 同值保持表头/行同缩进） */
  padding-right: 16px;
}
.cell-proto {
  flex: 0 0 var(--c-proto, 84px);
}
.cell-tags {
  flex: var(--c-tags, 1.2) 1 0;
  gap: 4px;
  overflow: hidden;
}
/* 备注列：一行纯文本直显，弱化色 + ellipsis；整格为悬停弹层 trigger */
.cell-note {
  flex: var(--c-note, 1.2) var(--c-note-grow, 1) 0;
  overflow: hidden;
}
.note-text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-3);
}
.cell-folder {
  flex: var(--c-folder, 1.4) var(--c-folder-grow, 1) 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-4);
}
.cell-time {
  flex: 0 0 var(--c-time, 104px);
  color: var(--text-3);
  white-space: nowrap;
}
.cell-act {
  flex: 0 0 var(--c-act, 100px);
  gap: 2px;
  justify-content: flex-end;
  padding-right: 0; /* 操作按钮常显（hover 浮现会让"这里能操作"不可发现）；行 hover 变色已足够区分 */
}

.icon {
  flex: 0 0 22px;
  width: 22px;
  height: 22px;
  border-radius: var(--radius-ctrl);
  object-fit: cover;
}
.icon-fb {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-elevated); /* 无自定义色 → 中性瓦片；有色 → 内联低饱和底+同色字 */
  color: var(--text-3);
  font-size: var(--fs-body);
  font-weight: 600;
}
.name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-1);
}

/* 弹层内 Markdown 排版：内容 teleport 到 body，但 slot 元素携带本组件 scope 属性（scoped 可达），
   主题变量定义在 html[data-theme] 上对 body 全局生效；v-html 子元素不带 scope 属性需 :deep。
   规则集与编辑器预览（MarkdownField .md-body）对齐，尺寸略收敛 */
.note-md {
  font-size: var(--fs-body);
  line-height: 1.6;
  color: var(--text-1);
  word-break: break-word;
}
.note-md :deep(h1),
.note-md :deep(h2),
.note-md :deep(h3),
.note-md :deep(h4) {
  margin: 0.5em 0 0.3em;
  color: var(--text-1);
  line-height: 1.3;
}
.note-md :deep(h1:first-child),
.note-md :deep(h2:first-child),
.note-md :deep(h3:first-child) {
  margin-top: 0;
}
.note-md :deep(p) {
  margin: 0.3em 0;
}
.note-md :deep(ul),
.note-md :deep(ol) {
  margin: 0.3em 0;
  padding-left: 1.4em;
}
.note-md :deep(code) {
  border: 1px solid var(--border);
  border-radius: var(--radius-xs);
  background: var(--bg-hover);
  padding: 0 3px;
  font-family: ui-monospace, Consolas, monospace;
  font-size: var(--fs-caption);
}
.note-md :deep(pre) {
  overflow-x: auto;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-hover);
  padding: 6px 8px;
}
.note-md :deep(pre code) {
  border: none;
  background: transparent;
  padding: 0;
}
.note-md :deep(blockquote) {
  margin: 0.4em 0;
  border-left: 3px solid var(--border-strong);
  padding-left: 8px;
  color: var(--text-3);
}
.note-md :deep(a) {
  color: var(--accent-text);
}
.note-md :deep(img) {
  max-width: 100%;
}
.note-md :deep(table) {
  border-collapse: collapse;
}
.note-md :deep(th),
.note-md :deep(td) {
  border: 1px solid var(--border);
  padding: 2px 8px;
}
.note-md :deep(hr) {
  border: none;
  border-top: 1px solid var(--border);
  margin: 0.6em 0;
}

/* 搜索命中高亮：mark 语义的实心强调底 + 对比文字
   （--accent-solid 深变体底 + --text-on-accent 白字——白字在 7 色深变体上全部 ≥5.18 AA；
   此前 accent 底+面板底色字 14 组合中 9 组合低于 AA，light+green 仅 2.54。
   不加粗，保持行高一致） */
.hl {
  background: var(--accent-solid);
  color: var(--text-on-accent);
  border-radius: 2px;
  padding: 0 1px;
}

/* 行左色条：整行高 4px 实色竖条贴行左缘；checkbox 居中于 30px 列内，
   4px 覆盖不触及。不占 flex 布局（absolute），列对齐与表头零位移 */
.cbar {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 4px;
}

.tag {
  flex: 0 0 auto;
  max-width: 96px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-micro);
  line-height: 1;
  padding: 2.5px 8px;
}
.tag-more {
  color: var(--text-4);
}

.cb {
  accent-color: var(--accent);
}
.act {
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  width: 26px;
  height: 24px;
  border-radius: var(--radius-ctrl);
  cursor: pointer;
}
.act:hover:not(:disabled) {
  background: var(--bg-elevated);
  color: var(--accent-text);
}
.act:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
</style>
