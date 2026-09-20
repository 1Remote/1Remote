<script setup>
// 两栏布局（边栏 216px + 内容区）。边栏承载 SideTree（数据源树+标签区）；
// 内容区 = 面包屑行 + 行列表。selection/tag 状态由本组件持有；
// tag 与搜索过滤在此取交集：基础列表 → 标签 → 搜索命中集 → 传 ServerTable。
// 连接动作与全局键盘流也在此汇聚：所有连接入口（行双击/hover ▸/
// 右键菜单/树叶双击/批量条/Enter 光标行）emit 到本组件统一走 api.connect；
// 全局 Esc 链是唯一的 window 级 Esc 处理器（App.vue 搜索框与 ServerTable 均不本地拦截，
// 避免焦点位置不同导致链序漂移或双触发）。
// 内容区三态（骨架屏/空库引导/无匹配）+ 底部状态栏（数据源状态点/统计/SSE/语言切换）
// + <900px 自动收起边栏。
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { useWindowSize } from '@vueuse/core'
import SideTree from '../components/SideTree.vue'
import ServerTable from '../components/ServerTable.vue'
import ImportModal from '../components/ImportModal.vue'
import EditorDrawer from '../components/editor/EditorDrawer.vue'
import TagManagerModal from '../components/settings/TagManagerModal.vue'
import { api } from '../api'
import { applyServerFilters, BATCH_CONNECT_THRESHOLD, useServers } from '../composables/useServers'
import { buildTree, countDirectChildServers, holderAt } from '../composables/folders'
import { useTreeState } from '../composables/useTreeState'
import { useFolderOps } from '../composables/folderOps'
import { useEditorBus } from '../composables/editorBus'
import { setLocale } from '../locales'
import { LANGUAGES } from '../locales/languages.js'

const { t, locale } = useI18n()
const message = useMessage()
const dialog = useDialog()
const selection = ref(null) // { dataSourceName, folderPath, serverId? } —— null=未选中（全部）
const activeTag = ref('') // ''=未按标签过滤
// 收起状态仅本地内存（持久化暂缓）。窄窗适配：<900px 自动收起，只收不展——
// 仅在跨过 900 阈值时收起（窄窗内用户手动展开后，同侧宽度微调不反复打回），≥900 不自动展开
const collapsed = ref(false)
const { width: winWidth } = useWindowSize()
watch(
  winWidth,
  (w, old) => {
    if (w < 900 && (old === undefined || old >= 900)) collapsed.value = true
  },
  { immediate: true }
)

const { servers, datasources, tags, loading, connected, reload, searchQuery, searchedIds } = useServers()

// tree-state 首载：虚拟文件夹物化需要 expansion 键，侧栏收起
//（SideTree 卸载）时也须可用；useTreeState 幂等（SideTree 挂载时同调不重复请求）
const { folderPathsByDs, load: loadTreeState } = useTreeState()
const folderOps = useFolderOps()
onMounted(() => loadTreeState())

// 树模型（含空文件夹物化）与「当前层级文件夹行」：
// 数据源根/文件夹 = 该层文件夹行；「全部数据」根不显示文件夹行——
// 全库服务器总览里文件夹行只添噪音，来源上下文由行内 folder 列（数据源 / 路径前缀）承担
const treeModel = computed(() => buildTree(servers.value, datasources.value, folderPathsByDs.value))
const currentFolders = computed(() => {
  // 搜索过滤激活时隐藏文件夹行：搜索只命中服务器（useServers
  // searchedIds 为 server id 集），文件夹名不参与匹配——保留会在命中结果上方悬浮一层
  // 与查询无关的文件夹，误导导航；空 Set（零命中）同样隐藏。
  if (searchedIds.value != null) return []
  const sel = selection.value
  // 「全部数据」根（selection=null）：只列服务器行（全库递归，
  // ServerTable 对 null selection 不过滤），不生成文件夹行
  if (!sel || !sel.dataSourceName) return []
  const out = []
  const holder = holderAt(treeModel.value, sel.dataSourceName, sel.folderPath || '')
  if (holder) {
    // 文件夹行计数与树徽标同口径（直接子级服务器数）：行内数字 = 进入该文件夹
    // 后能看到的台数（不含子文件夹内部，由子文件夹自己的行/徽标承载）
    for (const f of holder.folders)
      out.push({ name: f.name, path: f.path, dsName: sel.dataSourceName, count: countDirectChildServers(f) })
  }
  return out
})

// 双击文件夹行 = 进入；树选中态与面包屑共用 selection
function onOpenFolder(f) {
  selection.value = { dataSourceName: f.dsName, folderPath: f.path }
}
function onCreateFolder(target) {
  folderOps.createFolder(target.dsName, target.parentPath)
}
// 列表文件夹行右键的 重命名/删除：与树右键共用 folderOps 实现
function onRenameFolder(target) {
  folderOps.renameFolder(target.dsName, target.folderPath)
}
function onDeleteFolder(target) {
  folderOps.deleteFolder(target.dsName, target.folderPath)
}
function onMoveToFolder({ server, dsName, path }) {
  folderOps.moveServersToFolder([server], dsName, path)
}
// 列表文件夹行拖到文件夹行/「..」上级行：整个子树移动（与树内拖拽同语义）
function onMoveFolder({ folder, path }) {
  folderOps.moveFolder(folder.dsName, folder.path, path)
}

