<script setup>
/**
 * 服务器导入模态（Plan 4 Task 3）：空库引导卡「导入」与顶栏「+ ▾ 导入」共用入口，
 * ServerListView 挂载（v-if 收敛状态，与 TagManagerModal 同款）。
 *
 * - 数据源下拉：仅列可写数据源（后端对只读源 400，前置过滤避免明知必败的选择）；
 *   默认 = 打开时树选中的数据源（不可写则回退 Local / 首个可写）。
 * - 文件：拖放区 + 点击选择（accept 与后端 DetectImportKind 对齐：.json/.csv/.rdp/.db/.sqlite）；
 *   客户端扩展名校验先行（后端 400 的兜底仍在）。
 * - 导入 = api.importServers（multipart）→ {added, skipped}：成功 toast 后关闭，
 *   列表刷新走 SSE（后端导入成功即 ReloadAll(true) 联动），另补一次主动 reload 兜底
 *   （SSE 断连时不至于等 30s 轮询）；400 {errors}（解析失败/只读/空文件）内联展示不关模态。
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMessage } from 'naive-ui'
import { api } from '../api'
import { useServers } from '../composables/useServers'

const props = defineProps({
  show: { type: Boolean, default: false },
  /** 默认目标数据源（ServerListView 当前树选中） */
  defaultDs: { type: String, default: 'Local' },
})
const emit = defineEmits(['update:show'])

const { t } = useI18n()
const message = useMessage()
const { datasources, reload } = useServers()

const ACCEPT = '.json,.csv,.rdp,.db,.sqlite'
const EXT_RE = /\.(json|csv|rdp|db|sqlite)$/i

const showBind = computed({
  get: () => props.show,
  set: (v) => emit('update:show', v),
})

// ---- 数据源：可写优先（无可写时全列，避免空下拉——此时导入必然 400，由内联错误兜底）----
const writableDs = computed(() => datasources.value.filter((d) => d.writable !== false))
const dsOptions = computed(() =>
  (writableDs.value.length ? writableDs.value : datasources.value).map((d) => ({ label: d.name, value: d.name }))
)
const ds = ref('Local')

// ---- 文件选择（input 引用 + 拖放态）----
const fileInput = ref(null)
const file = ref(null) // File | null
const dragging = ref(false)

function pick(f) {
  if (!f) return
  if (!EXT_RE.test(f.name)) {
    message.warning(t('import.badFile', { name: f.name }))
    return
  }
  file.value = f
  errors.value = [] // 换文件即清上一轮的服务端错误
}
function onPick(e) {
  pick(e.target.files?.[0])
  e.target.value = '' // 同名文件重选也触发 change（清 value 而非重建 input）
}
function onDrop(e) {
  dragging.value = false
  pick(e.dataTransfer?.files?.[0])
}
function clearFile() {
  file.value = null
  errors.value = []
}

// ---- 导入执行 ----
const importing = ref(false)
const errors = ref([]) // 服务端 400 {errors}（内联展示；成功路径后端只回 {added, skipped}）

async function doImport() {
  const f = file.value
  if (!f || importing.value) return
  importing.value = true
  errors.value = []
  try {
    const res = await api.importServers(f, ds.value)
    if (res?.added > 0) {
      message.success(t('import.done', { n: res.added }))
      if (res.skipped > 0) message.info(t('import.skipped', { n: res.skipped }))
      // 部分失败（Ok 路径带回逐台 errors）：成功关模态，但失败明细以 warning 告知（后端原文英文）
      if (Array.isArray(res.errors) && res.errors.length)
        message.warning(t('import.errors') + ' ' + res.errors.join('; '))
      showBind.value = false // 列表刷新：后端已 ReloadAll(true) → SSE；此处再补主动 reload 兜底
      reload()
    } else {
      // 后端语义：解析出的条目全部插入失败才可能 added=0 且非 400——带逐台 errors 时内联展示，否则通用失败
      const list = res?.errors
      if (Array.isArray(list) && list.length) errors.value = list
      else message.error(t('import.failed'))
    }
  } catch (e) {
    // 400 {errors}：解析失败/只读数据源/无有效条目——内联列出，模态保留
    const list = e?.body?.errors
    if (e?.status === 400 && Array.isArray(list) && list.length) {
      errors.value = list
    } else {
      message.error(t('import.failed'))
    }
  } finally {
    importing.value = false
  }
}

// 开启时复位（上次的文件/错误/进度不跨次残留）；数据源默认值随打开时的树选中走
watch(
  () => props.show,
  (open) => {
    if (!open) return
    file.value = null
    errors.value = []
    importing.value = false
    const names = dsOptions.value.map((o) => o.value)
    ds.value = names.includes(props.defaultDs)
      ? props.defaultDs
      : names.includes('Local')
        ? 'Local'
        : names[0] || 'Local'
  }
)

