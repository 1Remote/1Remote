<script setup>
/**
 * 数据源分组（Plan 3 Task 6，spec §6）：卡片列表（状态点/类型/计数/配置摘要）+ 添加/编辑模态 +
 * 测试连接 + 删除（409 keepServers 二次确认）。
 *
 * - 列表来自 useServers 共享态（/api/datasources 含 config 连接参数视图，无密码）；
 *   变更后 reload() 主动刷新（SSE 兜底之外，数据源增删不必然触发 OnReloadAll）。
 * - 测试连接：卡片按钮按已存参数测（POST /api/datasources/{name}/test）；新建/编辑模态的
 *   "测试连接"按钮对**表单草稿**测试（WPF Mysql/Pgsql 弹窗测试按钮语义：构造临时配置
 *   直接测试，不经保存——未保存名查不到已存项，后端按 body.type 构造临时实例；
 *   密码留空时旧名可寻址到已存源则沿用其密码，与编辑弹窗"密码留空=保持"一致）。
 *   结果行内显示（成功/失败+详情），不再要求"先保存再测"。
 * - 名称（WPF 弹窗平价）：新建模态名称可填（sqlite 可缺省按路径推导）；编辑模态名称
 *   可改（WPF 弹窗 Name 直写 org.DataSourceName；Local 在 web 侧本就不可编辑）。
 *   重名即时提示（computed 比对现有列表，忽略大小写对齐后端 CurrentCultureIgnoreCase，
 *   编辑排除自身；后端最终守卫 POST 409 / PUT 409）。
 * - Local 卡片只读：测试可用；编辑/删除不开放（后端 PUT/DELETE Local 均为 400，SQLite 路径
 *   属安全域外）。
 * - 编辑模态：密码留空 = 保持原密码（后端 PUT 语义：空串跳过赋值），placeholder 注明。
 * - sqlite 路径"浏览…"：WPF SqliteSettingView 的 Select 按钮
 *   （SqliteSettingViewModel.cs:105，filter "SqliteSource Database|*.db" 照抄）的 web
 *   平价——添加/编辑两模态的路径行均带按钮，调 /api/files/pick 弹后端原生对话框，
 *   checkFileExists:false 与 WPF 对齐（round14：库文件不存在可选定路径新建）。路径
 *   本身须为 .db 文件（前端校验标红，见 sqlitePathOk）。
 * - 删除 409 {serverCount}：二段确认——首段普通确认；409 后二段显示仍有的服务器数与
 *   keepServers 语义（服务器留在库文件，重新添加即可找回），确认后带 keepServers=true 重试。
 */
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { confirmDialog } from '../../utils/confirmDialog'
import { api } from '../../api'
import { dsDotClass as dotClass, makeDsDotTitle } from '../../utils/dsTitle'
import { useServers } from '../../composables/useServers'
import { useNow } from '../../composables/useNow'
import { useDraftGuard } from '../../composables/useDraftGuard'
import { useRequiredGuard } from '../../composables/useRequiredGuard'
import { useSettingsEsc } from '../../composables/useSettingsEsc'
import { onFormEnter } from '../../utils/formEnter'
import HelpLink from '../HelpLink.vue'

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()
const { datasources, datasourcesFetchedAt, reload } = useServers()

// 下拉展开计数 + 模态 Esc 截停（Esc 链序见 SettingsView/useSettingsEsc 文件头注释）
const { shield, bindModalEsc } = useSettingsEsc()

// ---- 展示辅助 ----
// K16：卡片状态点悬停 title 走 i18n——单一实现在 utils/dsTitle.js（与树根行/状态栏共用），
// 此前 :title="d.status" 直出英文裸枚举
const dsDotTitle = makeDsDotTitle(t, { now: useNow(), fetchedAt: datasourcesFetchedAt })
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
// 空白草稿工厂：reactive 初始化与 openAdd 复位共用同一份（上次未提交的草稿不带入新会话，
// 与 CredentialVaultGroup.openCreate 同款）
const blankAddForm = () => ({
  type: 'sqlite',
  name: '',
  path: '',
  host: '',
  port: 3306,
  databaseName: '',
  userName: '',
  password: '',
})
const adding = ref(false)
const addForm = reactive(blankAddForm())
const addSaving = ref(false)
// round17：t() 本身响应式，computed 数组直接产 label（此前 label: computed(...) 包装
// + 模板 .map 解包绕远路）
const typeOptions = computed(() =>
  ['sqlite', 'mysql', 'pgsql'].map((v) => ({
    value: v,
    label: t('settings.d.type.' + v),
  }))
)

