<script setup>
/**
 * 数据源分组（Plan 3 Task 6，spec §6）：卡片列表（状态点/类型/计数/配置摘要）+ 添加/编辑模态 +
 * 测试连接 + 删除（409 keepServers 二次确认）。
 *
 * - 列表来自 useServers 共享态（/api/datasources 含 config 连接参数视图，无密码）；
 *   变更后 reload() 主动刷新（SSE 兜底之外，数据源增删不必然触发 OnReloadAll）。
 * - 测试连接：POST /api/datasources/{name}/test（不带 config=按已存参数测试）。
 *   **保存前无法预测试**：test 端点按"已存在数据源名"寻址，新建流程采用「先保存 → 卡片上测试」
 *   （save-then-test，模态内明确提示）——与 WPF 向导"测试通过才能保存"的差异有意为之，
 *   API 未提供按未保存 config 测试的入口。
 * - Local 卡片只读：测试可用；编辑/删除不开放（后端 PUT/DELETE Local 均为 400，SQLite 路径
 *   属安全域外）。
 * - 编辑模态：密码留空 = 保持原密码（后端 PUT 语义：空串跳过赋值），placeholder 注明。
 * - 删除 409 {serverCount}：二段确认——首段普通确认；409 后二段显示仍有的服务器数与
 *   keepServers 语义（服务器留在库文件，重新添加即可找回），确认后带 keepServers=true 重试。
 */
