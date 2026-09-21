<script setup>
/**
 * 标签管理模态（Plan 3 Task 5）：SideTree「+ 管理」chip 打开（ServerListView 挂载），
 * 独立于设置页——标签是列表页的过滤维度，从边栏直接进出更顺（spec §3.3）。
 *
 * - 列表来自 GET /api/tags/manage?ds=（权威置顶态 + 计数，服务端聚合）；
 *   ds 初值 = 打开时树选中的数据源（「全部数据」回落 Local），模态内可切换（G8：
 *   边栏 chips 计数是全库口径，管理器单库——「全部数据」下不再静默钉死 Local）。
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
import { useUiLockWhileMounted } from '../../composables/editorBus'

// 模态存在期间持有通用 UI 锁：App.vue 顶栏（搜索/「+」/⚙）随之禁用
//（本组件由 ServerListView 以 v-if 挂载/卸载，挂载即锁定、关闭释放）
useUiLockWhileMounted()

const props = defineProps({
  show: { type: Boolean, default: false },
  ds: { type: String, default: 'Local' },
})
const emit = defineEmits(['update:show'])

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()
const { servers, datasources, reload } = useServers()

// 数据源过滤：初值 = 打开时树选中的数据源（「全部数据」视图回落 Local），模态内可切换
//（第三轮 G8：chips 计数是全库口径而管理器单库——之前「全部数据」下静默回落 Local，
// 看得到其他源的标签 chip 却点不进管理器；凭据库模态同款 n-select 过滤行）
const currentDs = ref(props.ds)
watch(
  () => props.ds,
  (v) => {
    currentDs.value = v
  }
)
const dsOptions = computed(() => datasources.value.map((d) => ({ label: d.name, value: d.name })))
watch(currentDs, () => {
  renaming.value = null // 切数据源时收起内联重命名（草稿属于旧 ds）
  load()
})

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
    const list = await api.getTagsManage(currentDs.value)
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
    const updated = await api.saveTagPin(tg.name, !tg.pinned, currentDs.value)
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
    await api.renameTag(r.from, to, currentDs.value)
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

// ---- 删除：确认（计数影响警告）→ DELETE；404=列表过期静默刷新。
// autoFocus:false——删除确认禁 Enter 误触（Esc 仍可取消）----
function onDelete(tg) {
  dialog.warning({
    title: t('tagm.deleteTitle'),
    content: t('tagm.deleteConfirm', { name: tg.name, n: tg.count }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    autoFocus: false,
    onPositiveClick: async () => {
      try {
        await api.deleteTag(tg.name, currentDs.value)
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
    (s) => s.dataSourceName === currentDs.value && (s.tags || []).some((x) => x.toLowerCase() === target)
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
    :title="t('tagm.title') + ' · ' + currentDs"
    :bordered="false"
    :style="{ width: 'min(560px, 92vw)' }"
    role="dialog"
    aria-modal="true"
  >
    <!-- 数据源过滤行：与凭据库模态同款（cv.ds 词条通用名词，复用不新造） -->
    <div class="ds-filter">
      <span class="ds-label">{{ t('cv.ds') }}</span>
      <n-select size="small" :value="currentDs" :options="dsOptions" @update:value="currentDs = $event" />
    </div>
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
          <!-- 重命名输入框的 esc 加 .stop：vueuc focus-trap 的 document keydown 只判
               e.code 不查 defaultPrevented——元素级 .prevent 拦不住它，一次 Esc 会
               「取消重命名 + 关闭模态」双动作（第三轮 G6）。stopPropagation 截断冒泡到
               document，Esc 只取消内联重命名（IconPicker 的 window 捕获拦截同思路轻量版） -->
          <n-input
            class="rename-input"
            size="small"
            v-model:value="renaming.to"
            :input-props="{ spellcheck: false }"
            :placeholder="t('tagm.renamePlaceholder')"
            autofocus
            @keydown.enter.prevent="!$event.isComposing && confirmRename()"
            @keydown.esc.prevent.stop="cancelRename"
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
/* 数据源过滤行：与凭据库模态的 .ds-select/.ds-label 同参数（label + 紧凑下拉） */
.ds-filter {
  width: 240px;
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 0 10px;
}
.ds-label {
  flex: 0 0 auto;
  font-size: var(--fs-body);
  color: var(--text-2);
}
.hint {
  font-size: var(--fs-body);
  color: var(--text-3);
  margin: 0 0 4px;
}
.hint.err {
  color: var(--danger);
}
.empty {
  padding: 20px 0;
  font-size: var(--fs-body);
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
  border-radius: var(--radius-ctrl);
  background: transparent;
  font-size: var(--fs-body);
  filter: grayscale(1);
  /* 未选 pin 的弱化档：--opacity-hint(0.7) 与禁用 0.5 拉开（此前 0.55 撞值像禁用） */
  opacity: var(--opacity-hint);
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
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
.tag-name {
  flex: 0 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--fs-body);
  color: var(--text-1);
}
.rename-input {
  flex: 1 1 auto;
  min-width: 0;
}
.tag-count {
  flex: 0 0 auto;
  margin-left: auto;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
.tag-actions {
  flex: 0 0 auto;
  display: flex;
  gap: 4px;
}
.act {
  min-width: 24px;
  height: var(--ctrl-h-s);
  padding: 0 5px;
  border: none;
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}
.act:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-1);
}
.act:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
.act.connect {
  font-size: var(--fs-caption);
}
</style>