function openAdd() {
  Object.assign(addForm, blankAddForm())
  addTestResult.value = null
  addSnapshot = { ...addForm } // 草稿脏检查基准（U15）：打开即快照空白草稿
  adding.value = true
}

function onAddTypeChange(v) {
  addForm.type = v
  // 默认端口随类型切换（仅当用户未偏离默认值时跟随，避免覆盖手输）
  const defaults = { mysql: 3306, pgsql: 5432 }
  const other = v === 'mysql' ? 5432 : 3306
  if (addForm.port === other || !addForm.port) addForm.port = defaults[v] ?? 3306
}

// ---- 名称即时查重（新建/编辑共用谓词）：忽略大小写（对齐后端 CurrentCultureIgnoreCase；
// 编辑排除自身原名）——重名即提示 + 禁用保存，后端最终守卫 POST 409 / PUT 409。
// 开态守卫（round11 用户报告，凭据库同款修复）：保存成功关模态的淡出动画期间列表已含
// 新条目，表单残留名撞新条目会闪红字「名称已存在」——模态关闭后不参与查重 ----
const nameExistsAmong = (n, excludeName) =>
  !!n && datasources.value.some((d) => d.name !== excludeName && d.name.trim().toLowerCase() === n.trim().toLowerCase())
const addNameExists = computed(() => (adding.value ? nameExistsAmong(addForm.name, null) : false))
const editNameExists = computed(() => (editing.value ? nameExistsAmong(editForm.name, editing.value.name) : false))

// ---- 端口校验（round11 U17）：1-65535 整数，非法即标红 + 禁用测试/保存——
// 此前端口框是普通文本，"abc"/清空都直通，最终被 Number()||0 当成 0，失败原因
// 迟到且不指向端口 ----
const portValid = (p) => {
  const n = Number(p)
  return Number.isInteger(n) && n >= 1 && n <= 65535
}

// ---- sqlite 路径校验（round14 需求）：库文件路径必须指向 .db 文件（不存在可新建）；
// 填其他路径标红 + 禁用测试/保存（后端 sqlite 构造器对非 .db 路径报错迟到且不直观） ----
const sqlitePathOk = (p) => /\.db$/i.test(String(p || '').trim())

const addValid = computed(() => {
  if (addNameExists.value) return false
  if (addForm.type === 'sqlite') return sqlitePathOk(addForm.path) // name 可缺省（按 path 文件名推导）
  if (!portValid(addForm.port)) return false // U17：端口非法禁用测试/保存
  return !!(
    addForm.name.trim() &&
    addForm.host.trim() &&
    addForm.databaseName.trim() &&
    addForm.userName.trim() &&
    addForm.password
  )
})
const addPortOk = computed(() => portValid(addForm.port))
const addPathOk = computed(() => sqlitePathOk(addForm.path))

// ---- 必填项统一守卫（round14 需求，spec §表单必填项）：未填键集（表单序）——sqlite 仅
// 路径必填（名称按文件名推导）；在线库五项必填（密码编辑态留空=保持，见 editGuard）。
// 端口非法/.db 路径非法/重名不属"未填"——各自行内红字 + 保存钮真 disabled ----
const addMissingKeys = computed(() => {
  if (addForm.type === 'sqlite') return addForm.path.trim() ? [] : ['path']
  const keys = []
  if (!addForm.name.trim()) keys.push('name')
  if (!addForm.host.trim()) keys.push('host')
  if (!addForm.databaseName.trim()) keys.push('databaseName')
  if (!addForm.userName.trim()) keys.push('userName')
  if (!addForm.password) keys.push('password')
  return keys
})
const addReqGuard = useRequiredGuard({ getMissing: () => addMissingKeys.value })
// 保存钮的"非必填类"真禁（在途另由 :loading 语义承担；必填未填走假禁用引导）
const addHardBlocked = computed(
  () => addNameExists.value || (addForm.type === 'sqlite' ? !addPathOk.value : !addPortOk.value)
)

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

