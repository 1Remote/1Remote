<script setup>
/**
 * 图标选择器（Plan 2 Task 9）：三来源 + 清除，对齐 WPF IconPopupDialog 的能力面——
 *  - 内置图标网格：GET /api/icons（ServerIcons 内嵌 PNG base64 有序列表）。100+ 张每张
 *    数 KB base64，弹窗打开才拉取且模块级缓存（同会话多台服务器连续编辑不重复请求）；
 *  - 本地上传：input[type=file] → FileReader.readAsDataURL，纯浏览器侧转 base64
 *    （不经过服务器文件系统），剥掉 data URI 前缀只存裸 base64（与 IconBase64 存储格式一致）；
 *  - exe 提取：POST /api/icons/extract-from-exe（后端 ExtractAssociatedIcon），路径为运行
 *    1Remote 的主机上的绝对路径；404（路径不存在/非 .exe）以 toast 呈现；
 *  - 无图标：写空串 = 协议默认瓦片（与 ServerRow 的回退渲染一致）。
 *
 * Esc 交互：弹窗打开期间用 window 捕获阶段拦截 Escape（stopPropagation + 关弹窗）——
 * EditorDrawer 的 window 级 Esc（气泡阶段）在弹窗打开时不应关闭抽屉；捕获阶段先于目标/
 * 气泡阶段，可在事件到达抽屉 handler 前截停（naive 的 markEventEffectPerformed 不阻断冒泡，
 * 依赖其内部约定无法跨组件，故自行拦截）。
 */
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../../api'

const props = defineProps({
  /** 当前图标：裸 base64 字符串（json 的 IconBase64），空串 = 无图标 */
  modelValue: { type: String, default: '' },
  /** 预览底色（#RRGGBB，EditorDrawer 由 ColorHex 派生；颜色随 ColorHex 即时联动） */
  tint: { type: String, default: '' },
  disabled: { type: Boolean, default: false },
})
const emit = defineEmits(['update:modelValue'])
const { t } = useI18n()
const message = useMessage()

// 模块级内置图标缓存：跨组件实例共享，null = 尚未加载过（失败不缓存，下次可重试）
let builtinCache = null

const show = ref(false)
const icons = ref([])
const loadingIcons = ref(false)
const exePath = ref('')
const extracting = ref(false)
const picking = ref(false)

async function loadIcons() {
  if (builtinCache) {
    icons.value = builtinCache
    return
  }
  loadingIcons.value = true
  try {
    const resp = await api.icons()
    builtinCache = Array.isArray(resp?.icons) ? resp.icons : []
    icons.value = builtinCache
  } catch (e) {
    console.warn('[IconPicker] load icons failed:', e?.message || e)
    message.error(t('editor.iconLoadFailed'))
  } finally {
    loadingIcons.value = false
  }
}

function open() {
  if (props.disabled) return
  show.value = true
  loadIcons() // 惰性：首次打开才拉取（后续命中模块缓存）
}

function pick(b64) {
  emit('update:modelValue', b64)
  show.value = false
}

function onUploadChange(ev) {
  const file = ev.target.files?.[0]
  ev.target.value = '' // 允许连续两次选同一文件（change 不触发）
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => {
    const url = String(reader.result || '')
    const i = url.indexOf('base64,')
    if (i < 0) {
      message.error(t('editor.iconInvalidFile'))
      return
    }
    pick(url.slice(i + 'base64,'.length))
  }
  reader.onerror = () => message.error(t('editor.iconInvalidFile'))
  reader.readAsDataURL(file)
}

async function extractFromExe() {
  const path = exePath.value.trim()
  if (!path || extracting.value) return
  extracting.value = true
  try {
    const resp = await api.extractIcon(path)
    if (resp?.iconBase64) pick(resp.iconBase64)
    else message.error(t('editor.iconExtractFailed'))
  } catch (e) {
    // 404 = 路径不存在/非 .exe（后端语义）；400/500/网络 → 通用失败文案
    message.error(e?.status === 404 ? t('editor.iconExtractNotFound') : t('editor.iconExtractFailed'))
  } finally {
    extracting.value = false
  }
}

// exe 路径原生文件选择器：与运行器设置同一 API
// （POST /api/files/pick-exe，后端 WPF OpenFileDialog）。只填入路径不自动提取——
// 提取可能失败需要 toast，选择器职责保持单一；404 = 用户取消，静默。
async function browseExePath() {
  if (picking.value) return
  picking.value = true
  try {
    const resp = await api.pickExe(exePath.value)
    if (resp?.path) exePath.value = resp.path
  } catch (e) {
    if (e?.status !== 404) message.error(t('settings.r.pickFailed'))
  } finally {
    picking.value = false
  }
}

// ---- Esc 拦截（见文件头注释）：捕获阶段截停，弹窗自身关闭 ----
function onEscCapture(e) {
  if (show.value && e.key === 'Escape') {
    e.stopPropagation()
    show.value = false
  }
}
onMounted(() => window.addEventListener('keydown', onEscCapture, true))
onBeforeUnmount(() => window.removeEventListener('keydown', onEscCapture, true))
</script>

