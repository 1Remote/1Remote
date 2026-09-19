<script setup>
/**
 * 数据源分组（Plan 3 Task 6，spec §6）：卡片列表（状态点/类型/计数/配置摘要）+ 添加/编辑模态 +
 * 测试连接 + 删除（409 keepServers 二次确认）。
 *
 * - 列表来自 useServers 共享态（/api/datasources 含 config 连接参数视图，无密码）；
 *   变更后 reload() 主动刷新（SSE 兜底之外，数据源增删不必然触发 OnReloadAll）。
 * - 测试连接：卡片按钮按已存参数测（POST /api/datasources/{name}/test）；新建/编辑模态的
 *   "测试连接"按钮对**表单草稿**测试（batch10 Task B #8，WPF Mysql/Pgsql 弹窗测试按钮语义：
 *   构造临时配置直接测试，不经保存——未保存名查不到已存项，后端按 body.type 构造临时实例；
 *   密码留空时旧名可寻址到已存源则沿用其密码，与编辑弹窗"密码留空=保持"一致）。
 *   结果行内显示（成功/失败+详情），不再要求"先保存再测"。
 * - 名称（batch10 Task B #7，WPF 弹窗平价）：新建模态名称可填（sqlite 可缺省按路径推导）；
 *   编辑模态名称可改（WPF 弹窗 Name 直写 org.DataSourceName；Local 在 web 侧本就不可编辑）。
 *   重名即时提示（computed 比对现有列表，忽略大小写对齐后端 CurrentCultureIgnoreCase，
 *   编辑排除自身；后端最终守卫 POST 409 / PUT 409）。
 * - Local 卡片只读：测试可用；编辑/删除不开放（后端 PUT/DELETE Local 均为 400，SQLite 路径
 *   属安全域外）。
 * - 编辑模态：密码留空 = 保持原密码（后端 PUT 语义：空串跳过赋值），placeholder 注明。
 * - sqlite 路径"浏览…"：WPF SqliteSettingView 的 Select 按钮
 *   （SqliteSettingViewModel.cs:103，filter "SqliteSource Database|*.db" 照抄）的 web
 *   平价——添加/编辑两模态的路径行均带按钮，调 /api/files/pick 弹后端原生对话框。
 *   偏差：WPF checkFileExists:false（可选不存在的库文件），端点恒 true——只能选已
 *   存在的 .db，新建库文件仍走手输（schemas.js filePick 审计同记录）。
 * - 删除 409 {serverCount}：二段确认——首段普通确认；409 后二段显示仍有的服务器数与
 *   keepServers 语义（服务器留在库文件，重新添加即可找回），确认后带 keepServers=true 重试。
 */
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { api } from '../../api'
import { useServers } from '../../composables/useServers'
import { useSettingsEsc } from '../../composables/useSettingsEsc'
import { onFormEnter } from '../../utils/formEnter'
import HelpLink from '../HelpLink.vue'

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()
const { datasources, reload } = useServers()

// 下拉展开计数 + 模态 Esc 截停（Esc 链序见 SettingsView/useSettingsEsc 文件头注释）
const { shield, bindModalEsc } = useSettingsEsc()

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
  addTestResult.value = null
  adding.value = true
}

function onAddTypeChange(v) {
  addForm.type = v
  // 默认端口随类型切换（仅当用户未偏离默认值时跟随，避免覆盖手输）
  const defaults = { mysql: 3306, pgsql: 5432 }
  const other = v === 'mysql' ? 5432 : 3306
  if (addForm.port === other || !addForm.port) addForm.port = defaults[v] ?? 3306
}

// ---- 名称即时查重（batch10 Task B #6/#7）：忽略大小写（对齐后端 CurrentCultureIgnoreCase；
// 编辑排除自身原名）——重名即提示 + 禁用保存，后端 409 仍为最终守卫 ----
const nameExistsAmong = (n, excludeName) =>
  !!n && datasources.value.some((d) => d.name !== excludeName && d.name.trim().toLowerCase() === n.trim().toLowerCase())
const addNameExists = computed(() => nameExistsAmong(addForm.name, null))
const editNameExists = computed(() => (editing.value ? nameExistsAmong(editForm.name, editing.value.name) : false))