// 传给 ServerTable 的收窄列表（其内部再应用树选中过滤 + 排序，交集自然复合）
const visibleServers = computed(() => applyServerFilters(servers.value, activeTag.value, searchedIds.value))
const searchActive = computed(() => searchedIds.value != null) // null=未启用；空 Set=搜了但零命中

// 面包屑：可点击逐级返回——全部数据 › 数据源 · 全部服务器 › 路径段；
// 末段=当前层级（强显示不可点）。hover title 给完整路径
const crumbSegments = computed(() => {
  const sel = selection.value
  const segs = [{ label: t('crumb.allDataSources'), sel: null }]
  if (sel?.dataSourceName) {
    segs.push({
      label: sel.dataSourceName + ' · ' + t('crumb.allServers'),
      sel: { dataSourceName: sel.dataSourceName, folderPath: '' },
    })
    if (sel.folderPath) {
      const parts = sel.folderPath.split('/')
      parts.forEach((p, i) =>
        segs.push({
          label: p,
          sel: { dataSourceName: sel.dataSourceName, folderPath: parts.slice(0, i + 1).join('/') },
        })
      )
    }
  }
  return segs
})
const crumbTitle = computed(() => {
  const sel = selection.value
  return sel?.dataSourceName
    ? sel.dataSourceName + (sel.folderPath ? ' / ' + sel.folderPath : '')
    : t('crumb.allDataSources')
})
const tableCount = ref(0)
const table = ref(null) // ServerTable 实例引用：全局 Esc 链需调用其暴露的菜单/勾选/光标回退方法
const sideTree = ref(null) // SideTree 实例引用：Esc 链调用其树右键菜单回退（closeCtxIfOpen）

// ---- 内容区三态 + 骨架屏：互斥地取代 ServerTable（表格隐藏时 ref 为 null，
// Esc 链的 tb?. 守卫天然兼容）。SSE 重载时列表已有数据，不闪骨架 ----
const showSkeleton = computed(() => loading.value && !servers.value.length) // 首载进行中
// 后端不可达（拉取失败且无任何数据）：优先于空库引导展示——引导卡的「新建/导入」会把用户带向
// 错误方向；恢复靠 30s 轮询（useServers 断连恢复时会补一次全量重载，此处自动切回正常内容）
const showOffline = computed(() => !connected.value && !loading.value && !servers.value.length)
// 空库引导卡：已连通且整库为空（0 服务器且无任何文件夹——建过文件夹就不再算"空库"，
// 空视图由表内空态的 empty.folder 文案承接，引导卡的「新建/导入」会让已有文件夹结构
// 的用户误以为库丢了）。文件夹全集以 tree-state 物化键为准（folderPathsByDs）
const anyFolders = computed(() => [...folderPathsByDs.value.values()].some((set) => set.size > 0))
const showGuide = computed(() => connected.value && !loading.value && !servers.value.length && !anyFolders.value)
// 引导卡协议一览（与 ProtocolBadge 协议集一致）：9 协议灰阶瓦片——身份色仅用于行内徽章，
// 引导卡只表"支持这些"，克制灰阶
const GUIDE_PROTOCOLS = ['RDP', 'SSH', 'SFTP', 'FTP', 'VNC', 'Telnet', 'Serial', 'APP', 'RdpApp']
const showNoMatch = computed(() => servers.value.length > 0 && !visibleServers.value.length) // 标签/搜索交集为空
const tableHidden = computed(() => showSkeleton.value || showOffline.value || showGuide.value || showNoMatch.value)
// 表格卸载后 counted 不再上报，面包屑计数跟随空态归零（骨架期如实显示 0）
const listCount = computed(() => (tableHidden.value ? 0 : tableCount.value))
const noMatchDetail = computed(() => {
  if (searchActive.value) return t('empty.searchedFor', { q: searchQuery.value })
  if (activeTag.value) return t('empty.taggedNone', { tag: activeTag.value })
  return t('empty.filtered') // 兜底（视图层无匹配仅在搜索/标签生效时可达）
})
// 清除过滤（无匹配态按钮）只清致因维度，一次点击清一层：优先清搜索词，清后若仍空
//（标签致因）按钮仍在、文案切到下一维度，二次点击清标签。树选中（文件夹）永不清——
// 导航意图与过滤意图独立，顺手清掉会把用户踢出当前文件夹。searchQuery 清空即时撤销
// 过滤（useServers 对空串走立即分支，无防抖延迟），按钮文案随 searchActive 即时回退。
const clearFilterLabel = computed(() =>
  searchActive.value ? t('empty.clearSearch') : activeTag.value ? t('empty.clearTag') : t('empty.clearFilters')
)
function clearNextFilter() {
  if (searchQuery.value) searchQuery.value = ''
  else if (activeTag.value) activeTag.value = ''
}

