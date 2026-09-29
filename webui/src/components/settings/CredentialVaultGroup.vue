<script setup>
/**
 * 凭据库分组（spec §6；表单对齐凭据库模型）：
 * GET /api/credentials?ds= 表格 + 新建/编辑模态（模态内 👁 明文 reveal 流）+ 删除确认（引用数警告）。
 *
 * - 表单字段集 = WPF 凭据库弹窗（CredentialVaultViewModel 以 showHost:false 复用
 *   AlternativeCredentialEditView）：Name/UserName/Password/PrivateKeyPath 四项，
 *   顺序同 WPF（Name → UserName → Password → 私钥路径）。Address/Port 两项已去掉——
 *   凭据库不使用这两个字段（Dapper UpdateCredential 落库前强制清空），web 表单原先
 *   复用备用凭据子表单的这两项属多余；后端 DTO 字段保留仅为 API 兼容，前端不再提交。
 * - 数据源过滤：n-select（useServers 共享态），切换即重载列表；只读数据源禁用 CRUD 按钮
 *   （后端也会前置拦截，这里只做 UI 预防）。
 * - 列表 DTO 不含密码/私钥路径的值（后端安全红线），仅含存在性布尔 hasPwd/hasKeyPath
 *   （加密不改空串性，密文非空 ⇔ 明文非空）——明文只能走编辑模态内的 reveal（round14
 *   需求：列表操作列不再提供明文查看，只留 编辑/删除）。列表列与 WPF 凭据库表格对齐
 *   （名称/用户名/操作），另加 web 侧引用计数列。
 * - 编辑模态的密码/私钥路径（"正常行为"对齐）：
 *   按存在性布尔预填：存在 → 掩码串 MASK（不自动 reveal——那会在打开模态时弹验证）；
 *   不存在 → 空输入框（如实显示无密码，round9.1 用户报告修复）。
 *   点 👁 调 reveal（复用行级同一端点与 30s 免验证窗口）回填真实值后可编辑。
 *   保存语义（后端 Update 对两字段 null=保持、空串=清除、非空=新值）：
 *   未 reveal 且值仍为 MASK → 提交 null（保持）；reveal 后未改 → 提交原值（等价保持）；
 *   改过/清空 → 提交新值/空串（空串=显式清除）。
 *   密码/私钥二选一（WPF 弹窗 IsUsePrivateKey 复选框的 web 形态）：segmented 切换
 *   仅切换展示（ed-seg 样式，EditorDrawer 凭据组同款），保存只提交可见侧——隐藏侧
 *   编辑态提交 null（保持原值）、新建态提交空串（无）。与 WPF"保存时清空另一侧"
 *   不同：web 列表无从预填，默认侧是猜测（密码），切换即清会把仅改名/仅换私钥的
 *   保存变成静默清库，故取保守语义（显式清除走"reveal 后清空再保存"）。
 *   reveal 成功后若密码侧为空且私钥侧非空则自动切到私钥侧（WPF 编辑打开时
 *   org.PrivateKeyPath 非空默认勾选私钥的对齐；仅用户未手动切换过时应用）。
 *   私钥路径行带"浏览…"按钮（api.pickFile，filter 照抄 WPF 弹窗 ppk|*.*）。
 * - 模态的 Esc：捕获阶段截停（与 IconPicker 同款）——SettingsView 的 window 级 Esc 返回链
 *   不应因"关模态"误触导航；n-select 的展开计数走 settingsEscShield（与 GeneralGroup 同款）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { confirmDialog } from '../../utils/confirmDialog'
import { api } from '../../api'
import { useServers } from '../../composables/useServers'
import { useDraftGuard } from '../../composables/useDraftGuard'
import { useRequiredGuard } from '../../composables/useRequiredGuard'
import { useSettingsEsc } from '../../composables/useSettingsEsc'
import { onFormEnter } from '../../utils/formEnter'
import { saveErrorText } from '../../utils/formError'

const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()
const { datasources } = useServers()

// 下拉展开计数 + 模态 Esc 截停（Esc 链序见 SettingsView/useSettingsEsc 文件头注释）
const { shield, bindModalEsc } = useSettingsEsc()

// ---- 数据源过滤（默认 Local）----
const ds = ref('Local')
const dsOptions = computed(() => datasources.value.map((d) => ({ label: d.name, value: d.name })))
const isReadOnly = computed(() => datasources.value.find((d) => d.name === ds.value)?.writable === false)

// ---- 列表 ----
const credentials = ref([])
const loading = ref(false)
const loadError = ref(false)
let loadGen = 0 // 乱序完成保护：快速切数据源时旧响应不覆盖新数据源

async function load() {
  const my = ++loadGen
  loading.value = true
  loadError.value = false
  try {
    const r = await api.getCredentials(ds.value)
    if (my !== loadGen) return
    credentials.value = r.credentials || []
  } catch {
    if (my === loadGen) loadError.value = true
  } finally {
    if (my === loadGen) loading.value = false
  }
}
onMounted(load)
watch(ds, () => {
  load()
})

// ---- 新建/编辑模态 ----
const editing = ref(null) // null=关 | { mode: 'create' } | { mode: 'edit', name }
// round17：改回只读 computed——模板用 :show + @update:show="editGuard.requestClose"（非
// v-model），可写 set 分支永不执行；关闭路径统一走 editGuard（草稿确认）而不是裸赋值
const showEdit = computed(() => !!editing.value)
// 编辑态未 reveal 前密码/私钥路径的掩码占位（与行级 reveal 的 8 点掩码同观感）；
// 保存时"值仍为掩码且未 reveal"映射为 null（后端语义 null=保持原值）
const SECRET_MASK = '••••••••'
// 表单字段集与 WPF 凭据库弹窗一致：Name/UserName/Password/PrivateKeyPath，
// 不含 Address/Port（凭据库不使用，后端落库前本就清空）
const form = reactive({ name: '', userName: '', password: '', privateKeyPath: '' })
const showPwd = ref(false)
const saving = ref(false)
// 密码/私钥二选一（展示切换）+ 编辑态掩码 reveal 回填 + 私钥浏览
const authMode = ref('password') // 'password' | 'key'：仅控制展示哪一侧，保存语义见 save()
let segTouched = false // 用户手动切换过 segmented 后，reveal 回填不再自动换侧
const secretsLoaded = ref(false) // 编辑态是否已 reveal 回填明文（此后 👁=普通明文切换）
const revealing = ref(false) // 模态内 reveal 在途（防连点重复弹本地验证）
const browsing = ref(false) // 私钥浏览在途（请求寿命 = 用户开着对话框的时间）

function resetSecretUi() {
  authMode.value = 'password'
  segTouched = false
  secretsLoaded.value = false
  showPwd.value = false
  // round17：在途态一并复位——reveal 验证在途时关模态再重开，旧请求 settle 前点 👁
  // 会被防连点守卫静默吞掉一次；browsing 同理（pickFile 请求寿命长，重开后按钮假死）
  revealing.value = false
  browsing.value = false
}

function openCreate() {
  Object.assign(form, { name: '', userName: '', password: '', privateKeyPath: '' })
  resetSecretUi()
  formSnapshot = { ...form } // 草稿守卫快照（round13：干净/有输入二分关闭语义）
  editing.value = { mode: 'create' }
}

function openEdit(c) {
  // 密码/私钥路径预填：列表含存在性布尔（hasPwd/hasKeyPath，安全红线不含值）——
  // 存在 → 预填掩码占位（真实值由 👁 reveal 回填）；不存在 → 空输入框（如实显示"无密码"，
  // 不再用掩码误导用户以为存有密码；用户报告 BUG）。掩码回填与保存三态语义不变
  Object.assign(form, {
    name: c.name,
    userName: c.userName,
    password: c.hasPwd ? SECRET_MASK : '',
    privateKeyPath: c.hasKeyPath ? SECRET_MASK : '',
  })
  resetSecretUi()
  // WPF 编辑打开的对齐规则（org.PrivateKeyPath 非空默认私钥侧）：用存在性布尔在打开时即应用
  if (!c.hasPwd && c.hasKeyPath) authMode.value = 'key'
  formSnapshot = { ...form } // 草稿守卫快照（round13）
  editing.value = { mode: 'edit', name: c.name }
}

function setAuthMode(mode) {
  if (authMode.value === mode) return
  segTouched = true
  authMode.value = mode
}

// ---- 名称即时查重（WPF 弹窗 IDataErrorInfo 即时判重的 web 形态）：忽略大小写
//（对齐后端 UpdateCredential 的 CurrentCultureIgnoreCase 判重，排除原名）；
// 重名即输入框下方红字提示 + 禁用保存（后端 400 仍为最终守卫） ----
const nameExists = computed(() => {
  if (!editing.value) return false // 模态已关（保存成功后的淡出动画期间）不参与查重——否则
  // 刚保存进列表的新凭据会与未清空的表单值撞名，淡出瞬间闪红字「名称已存在」（用户报告 BUG）
  const n = form.name.trim()
  if (!n) return false
  return credentials.value.some((c) => c.name !== editing.value.name && c.name.trim().toLowerCase() === n.toLowerCase())
})

// 必填项统一守卫（round14 需求）：名称未填=假禁用+提示+强点聚焦；重名/保存中走真禁用
const reqGuard = useRequiredGuard({ getMissing: () => (form.name.trim() ? [] : ['name']) })

// 👁：编辑态未加载明文时 = reveal（本地验证门，与行级 reveal 同端点同 30s 窗口）；
// 已加载（或新建态）= 普通明文切换（仅密码行有切换，私钥路径 reveal 后即明文可编辑）
function onEye(field) {
  if (editing.value?.mode === 'edit' && !secretsLoaded.value) {
    revealSecrets(field)
    return
  }
  if (field === 'password') showPwd.value = !showPwd.value
}

async function revealSecrets(field) {
  if (revealing.value) return // 防连点重复弹本地验证
  const target = editing.value
  revealing.value = true
  try {
    const r = await api.revealCredential(target.name, ds.value)
    if (editing.value !== target) return // 等待期间模态已关/换目标
    // 只回填仍为掩码的字段：用户已 typed/浏览选定的新值不被旧值覆盖
    if (form.password === SECRET_MASK) form.password = r.password || ''
    if (form.privateKeyPath === SECRET_MASK) form.privateKeyPath = r.privateKeyPath || ''
    secretsLoaded.value = true
    if (field === 'password') showPwd.value = true
    // WPF 编辑打开的对齐规则（org.PrivateKeyPath 非空 → 私钥侧）：仅当密码侧实际为空
    // 且用户未手动切换时应用——点了密码行的 👁 却跳到私钥侧会违背当前查看意图
    if (!segTouched && !form.password && form.privateKeyPath) authMode.value = 'key'
  } catch (e) {
    if (editing.value !== target) return
    if (e?.status === 404 || e?.status === 400) {
      editing.value = null // 凭据/数据源已不存在：关模态静默刷新（行级 reveal 同款）
      load()
    } else {
      message.error(t('cv.revealFailed')) // 403=验证失败/取消；网络异常同文案（避免明文相关细节泄漏）
    }
  } finally {
    revealing.value = false
  }
}

// 私钥路径"浏览…"：后端弹 WPF 同款 OpenFileDialog（filter 照抄 WPF 凭据弹窗 ppk|*.*）；
// 404=用户取消静默（api.pickFile 约定）
async function pickKeyFile() {
  if (browsing.value) return
  browsing.value = true
  try {
    const resp = await api.pickFile('ppk|*.*', { path: form.privateKeyPath === SECRET_MASK ? '' : form.privateKeyPath })
    if (resp?.path) form.privateKeyPath = resp.path
  } catch (e) {
    if (e?.status !== 404) message.error(t('settings.r.pickFailed'))
  } finally {
    browsing.value = false
  }
}

async function save() {
  if (!form.name.trim() || nameExists.value || saving.value) return
  saving.value = true
  const isEdit = editing.value.mode === 'edit'
  // 可见侧：未 reveal 且掩码未动 → null（保持原值）；其余提交现值（空串=显式清除，
  // 非空=新值）。隐藏侧：编辑=不提交（null=保持），新建=空串（无）——二选一切换
  // 只影响展示，不清另一侧（保守语义，见文件头注释；显式清除走"reveal 后清空再保存"）
  const secretOut = (v) => (isEdit && !secretsLoaded.value && v === SECRET_MASK ? null : v)
  const hidden = isEdit ? null : ''
  // credential 域与后端 DTO 一致（PascalCase；Password/PrivateKeyPath 明文入/null 保持/
  // 空串清除，服务端加密落库）
  const credential = {
    Name: form.name.trim(),
    UserName: form.userName.trim(),
    Password: authMode.value === 'password' ? secretOut(form.password) : hidden,
    PrivateKeyPath: authMode.value === 'key' ? secretOut(form.privateKeyPath.trim()) : hidden,
  }
  try {
    if (editing.value.mode === 'create') await api.createCredential(credential, ds.value)
    else await api.updateCredential(editing.value.name, credential, ds.value)
    message.success(t('settings.saved'))
    editing.value = null
    load() // 引用服务器联动改名等由后端 ReloadAll+SSE 兜底，这里主动刷一次拿到最新 refCount
  } catch (e) {
    message.error(saveErrorText(t, e)) // round17：统一拼装（utils/formError）
  } finally {
    saving.value = false
  }
}

// 模态表单回车=保存：共通语义见 utils/formEnter.js（与保存按钮同守卫：名称空/重名/保存中不动作）

// ---- 删除（引用数警告 + 404 静默刷新）。autoFocus:false——删除确认不自动聚焦按钮，
// Enter 不可误触确认（Esc 仍可取消；naive 默认 autoFocus 会让回车落到 positive 上）----
function onDelete(c) {
  confirmDialog(dialog, {
    danger: true,
    title: t('cv.deleteTitle'),
    // 删除类确认统一形态：无图标 + 红 positive（folderOps 文件头策略）
    content:
      c.refCount > 0
        ? t('cv.deleteConfirm', { name: c.name }) + ' ' + t('cv.deleteRefWarning', { n: c.refCount })
        : t('cv.deleteConfirm', { name: c.name }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    onPositive: async () => {
      try {
        await api.deleteCredential(c.name, ds.value)
        message.success(t('cv.deleteOk'))
        load()
      } catch (e) {
        if (e?.status === 404)
          load() // 已被其它端删除：静默刷新
        else message.error(t('cv.deleteFailed'))
      }
    },
  })
}

// ---- Esc 链：模态开着时捕获截停（SettingsView 的返回导航让位），只关模态 ----
// ---- 草稿守卫（round13 统一关闭语义，兼 round12 N2：凭据表单含密码/私钥等长文本，
// 此前遮罩/Esc/取消直接丢光）。脏检查快照在 openCreate/openEdit 设置；reveal 回填
// 真实值视为"有输入"（保守：宁可多确认，不丢内容）----
let formSnapshot = {}
const formDirty = () => Object.keys(form).some((k) => String(form[k] ?? '') !== String(formSnapshot[k] ?? ''))
const editGuard = useDraftGuard({
  isDirty: formDirty,
  doClose: () => {
    editing.value = null
  },
  isBusy: () => saving.value,
  dialog,
  message,
  t,
})
bindModalEsc([{ isOpen: () => showEdit.value, close: () => editGuard.requestClose() }])
</script>

<template>
  <div class="group">
    <p v-if="loading && !credentials.length" class="hint">{{ t('settings.loading') }}</p>
    <p v-else-if="loadError" class="hint err">{{ t('cv.loadFailed') }}</p>
    <template v-else>
      <div class="toolbar">
        <div class="ds-select">
          <span class="ds-label">{{ t('cv.ds') }}</span>
          <n-select size="small" :value="ds" :options="dsOptions" @update:show="shield" @update:value="ds = $event" />
        </div>
        <span v-if="isReadOnly" class="ro-flag">{{ t('cv.readOnly') }}</span>
        <n-button class="new-btn" size="small" type="primary" ghost :disabled="isReadOnly" @click="openCreate">
          {{ t('cv.new') }}
        </n-button>
      </div>

      <div v-if="!credentials.length" class="empty">{{ t('cv.empty') }}</div>
      <div v-else class="cv-table">
        <div class="cv-row head">
          <span class="c-name">{{ t('col.name') }}</span>
          <span class="c-user">{{ t('common.username') }}</span>
          <span class="c-refs">{{ t('cv.col.refCount') }}</span>
          <span class="c-actions">{{ t('col.actions') }}</span>
        </div>
        <div v-for="c in credentials" :key="c.name" class="cv-row">
          <span class="c-name" :title="c.name">{{ c.name }}</span>
          <span class="c-user" :title="c.userName">{{ c.userName || '—' }}</span>
          <span class="c-refs" :class="{ hot: c.refCount > 0 }">{{ c.refCount }}</span>
          <span class="c-actions">
            <!-- 明文查看只在编辑弹窗内（round14 需求：列表操作列不再提供 reveal） -->
            <button class="act" type="button" :disabled="isReadOnly" :title="t('cv.edit')" @click="openEdit(c)">
              ✎
            </button>
            <button
              class="act"
              type="button"
              :disabled="isReadOnly"
              :title="t('editor.deleteYes')"
              @click="onDelete(c)"
            >
              ✕
            </button>
          </span>
        </div>
      </div>
    </template>

    <!-- 新建/编辑模态：Name 必填 + 即时查重；密码/私钥二选一（segmented，
         仅切换展示）；编辑态两字段预填掩码，👁 reveal（本地验证）回填明文；私钥行带浏览按钮 -->
    <n-modal
      :show="showEdit"
      :mask-closable="false"
      @update:show="editGuard.requestClose"
      @mask-click="editGuard.onMaskClick"
      preset="card"
      :title="editing?.mode === 'edit' ? t('cv.editTitle') : t('cv.newTitle')"
      :bordered="false"
      :style="{ width: 'min(480px, 92vw)' }"
      role="dialog"
      aria-modal="true"
    >
      <div class="form" @keydown="onFormEnter($event, () => reqGuard.trySave(save))">
        <div class="s-form-row" :class="{ 'req-hot': reqGuard.isHot('name') }" data-req-key="name">
          <label>{{ t('editor.f.Name') }}<span class="req-star">*</span></label>
          <div>
            <n-input
              size="small"
              v-model:value="form.name"
              :status="nameExists ? 'error' : reqGuard.isHot('name') ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
            <!-- 重名即时提示（secretHint 长提示行已随即时查重移除并按 P13 走管线删除；
                 文案复用运行器重名词条） -->
            <p v-if="nameExists" class="f-err">{{ t('settings.r.nameExists', { name: form.name.trim() }) }}</p>
          </div>
        </div>
        <div class="s-form-row">
          <label>{{ t('editor.f.UserName') }}</label>
          <n-input size="small" v-model:value="form.userName" :input-props="{ spellcheck: false }" />
        </div>
        <!-- 密码/私钥二选一：WPF 弹窗 IsUsePrivateKey 复选框的 web 形态（ed-seg 样式，
             EditorDrawer 凭据组同款）；仅切换展示，保存语义见 save() -->
        <div class="s-form-row">
          <label>{{ t('cv.authType') }}</label>
          <div class="cv-seg" role="tablist">
            <button
              type="button"
              role="tab"
              :aria-selected="authMode === 'password'"
              :class="{ on: authMode === 'password' }"
              @click="setAuthMode('password')"
            >
              {{ t('editor.f.Password') }}
            </button>
            <button
              type="button"
              role="tab"
              :aria-selected="authMode === 'key'"
              :class="{ on: authMode === 'key' }"
              @click="setAuthMode('key')"
            >
              {{ t('editor.f.PrivateKeyPath') }}
            </button>
          </div>
        </div>
        <div v-if="authMode === 'password'" class="s-form-row">
          <label>{{ t('editor.f.Password') }}</label>
          <n-input
            size="small"
            :type="showPwd ? 'text' : 'password'"
            v-model:value="form.password"
            :input-props="{ spellcheck: false }"
          >
            <template #suffix>
              <!-- 编辑态未 reveal：👁=验证后加载明文（title=cv.reveal）；此后=普通明文切换 -->
              <button
                class="eye"
                type="button"
                :disabled="revealing"
                :title="
                  editing?.mode === 'edit' && !secretsLoaded
                    ? t('cv.reveal')
                    : showPwd
                      ? t('editor.hidePassword')
                      : t('editor.showPassword')
                "
                @click="onEye('password')"
              >
                {{ showPwd ? '🙈' : '👁' }}
              </button>
            </template>
          </n-input>
        </div>
        <div v-else class="s-form-row">
          <label>{{ t('editor.f.PrivateKeyPath') }}</label>
          <n-input size="small" v-model:value="form.privateKeyPath" :input-props="{ spellcheck: false }">
            <template #suffix>
              <!-- 编辑态未 reveal：👁=验证后加载明文（reveal 后路径即明文可编辑，WPF 同款纯文本框） -->
              <button
                v-if="editing?.mode === 'edit' && !secretsLoaded"
                class="eye"
                type="button"
                :disabled="revealing"
                :title="t('cv.reveal')"
                @click="onEye('key')"
              >
                👁
              </button>
              <button
                class="eye browse"
                type="button"
                :disabled="browsing"
                :title="t('cv.browse')"
                @click="pickKeyFile"
              >
                …
              </button>
            </template>
          </n-input>
        </div>
      </div>
      <template #footer>
        <div class="modal-actions">
          <n-button size="small" @click="editGuard.requestClose">{{ t('editor.cancel') }}</n-button>
          <!-- round14 统一必填处理：名称未填=假禁用+旁侧提示，强点聚焦名称框；重名/保存中真禁 -->
          <span v-if="reqGuard.blocked" class="req-hint">{{ t('modal.requiredHint') }}</span>
          <n-button
            size="small"
            type="primary"
            ghost
            :class="{ 'req-blocked': reqGuard.blocked }"
            :disabled="nameExists"
            :loading="saving"
            @click="reqGuard.trySave(save)"
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
/* .hint/.hint.err 走 theme.css 全局（round17 收敛四份逐字拷贝） */
.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}
.ds-select {
  width: 240px;
  display: flex;
  align-items: center;
  gap: 8px;
}
.ds-label {
  flex: 0 0 auto;
  font-size: var(--fs-body);
  color: var(--text-2);
}
.ro-flag {
  font-size: var(--fs-caption);
  color: var(--warning);
}
.new-btn {
  margin-left: auto;
}
.empty {
  padding: 24px 0;
  font-size: var(--fs-body);
  color: var(--text-4);
}

