<script setup>
/**
 * 凭据库分组（Plan 3 Task 5，spec §6；fix batch8 Task E #16 表单对齐凭据库模型）：
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
 * - 编辑模态的密码/私钥路径不可预填（列表无值、reveal 有 30s 窗口与验证成本）——后端 PUT
 *   对这两个加密字段为"空=保持原值"语义（batch8 Task E #17：明文不回显，空提交沿用原值），
 *   输入框以 placeholder 注明（settings.ph.keepCurrent）。
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
// 表单字段集与 WPF 凭据库弹窗一致（#16）：Name/UserName/Password/PrivateKeyPath，
// 不含 Address/Port（凭据库不使用，后端落库前本就清空）
const form = reactive({ name: '', userName: '', password: '', privateKeyPath: '' })
const showPwd = ref(false)
const saving = ref(false)

function openCreate() {
  Object.assign(form, { name: '', userName: '', password: '', privateKeyPath: '' })
  showPwd.value = false
  editing.value = { mode: 'create' }
}

function openEdit(c) {
  Object.assign(form, {
    name: c.name,
    userName: c.userName,
    password: '',
    privateKeyPath: '',
  })
  showPwd.value = false
  editing.value = { mode: 'edit', name: c.name }
}

async function save() {
  if (!form.name.trim() || saving.value) return
  saving.value = true
  // credential 域与后端 DTO 一致（PascalCase；Password/PrivateKeyPath 明文入，服务端加密落库；
  // 编辑态留空 = 保持原值，见文件头 #17 注释）
  const credential = {
    Name: form.name.trim(),
    UserName: form.userName.trim(),
    Password: form.password,
    PrivateKeyPath: form.privateKeyPath.trim(),
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

// 模态表单回车=保存：共通语义见 utils/formEnter.js（与保存按钮同守卫：名称空/保存中不动作）

// ---- 删除（引用数警告 + 404 静默刷新）----
function onDelete(c) {
  dialog.warning({
    title: t('cv.deleteTitle'),
    content:
      c.refCount > 0
        ? t('cv.deleteConfirm', { name: c.name }) + ' ' + t('cv.deleteRefWarning', { n: c.refCount })
        : t('cv.deleteConfirm', { name: c.name }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
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

    <!-- 新建/编辑模态：Name 必填；密码眼睛切换；编辑态密码/私钥路径留空=保持原值（placeholder 注明） -->
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
          <n-input size="small" v-model:value="form.name" :input-props="{ spellcheck: false }" />
        </div>
        <div class="f-row">
          <label>{{ t('editor.f.UserName') }}</label>
          <n-input size="small" v-model:value="form.userName" :input-props="{ spellcheck: false }" />
        </div>
        <div class="f-row">
          <label>{{ t('editor.f.Password') }}</label>
          <n-input
            size="small"
            :type="showPwd ? 'text' : 'password'"
            v-model:value="form.password"
            :placeholder="editing?.mode === 'edit' ? t('settings.ph.keepCurrent') : undefined"
            :input-props="{ spellcheck: false }"
          >
            <template #suffix>
              <button
                class="eye"
                type="button"
                :title="showPwd ? t('editor.hidePassword') : t('editor.showPassword')"
                @click="showPwd = !showPwd"
              >
                👁
              </button>
            </template>
          </n-input>
        </div>
        <div class="f-row">
          <label>{{ t('editor.f.PrivateKeyPath') }}</label>
          <n-input
            size="small"
            v-model:value="form.privateKeyPath"
            :placeholder="editing?.mode === 'edit' ? t('settings.ph.keepCurrent') : undefined"
            :input-props="{ spellcheck: false }"
          />
        </div>
      </div>
      <template #footer>
        <div class="modal-actions">
          <n-button size="small" @click="editing = null">{{ t('editor.cancel') }}</n-button>
          <n-button size="small" type="primary" :disabled="!form.name.trim()" :loading="saving" @click="save">
            {{ t('settings.save') }}
          </n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.group {
  max-width: 760px;
}
.hint {
  font-size: 0.9615rem;
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
  font-size: 0.9615rem;
  color: var(--text-2);
}
.ro-flag {
  font-size: 0.8846rem;
  color: var(--warning);
}
.new-btn {
  margin-left: auto;
}
.empty {
  padding: 24px 0;
  font-size: 0.9615rem;
  color: var(--text-4);
}

/* ---- 表格：4 列网格（名称/用户名/被引用/操作，对齐 WPF 凭据库列集），主题变量取色 ---- */
.cv-table {
  border: 1px solid var(--border);
  border-radius: 8px;
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
  font-size: 0.9615rem;
  color: var(--text-2);
}
.cv-row:last-child {
  border-bottom: none;
}
.cv-row.head {
  background: var(--bg-panel);
  color: var(--text-4);
  font-size: 0.8846rem;
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
  height: 22px;
  padding: 0 4px;
  border: none;
  border-radius: 5px;
  background: transparent;
  color: var(--text-3);
  font-size: 0.9231rem;
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
  font-size: 0.9231rem;
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
  font-size: 0.8846rem;
  color: var(--text-4);
}
.rv-line code {
  min-width: 0;
  overflow-wrap: anywhere;
  font-size: 0.9231rem;
  color: var(--text-1);
}
.rv-count {
  align-self: flex-end;
  font-size: 0.8462rem;
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
  font-size: 0.9615rem;
  color: var(--text-2);
}
.eye {
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: 0.9231rem;
  cursor: pointer;
  padding: 0 2px;
}
.eye:hover {
  color: var(--text-1);
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