// ---- 状态栏（内容区底部 26px）：左=数据源状态点+名称（最多 3 个，超出 +N），
// 右=统计 + SSE 可达性 + 语言切换。状态点语义与 SideTree 根节点一致（绿=connected /
// 红=reconnecting / 灰=其余）；title 用状态文案（i18n）而非裸枚举——重连时附后端的
// 重连信息（reconnectInfo）----
const MAX_DS = 3
const dsShown = computed(() => datasources.value.slice(0, MAX_DS))
const dsHidden = computed(() => Math.max(0, datasources.value.length - MAX_DS))
const dsDotClass = (status) => (status === 'connected' ? 'ok' : status === 'reconnecting' ? 'bad' : 'idle')
const dsTitle = (ds) => {
  const base =
    ds.status === 'connected'
      ? t('statusbar.dsConnected', { name: ds.name })
      : ds.status === 'reconnecting'
        ? t('statusbar.dsReconnecting', { name: ds.name })
        : t('statusbar.dsDisconnected', { name: ds.name })
  return ds.status === 'reconnecting' && ds.reconnectInfo ? base + ' · ' + ds.reconnectInfo : base
}
function toggleLocale() {
  setLocale(locale.value === 'en-US' ? nonEnglishLocale : 'en-US')
}
// 语言切换：当前语言 ⇄ English——选了日语就按日语⇄英语切，不再硬编码
// 中英。nonEnglishLocale 记住最近使用的非英语界面语言：locale 初值来自 localStorage/浏览器
// 探测，且设置页选语言也会 setLocale（可能落到任一非英语码），用 watch 跟踪而非只读一次；
// 首启即英语（从未见过非英语界面）回落 zh-CN——与旧版 en↔zh 行为一致，避免按钮空操作。
// 不落库（与旧版一致，语言持久化由设置页负责）。
let nonEnglishLocale = 'zh-CN'
watch(
  locale,
  (l) => {
    if (l !== 'en-US') nonEnglishLocale = l
  },
  { immediate: true }
)
// 按钮显示将要切到的语言的自称（语言名不做 i18n，与 LANGUAGES 清单/WPF language_name
// 同语义）；旧键 statusbar.langEn/langZh 不再使用，locale JSON 中保留不删（避免动生成映射）
const langNative = (code) => LANGUAGES.find((l) => l.code === code)?.native || 'English'
const nextLang = computed(() => (locale.value === 'en-US' ? langNative(nonEnglishLocale) : 'English'))

// ---- 连接动作：api.connect → 后端触发 OnRequestServerConnect（fromView="WebUi"），
// 密码交互与会话窗口由桌面端既有管线处理（Web 侧不感知，spec 约定凭据留在本地）----
async function onConnect(id) {
  const name = servers.value.find((s) => s.id === id)?.displayName || id
  try {
    await api.connect(id)
    message.success(t('toast.connectStarted', { name }))
  } catch (e) {
    console.warn('[ServerListView] connect failed:', e?.message || e)
    message.error(t('toast.connectFailed'))
  }
}

async function onBatchConnect(ids) {
  if (!ids?.length) return
  // 批量连接阈值（产品决策项）：超过 BATCH_CONNECT_THRESHOLD 台先弹确认
  //（TagManagerModal「连接全部」同款），防误点一次拉起整屏会话
  if (ids.length > BATCH_CONNECT_THRESHOLD) {
    dialog.warning({
      title: t('batchConnect.confirmTitle'),
      content: t('batchConnect.confirmText', { n: ids.length }),
      positiveText: t('batch.connect'),
      negativeText: t('editor.cancel'),
      onPositiveClick: () => runBatchConnect(ids),
    })
    return
  }
  await runBatchConnect(ids)
}

async function runBatchConnect(ids) {
  let ok = 0
  for (const id of ids) {
    // 逐个串行 await：批量并发轰炸后端/桌面端连接管线不友好
    try {
      await api.connect(id)
      ok++
    } catch (e) {
      console.warn('[ServerListView] batch connect failed:', id, e?.message || e)
    }
  }
  if (ok) message.success(t('toast.batchConnectStarted', { n: ok }))
  if (ok < ids.length) message.error(t('toast.batchConnectFailed', { n: ids.length - ok }))
}

// ---- 导出：批量条「导出」→ blob 下载；403 = 桌面端已弹二次验证
//（未通过/取消），提示引导重试（通过后 30s 窗口内重试免验证）----
async function onExport(ids) {
  if (!ids?.length) return
  try {
    const { blob, filename } = await api.exportServers(ids)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename || '1remote-export.json' // 后端 Content-Disposition 缺失时的兜底名
    a.click()
    setTimeout(() => URL.revokeObjectURL(url)) // 异步 revoke：同步 revoke 在个别浏览器会截断未开始的下载
    message.success(t('toast.exportDone', { n: ids.length }))
  } catch (e) {
    if (e?.status === 403) {
      message.error(t('toast.exportNeedVerify'))
      return
    }
    const detail = e?.body?.errors?.[0]
    message.error(t('toast.exportFailed') + (detail ? ` (${detail})` : ''))
  }
}

