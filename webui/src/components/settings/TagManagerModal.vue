<script setup>
/**
 * 标签管理模态（Plan 3 Task 5）：SideTree「+ 管理」chip 打开（ServerListView 挂载），
 * 独立于设置页——标签是列表页的过滤维度，从边栏直接进出更顺（spec §3.3）。
 *
 * - 列表来自 GET /api/tags/manage?ds=（权威置顶态 + 计数，服务端聚合）；
 *   ds = 打开时边栏树选中的数据源（未选 = Local）。
 * - 行操作：📌 置顶/取消（PUT tags/manage，幂等）｜名称｜计数｜✎ 内联重命名（回车确认 →
 *   POST tags/rename，Esc 取消）｜🗑 删除（确认 → DELETE tags/{name}）｜「连接全部」
 *   （从 useServers 列表按 tags 命中该数据源的服务器，逐个串行 connect——与批量连接同款节流）。
 * - rename/delete 改的是服务器数据：成功后 reload()（useServers）刷新边栏标签区，
 *   并重载本列表（SSE 兜底之外的主动刷新）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { api } from '../../api'
import { BATCH_CONNECT_THRESHOLD, useServers } from '../../composables/useServers'

const props = defineProps({
  show: { type: Boolean, default: false },
  ds: { type: String, default: 'Local' },
})
const emit = defineEmits(['update:show'])

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()
const { servers, reload } = useServers()

const tags = ref([])
const loading = ref(false)
const loadError = ref(false)
const renaming = ref(null) // null | { from, to }：内联重命名草稿
const busy = ref('') // 行级操作锁：正在执行的 tag 名（防重入）

let loadGen = 0 // 乱序完成保护：快速重开/切数据源时旧响应丢弃
async function load() {
  const my = ++loadGen
  loading.value = true
  loadError.value = false
  try {
    const list = await api.getTagsManage(props.ds)
    if (my !== loadGen) return
    tags.value = Array.isArray(list) ? list : []
  } catch {
    if (my === loadGen) loadError.value = true
  } finally {
    if (my === loadGen) loading.value = false
  }
}

watch(
  () => props.show,
  (open) => {
    if (open) {
      renaming.value = null
      load()
    }
  }
)
onMounted(() => {
  if (props.show) load()
})

const showBind = computed({
  get: () => props.show,
  set: (v) => emit('update:show', v),
})

// ---- 置顶/取消（幂等目标值；置顶态是机器本地跨数据源共享）----
async function togglePin(tg) {
  if (busy.value) return
  busy.value = tg.name
  try {
    const updated = await api.saveTagPin(tg.name, !tg.pinned, props.ds)
    const row = tags.value.find((x) => x.name === tg.name)
    if (row && updated) {
      row.pinned = !!updated.pinned
      row.count = updated.count ?? row.count
    }
    // 重排序：置顶在前（与后端聚合排序一致），组内保持相对顺序（稳定排序）
    tags.value = tags.value.slice().sort((a, b) => Number(b.pinned) - Number(a.pinned))
    reload() // 边栏标签区（useServers tags）同步置顶顺序
  } catch (e) {
    message.error(t('tagm.opFailed'))
  } finally {
    busy.value = ''
  }
}

// ---- 内联重命名：✎ 进入编辑 → 回车提交 / Esc 取消 / 失焦（未改名=取消，已改名=提交）----
function startRename(tg) {
  renaming.value = { from: tg.name, to: tg.name }
}
function cancelRename() {
  renaming.value = null
}
function onRenameBlur() {
  const r = renaming.value
  if (!r) return // 回车提交后 blur 再触发：已收起，忽略
  if (r.to.trim() === r.from || !r.to.trim()) cancelRename()
  else confirmRename()
}
async function confirmRename() {
  const r = renaming.value
  if (!r || busy.value) return
  const to = r.to.trim()
  if (!to || to === r.from) {
    renaming.value = null
    return
  }
  busy.value = r.from
  try {
    await api.renameTag(r.from, to, props.ds)
    renaming.value = null
    await reload() // 边栏标签 + 服务器 Tags 已变（SSE 兜底之外的主动刷新）
    await load()
  } catch (e) {
    // 400=空/同名/已存在，404=源标签不在该数据源（列表过期）
    const detail = e?.body?.errors?.join('; ')
    message.error(t('tagm.opFailed') + (detail ? ` ${detail}` : ''))
    if (e?.status === 404) load()
  } finally {
    busy.value = ''
  }
}

// ---- 删除：确认（计数影响警告）→ DELETE；404=列表过期静默刷新 ----
function onDelete(tg) {
  dialog.warning({
    title: t('tagm.deleteTitle'),
    content: t('tagm.deleteConfirm', { name: tg.name, n: tg.count }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    onPositiveClick: async () => {
      try {
        await api.deleteTag(tg.name, props.ds)
        if (renaming.value?.from === tg.name) renaming.value = null
        await reload()
        await load()
      } catch (e) {
        if (e?.status === 404) load()
        else message.error(t('tagm.opFailed'))
      }
    },
  })
}

// ---- 连接全部：该数据源下带此标签的服务器，逐个串行 connect（批量连接同款节流）。
// 超过 BATCH_CONNECT_THRESHOLD 台先弹确认（Plan 4 Task 3，与 ServerListView 批量条同款）----
function connectAll(tg) {
  const target = tg.name.toLowerCase()
  const list = servers.value.filter(
    (s) => s.dataSourceName === props.ds && (s.tags || []).some((x) => x.toLowerCase() === target)
  )
  if (!list.length) {
    message.warning(t('tagm.connectNone'))
    return
  }
  if (list.length > BATCH_CONNECT_THRESHOLD) {
    dialog.warning({
      title: t('batchConnect.confirmTitle'),
      content: t('batchConnect.confirmText', { n: list.length }),
      positiveText: t('batch.connect'),
      negativeText: t('editor.cancel'),
      onPositiveClick: () => runConnectAll(list),
    })
    return
  }
  runConnectAll(list)
}

function runConnectAll(list) {
  ;(async () => {
    let ok = 0
    for (const s of list) {
      try {
        await api.connect(s.id)
        ok++
      } catch (e) {
        console.warn('[TagManagerModal] connect failed:', s.id, e?.message || e)
      }
    }
    if (ok) message.success(t('toast.batchConnectStarted', { n: ok }))
    if (ok < list.length) message.error(t('toast.batchConnectFailed', { n: list.length - ok }))
  })()
}
</script>

<template>
  <n-modal
    v-model:show="showBind"
    preset="card"
    :title="t('tagm.title') + ' · ' + ds"
    :bordered="false"
    :style="{ width: 'min(560px, 92vw)' }"
    role="dialog"
    aria-modal="true"
  >
    <p v-if="loading && !tags.length" class="hint">{{ t('settings.loading') }}</p>
    <p v-else-if="loadError" class="hint err">{{ t('tagm.loadFailed') }}</p>
    <div v-else-if="!tags.length" class="empty">{{ t('tagm.empty') }}</div>
    <div v-else class="tagm-list">
      <div class="tagm-row" v-for="tg in tags" :key="tg.name">
        <!-- 置顶切换：📌 高亮 = 已置顶 -->
        <button
          class="pin-btn"
          type="button"
          :class="{ pinned: tg.pinned }"
          :disabled="busy === tg.name"
          :title="tg.pinned ? t('tagm.unpin') : t('tagm.pin')"
          @click="togglePin(tg)"
        >
          📌
        </button>

        <!-- 名称：内联重命名（编辑态换输入框，回车/Esc/失焦收起） -->
        <template v-if="renaming?.from === tg.name">
          <n-input
            class="rename-input"
            size="small"
            v-model:value="renaming.to"
            :input-props="{ spellcheck: false }"
            :placeholder="t('tagm.renamePlaceholder')"
            autofocus
            @keydown.enter.prevent="confirmRename"
            @keydown.esc.prevent="cancelRename"
            @blur="onRenameBlur"
          />
        </template>
        <span v-else class="tag-name" :title="tg.name">{{ tg.name }}</span>

        <span class="tag-count" :title="t('tagm.col.count')">{{ tg.count }}</span>

        <span class="tag-actions">
          <button
            class="act"
            type="button"
            :disabled="busy === tg.name"
            :title="t('tagm.rename')"
            @click="startRename(tg)"
          >
            ✎
          </button>
          <button
            class="act"
            type="button"
            :disabled="busy === tg.name"
            :title="t('editor.deleteYes')"
            @click="onDelete(tg)"
          >
            🗑
          </button>
          <button
            class="act connect"
            type="button"
            :disabled="busy === tg.name"
            :title="t('tagm.connectAll')"
            @click="connectAll(tg)"
          >
            ▸ {{ t('tagm.connectAll') }}
          </button>
        </span>
      </div>
    </div>
  </n-modal>
</template>

<style scoped>
.hint {
  font-size: 12.5px;
  color: var(--text-3);
  margin: 0 0 4px;
}
.hint.err {
  color: var(--danger);
}
.empty {
  padding: 20px 0;
  font-size: 12.5px;
  color: var(--text-4);
  text-align: center;
}
.tagm-list {
  display: flex;
  flex-direction: column;
  max-height: 50vh;
  overflow: auto;
}
.tagm-row {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 34px;
  padding: 2px 4px;
  border-bottom: 1px solid var(--border);
}
.tagm-row:last-child {
  border-bottom: none;
}
.pin-btn {
  flex: 0 0 26px;
  height: 24px;
  border: none;
  border-radius: 5px;
  background: transparent;
  font-size: 12px;
  filter: grayscale(1);
  opacity: 0.55;
  cursor: pointer;
}
.pin-btn.pinned {
  filter: none;
  opacity: 1;
}
.pin-btn:hover:not(:disabled) {
  background: var(--bg-hover);
}
.pin-btn:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}
.tag-name {
  flex: 0 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12.5px;
  color: var(--text-1);
}
.rename-input {
  flex: 1 1 auto;
  min-width: 0;
}
.tag-count {
  flex: 0 0 auto;
  margin-left: auto;
  font-size: 11.5px;
  color: var(--text-4);
}
.tag-actions {
  flex: 0 0 auto;
  display: flex;
  gap: 4px;
}
.act {
  min-width: 24px;
  height: 22px;
  padding: 0 5px;
  border: none;
  border-radius: 5px;
  background: transparent;
  color: var(--text-3);
  font-size: 12px;
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}
.act:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-1);
}
.act:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
.act.connect {
  font-size: 11.5px;
}
</style>