// 保存失败 toast 的后端明细（400/409 的 {errors} 串接；无明细则仅通用文案）
const errDetail = (e) => e?.body?.errors?.join('; ') || ''

async function addSave() {
  if (!addValid.value || addSaving.value) return
  addSaving.value = true
  const config = draftConfig(addForm, addForm.type)
  const name = addForm.name.trim() || undefined // sqlite 缺省由后端从 path 文件名推导
  try {
    const r = await api.addDataSource(addForm.type, config, name)
    adding.value = false
    // 保存成功即重置表单（round11 用户报告：淡出动画期间残留已存内容/重查重闪变）：
    // 关闭动画显示的是空白表单，下次打开 openAdd 的复位语义提前到此，无中间形态
    Object.assign(addForm, blankAddForm())
    addSnapshot = { ...addForm }
    addTestResult.value = null
    await reload()
    // 创建即连接失败不回滚（WPF 同款）：如实提示 + 指引到卡片测试
    // （失败档位统一 error，J12：与卡片测试按钮/模态草稿测试同档）
    if (r?.dataSource && r.dataSource.status === 'connected') message.success(t('settings.d.testOk'))
    else message.error(t('settings.d.testFailed') + (r?.connectError ? ` (${r.connectError})` : ''))
  } catch (e) {
    const detail = errDetail(e)
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  } finally {
    addSaving.value = false
  }
}

// ---- 新建/编辑模态"测试连接"：对表单草稿测试（不经保存，WPF Mysql/Pgsql 弹窗测试按钮
// 语义——未保存名后端按 type 构造临时实例；密码留空且旧名可寻址到已存源则沿用其密码，
// 与编辑弹窗"密码留空=保持"一致）。结果行内展示（成功/失败+detail），不抛错 ----
async function testDraft(name, form, type) {
  try {
    const r = await api.testDataSource(name, draftConfig(form, type), type)
    return { ok: !!r?.ok, detail: r?.detail || '' }
  } catch (e) {
    return { ok: false, detail: e?.body?.errors?.join('; ') || e?.body?.detail || '' }
  }
}

const addTesting = ref(false)
const addTestResult = ref(null) // null | { ok, detail }
async function testAdd() {
  if (addTesting.value) return
  addTesting.value = true
  addTestResult.value = null
  try {
    // sqlite 新建名称可缺省：以 path 作寻址名（未保存项后端按 body 配置构造，名仅辅助寻址）
    addTestResult.value = await testDraft(addForm.name.trim() || addForm.path.trim(), addForm, addForm.type)
  } finally {
    addTesting.value = false
  }
}

// ---- 编辑模态（非 Local）：名称可改（WPF 弹窗平价）；密码留空 = 保持 ----
const editing = ref(null) // null | { name, type }
const editForm = reactive({ name: '', path: '', host: '', port: 3306, databaseName: '', userName: '', password: '' })
const editSaving = ref(false)
const showEdit = computed(() => !!editing.value) // 编辑模态开合（关闭走 requestEditClose 脏检查，非 v-model）

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
  editSnapshot = { ...editForm } // 草稿脏检查基准（U15）
  editing.value = { name: d.name, type: d.type }
}

const editValid = computed(() => {
  if (!editing.value || editNameExists.value || !editForm.name.trim()) return false
  if (editing.value.type === 'sqlite') return sqlitePathOk(editForm.path)
  if (!portValid(editForm.port)) return false // U17：端口非法禁用测试/保存
  return !!(editForm.host.trim() && editForm.databaseName.trim() && editForm.userName.trim())
})
const editPortOk = computed(() => portValid(editForm.port))
const editPathOk = computed(() => sqlitePathOk(editForm.path))