// ---- 全局 Esc 链：一次 Esc 只退一级，按 右键菜单（服务器行 → 文件夹/空白 → 树）→ 列菜单
// → 勾选 → 搜索 → 标签 → 表格光标 逐级回退。菜单/勾选/光标归 ServerTable（经 ref 暴露的
// *IfOpen/*IfAny 方法，返回是否消费），树菜单归 SideTree（closeCtxIfOpen），搜索/标签归本组件
//（useServers / activeTag 共享态）——各处状态在唯一的 window 级 handler 里按序裁决，
// 与焦点位置无关（搜索框元素级 handler 在焦点不在输入框时不会触发，无法参与统一链序）。
// 列菜单开时 Esc 只关列菜单（closeColMenuIfOpen 在勾选/搜索/光标之前——菜单一层）。
// 编辑抽屉打开时 Esc 归抽屉（关闭/未保存确认，EditorDrawer 自持 window 级 handler，注册在
// 本链之后，若此处不守卫会先消费掉 Esc），本链整体让位。----
function onGlobalEsc(e) {
  if (e.key !== 'Escape') return
  if (editor.value) return // 抽屉在开：Esc 由抽屉处理
  if (tagManager.value) return // 标签管理模态在开：Esc 归 n-modal（关模态），不清搜索/光标
  if (importModal.value) return // 导入模态在开：Esc 归 n-modal（关模态/其内下拉）
  if (document.querySelector('.n-dialog')) return // naive 对话框在开（删除/重命名确认等）：Esc 只关框（naive 自持关闭），本链不清搜索/光标
  const tb = table.value // 命名避免遮蔽 i18n 的 t
  if (tb?.closeMenuIfOpen()) e.preventDefault()
  else if (tb?.closeNfMenuIfOpen()) e.preventDefault()
  else if (sideTree.value?.closeCtxIfOpen()) e.preventDefault()
  else if (tb?.closeColMenuIfOpen()) e.preventDefault()
  else if (tb?.clearCheckedIfAny()) e.preventDefault()
  else if (searchQuery.value) {
    searchQuery.value = ''
    e.preventDefault()
  } else if (activeTag.value) {
    // 标签过滤与搜索同为过滤意图，链中同级回退（Esc 清标签后下一 Esc 才清光标）；
    // 树选中（文件夹）仍永不清——导航意图与过滤意图独立
    activeTag.value = ''
    e.preventDefault()
  } else if (tb?.clearCursorIfAny()) e.preventDefault()
}
onMounted(() => window.addEventListener('keydown', onGlobalEsc))
onBeforeUnmount(() => window.removeEventListener('keydown', onGlobalEsc))

// ---- 编辑抽屉：状态 + 全部入口汇聚于此 ----
// editor = { mode:'create', ds, protocol?, duplicateFrom?, initial? } | { mode:'edit', serverId, ds, initial } | null
const editor = ref(null)

// App.vue 顶栏「+」经 editorBus 请求新建（跨层：顶栏在 router-view 之外无法向本视图 emit）。
// 抽屉已开时忽略——替换状态会丢掉未保存编辑且绕过脏确认。
const { createRequest, importRequest, setEditorOpen } = useEditorBus()
// 占用态上抛：editor 的全部赋值/清空路径（open* 打开 / close / onSaved 清空）经 watch
// 统一同步到 editorBus，App.vue 顶栏消费（编辑期间禁用搜索/「+」/⚙）。immediate
// 覆盖首挂载（null → false，保证总线初值与本视图一致）
watch(editor, (v) => setEditorOpen(!!v), { immediate: true })
watch(createRequest, () => {
  if (!editor.value) openCreate()
})
// 「+ ▾ 导入」同款：打开导入模态（与编辑抽屉互不排斥——模态在其上层，
// 但导入是明确的新任务入口，无需像 createRequest 那样守卫未保存编辑）
watch(importRequest, () => {
  importModal.value = true
})

function openCreate() {
  // 归属数据源 = 当前树选中（根/文件夹/叶）的数据源；未选 = Local。
  // 文件夹归属：选中根/文件夹时其 folderPath 随 initialFolder 传入，
  // EditorDrawer create 模式把 TreeNodes 预置为该路径——「全部数据」根（selection=null）
  // 不注入（无确定归属，落数据源根）；数据源根 folderPath='' 同样不注入
  editor.value = {
    mode: 'create',
    ds: selection.value?.dataSourceName || 'Local',
    protocol: 'RDP',
    initialFolder: selection.value?.folderPath || '',
  }
}

function openEdit(server) {
  editor.value = { mode: 'edit', serverId: server.id, ds: server.dataSourceName || 'Local', initial: server }
}

// 复制 = create 语义 + 抽屉预填来源服务器 config（EditorDrawer duplicateFrom：加载→清 Id→POST 新建）
function openDuplicate(server) {
  editor.value = { mode: 'create', ds: server.dataSourceName || 'Local', duplicateFrom: server.id, initial: server }
}

