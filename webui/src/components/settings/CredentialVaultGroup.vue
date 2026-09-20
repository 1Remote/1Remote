<script setup>
/**
 * 凭据库分组（spec §6；表单对齐凭据库模型）：
 * GET /api/credentials?ds= 表格 + 新建/编辑模态 + 删除确认（引用数警告）+ 👁 明文查看（reveal 流）。
 *
 * - 表单字段集 = WPF 凭据库弹窗（CredentialVaultViewModel 以 showHost:false 复用
 *   AlternativeCredentialEditView）：Name/UserName/Password/PrivateKeyPath 四项，
 *   顺序同 WPF（Name → UserName → Password → 私钥路径）。Address/Port 两项已去掉——
 *   凭据库不使用这两个字段（Dapper UpdateCredential 落库前强制清空），web 表单原先
 *   复用备用凭据子表单的这两项属多余；后端 DTO 字段保留仅为 API 兼容，前端不再提交。
 * - 数据源过滤：n-select（useServers 共享态），切换即重载列表；只读数据源禁用 CRUD 按钮
 *   （后端也会前置拦截，这里只做 UI 预防）。
 * - 列表 DTO 不含密码/私钥路径（后端安全红线）——明文只能走 reveal：
 *   点 👁 → 行内「请在桌面端完成验证…」等待态（服务端在桌面端弹本地验证，未开启则直通）→
 *   成功后行内展开明文（默认掩码，可切换）+ 30s 倒计时自动隐藏；403 → toast 验证失败；
 *   404 → 静默刷新列表（凭据已被其它端删除/改名）。列表列与 WPF 凭据库表格对齐
 *   （名称/用户名/操作），另加 web 侧引用计数列；WPF 的密码/私钥掩码列由 reveal 行承载。
 * - 编辑模态的密码/私钥路径（"正常行为"对齐）：
 *   打开即预填掩码串 MASK（列表无值、不自动 reveal——那会在打开模态时弹验证）；
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
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { api } from '../../api'
import { useServers } from '../../composables/useServers'
import { useSettingsEsc } from '../../composables/useSettingsEsc'
import { onFormEnter } from '../../utils/formEnter'

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
  clearReveal()
  load()
})

// ---- 新建/编辑模态 ----
const editing = ref(null) // null=关 | { mode: 'create' } | { mode: 'edit', name }
const showEdit = computed({
  get: () => !!editing.value,
  set: (v) => {
    if (!v) editing.value = null
  },
})
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
}

function openCreate() {
  Object.assign(form, { name: '', userName: '', password: '', privateKeyPath: '' })
  resetSecretUi()
  editing.value = { mode: 'create' }
}

function openEdit(c) {
  // 密码/私钥路径预填掩码：列表无值（安全红线），真实值由 👁 reveal 回填
  Object.assign(form, {
    name: c.name,
    userName: c.userName,
    password: SECRET_MASK,
    privateKeyPath: SECRET_MASK,
  })
  resetSecretUi()
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
  const n = form.name.trim()
  if (!n) return false
  return credentials.value.some(
    (c) => c.name !== editing.value?.name && c.name.trim().toLowerCase() === n.toLowerCase()
  )
})

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
    const detail = e?.body?.errors?.join('; ')
    message.error(t('settings.saveFailed') + (detail ? ` ${detail}` : ''))
  } finally {
    saving.value = false
  }
}

// 模态表单回车=保存：共通语义见 utils/formEnter.js（与保存按钮同守卫：名称空/重名/保存中不动作）

// ---- 删除（引用数警告 + 404 静默刷新）。autoFocus:false——删除确认不自动聚焦按钮，
// Enter 不可误触确认（Esc 仍可取消；naive 默认 autoFocus 会让回车落到 positive 上）----
function onDelete(c) {
  dialog.warning({
    title: t('cv.deleteTitle'),
    content:
      c.refCount > 0
        ? t('cv.deleteConfirm', { name: c.name }) + ' ' + t('cv.deleteRefWarning', { n: c.refCount })
        : t('cv.deleteConfirm', { name: c.name }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    autoFocus: false,
    onPositiveClick: async () => {
      try {
        await api.deleteCredential(c.name, ds.value)
        message.success(t('cv.deleteOk'))
        if (revealState.value?.name === c.name) clearReveal()
        load()
      } catch (e) {
        if (e?.status === 404)
          load() // 已被其它端删除：静默刷新
        else message.error(t('cv.deleteFailed'))
      }
    },
  })
}

// ---- reveal（明文查看）：行级状态机 waiting → shown(30s 倒计时) → 清除 ----
const REVEAL_SECONDS = 30
const revealState = ref(null) // { name, waiting, password, privateKeyPath, unmasked, left }
let revealTimer = null

function clearReveal() {
  clearInterval(revealTimer)
  revealTimer = null
  revealState.value = null
}

function onReveal(c) {
  if (revealState.value?.waiting) return // 已有一个验证在途：防连点重复弹本地验证
  clearReveal()
  revealState.value = {
    name: c.name,
    waiting: true,
    password: '',
    privateKeyPath: '',
    unmasked: false,
    left: REVEAL_SECONDS,
  }
  api
    .revealCredential(c.name, ds.value)
    .then((r) => {
      if (revealState.value?.name !== c.name) return // 等待期间已切行/清理
      revealState.value = {
        name: c.name,
        waiting: false,
        password: r.password || '',
        privateKeyPath: r.privateKeyPath || '',
        unmasked: true,
        left: REVEAL_SECONDS,
      }
      revealTimer = setInterval(() => {
        const s = revealState.value
        if (!s || s.waiting) return clearReveal()
        s.left -= 1
        if (s.left <= 0) clearReveal()
      }, 1000)
    })
    .catch((e) => {
      if (revealState.value?.name === c.name) clearReveal()
      if (e?.status === 404 || e?.status === 400)
        load() // 凭据/数据源已不存在：静默刷新
      else message.error(t('cv.revealFailed')) // 403=验证失败/取消；网络异常同文案（避免明文相关细节泄漏）
    })
}
onBeforeUnmount(clearReveal)

// ---- Esc 链：模态开着时捕获截停（SettingsView 的返回导航让位），只关模态 ----
bindModalEsc([{ isOpen: () => showEdit.value, close: () => (editing.value = null) }])
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
        <n-button class="new-btn" size="small" type="primary" :disabled="isReadOnly" @click="openCreate">
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
        <template v-for="c in credentials" :key="c.name">
          <div class="cv-row" :class="{ revealing: revealState?.name === c.name }">
            <span class="c-name" :title="c.name">{{ c.name }}</span>
            <span class="c-user" :title="c.userName">{{ c.userName || '—' }}</span>
            <span class="c-refs" :class="{ hot: c.refCount > 0 }">{{ c.refCount }}</span>
            <span class="c-actions">
              <!-- 明文已展开：掩码切换 + 手动隐藏；未展开：👁 发起 reveal -->
              <template v-if="revealState?.name === c.name && !revealState.waiting">
                <button
                  class="act"
                  type="button"
                  :title="revealState.unmasked ? t('editor.hidePassword') : t('editor.showPassword')"
                  @click="revealState.unmasked = !revealState.unmasked"
                >
                  {{ revealState.unmasked ? '🙈' : '👁' }}
                </button>
                <button class="act" type="button" @click="clearReveal">{{ t('cv.revealHide') }}</button>
              </template>
              <button
                v-else
                class="act"
                type="button"
                :disabled="revealState?.waiting"
                :title="t('cv.reveal')"
                @click="onReveal(c)"
              >
                👁
              </button>
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
                🗑
              </button>
            </span>
          </div>
          <!-- 明文详情行（reveal 展开态）：等待桌面验证 / 密码+私钥路径（掩码可切）+ 倒计时 -->
          <div v-if="revealState?.name === c.name" class="cv-reveal">
            <div v-if="revealState.waiting" class="waiting">
              <span class="spin" aria-hidden="true"></span>{{ t('cv.revealWaiting') }}
            </div>
            <template v-else>
              <div class="rv-line">
                <label>{{ t('editor.f.Password') }}</label>
                <code>{{ revealState.unmasked ? revealState.password || '—' : '••••••••' }}</code>
              </div>
              <div class="rv-line">
                <label>{{ t('editor.f.PrivateKeyPath') }}</label>
                <code>{{ revealState.unmasked ? revealState.privateKeyPath || '—' : '••••••••' }}</code>
              </div>
              <span class="rv-count">{{ t('cv.autoHide', { n: revealState.left }) }}</span>
            </template>
          </div>
        </template>
      </div>
    </template>

    <!-- 新建/编辑模态：Name 必填 + 即时查重；密码/私钥二选一（segmented，
         仅切换展示）；编辑态两字段预填掩码，👁 reveal（本地验证）回填明文；私钥行带浏览按钮 -->
    <n-modal
      v-model:show="showEdit"
      preset="card"
      :title="editing?.mode === 'edit' ? t('cv.editTitle') : t('cv.newTitle')"
      :bordered="false"
      :style="{ width: 'min(480px, 92vw)' }"
      role="dialog"
      aria-modal="true"
    >
      <div class="form" @keydown="onFormEnter($event, save)">
        <div class="f-row">
          <label>{{ t('editor.f.Name') }} *</label>
          <div>
            <n-input
              size="small"
              v-model:value="form.name"
              :status="nameExists ? 'error' : undefined"
              :input-props="{ spellcheck: false }"
            />
            <!-- 重名即时提示（secretHint 长提示行已随即时查重移除，locale 键保留避免
                 动 locales 平价；文案复用运行器重名词条） -->
            <p v-if="nameExists" class="f-err">{{ t('settings.r.nameExists', { name: form.name.trim() }) }}</p>
          </div>
        </div>
        <div class="f-row">
          <label>{{ t('editor.f.UserName') }}</label>
          <n-input size="small" v-model:value="form.userName" :input-props="{ spellcheck: false }" />
        </div>
        <!-- 密码/私钥二选一：WPF 弹窗 IsUsePrivateKey 复选框的 web 形态（ed-seg 样式，
             EditorDrawer 凭据组同款）；仅切换展示，保存语义见 save() -->
        <div class="f-row">
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
        <div v-if="authMode === 'password'" class="f-row">
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
        <div v-else class="f-row">
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
          <n-button size="small" @click="editing = null">{{ t('editor.cancel') }}</n-button>
          <n-button
            size="small"
            type="primary"
            :disabled="!form.name.trim() || nameExists"
            :loading="saving"
            @click="save"
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
.hint {
  font-size: var(--fs-body);
  color: var(--text-3);
}
.hint.err {
  color: var(--danger);
}
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
.cv-row.revealing {
  background: var(--accent-container);
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
.act {
  min-width: 24px;
  height: var(--ctrl-h-s);
  padding: 0 4px;
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

/* ---- reveal 详情行：展开在凭据行下方，含等待态与倒计时 ---- */
.cv-reveal {
  padding: 6px 12px 8px;
  border-bottom: 1px solid var(--border);
  background: var(--accent-container);
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.waiting {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: var(--fs-body);
  color: var(--text-2);
}
.spin {
  width: 12px;
  height: 12px;
  border: 2px solid var(--border-strong);
  border-top-color: var(--accent);
  border-radius: 50%;
  animation: cv-spin 0.8s linear infinite;
}
@keyframes cv-spin {
  to {
    transform: rotate(360deg);
  }
}
@media (prefers-reduced-motion: reduce) {
  .spin {
    animation: none;
  }
}
.rv-line {
  display: flex;
  align-items: baseline;
  gap: 10px;
  min-width: 0;
}
.rv-line label {
  flex: 0 0 110px;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
.rv-line code {
  min-width: 0;
  overflow-wrap: anywhere;
  font-size: var(--fs-body);
  color: var(--text-1);
}
.rv-count {
  align-self: flex-end;
  font-size: var(--fs-caption);
  color: var(--text-4);
}

/* ---- 模态表单 ---- */
.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.f-row {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}
.f-row label {
  font-size: var(--fs-body);
  color: var(--text-2);
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