function fmtSize(n) {
  if (n == null) return ''
  if (n < 1024) return n + ' B'
  if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB'
  return (n / 1024 / 1024).toFixed(1) + ' MB'
}
</script>

<template>
  <n-modal
    v-model:show="showBind"
    preset="card"
    :title="t('import.title')"
    :bordered="false"
    :style="{ width: 'min(460px, 92vw)' }"
    role="dialog"
    aria-modal="true"
  >
    <!-- 目标数据源（只读源后端必 400，直接不进选项） -->
    <div class="f-row">
      <label>{{ t('import.ds') }}</label>
      <n-select size="small" :value="ds" :options="dsOptions" :disabled="importing" @update:value="ds = $event" />
    </div>

    <!-- 拖放区 = 点击选择（整个区域可点；拖入高亮） -->
    <div
      v-if="!file"
      class="dropzone"
      :class="{ drag: dragging }"
      role="button"
      tabindex="0"
      @click="fileInput?.click()"
      @keydown.enter.prevent="fileInput?.click()"
      @keydown.space.prevent="fileInput?.click()"
      @dragover.prevent="dragging = true"
      @dragleave="dragging = false"
      @drop="onDrop"
    >
      <div class="dz-icon">⤓</div>
      <div class="dz-hint">{{ t('import.dropHint') }}</div>
      <div class="dz-formats">{{ t('import.formats') }}</div>
    </div>
    <!-- 已选文件：名称/大小 + 移除 -->
    <div v-else class="file-row">
      <span class="f-icon">🗎</span>
      <span class="f-name" :title="file.name">{{ file.name }}</span>
      <span class="f-size">{{ fmtSize(file.size) }}</span>
      <button class="f-x" type="button" :disabled="importing" :title="t('import.clear')" @click="clearFile">✕</button>
    </div>

    <!-- 服务端 400 错误列表（后端原文为英文，原样展示不翻译数据） -->
    <div v-if="errors.length" class="err-list">
      <div class="err-title">{{ t('import.errors') }}</div>
      <div v-for="(e, i) in errors" :key="i" class="err-item">· {{ e }}</div>
    </div>

    <template #footer>
      <div class="imp-actions">
        <n-button size="small" :disabled="importing" @click="showBind = false">{{ t('editor.cancel') }}</n-button>
        <n-button size="small" type="primary" :loading="importing" :disabled="!file" @click="doImport">
          {{ importing ? t('import.importing') : t('import.button') }}
        </n-button>
      </div>
    </template>

    <input ref="fileInput" type="file" class="hidden-input" :accept="ACCEPT" @change="onPick" />
  </n-modal>
</template>

<style scoped>
.f-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
}
.f-row label {
  flex: 0 0 auto;
  font-size: 12.5px;
  color: var(--text-2);
}
.f-row .n-select {
  flex: 1 1 auto;
  min-width: 0;
}

.dropzone {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 26px 16px;
  border: 1px dashed var(--border-strong);
  border-radius: 8px;
  background: var(--bg-elevated);
  cursor: pointer;
  user-select: none;
  transition:
    border-color 0.15s,
    background 0.15s;
}
.dropzone:hover,
.dropzone.drag {
  border-color: var(--accent);
  background: var(--bg-hover);
}
.dz-icon {
  font-size: 20px;
  color: var(--text-3);
  line-height: 1;
}
.dz-hint {
  font-size: 12.5px;
  color: var(--text-2);
}
.dz-formats {
  font-size: 11.5px;
  color: var(--text-4);
}

.file-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-elevated);
}
.f-icon {
  flex: 0 0 auto;
  font-size: 14px;
  color: var(--text-3);
}
.f-name {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12.5px;
  color: var(--text-1);
}
.f-size {
  flex: 0 0 auto;
  font-size: 11.5px;
  color: var(--text-4);
}
.f-x {
  flex: 0 0 auto;
  width: 20px;
  height: 20px;
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--text-4);
  font-size: 10px;
  line-height: 1;
  cursor: pointer;
}
.f-x:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-1);
}
.f-x:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.err-list {
  margin-top: 12px;
  padding: 8px 10px;
  border: 1px solid var(--danger);
  border-radius: 7px;
  max-height: 140px;
  overflow: auto;
}
.err-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--danger);
  margin-bottom: 4px;
}
.err-item {
  font-size: 11.5px;
  color: var(--text-3);
  word-break: break-all;
  line-height: 1.5;
}

.imp-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.hidden-input {
  display: none;
}
</style>