// 删除：确认对话框（naive dialog）→ DELETE → toast；UpdateServer/DeleteServer 系不触发
// SSE（已知后端行为），前端兜底 reload 刷新列表。autoFocus:false——删除确认不自动聚焦
// positive 按钮，Enter 不可误触确认（Esc 仍可取消；naive 默认 autoFocus 会让回车落到按钮上）
function onDelete(server) {
  dialog.warning({
    title: t('editor.deleteTitle'),
    content: t('editor.deleteConfirm', { name: server.displayName }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    autoFocus: false,
    onPositiveClick: async () => {
      try {
        await api.deleteServer(server.id, server.dataSourceName)
        message.success(t('editor.deleteOk', { name: server.displayName }))
        // 选中态可能指向已删对象（树叶选中）：清理回退，避免高亮悬空
        if (selection.value?.serverId === server.id) selection.value = null
        reload()
      } catch (e) {
        message.error(t('editor.deleteFailed') + (e?.message ? ` (${e.message})` : ''))
      }
    },
  })
}

// ---- 批量删除：批量条「🗑 删除」→ 确认（autoFocus:false 同 onDelete——回车不可误触）→
// 逐台串行 DELETE + 进度 toast（loading 句柄原地更新 content）→ reload。
// 删除后指向已删行的树叶选中态回退；勾选集由 useRowChecks 的数据剔除 watch 自动收敛 ----
function onBatchDelete(ids) {
  if (!ids?.length) return
  const list = (ids || []).map((id) => servers.value.find((s) => s.id === id)).filter(Boolean)
  if (!list.length) return
  dialog.warning({
    title: t('batchDelete.confirmTitle'),
    content: t('batchDelete.confirmText', { n: list.length }),
    positiveText: t('editor.deleteYes'),
    negativeText: t('editor.cancel'),
    autoFocus: false,
    onPositiveClick: () => runBatchDelete(list),
  })
}

async function runBatchDelete(list) {
  const n = list.length
  const progress = message.loading(t('toast.batchDeleting', { ok: 0, n }), { duration: 0 })
  let ok = 0
  for (const s of list) {
    try {
      await api.deleteServer(s.id, s.dataSourceName)
      ok++
      if (selection.value?.serverId === s.id) selection.value = null
    } catch (e) {
      console.warn('[ServerListView] batch delete failed:', s.id, e?.message || e)
    }
    progress.content = t('toast.batchDeleting', { ok, n })
  }
  await reload()
  const failed = n - ok
  if (!failed) {
    progress.type = 'success'
    progress.content = t('toast.batchDeleted', { n: ok })
  } else {
    progress.type = ok ? 'warning' : 'error'
    progress.content = t('toast.batchDeleted', { n: ok }) + ' · ' + t('toast.batchDeleteFailed', { n: failed })
  }
  setTimeout(() => progress.destroy(), failed ? 5000 : 2500)
}

// ---- 批量编辑：批量条按钮 → 抽屉 bulk 模式 ----
// 共享值计算需要列表 DTO：按勾选 id 从 servers 快照取（列表 DTO = camelCase 域，
// 与批量 patch 同域）；快照里找不到的 id（列表恰在勾选后变化）直接跳过，以能取到的为准。
function openBulkEdit(ids) {
  const list = (ids || []).map((id) => servers.value.find((s) => s.id === id)).filter(Boolean)
  if (!list.length) return
  // 恰勾 1 台：工具栏按钮已显「编辑」，转单台编辑抽屉（与右键「编辑」/E 键同走 openEdit），
  // 不进 bulk 模式——单台能看到全量字段，不必受批量 allow-list 限制
  if (list.length === 1) {
    openEdit(list[0])
    return
  }
  editor.value = {
    mode: 'bulk',
    ds: list[0].dataSourceName || 'Local', // 后端 batch 端点单 ds；跨源勾选由抽屉内提示拦下
    bulkIds: list.map((s) => s.id),
    bulkServers: list,
  }
}

// 保存成功（新建/编辑/复制/批量共用的 saved 事件）：UpdateServer 系不触发 SSE（已知后端
// 行为），前端兜底 reload；这里收敛抽屉状态 + 清理指向旧行的选中态
// （名称/协议可能已变）。bulk 模式目标是一个 id 集，不涉及树叶选中回退。
function onSaved({ id, mode }) {
  if (mode === 'edit' && selection.value?.serverId && selection.value.serverId !== id) {
    // 编辑目标的树叶选中态与保存对象不符（多选中残留）——保守回退，避免错误高亮
    selection.value = null
  }
  editor.value = null
  reload()
}

// ---- 标签管理模态：SideTree「+ 管理」chip 打开；ds = 当前树选中的数据源 ----
const tagManager = ref(null) // null=关 | { ds }
function openTagManager() {
  tagManager.value = { ds: selection.value?.dataSourceName || 'Local' }
}

// ---- 导入模态：空库引导卡「导入」与顶栏「+ ▾ 导入」两个入口共用；
// 默认目标数据源 = 当前树选中（模态打开时取快照，关闭即销毁不跨次残留）----
const importModal = ref(false)
</script>

<template>
  <div class="server-list">
    <aside class="sidebar" :class="{ collapsed }">
      <SideTree
        v-if="!collapsed"
        ref="sideTree"
        v-model:selection="selection"
        v-model:tag="activeTag"
        @update:collapsed="collapsed = $event"
        @manage-tags="openTagManager"
      />
      <button v-else class="expand-rail" :title="t('sidebar.expand')" @click="collapsed = false">»</button>
    </aside>
    <main class="content">
      <div class="crumb-row">
        <!-- 可点击面包屑：逐级返回；末段=当前层级 -->
        <div class="crumb" :title="crumbTitle">
          <template v-for="(seg, i) in crumbSegments" :key="i">
            <button v-if="i < crumbSegments.length - 1" class="crumb-btn" @click="selection = seg.sel">
              {{ seg.label }}
            </button>
            <span v-else class="crumb-cur">{{ seg.label }}</span>
            <span v-if="i < crumbSegments.length - 1" class="crumb-sep">›</span>
          </template>
          <span class="crumb-count">{{ t('crumb.count', { n: listCount }) }}</span>
        </div>
        <!-- 活动过滤器 chips：搜索词 + 标签（文件夹选择由面包屑本身表达，不重复）；
             点击 chip 上的 ✕ 清对应过滤器，样式沿用 search-chip -->
        <span v-if="searchActive" class="search-chip" :title="t('crumb.searchChip')">
          <span class="sc-label">⌕ {{ searchQuery }}</span>
          <button class="sc-x" :title="t('crumb.clearSearch')" @click="searchQuery = ''">✕</button>
        </span>
        <span v-if="activeTag" class="search-chip" :title="'#' + activeTag">
          <span class="sc-label"># {{ activeTag }}</span>
          <button class="sc-x" :title="t('empty.clearTag')" @click="activeTag = ''">✕</button>
        </span>
        <!-- 批量条 + 表头工具簇宿主：ServerTable 把勾选批量操作与
             ≡ 自定义顺序 / ▦ 列菜单 Teleport 进来，与面包屑同行右侧对齐。容器位于
             骨架屏/空态 v-if 链之外恒存在（Teleport 目标必须先于 ServerTable 挂载），
             内容随表格卸载自动消失 -->
        <div id="crumb-actions" class="crumb-actions"></div>
      </div>

      <!-- 首载骨架屏：6 行灰块脉动（状态点 + 图标圆 + 名称/地址两横条，行高对齐真实行），
           数据到达后被表格原地替换（同布局高度，无跳动） -->
      <div v-if="showSkeleton" class="skeleton-host" aria-hidden="true">
        <div v-for="i in 6" :key="i" class="sk-row" :style="{ '--d': (i - 1) * 120 + 'ms' }">
          <span class="sk sk-dot"></span>
          <span class="sk sk-icon"></span>
          <span class="sk sk-bar sk-name"></span>
          <span class="sk sk-bar sk-addr"></span>
        </div>
      </div>

      <!-- 后端不可达：居中提示，取代空库引导卡——后端未运行时引导用户新建/导入会误导 -->
      <div v-else-if="showOffline" class="empty-offline">
        <div class="eo-title">{{ t('empty.offline') }}</div>
        <div class="eo-hint">{{ t('empty.offlineHint') }}</div>
      </div>

      <!-- 空库引导卡片：新建与导入均已接线；导入按钮下补支持格式说明 + 支持协议一览
           （灰阶瓦片：协议身份色留给行内徽章，引导卡保持克制） -->
      <div v-else-if="showGuide" class="empty-guide">
        <div class="eg-title">{{ t('empty.none') }}</div>
        <div class="eg-actions">
          <button class="eg-btn eg-primary" @click="openCreate">+ {{ t('empty.newFirst') }}</button>
          <button class="eg-btn" @click="importModal = true">⤓ {{ t('import.title') }}</button>
        </div>
        <div class="eg-import-hint">{{ t('empty.importFormats') }}</div>
        <div class="eg-protocols" aria-hidden="true">
          <span v-for="p in GUIDE_PROTOCOLS" :key="p" class="eg-proto">
            <span class="eg-tile">{{ p.charAt(0) }}</span
            >{{ p }}
          </span>
        </div>
        <div class="eg-hint">{{ t('empty.launcherHint') }}</div>
      </div>

      <!-- 无匹配（库非空但标签/搜索交集为空）：轻提示（附搜索词或标签名）+ 按维度清除过滤 -->
      <div v-else-if="showNoMatch" class="empty-nomatch">
        <div class="en-title">{{ t('empty.noMatch') }}</div>
        <div class="en-detail">{{ noMatchDetail }}</div>
        <button class="en-clear" @click="clearNextFilter">{{ clearFilterLabel }}</button>
      </div>

      <ServerTable
        v-else
        ref="table"
        class="table-host"
        :servers="visibleServers"
        :selection="selection"
        :folders="currentFolders"
        :query="searchQuery"
        @counted="tableCount = $event"
        @connect="onConnect"
        @batch-connect="onBatchConnect"
        @batch-delete="onBatchDelete"
        @bulk-edit="openBulkEdit"
        @export="onExport"
        @edit="openEdit"
        @duplicate="openDuplicate"
        @delete="onDelete"
        @open-folder="onOpenFolder"
        @create-folder="onCreateFolder"
        @rename-folder="onRenameFolder"
        @delete-folder="onDeleteFolder"
        @move-to-folder="onMoveToFolder"
        @move-folder="onMoveFolder"
        @new-server="openCreate"
        @import-servers="importModal = true"
      >
        <!-- 表内空态（表格可见、当前视图 0 行）覆写默认文案，按致因二分统一：
             ① 标签过滤致空（当前文件夹没有任何带该标签的服务器）→ 与表外无匹配态同款
             「无匹配结果 + 标签说明 + 清除标签」——有过滤器致空一律归无匹配，不再说"此视图暂无服务器"；
             ② 无任何过滤的真空文件夹 → 引导文案（#empty.folder，指向 + / 右键两个入口）。
             搜索致空不会到这（零命中在表外 showNoMatch 接管；有命中则行集非空） -->
        <template #empty>
          <div v-if="activeTag" class="table-empty">
            <div class="te-title">{{ t('empty.noMatch') }}</div>
            <div class="te-detail">{{ t('empty.taggedNone', { tag: activeTag }) }}</div>
            <button class="te-clear" @click="clearNextFilter">{{ clearFilterLabel }}</button>
          </div>
          <div v-else class="table-empty">
            <div class="te-detail">{{ t('empty.folder') }}</div>
          </div>
        </template>
      </ServerTable>

      <!-- 标签管理模态：置顶/重命名/删除/连接全部；关闭即销毁（v-if 收敛状态） -->
      <TagManagerModal
        v-if="tagManager"
        :show="true"
        :ds="tagManager.ds"
        @update:show="tagManager = $event ? tagManager : null"
      />

      <!-- 导入模态：默认数据源/文件夹取当前树选中（文件夹内入口导入即落该文件夹）；
           关闭即销毁（文件/错误不跨次残留） -->
      <ImportModal
        v-if="importModal"
        :show="true"
        :default-ds="selection?.dataSourceName || 'Local'"
        :default-folder="selection?.folderPath || ''"
        @update:show="importModal = $event"
      />

      <!-- 连接编辑抽屉：新建/编辑/复制/批量入口共用；fixed 覆盖层，不参与 flex 布局 -->
      <EditorDrawer
        v-if="editor"
        :mode="editor.mode"
        :server-id="editor.serverId || ''"
        :data-source-name="editor.ds"
        :protocol="editor.protocol || ''"
        :initial-server="editor.initial || null"
        :initial-folder="editor.initialFolder || ''"
        :duplicate-from="editor.duplicateFrom || ''"
        :bulk-ids="editor.bulkIds || []"
        :bulk-servers="editor.bulkServers || []"
        @close="editor = null"
        @saved="onSaved"
      />

      <!-- 底部状态栏：数据源状态点 · 台数/标签数 · SSE 可达性 · 语言切换 -->
      <footer class="status-bar">
        <span v-for="ds in dsShown" :key="ds.name" class="sb-ds" :title="dsTitle(ds)">
          <span class="sb-dot" :class="dsDotClass(ds.status)"></span>
          <span class="sb-name">{{ ds.name }}</span>
        </span>
        <span v-if="dsHidden" class="sb-more" :title="t('statusbar.dsMore', { n: dsHidden })">+{{ dsHidden }}</span>
        <div class="sb-right">
          <!-- 计数用 vue-i18n 复数形式（en："{n} server | {n} servers"）；zh 无管道单形式同样兼容，
               {m} 需显式传命名参数 + 复数值（隐式仅绑定 n） -->
          <span
            >{{ t('statusbar.serverCount', servers.length) }} ·
            {{ t('statusbar.tagCount', { m: tags.length }, tags.length) }}</span
          >
          <span class="sb-sse" :title="t('statusbar.sseTip')">
            <span class="sb-dot" :class="connected ? 'ok' : 'bad'"></span>
            {{ connected ? t('statusbar.sseOk') : t('statusbar.sseOff') }}
          </span>
          <button class="sb-lang" :title="t('statusbar.langSwitch')" @click="toggleLocale">{{ nextLang }}</button>
        </div>
      </footer>
    </main>
  </div>
</template>

<style scoped>
.server-list {
  flex: 1;
  min-width: 0;
  min-height: 0;
  display: flex;
}
.sidebar {
  flex: 0 0 216px;
  width: 216px;
  min-height: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--border);
  background: var(--bg-panel);
}
.sidebar.collapsed {
  flex-basis: 44px;
  width: 44px;
}
.expand-rail {
  flex: 1;
  border: none;
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-title);
  cursor: pointer;
}
.expand-rail:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
.content {
  flex: 1;
  min-width: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--bg);
}
.crumb-row {
  flex: 0 0 36px;
  display: flex;
  align-items: center;
  padding: 0 14px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-panel);
}
.crumb {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--fs-body);
  color: var(--text-2);
}
.crumb-cur {
  color: var(--text-1);
}
.crumb-btn {
  border: none;
  background: transparent;
  padding: 1px 2px;
  color: var(--text-3);
  font-size: var(--fs-body);
  line-height: 1.4;
  cursor: pointer;
  border-radius: var(--radius-xs);
}
.crumb-btn:hover {
  background: var(--bg-hover);
  color: var(--accent-text);
}
.crumb-sep {
  margin: 0 3px;
  color: var(--text-4);
}
.crumb-count {
  color: var(--text-4);
}
.search-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  flex: 0 1 auto;
  min-width: 0;
  max-width: 280px;
  margin-left: 10px;
  padding: 2px 4px 2px 9px;
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  background: var(--bg-elevated);
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1.4;
}
.sc-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sc-x {
  flex: 0 0 auto;
  width: 18px;
  height: 18px;
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--text-4);
  font-size: var(--fs-micro);
  line-height: 1;
  cursor: pointer;
}
.sc-x:hover {
  background: var(--bg-hover);
  color: var(--text-1);
}
/* ServerTable Teleport 内容宿主：批量条 + ≡/▦ 工具簇靠右与面包屑同行；
   gap 由内容自带（batch-bar 8px / table-tools 4px），此处只管整体右贴与纵向居中 */