// 必填守卫（同添加模态；编辑态密码留空=保持原值，不在必填列）
const editMissingKeys = computed(() => {
  if (!editing.value) return []
  if (editing.value.type === 'sqlite') return editForm.path.trim() ? [] : ['path']
  const keys = []
  if (!editForm.name.trim()) keys.push('name')
  if (!editForm.host.trim()) keys.push('host')
  if (!editForm.databaseName.trim()) keys.push('databaseName')
  if (!editForm.userName.trim()) keys.push('userName')
  return keys
})
const editReqGuard = useRequiredGuard({ getMissing: () => editMissingKeys.value })
const editHardBlocked = computed(
  () => editNameExists.value || (editing.value?.type === 'sqlite' ? !editPathOk.value : !editPortOk.value)
)

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
    else message.error(t('settings.d.testFailed') + (r?.connectError ? ` (${r.connectError})` : ''))
  } catch (e) {
    const detail = errDetail(e)
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  } finally {
    editSaving.value = false
  }
}

// ---- 编辑模态"测试连接"：对当前表单草稿测试（未保存的改名/参数草稿）；
// name 传旧名——已存项寻址 + 密码留空时沿用已存密码 ----
const editTesting = ref(false)
const editTestResult = ref(null) // null | { ok, detail }
async function testEdit() {
  if (editTesting.value || !editing.value) return
  editTesting.value = true
  editTestResult.value = null
  try {
    editTestResult.value = await testDraft(editing.value.name, editForm, editing.value.type)
  } finally {
    editTesting.value = false
  }
}

// 模态表单回车=保存：共通语义见 utils/formEnter.js（添加/编辑模态各自传保存函数；
// 与保存按钮同守卫——校验未过/保存中不动作）

// ---- sqlite 路径"浏览…"（⑱A）：添加/编辑两模态共用（写目标由调用方传入） ----
// filter 照抄 WPF SqliteSettingViewModel.cs:105；checkFileExists:false 对齐 WPF 的
// CmdSelectDbPath（库文件不存在时允许选定路径新建，round14 需求）；404=用户取消静默；
// 请求寿命 = 用户开着对话框的时间（browsing 期间两模态的按钮同禁用，全局单飞）
const sqliteBrowsing = ref(false)
async function pickSqlitePath(form) {
  if (sqliteBrowsing.value) return
  sqliteBrowsing.value = true
  try {
    const resp = await api.pickFile('SqliteSource Database|*.db', { path: form.path, checkFileExists: false })
    if (resp?.path) form.path = resp.path
  } catch (e) {
    if (e?.status !== 404) message.error(t('settings.r.pickFailed'))
  } finally {
    sqliteBrowsing.value = false
  }
}