<template>
  <div class="icon-picker">
    <!-- 48px 预览瓦片：点击即开选择器；tint = 当前 ColorHex 低饱和底色，
         无图标时 tint 仍生效（空瓦片也即时反映所选颜色） -->
    <button
      type="button"
      class="ip-thumb"
      :class="{ empty: !modelValue }"
      :style="tint ? { background: tint + '26' } : null"
      :disabled="disabled"
      :title="t('editor.pickIcon')"
      @click="open"
    >
      <img v-if="modelValue" :src="'data:image/png;base64,' + modelValue" alt="" />
    </button>
    <button class="ip-btn" type="button" :disabled="disabled" :title="t('editor.pickIcon')" @click="open">…</button>

    <n-modal
      v-model:show="show"
      preset="card"
      class="ip-modal"
      :title="t('editor.pickIcon')"
      :bordered="false"
      :style="{ width: 'min(560px, 92vw)' }"
      role="dialog"
      aria-modal="true"
    >
      <!-- 内置图标：32px 网格，点击即选并关闭 -->
      <div class="ip-section">{{ t('editor.iconBuiltin') }}</div>
      <div v-if="loadingIcons" class="ip-hint">{{ t('editor.loading') }}</div>
      <div v-else-if="!icons.length" class="ip-hint">{{ t('editor.iconLoadFailed') }}</div>
      <div v-else class="ip-grid">
        <button
          v-for="(b64, i) in icons"
          :key="i"
          class="ip-cell"
          :class="{ active: b64 === modelValue }"
          type="button"
          @click="pick(b64)"
        >
          <img :src="'data:image/png;base64,' + b64" alt="" loading="lazy" />
        </button>
      </div>

      <!-- 本地上传：浏览器侧 FileReader，不经服务器 -->
      <div class="ip-section">{{ t('editor.iconUpload') }}</div>
      <label class="ip-upload">
        <input type="file" accept="image/*" @change="onUploadChange" />
        {{ t('editor.iconUploadBtn') }}
      </label>

      <!-- exe 提取：主机绝对路径（+ 原生文件选择器填路径） -->
      <div class="ip-section">{{ t('editor.iconExtractFromExe') }}</div>
      <div class="ip-exe">
        <n-input
          size="small"
          v-model:value="exePath"
          :placeholder="t('editor.iconExePlaceholder')"
          :input-props="{ spellcheck: false }"
          @keyup.enter="extractFromExe"
        />
        <button class="ip-btn" type="button" :disabled="picking" @click="browseExePath">
          {{ t('settings.r.f.browse') }}
        </button>
        <button class="ip-btn" type="button" :disabled="extracting || !exePath.trim()" @click="extractFromExe">
          {{ extracting ? t('editor.saving') : t('editor.iconExtract') }}
        </button>
      </div>

      <template #footer>
        <button v-if="modelValue" class="ip-clear" type="button" @click="pick('')">{{ t('editor.noIcon') }}</button>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.icon-picker {
  display: flex;
  align-items: center;
  gap: 8px;
}
.ip-thumb {
  width: 48px;
  height: 48px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--bg-elevated);
  cursor: pointer;
  padding: 3px;
}
.ip-thumb img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.ip-thumb.empty {
  border-style: dashed;
  border-color: var(--border-strong);
}
.ip-thumb:hover:not(:disabled) {
  border-color: var(--accent);
}
.ip-thumb:disabled {
  cursor: not-allowed;
  opacity: 0.75;
}
.ip-btn {
  border: 1px solid var(--border);
  border-radius: 5px;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: 0.9231rem;
  line-height: 1;
  padding: 5px 10px;
  cursor: pointer;
}
.ip-btn:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.ip-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

/* 弹窗内容（n-modal 传送门内渲染，仍属本组件 scoped 作用域） */
.ip-section {
  margin: 10px 0 6px;
  font-size: 0.9231rem;
  font-weight: 600;
  color: var(--text-2);
}
.ip-section:first-child {
  margin-top: 0;
}
.ip-hint {
  font-size: 0.9231rem;
  color: var(--text-4);
  padding: 8px 0;
}
.ip-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, 32px);
  gap: 4px;
  max-height: 240px;
  overflow-y: auto;
  padding: 2px;
}
.ip-cell {
  width: 32px;
  height: 32px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--border);
  border-radius: 5px;
  background: var(--bg-elevated);
  cursor: pointer;
  padding: 2px;
}
.ip-cell img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.ip-cell:hover {
  border-color: var(--accent);
  background: var(--bg-hover);
}
.ip-cell.active {
  border-color: var(--accent);
  box-shadow: 0 0 0 1px var(--accent);
}
.ip-upload {
  display: inline-block;
  border: 1px dashed var(--border-strong);
  border-radius: 5px;
  background: transparent;
  color: var(--text-2);
  font-size: 0.9231rem;
  line-height: 1;
  padding: 6px 12px;
  cursor: pointer;
}
.ip-upload:hover {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}
.ip-upload input {
  display: none;
}
.ip-exe {
  display: flex;
  align-items: center;
  gap: 8px;
}
.ip-exe .ip-btn {
  flex: 0 0 auto;
}
.ip-clear {
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: 0.9231rem;
  line-height: 1;
  padding: 4px 2px;
  cursor: pointer;
}
.ip-clear:hover {
  color: var(--danger);
}
</style>