.crumb-actions {
  flex: 0 0 auto;
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 8px;
}
.table-host {
  flex: 1;
  min-height: 0;
}

/* ---- 首载骨架屏：行结构对齐 ServerRow（状态点 + 图标圆 + 名称/地址横条），纯 CSS 透明度脉动；
   灰块用 --bg-hover 浮于 --bg，逐行错峰（--d 由内联注入），36px 行高 + 分隔线与真实行一致 ---- */
.skeleton-host {
  flex: 1;
  min-height: 0;
  overflow: auto;
}
.sk-row {
  display: flex;
  align-items: center;
  height: 36px;
  padding: 0 10px 0 34px;
  border-bottom: 1px solid var(--border);
}
.sk {
  background: var(--bg-hover);
  animation: sk-pulse 1.4s ease-in-out infinite;
  animation-delay: var(--d, 0s);
}
.sk-dot {
  flex: 0 0 8px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.sk-icon {
  flex: 0 0 22px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  margin: 0 10px 0 30px;
}
.sk-bar {
  height: 10px;
  border-radius: var(--radius-ctrl);
}
.sk-name {
  flex: 0 0 26%;
}
.sk-addr {
  flex: 0 0 16%;
  margin-left: 28px;
}
@keyframes sk-pulse {
  0%,
  100% {
    opacity: 0.5;
  }
  50% {
    opacity: 1;
  }
}
@media (prefers-reduced-motion: reduce) {
  .sk {
    animation: none;
    opacity: var(--opacity-hint); /* 静态弱化档与装饰性弱化同源（F16） */
  }
}

/* ---- 空库引导卡片：居中；新建/导入均已接线，提示行指向桌面启动器热键 ---- */
.empty-guide {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 14px;
  padding: 24px;
}
.eg-title {
  font-size: var(--fs-title);
  font-weight: 600;
  color: var(--text-2);
}
.eg-actions {
  display: flex;
  gap: 10px;
}
.eg-btn {
  height: var(--ctrl-h-m);
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 0 14px;
  cursor: pointer;
}
.eg-btn:disabled {
  opacity: var(--opacity-disabled);
  cursor: not-allowed;
}
/* 空态 CTA hover（V4）：与 .ed-btn/.bb-btn 同款反馈（border-strong + bg-hover + text-1） */
.eg-btn:hover:not(:disabled) {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
.eg-primary {
  border-color: var(--accent);
  color: var(--accent-text);
}
/* 主按钮 hover 保留 accent 边框（E3 统一规则：accent 边框 + bg-hover + accent-text） */
.eg-primary:hover:not(:disabled) {
  border-color: var(--accent);
  background: var(--bg-hover);
  color: var(--accent-text);
}
.eg-hint {
  font-size: var(--fs-body);
  color: var(--text-4);
}
.eg-import-hint {
  margin-top: 4px;
  font-size: var(--fs-caption);
  color: var(--text-4);
}
/* 支持协议一览：灰阶瓦片（首字母）+名称，小字排一行；身份色留给行内徽章 */
.eg-protocols {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 6px 12px;
  max-width: 460px;
}
.eg-proto {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--text-4);
  font-size: var(--fs-caption);
}
.eg-tile {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border-radius: var(--radius-xs);
  background: var(--bg-elevated);
  border: 1px solid var(--border);
  color: var(--text-3);
  font-size: var(--fs-micro);
  font-weight: 600;
}

/* ---- 后端不可达：居中提示（无操作——恢复自动进行，不做手动重试按钮） ---- */
.empty-offline {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
}
.eo-title {
  font-size: var(--fs-title);
  font-weight: 600;
  color: var(--text-2);
}
.eo-hint {
  font-size: var(--fs-body);
  color: var(--text-4);
}

/* ---- 无匹配（过滤后为空）：轻提示 + 清除过滤按钮 ---- */
.empty-nomatch {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
}
.en-title {
  font-size: var(--fs-body);
  color: var(--text-3);
}
.en-detail {
  font-size: var(--fs-body);
  color: var(--text-4);
}
/* 无匹配/表内空态的清除过滤按钮共用一份定义（此前 en-clear/te-clear 两份逐行重复且
   与 eg-btn 底色/内距漂移）；参数对齐 eg-btn（elevated 底 + 14px 内距） */
.en-clear,
.te-clear {
  margin-top: 6px;
  height: var(--ctrl-h-m);
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: var(--bg-elevated);
  color: var(--text-2);
  font-size: var(--fs-body);
  line-height: 1;
  padding: 0 14px;
  cursor: pointer;
}

.en-clear:hover,
.te-clear:hover {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}

/* ---- 表内空态（#empty 插槽）：居中轻文案，与表外空态同视觉语言；
   高度沿用 ServerTable 默认空态的 160px，避免切换时空区跳变 ---- */
.table-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  height: 160px;
  padding: 0 24px;
  text-align: center;
}
.te-title {
  font-size: var(--fs-body);
  color: var(--text-3);
}
.te-detail {
  font-size: var(--fs-body);
  color: var(--text-4);
}