import { computed, inject, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { api } from '../../api'
import { useServers } from '../../composables/useServers'

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()
const { datasources, reload } = useServers()

// 下拉展开计数（SettingsView 的 Esc 返回链序，见 SettingsView 文件头注释；与 GeneralGroup 同款）
const escShield = inject('settingsEscShield', null)
function shield(show) {
  if (escShield) escShield.open += show ? 1 : -1
}

// ---- 展示辅助 ----
const dotClass = (status) => (status === 'connected' ? 'ok' : status === 'reconnecting' ? 'bad' : 'idle')
const typeLabel = (d) => t('settings.d.type.' + (d.type || 'sqlite'))
const configSummary = (d) => {
  const c = d.config || {}
  if (d.type === 'sqlite') return c.path || ''
  if (!c.host) return ''
  return `${c.host}:${c.port ?? ''} · ${c.databaseName || ''}`
}
const isLocal = (d) => d.name === 'Local'

// ---- 测试连接（卡片按钮）：按已存参数；完成后 reload 刷新状态点 ----
const testing = ref('')
async function onTest(d) {
  if (testing.value) return
  testing.value = d.name
  try {
    const r = await api.testDataSource(d.name)
    if (r?.ok) message.success(t('settings.d.testOk'))
    else message.error(t('settings.d.testFailed') + (r?.detail ? ` (${r.detail})` : ''))
    await reload()
  } catch (e) {
    message.error(t('settings.d.testFailed'))
  } finally {
    testing.value = ''
  }
}

// ---- 添加模态：类型三选 + 动态表单（sqlite: name?/path；mysql/pgsql: name/host/port/db/user/password）----
const adding = ref(false)
const addForm = reactive({
  type: 'sqlite',
  name: '',
  path: '',
  host: '',
  port: 3306,
  databaseName: '',
  userName: '',
  password: '',
})
const addSaving = ref(false)
const typeOptions = ['sqlite', 'mysql', 'pgsql'].map((v) => ({
  value: v,
  label: computed(() => t('settings.d.type.' + v)),
}))

// 打开即重置（与 CredentialVaultGroup.openCreate 同款）：上次未提交的草稿不带入新会话
function openAdd() {
  Object.assign(addForm, {
    type: 'sqlite',
    name: '',
    path: '',
    host: '',
    port: 3306,
    databaseName: '',
    userName: '',
    password: '',
  })
  adding.value = true
}

function onAddTypeChange(v) {
  addForm.type = v
  // 默认端口随类型切换（仅当用户未偏离默认值时跟随，避免覆盖手输）
  const defaults = { mysql: 3306, pgsql: 5432 }
  const other = v === 'mysql' ? 5432 : 3306
  if (addForm.port === other || !addForm.port) addForm.port = defaults[v] ?? 3306
}

const addValid = computed(() => {
  if (addForm.type === 'sqlite') return !!addForm.path.trim() // name 可缺省（按 path 文件名推导）
  return !!(
    addForm.name.trim() &&
    addForm.host.trim() &&
    addForm.databaseName.trim() &&
    addForm.userName.trim() &&
    addForm.password
  )
})

async function addSave() {
  if (!addValid.value || addSaving.value) return
  addSaving.value = true
  const config =
    addForm.type === 'sqlite'
      ? { path: addForm.path.trim() }
      : {
          host: addForm.host.trim(),
          port: Number(addForm.port) || 0,
          databaseName: addForm.databaseName.trim(),
          userName: addForm.userName.trim(),
          password: addForm.password,
        }
  const name = addForm.name.trim() || undefined // sqlite 缺省由后端从 path 文件名推导
  try {
    const r = await api.addDataSource(addForm.type, config, name)
    adding.value = false
    await reload()
    // 创建即连接失败不回滚（WPF 同款）：如实提示 + 指引到卡片测试
    if (r?.dataSource && r.dataSource.status === 'connected') message.success(t('settings.d.testOk'))
    else message.warning(t('settings.d.testFailed') + (r?.connectError ? ` (${r.connectError})` : ''))
  } catch (e) {
    const detail = e?.body?.errors?.join('; ')
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  } finally {
    addSaving.value = false
  }
}

// ---- 编辑模态（非 Local）：密码留空 = 保持 ----
const editing = ref(null) // null | { name, type }
const editForm = reactive({ path: '', host: '', port: 3306, databaseName: '', userName: '', password: '' })
const editSaving = ref(false)
const showEdit = computed({
  get: () => !!editing.value,
  set: (v) => {
    if (!v) editing.value = null
  },
})

function openEdit(d) {
  const c = d.config || {}
  Object.assign(editForm, {
    path: c.path || '',
    host: c.host || '',
    port: c.port ?? (d.type === 'pgsql' ? 5432 : 3306),
    databaseName: c.databaseName || '',
    userName: c.userName || '',
    password: '', // 无密码回显（读接口不含密码）；空 = 保持
  })
  editing.value = { name: d.name, type: d.type }
}

async function editSave() {
  if (!editing.value || editSaving.value) return
  editSaving.value = true
  const config =
    editing.value.type === 'sqlite'
      ? { path: editForm.path.trim() }
      : {
          host: editForm.host.trim(),
          port: Number(editForm.port) || 0,
          databaseName: editForm.databaseName.trim(),
          userName: editForm.userName.trim(),
          password: editForm.password, // 空 = 保持原密码（后端语义）
        }
  try {
    const r = await api.updateDataSource(editing.value.name, config)
    editing.value = null
    await reload()
    if (r?.dataSource && r.dataSource.status === 'connected') message.success(t('settings.saved'))
    else message.warning(t('settings.d.testFailed') + (r?.connectError ? ` (${r.connectError})` : ''))
  } catch (e) {
    const detail = e?.body?.errors?.join('; ')
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  } finally {
    editSaving.value = false
  }
}

// 模态表单回车=保存（添加/编辑模态各自传保存函数；与保存按钮同守卫——校验未过/保存中
// 不动作）：输入框聚焦回车提交；按钮/textarea/下拉（类型下拉的回车=选中选项）留给原生
// 行为；IME 组合中的回车（选字）不触发
function onFormEnter(e, fn) {
  if (e.key !== 'Enter' || e.isComposing) return
  if (e.target?.closest?.('button, textarea, .n-select')) return
  e.preventDefault()
  fn()
}

// ---- 删除：二段确认（409 keepServers 重试）----
function onDelete(d) {
  dialog.warning({
    title: t('settings.d.deleteTitle'),
    content: t('settings.d.deleteConfirm', { name: d.name }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    onPositiveClick: async () => {
      try {
        await api.deleteDataSource(d.name)
        message.success(t('settings.d.deleted'))
        await reload()
      } catch (e) {
        if (e?.status === 409 && e.body && typeof e.body.serverCount === 'number') {
          confirmKeepServers(d, e.body.serverCount)
        } else if (e?.status === 404) {
          await reload() // 已被其它端删除：静默刷新
        } else {
          message.error(t('settings.d.deleteFailed'))
        }
      }
    },
  })
}

// 二段：仍有服务器 → keepServers=true 重试（服务器留在库文件中，重新添加即可找回）
function confirmKeepServers(d, serverCount) {
  dialog.warning({
    title: t('settings.d.deleteTitle'),
    content:
      t('settings.d.deleteConfirm', { name: d.name }) +
      ' ' +
      t('settings.d.deleteHasServers', { n: serverCount }) +
      ' ' +
      t('settings.d.keepServersHint'),
    positiveText: t('settings.d.keepServers'),
    negativeText: t('editor.cancel'),
    onPositiveClick: async () => {
      try {
        await api.deleteDataSource(d.name, true)
        message.success(t('settings.d.deleted'))
        await reload()
      } catch {
        message.error(t('settings.d.deleteFailed'))
      }
    },
  })
}

// ---- Esc 链：模态开着时捕获截停（SettingsView 返回导航让位）----
// 下拉开着（shield>0）时让位：Esc 先由 naive 组件层消化关下拉，不动模态
function onEscCapture(e) {
  if (e.key !== 'Escape') return
  if (escShield && escShield.open > 0) return
  if (adding.value) {
    e.stopPropagation()
    adding.value = false
  } else if (editing.value) {
    e.stopPropagation()
    editing.value = null
  }
}
onMounted(() => window.addEventListener('keydown', onEscCapture, true))
onBeforeUnmount(() => window.removeEventListener('keydown', onEscCapture, true))
</script>

<template>
  <div class="group">
    <div class="toolbar">
      <n-button size="small" type="primary" @click="openAdd">{{ t('settings.d.add') }}</n-button>
    </div>

    <div v-if="!datasources.length" class="empty">{{ t('tree.noDatasources') }}</div>
    <div v-else class="cards">
      <div v-for="d in datasources" :key="d.name" class="ds-card">
        <div class="card-main">
          <span class="dot" :class="dotClass(d.status)" :title="d.status"></span>
          <span class="ds-name" :title="d.name">{{ d.name }}</span>
          <span class="type-badge">{{ typeLabel(d) }}</span>
          <span v-if="d.writable === false" class="ro-badge">{{ t('settings.d.readOnly') }}</span>
          <span class="srv-count" :title="t('tagm.col.count')">{{ d.serverCount }}</span>
        </div>
        <div class="card-config" :title="configSummary(d)">{{ configSummary(d) || '—' }}</div>
        <div class="card-actions">
          <button class="act" type="button" :disabled="testing === d.name" @click="onTest(d)">
            {{ testing === d.name ? t('settings.d.testing') : t('settings.d.test') }}
          </button>
          <!-- Local 只读：路径/删除不开放（title 说明） -->
          <button
            class="act"
            type="button"
            :disabled="isLocal(d)"
            :title="isLocal(d) ? t('settings.d.localHint') : t('settings.d.edit')"
            @click="openEdit(d)"
          >
            {{ t('settings.d.edit') }}
          </button>
          <button
            class="act"
            type="button"
            :disabled="isLocal(d)"
            :title="isLocal(d) ? t('settings.d.localHint') : t('editor.deleteYes')"
            @click="onDelete(d)"
          >
            {{ t('editor.deleteYes') }}
          </button>
        </div>
      </div>
    </div>

    <!-- 添加模态：类型三选 + 动态表单；保存前无法测试（save-then-test，见文件头） -->
    <n-modal
      v-model:show="adding"
      preset="card"
      :title="t('settings.d.addTitle')"
      :bordered="false"
      :style="{ width: 'min(480px, 92vw)' }"
      role="dialog"
      aria-modal="true"
    >
      <div class="form" @keydown="onFormEnter($event, addSave)">
        <div class="f-row">
          <label>{{ t('settings.d.type') }}</label>
          <n-select
            size="small"
            :value="addForm.type"
            :options="typeOptions.map((o) => ({ value: o.value, label: o.label.value }))"
            @update:show="shield"
            @update:value="onAddTypeChange"
          />
        </div>
        <div class="f-row">
          <label>{{ t('settings.d.name') }}</label>
          <div>
            <n-input size="small" v-model:value="addForm.name" :input-props="{ spellcheck: false }" />
            <p v-if="addForm.type === 'sqlite'" class="f-hint">{{ t('settings.d.nameHint') }}</p>
          </div>
        </div>
        <div v-if="addForm.type === 'sqlite'" class="f-row">
          <label>{{ t('settings.d.f.path') }}</label>
          <n-input size="small" v-model:value="addForm.path" :input-props="{ spellcheck: false }" />
        </div>
        <template v-else>
          <div class="f-row">
            <label>{{ t('settings.d.f.host') }}</label>
            <n-input size="small" v-model:value="addForm.host" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.port') }}</label>
            <n-input size="small" v-model:value="addForm.port" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.database') }}</label>
            <n-input size="small" v-model:value="addForm.databaseName" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.user') }}</label>
            <n-input size="small" v-model:value="addForm.userName" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.password') }}</label>
            <n-input
              size="small"
              type="password"
              show-password-on="click"
              v-model:value="addForm.password"
              :input-props="{ spellcheck: false }"
            />
          </div>
        </template>
        <p class="f-note">{{ t('settings.d.saveFirstHint') }}</p>
      </div>
      <template #footer>
        <div class="modal-actions">
          <n-button size="small" @click="adding = false">{{ t('editor.cancel') }}</n-button>
          <n-button size="small" type="primary" :disabled="!addValid" :loading="addSaving" @click="addSave">
            {{ t('settings.save') }}
          </n-button>
        </div>
      </template>
    </n-modal>

    <!-- 编辑模态：密码留空 = 保持原密码 -->
    <n-modal
      v-model:show="showEdit"
      preset="card"
      :title="t('settings.d.editTitle')"
      :bordered="false"
      :style="{ width: 'min(480px, 92vw)' }"
      role="dialog"
      aria-modal="true"
    >
      <div class="form" @keydown="onFormEnter($event, editSave)">
        <div v-if="editing?.type === 'sqlite'" class="f-row">
          <label>{{ t('settings.d.f.path') }}</label>
          <n-input size="small" v-model:value="editForm.path" :input-props="{ spellcheck: false }" />
        </div>
        <template v-else>
          <div class="f-row">
            <label>{{ t('settings.d.f.host') }}</label>
            <n-input size="small" v-model:value="editForm.host" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.port') }}</label>
            <n-input size="small" v-model:value="editForm.port" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.database') }}</label>
            <n-input size="small" v-model:value="editForm.databaseName" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.user') }}</label>
            <n-input size="small" v-model:value="editForm.userName" :input-props="{ spellcheck: false }" />
          </div>
          <div class="f-row">
            <label>{{ t('settings.d.f.password') }}</label>
            <div>
              <n-input
                size="small"
                type="password"
                show-password-on="click"
                v-model:value="editForm.password"
                :placeholder="t('settings.ph.keepCurrent')"
                :input-props="{ spellcheck: false }"
              />
              <p class="f-hint">{{ t('settings.d.f.passwordKeep') }}</p>
            </div>
          </div>
        </template>
      </div>
      <template #footer>
        <div class="modal-actions">
          <n-button size="small" @click="editing = null">{{ t('editor.cancel') }}</n-button>
          <n-button size="small" type="primary" :loading="editSaving" @click="editSave">
            {{ t('settings.save') }}
          </n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.group {
  max-width: 720px;
}
.toolbar {
  display: flex;
  margin-bottom: 12px;
}
.empty {
  padding: 24px 0;
  font-size: 0.9615rem;
  color: var(--text-4);
}
.cards {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.ds-card {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 2px 12px;
  align-items: center;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-elevated);
}
.card-main {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.dot {
  flex: 0 0 7px;
  width: 7px;
  height: 7px;
  border-radius: 50%;
}
.dot.ok {
  background: var(--success);
}
.dot.idle {
  background: var(--text-4);
}
.dot.bad {
  background: var(--danger);
}
.ds-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 1rem;
  color: var(--text-1);
}
.type-badge,
.ro-badge {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 1px 5px;
  font-size: 0.8077rem;
  color: var(--text-4);
}
.ro-badge {
  border-color: var(--warning);
  color: var(--warning);
}
.srv-count {
  flex: 0 0 auto;
  font-size: 0.8846rem;
  color: var(--text-4);
}
.card-config {
  grid-column: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.8846rem;
  color: var(--text-4);
}
.card-actions {
  grid-row: 1 / span 2;
  grid-column: 2;
  display: flex;
  gap: 6px;
}
.act {
  height: 24px;
  padding: 0 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: transparent;
  color: var(--text-2);
  font-size: 0.8846rem;
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}
.act:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.act:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* ---- 模态表单 ---- */
.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.f-row {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}
.f-row label {
  font-size: 0.9615rem;
  color: var(--text-2);
}
.f-hint {
  margin: 4px 0 0;
  font-size: 0.8462rem;
  color: var(--text-4);
}
.f-note {
  margin: 2px 0 0;
  font-size: 0.8846rem;
  color: var(--warning);
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