// ---- 删除：二段确认（409 keepServers 重试）。autoFocus:false——删除确认禁 Enter 误触（Esc 仍可取消）----
function onDelete(d) {
  confirmDialog(dialog, {
    danger: true,
    title: t('settings.d.deleteTitle'),
    content: t('settings.d.deleteConfirm', { name: d.name }),
    // 删除类确认统一形态：无图标 + 红 positive（folderOps 文件头策略）
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    onPositive: async () => {
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
  confirmDialog(dialog, {
    danger: true,
    title: t('settings.d.deleteTitle'),
    // 删除类确认统一形态：无图标 + 红 positive（folderOps 文件头策略）
    content:
      t('settings.d.deleteConfirm', { name: d.name }) +
      ' ' +
      t('settings.d.deleteHasServers', { n: serverCount }) +
      ' ' +
      t('settings.d.keepServersHint'),
    positiveText: t('settings.d.keepServers'),
    negativeText: t('editor.cancel'),
    onPositive: async () => {
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

// ---- 模态草稿守卫（round13 需求：统一关闭语义，取代 round12 U15 的遮罩无条件禁用）：
// 干净态点遮罩直接关；有输入点遮罩不关、提示走「保存/取消/关闭」按钮；
// ✕/取消/Esc 有输入时先确认丢弃。共享实现见 composables/useDraftGuard.js ----
let addSnapshot = {} // 打开/保存成功时的表单快照（脏检查基准）
let editSnapshot = {}
const formDirty = (form, snap) => Object.keys(form).some((k) => String(form[k] ?? '') !== String(snap[k] ?? ''))
const addGuard = useDraftGuard({
  isDirty: () => formDirty(addForm, addSnapshot),
  doClose: () => {
    adding.value = false
  },
  isBusy: () => addSaving.value,
  dialog,
  message,
  t,
})
const editGuard = useDraftGuard({
  isDirty: () => formDirty(editForm, editSnapshot),
  doClose: () => {
    editing.value = null
  },
  isBusy: () => editSaving.value,
  dialog,
  message,
  t,
})

// ---- Esc 链：模态开着时捕获截停只关模态（SettingsView 返回导航让位）；
// 关闭走草稿守卫（干净即关 / 有输入确认丢弃）----
bindModalEsc([
  { isOpen: () => adding.value, close: () => addGuard.requestClose() },
  { isOpen: () => !!editing.value, close: () => editGuard.requestClose() },
])
</script>

<template>
  <div class="group">
    <!-- 列表标题行：添加按钮右对齐（与 RunnerGroup 默认运行器行同款 grid/justify-self 模式）。
         (?) 帮助链接：WPF DataSourceView.xaml:255-260 添加菜单旁
         (?) → 团队共享文档（url 照抄 WPF） -->
    <div class="toolbar">
      <span class="add-wrap">
        <n-button size="small" type="primary" ghost @click="openAdd">{{ t('settings.d.add') }}</n-button>
        <HelpLink href="https://1remote.github.io/usage/team/team-sharing/" />
      </span>
    </div>

    <div v-if="!datasources.length" class="empty">{{ t('tree.noDsHint') }}</div>
    <div v-else class="cards">
      <div v-for="d in datasources" :key="d.name" class="ds-card">
        <div class="card-main">
          <span class="dot" :class="dotClass(d.status)" :title="dsDotTitle(d)"></span>
          <span class="ds-name" :title="d.name">{{ d.name }}</span>
          <span class="type-badge">{{ typeLabel(d) }}</span>
          <span v-if="d.writable === false" class="ro-badge">{{ t('settings.d.readOnly') }}</span>
          <span class="srv-count" :title="t('tagm.col.count')">{{ d.serverCount }}</span>
        </div>
        <div class="card-config" :title="configSummary(d)">{{ configSummary(d) || '—' }}</div>
        <div class="card-actions">
          <!-- U16：测试进行中整卡锁定（编辑/删除禁用）——测试跑的是发起时的旧配置，
               期间改配置会让弹回的结果与当前配置对不上 -->
          <button class="card-act" type="button" :disabled="testing === d.name" @click="onTest(d)">
            {{ testing === d.name ? t('settings.d.testing') : t('settings.d.test') }}
          </button>
          <!-- Local 只读：路径/删除不开放（title 说明） -->
          <button
            class="card-act"
            type="button"
            :disabled="isLocal(d) || testing === d.name"
            :title="isLocal(d) ? t('settings.d.localHint') : t('settings.d.edit')"
            @click="openEdit(d)"
          >
            {{ t('settings.d.edit') }}
          </button>
          <button
            class="card-act"
            type="button"
            :disabled="isLocal(d) || testing === d.name"
            :title="isLocal(d) ? t('settings.d.localHint') : t('editor.deleteYes')"
            @click="onDelete(d)"
          >
            {{ t('editor.deleteYes') }}
          </button>
        </div>
      </div>
    </div>

    <!-- 添加模态：类型三选 + 动态表单；名称即时查重；测试连接对草稿发起（见文件头）。
         遮罩关闭禁用 + Esc/✕/取消走脏检查（U15：草稿防丢，见 requestAddClose） -->
    <n-modal
      :show="adding"
      preset="card"
      :title="t('settings.d.addTitle')"
      :bordered="false"
      :style="{ width: 'min(480px, 92vw)' }"
      :mask-closable="false"
      role="dialog"
      aria-modal="true"
      @update:show="addGuard.requestClose"
      @mask-click="addGuard.onMaskClick"
    >
      <div class="form" @keydown="onFormEnter($event, () => addReqGuard.trySave(addSave))">
        <div class="s-form-row">
          <label>{{ t('settings.d.type') }}</label>
          <div class="type-wrap">
            <n-select
              size="small"
              :value="addForm.type"
              :options="typeOptions"
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
        <div
          class="s-form-row"
          :class="{ 'req-hot': addReqGuard.isHot('name') }"
          :data-req-key="addForm.type !== 'sqlite' ? 'name' : undefined"
        >
          <!-- K9：必填星标（与凭据库「名称 *」同语言）；sqlite 名称可缺省（按路径文件名推导） -->
          <label>{{ t('settings.d.name') }}<span v-if="addForm.type !== 'sqlite'" class="req-star">*</span></label>
          <div>
            <n-input
              size="small"
              v-model:value="addForm.name"
              :status="addNameExists ? 'error' : addReqGuard.isHot('name') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
            <p v-if="addNameExists" class="f-err">{{ t('settings.r.nameExists', { name: addForm.name.trim() }) }}</p>
            <p v-else-if="addForm.type === 'sqlite'" class="f-hint">{{ t('settings.d.nameHint') }}</p>
          </div>
        </div>
        <div
          v-if="addForm.type === 'sqlite'"
          class="s-form-row"
          :class="{ 'req-hot': addReqGuard.isHot('path') }"
          data-req-key="path"
        >
          <label>{{ t('settings.d.f.path') }}<span class="req-star">*</span></label>
          <!-- 路径 + "浏览…"（⑱A：WPF SqliteSettingView Select 按钮的 web 平价）。
               round14：非 .db 路径标红（文件不存在不算错——sqlite 可新建库文件） -->
          <div>
            <div class="path-wrap">
              <n-input
                size="small"
                v-model:value="addForm.path"
                :status="!addPathOk ? 'error' : addReqGuard.isHot('path') ? 'error' : undefined"
                :input-props="{ spellcheck: false }"
              />
              <button class="act-btn" type="button" :disabled="sqliteBrowsing" @click="pickSqlitePath(addForm)">
                {{ t('settings.r.f.browse') }}
              </button>
            </div>
            <p v-if="!addPathOk" class="f-err">{{ t('settings.d.pathInvalid') }}</p>
          </div>
        </div>
        <template v-else>
          <div class="s-form-row" :class="{ 'req-hot': addReqGuard.isHot('host') }" data-req-key="host">
            <label>{{ t('settings.d.f.host') }}<span class="req-star">*</span></label>
            <n-input
              size="small"
              v-model:value="addForm.host"
              :status="addReqGuard.isHot('host') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
          </div>
          <div class="s-form-row">
            <label>{{ t('settings.d.f.port') }}</label>
            <!-- U17：端口非法即标红 + 禁用测试/保存（此前任意文本直通，被当成 0） -->
            <div>
              <n-input
                size="small"
                v-model:value="addForm.port"
                :status="addForm.type !== 'sqlite' && !addPortOk ? 'error' : undefined"
                :input-props="{ spellcheck: false }"
              />
              <p v-if="addForm.type !== 'sqlite' && !addPortOk" class="f-err">{{ t('settings.d.portInvalid') }}</p>
            </div>
          </div>
          <div class="s-form-row" :class="{ 'req-hot': addReqGuard.isHot('databaseName') }" data-req-key="databaseName">
            <label>{{ t('settings.d.f.database') }}<span class="req-star">*</span></label>
            <n-input
              size="small"
              v-model:value="addForm.databaseName"
              :status="addReqGuard.isHot('databaseName') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
          </div>
          <div class="s-form-row" :class="{ 'req-hot': addReqGuard.isHot('userName') }" data-req-key="userName">
            <label>{{ t('settings.d.f.user') }}<span class="req-star">*</span></label>
            <n-input
              size="small"
              v-model:value="addForm.userName"
              :status="addReqGuard.isHot('userName') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
          </div>
          <div class="s-form-row" :class="{ 'req-hot': addReqGuard.isHot('password') }" data-req-key="password">
            <label>{{ t('settings.d.f.password') }}<span class="req-star">*</span></label>
            <n-input
              size="small"
              type="password"
              show-password-on="click"
              v-model:value="addForm.password"
              :status="addReqGuard.isHot('password') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
          </div>
        </template>
        <!-- 草稿测试结果行：成功/失败 + 后端 detail（校验错误串或连接错误） -->
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
          <n-button size="small" @click="addGuard.requestClose">{{ t('editor.cancel') }}</n-button>
          <!-- round14 统一必填处理：未填=假禁用观感+旁侧提示，强点走守卫聚焦引导；
               真 disabled 只留给在途（loading）/重名/端口与路径非法 -->
          <span v-if="addReqGuard.blocked" class="req-hint">{{ t('modal.requiredHint') }}</span>
          <n-button
            size="small"
            type="primary"
            ghost
            :class="{ 'req-blocked': addReqGuard.blocked }"
            :disabled="addHardBlocked"
            :loading="addSaving"
            @click="addReqGuard.trySave(addSave)"
          >
            {{ t('settings.save') }}
          </n-button>
        </div>
      </template>
    </n-modal>

    <!-- 编辑模态：名称可改（即时查重）；密码留空 = 保持原密码。遮罩关闭禁用 + 脏检查（U15） -->
    <n-modal
      :show="showEdit"
      preset="card"
      :title="t('settings.d.editTitle')"
      :bordered="false"
      :style="{ width: 'min(480px, 92vw)' }"
      :mask-closable="false"
      role="dialog"
      aria-modal="true"
      @update:show="editGuard.requestClose"
      @mask-click="editGuard.onMaskClick"
    >
      <div class="form" @keydown="onFormEnter($event, () => editReqGuard.trySave(editSave))">
        <!-- 名称行：WPF 弹窗 Name 编辑平价（改名走 PUT body.name）；编辑态名称恒必填 -->
        <div class="s-form-row" :class="{ 'req-hot': editReqGuard.isHot('name') }" data-req-key="name">
          <label>{{ t('settings.d.name') }}<span class="req-star">*</span></label>
          <div>
            <n-input
              size="small"
              v-model:value="editForm.name"
              :status="editNameExists ? 'error' : editReqGuard.isHot('name') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
            <p v-if="editNameExists" class="f-err">{{ t('settings.r.nameExists', { name: editForm.name.trim() }) }}</p>
          </div>
        </div>
        <div
          v-if="editing?.type === 'sqlite'"
          class="s-form-row"
          :class="{ 'req-hot': editReqGuard.isHot('path') }"
          data-req-key="path"
        >
          <label>{{ t('settings.d.f.path') }}<span class="req-star">*</span></label>
          <!-- 路径 + "浏览…"（⑱A，同添加模态）；round14：非 .db 路径标红 -->
          <div>
            <div class="path-wrap">
              <n-input
                size="small"
                v-model:value="editForm.path"
                :status="!editPathOk ? 'error' : editReqGuard.isHot('path') ? 'error' : undefined"
                :input-props="{ spellcheck: false }"
              />
              <button class="act-btn" type="button" :disabled="sqliteBrowsing" @click="pickSqlitePath(editForm)">
                {{ t('settings.r.f.browse') }}
              </button>
            </div>
            <p v-if="!editPathOk" class="f-err">{{ t('settings.d.pathInvalid') }}</p>
          </div>
        </div>
        <template v-else>
          <div class="s-form-row" :class="{ 'req-hot': editReqGuard.isHot('host') }" data-req-key="host">
            <label>{{ t('settings.d.f.host') }}<span class="req-star">*</span></label>
            <n-input
              size="small"
              v-model:value="editForm.host"
              :status="editReqGuard.isHot('host') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
          </div>
          <div class="s-form-row">
            <label>{{ t('settings.d.f.port') }}</label>
            <!-- U17：端口非法即标红 + 禁用测试/保存 -->
            <div>
              <n-input
                size="small"
                v-model:value="editForm.port"
                :status="editing?.type !== 'sqlite' && !editPortOk ? 'error' : undefined"
                :input-props="{ spellcheck: false }"
              />
              <p v-if="editing?.type !== 'sqlite' && !editPortOk" class="f-err">{{ t('settings.d.portInvalid') }}</p>
            </div>
          </div>
          <div
            class="s-form-row"
            :class="{ 'req-hot': editReqGuard.isHot('databaseName') }"
            data-req-key="databaseName"
          >
            <label>{{ t('settings.d.f.database') }}<span class="req-star">*</span></label>
            <n-input
              size="small"
              v-model:value="editForm.databaseName"
              :status="editReqGuard.isHot('databaseName') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
          </div>
          <div class="s-form-row" :class="{ 'req-hot': editReqGuard.isHot('userName') }" data-req-key="userName">
            <label>{{ t('settings.d.f.user') }}<span class="req-star">*</span></label>
            <n-input
              size="small"
              v-model:value="editForm.userName"
              :status="editReqGuard.isHot('userName') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
          </div>
          <div class="s-form-row">
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
          <n-button size="small" @click="editGuard.requestClose">{{ t('editor.cancel') }}</n-button>
          <!-- round14 统一必填处理（同添加模态） -->
          <span v-if="editReqGuard.blocked" class="req-hint">{{ t('modal.requiredHint') }}</span>
          <n-button
            size="small"
            type="primary"
            ghost
            :class="{ 'req-blocked': editReqGuard.blocked }"
            :disabled="editHardBlocked"
            :loading="editSaving"
            @click="editReqGuard.trySave(editSave)"
          >
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
/* 添加模态类型行：下拉 + (?) 帮助并排（s-form-row 的控件列原为裸控件） */
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
  font-size: var(--fs-body);
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
  border-radius: var(--radius-box);
  background: var(--bg-elevated);
}
.card-main {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.dot {
  /* 数据源状态点 6px 紧凑档（G19）：与 SideTree 根行/底部状态栏同值同义；
     服务器级状态点为 8px（StatusDot），按宿主行高一档区分 */
  flex: 0 0 6px;
  width: 6px;
  height: 6px;
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
  font-size: var(--fs-body);
  color: var(--text-1);
}
.type-badge,
.ro-badge {
  flex: 0 0 auto;
  border: 1px solid var(--border);
  border-radius: var(--radius-xs);
  padding: 1px 6px; /* 微 badge 内距归偶（G22，RunnerCard .badge 同值） */
  font-size: var(--fs-micro);
  color: var(--text-4);
}
.ro-badge {
  border-color: var(--warning);
  color: var(--warning);
}
.srv-count {
  flex: 0 0 auto;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
.card-config {
  grid-column: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
.card-actions {
  grid-row: 1 / span 2;
  grid-column: 2;
  display: flex;
  gap: 6px;
}
.card-act {
  /* 行内文字小钮族的带边框变体（G18）：无边框基型是 theme.css 全局 .act（round11 A1
     收敛）；卡片动作钮带边框、caption 字号。.act-btn 为上传型变体（见下）。
     高度走 --ctrl-h-s 令牌（J22：值同 24px，字面值改令牌随档联动） */
  height: var(--ctrl-h-s);
  padding: 0 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-2);
  font-size: var(--fs-caption);
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}
.card-act:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.card-act:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}

/* ---- 模态表单 ---- */
.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
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
  height: var(--ctrl-h-m);
  padding: 0 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: var(--fs-caption);
  line-height: 1;
  cursor: pointer;
  white-space: nowrap;
}
.act-btn:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.act-btn:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
.f-hint {
  margin: 4px 0 0;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
/* 名称重名即时提示：输入框下方红字 */
.f-err {
  margin: 4px 0 0;
  font-size: var(--fs-caption);
  color: var(--danger);
}
.f-note {
  margin: 2px 0 0;
  font-size: var(--fs-caption);
  color: var(--danger);
}
/* 草稿测试结果：成功转 success 色（失败 danger——J12 与卡片/保存后提示的失败档统一；
   校验错误也走失败色） */
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