const addValid = computed(() => {
  if (addNameExists.value) return false
  if (addForm.type === 'sqlite') return !!addForm.path.trim() // name 可缺省（按 path 文件名推导）
  return !!(
    addForm.name.trim() &&
    addForm.host.trim() &&
    addForm.databaseName.trim() &&
    addForm.userName.trim() &&
    addForm.password
  )
})

// 表单草稿 → config 提交体（保存与测试共用）
function draftConfig(f, type) {
  return type === 'sqlite'
    ? { path: f.path.trim() }
    : {
        host: f.host.trim(),
        port: Number(f.port) || 0,
        databaseName: f.databaseName.trim(),
        userName: f.userName.trim(),
        password: f.password, // 新建=明文必填；编辑空=保持（后端语义）
      }
}

async function addSave() {
  if (!addValid.value || addSaving.value) return
  addSaving.value = true
  const config = draftConfig(addForm, addForm.type)
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

// ---- 新建模态"测试连接"（batch10 Task B #8）：对表单草稿测试（不经保存，WPF 测试按钮语义；
// 未保存名后端按 type 构造临时实例；结果行内展示，失败带 detail）----
const addTesting = ref(false)
const addTestResult = ref(null) // null | { ok, detail }
async function testAdd() {
  if (addTesting.value) return
  addTesting.value = true
  addTestResult.value = null
  try {
    const r = await api.testDataSource(
      addForm.name.trim() || addForm.path.trim(),
      draftConfig(addForm, addForm.type),
      addForm.type
    )
    addTestResult.value = { ok: !!r?.ok, detail: r?.detail || '' }
  } catch (e) {
    const detail = e?.body?.errors?.join('; ') || e?.body?.detail || ''
    addTestResult.value = { ok: false, detail }
  } finally {
    addTesting.value = false
  }
}

// ---- 编辑模态（非 Local）：名称可改（WPF 弹窗平价）；密码留空 = 保持 ----
const editing = ref(null) // null | { name, type }
const editForm = reactive({ name: '', path: '', host: '', port: 3306, databaseName: '', userName: '', password: '' })
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
    name: d.name, // 名称可改（WPF 弹窗平价；Local 本就不可编辑不进此模态）
    path: c.path || '',
    host: c.host || '',
    port: c.port ?? (d.type === 'pgsql' ? 5432 : 3306),
    databaseName: c.databaseName || '',
    userName: c.userName || '',
    password: '', // 无密码回显（读接口不含密码）；空 = 保持
  })
  editTestResult.value = null
  editing.value = { name: d.name, type: d.type }
}

const editValid = computed(() => {
  if (!editing.value || editNameExists.value || !editForm.name.trim()) return false
  if (editing.value.type === 'sqlite') return !!editForm.path.trim()
  return !!(editForm.host.trim() && editForm.databaseName.trim() && editForm.userName.trim())
})