/* ---- 表格：4 列网格（名称/用户名/被引用/操作，对齐 WPF 凭据库列集），主题变量取色 ---- */
.cv-table {
  border: 1px solid var(--border);
  border-radius: var(--radius-box);
  overflow: hidden;
}
.cv-row {
  display: grid;
  grid-template-columns: minmax(120px, 1.6fr) minmax(100px, 1fr) 56px 110px;
  gap: 6px;
  align-items: center;
  min-height: 32px;
  padding: 0 10px;
  border-bottom: 1px solid var(--border);
  font-size: var(--fs-body);
  color: var(--text-2);
}
.cv-row:last-child {
  border-bottom: none;
}
.cv-row.head {
  background: var(--bg-panel);
  color: var(--text-4);
  font-size: var(--fs-caption);
}
.c-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-1);
}
.c-user {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.c-refs {
  text-align: right;
  color: var(--text-4);
}
.c-refs.hot {
  color: var(--warning);
}
.c-actions {
  display: flex;
  justify-content: flex-end;
  gap: 4px;
}

/* ---- 模态表单 ---- */
.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
/* 名称重名即时提示：输入框下方红字 */
.f-err {
  margin: 4px 0 0;
  font-size: var(--fs-caption);
  color: var(--danger);
}
/* 密码/私钥二选一 segmented（ed-seg 同款：容器 28 档 border-box，按钮 stretch + 横距 14） */
.cv-seg {
  display: inline-flex;
  align-self: start;
  height: var(--ctrl-h-m);
  box-sizing: border-box;
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  overflow: hidden;
}
.cv-seg button {
  border: none;
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 0 14px;
  cursor: pointer;
  white-space: nowrap;
}
.cv-seg button + button {
  border-left: 1px solid var(--border);
}
.cv-seg button:hover:not(.on) {
  background: var(--bg-hover);
  color: var(--text-1);
}
.cv-seg button.on {
  background: var(--accent-container);
  color: var(--accent-text);
}
.eye {
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-body);
  cursor: pointer;
  padding: 0 2px;
}
.eye:hover {
  color: var(--text-1);
}
.eye:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
/* 私钥"浏览…"：WPF 弹窗 Select 按钮的 web 形态（"…"紧凑形态，title 注明） */
.eye.browse {
  font-size: var(--fs-body);
  font-weight: 600;
  line-height: 1;
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