/* ---- 底部状态栏：26px 单行，左=数据源状态点（≤3 个 + 溢出 +N），右=统计/SSE/语言 ---- */
.status-bar {
  flex: 0 0 26px;
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 0 12px;
  border-top: 1px solid var(--border);
  background: var(--bg-panel);
  color: var(--text-3);
  font-size: var(--fs-caption);
  white-space: nowrap;
  overflow: hidden; /* 数据源名过长时截断而非把右侧统计挤出可视区 */
}
.sb-ds {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
  max-width: 130px;
}
.sb-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}
.sb-dot {
  flex: 0 0 6px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
}
.sb-dot.ok {
  background: var(--success);
}
.sb-dot.bad {
  background: var(--danger);
}
.sb-dot.idle {
  background: var(--text-4);
}
.sb-more {
  color: var(--text-4);
}
.sb-right {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  overflow: hidden; /* 左侧数据源名撑满时不被顶出，内部整体截断 */
}
.sb-sse {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: help; /* title 说明其语义为后端可达性而非连接会话状态 */
}
.sb-lang {
  height: var(--ctrl-h-s);
  border: 1px solid var(--border);
  border-radius: var(--radius-ctrl);
  background: transparent;
  color: var(--text-3);
  font-size: var(--fs-caption);
  line-height: 1;
  padding: 0 10px;
  cursor: pointer;
}
.sb-lang:hover {
  border-color: var(--border-strong);
  background: var(--bg-hover);
  color: var(--text-1);
}
</style>