async function editSave() {
  if (!editValid.value || editSaving.value) return
  editSaving.value = true
  const config = draftConfig(editForm, editing.value.type)
  const newName = editForm.name.trim() !== editing.value.name ? editForm.name.trim() : undefined
  try {
    const r = await api.updateDataSource(editing.value.name, config, newName)
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

// ---- 编辑模态"测试连接"：对当前表单草稿测试（未保存的改名/参数草稿，WPF 弹窗测试按钮语义；
// name 传旧名——已存项寻址 + 密码留空时沿用已存密码；结果行内展示）----
const editTesting = ref(false)
const editTestResult = ref(null) // null | { ok, detail }
async function testEdit() {
  if (editTesting.value || !editing.value) return
  editTesting.value = true
  editTestResult.value = null
  try {
    const r = await api.testDataSource(
      editing.value.name,
      draftConfig(editForm, editing.value.type),
      editing.value.type
    )
    editTestResult.value = { ok: !!r?.ok, detail: r?.detail || '' }
  } catch (e) {
    const detail = e?.body?.errors?.join('; ') || e?.body?.detail || ''
    editTestResult.value = { ok: false, detail }
  } finally {
    editTesting.value = false
  }
}

// 模态表单回车=保存：共通语义见 utils/formEnter.js（添加/编辑模态各自传保存函数；
// 与保存按钮同守卫——校验未过/保存中不动作）

// ---- sqlite 路径"浏览…"（⑱A）：添加/编辑两模态共用（写目标由调用方传入） ----
// filter 照抄 WPF SqliteSettingViewModel.cs:103；404=用户取消静默；
// 请求寿命 = 用户开着对话框的时间（browsing 期间两模态的按钮同禁用，全局单飞）
const sqliteBrowsing = ref(false)
async function pickSqlitePath(form) {
  if (sqliteBrowsing.value) return
  sqliteBrowsing.value = true
  try {
    const resp = await api.pickFile('SqliteSource Database|*.db', { path: form.path })
    if (resp?.path) form.path = resp.path
  } catch (e) {
    if (e?.status !== 404) message.error(t('settings.r.pickFailed'))
  } finally {
    sqliteBrowsing.value = false
  }
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

// ---- Esc 链：模态开着时捕获截停只关模态（SettingsView 返回导航让位）----
bindModalEsc([
  { isOpen: () => adding.value, close: () => (adding.value = false) },
  { isOpen: () => !!editing.value, close: () => (editing.value = null) },
])
</script>

<template>
  <div class="group">
    <!-- 列表标题行：添加按钮右对齐（与 RunnerGroup 默认运行器行同款 grid/justify-self 模式）。
         (?) 帮助链接：WPF DataSourceView.xaml:255-260 添加菜单旁
         (?) → 团队共享文档（url 照抄 WPF） -->
    <div class="toolbar">
      <span class="add-wrap">
        <n-button size="small" type="primary" @click="openAdd">{{ t('settings.d.add') }}</n-button>
        <HelpLink href="https://1remote.github.io/usage/team/team-sharing/" />
      </span>
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

    <!-- 添加模态：类型三选 + 动态表单；名称即时查重；测试连接对草稿发起（见文件头） -->
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
          <div class="type-wrap">
            <n-select
              size="small"
              :value="addForm.type"
              :options="typeOptions.map((o) => ({ value: o.value, label: o.label.value }))"
              @update:show="shield"
              @update:value="onAddTypeChange"
            />
            <!-- 在线数据库帮助：WPF 添加菜单的 MySQL/PostgreSQL 项各带
                 (?) → 在线数据库文档（DataSourceView.xaml:222-243，url 照抄）；sqlite 为本地
                 文件无此链接，WPF 同款（菜单里只有两项带 (?)） -->
            <HelpLink
              v-if="addForm.type === 'mysql' || addForm.type === 'pgsql'"
              href="https://1remote.github.io/usage/database/use-online-database/"
            />
          </div>
        </div>
        <div class="f-row">
          <label>{{ t('settings.d.name') }}</label>
          <div>
            <n-input
              size="small"
              v-model:value="addForm.name"
              :status="addNameExists ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
            <p v-if="addNameExists" class="f-err">{{ t('settings.r.nameExists', { name: addForm.name.trim() }) }}</p>
            <p v-else-if="addForm.type === 'sqlite'" class="f-hint">{{ t('settings.d.nameHint') }}</p>
          </div>
        </div>
        <div v-if="addForm.type === 'sqlite'" class="f-row">
          <label>{{ t('settings.d.f.path') }}</label>
          <!-- 路径 + "浏览…"（⑱A：WPF SqliteSettingView Select 按钮的 web 平价） -->
          <div class="path-wrap">
            <n-input size="small" v-model:value="addForm.path" :input-props="{ spellcheck: false }" />
            <button class="act-btn" type="button" :disabled="sqliteBrowsing" @click="pickSqlitePath(addForm)">
              {{ t('settings.r.f.browse') }}
            </button>
          </div>
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
        <!-- 草稿测试结果行（batch10 Task B #8）：成功/失败 + 后端 detail（校验错误串或连接错误） -->
        <p v-if="addTestResult" class="f-note" :class="{ ok: addTestResult.ok }">
          {{
            addTestResult.ok
              ? t('settings.d.testOk')
              : t('settings.d.testFailed') + (addTestResult.detail ? ` (${addTestResult.detail})` : '')
          }}
        </p>
      </div>
      <template #footer>
        <div class="modal-actions">
          <!-- 测试连接（对草稿，不经保存）：与保存同守卫（必填齐全/无重名才可测，避免空密码连测） -->
          <n-button size="small" class="test-btn" :disabled="!addValid" :loading="addTesting" @click="testAdd">
            {{ t('settings.d.test') }}
          </n-button>
          <n-button size="small" @click="adding = false">{{ t('editor.cancel') }}</n-button>
          <n-button size="small" type="primary" :disabled="!addValid" :loading="addSaving" @click="addSave">
            {{ t('settings.save') }}
          </n-button>
        </div>
      </template>
    </n-modal>

    <!-- 编辑模态：名称可改（即时查重）；密码留空 = 保持原密码 -->
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
        <!-- 名称行（batch10 Task B #7）：WPF 弹窗 Name 编辑平价（改名走 PUT body.name） -->
        <div class="f-row">
          <label>{{ t('settings.d.name') }}</label>
          <div>
            <n-input
              size="small"
              v-model:value="editForm.name"
              :status="editNameExists ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
            <p v-if="editNameExists" class="f-err">{{ t('settings.r.nameExists', { name: editForm.name.trim() }) }}</p>
          </div>
        </div>
        <div v-if="editing?.type === 'sqlite'" class="f-row">
          <label>{{ t('settings.d.f.path') }}</label>
          <!-- 路径 + "浏览…"（⑱A，同添加模态） -->
          <div class="path-wrap">
            <n-input size="small" v-model:value="editForm.path" :input-props="{ spellcheck: false }" />
            <button class="act-btn" type="button" :disabled="sqliteBrowsing" @click="pickSqlitePath(editForm)">
              {{ t('settings.r.f.browse') }}
            </button>
          </div>
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
        <!-- 草稿测试结果行（同添加模态） -->
        <p v-if="editTestResult" class="f-note" :class="{ ok: editTestResult.ok }">
          {{
            editTestResult.ok
              ? t('settings.d.testOk')
              : t('settings.d.testFailed') + (editTestResult.detail ? ` (${editTestResult.detail})` : '')
          }}
        </p>
      </div>
      <template #footer>
        <div class="modal-actions">
          <!-- 测试连接（对草稿，不经保存）：必填齐全/无重名才可测 -->
          <n-button size="small" class="test-btn" :disabled="!editValid" :loading="editTesting" @click="testEdit">
            {{ t('settings.d.test') }}
          </n-button>
          <n-button size="small" @click="editing = null">{{ t('editor.cancel') }}</n-button>
          <n-button size="small" type="primary" :disabled="!editValid" :loading="editSaving" @click="editSave">
            {{ t('settings.save') }}
          </n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.group {
  /* 统一设置内容宽（SettingsView.s-body 的 --settings-content-w 穿透继承）；内部网格不动 */
  width: min(100%, var(--settings-content-w));
}
.toolbar {
  /* 单列 1fr + 按钮 justify-self 推到行右端（与 RunnerGroup 的默认运行器行同款模式） */
  display: grid;
  grid-template-columns: 1fr;
  align-items: center;
  margin-bottom: 12px;
}
/* 添加按钮与旁侧 (?) 成组右对齐（grid 单列 + justify-self 端对齐的组形态） */
.add-wrap {
  justify-self: end;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
/* 添加模态类型行：下拉 + (?) 帮助并排（f-row 的控件列原为裸控件） */
.type-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}
.type-wrap .n-select {
  flex: 1 1 auto;
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
/* sqlite 路径行：输入框 + "浏览…"按钮（⑱A；RunnerCard exe 行同款形态） */
.path-wrap {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}
.path-wrap .n-input {
  flex: 1 1 auto;
  min-width: 0;
}
.act-btn {
  flex: 0 0 auto;
  height: 28px;
  padding: 0 10px;
  border: 1px solid var(--border);
  border-radius: 3px;
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: 0.8846rem;
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}
.act-btn:hover:not(:disabled) {
  border-color: var(--accent);
  color: var(--accent-text);
  background: var(--bg-hover);
}
.act-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.f-hint {
  margin: 4px 0 0;
  font-size: 0.8462rem;
  color: var(--text-4);
}
/* 名称重名即时提示（batch10 Task B #6）：输入框下方红字 */
.f-err {
  margin: 4px 0 0;
  font-size: 0.8462rem;
  color: var(--danger);
}
.f-note {
  margin: 2px 0 0;
  font-size: 0.8846rem;
  color: var(--warning);
}
/* 草稿测试结果：成功转 success 色（失败沿用 warning；校验错误也走失败色） */
.f-note.ok {
  color: var(--success);
}
/* footer 测试按钮推到左侧（与取消/保存分列两端） */
.modal-actions .test-btn {
  margin-right: auto;
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
