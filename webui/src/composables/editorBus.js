/**
 * 编辑器入口总线：App.vue 顶栏「+」下拉（新建/导入）与 ServerListView 的编辑抽屉/
 * 导入模态状态之间的跨层通信——顶栏在 router-view 之外、无法向子视图 emit，
 * 沿用 useServers 的模块级共享 ref 模式（全局约定：不引 Pinia）。
 * 语义：requestNewServer()/requestImport() 各自递增计数；ServerListView watch
 * 计数变化分别打开新建抽屉（归属数据源取其当前树选中，见 openCreate）与导入模态。
 * 反向承载两类占用态，App.vue 顶栏消费（禁用搜索/「+」/⚙，避免界面叠加导致误导航）：
 * - editorOpen：编辑抽屉打开（新建/编辑/复制/批量任一模式），由 ServerListView watch 同步；
 * - uiLock：其他整屏/模态界面的通用锁（设置页路由、标签管理模态、导入模态——挂载即
 *   持锁、卸载释放，useUiLockWhileMounted）。计数而非布尔：模态可叠加（如标签管理
 *   与导入同开），后关者不能把先开者仍持有的锁误释放。
 */
import { computed, onBeforeUnmount, ref } from 'vue'

const createRequest = ref(0)
const importRequest = ref(0)
// 编辑抽屉占用态：true = 抽屉打开（新建/编辑/复制/批量任一模式）
const editorOpen = ref(false)
// 通用 UI 锁（计数）：>0 = 有设置页/模态等界面打开，顶栏整体禁用
const uiLockCount = ref(0)
const uiLock = computed(() => uiLockCount.value > 0)

function requestNewServer() {
  createRequest.value++
}

function requestImport() {
  importRequest.value++
}

function setEditorOpen(open) {
  editorOpen.value = !!open
}

// 取锁并返回释放函数（幂等）：调用方负责在界面关闭时释放
function acquireUiLock() {
  uiLockCount.value++
  let released = false
  return () => {
    if (released) return
    released = true
    uiLockCount.value = Math.max(0, uiLockCount.value - 1)
  }
}

/**
 * 组件存在期间持有 UI 锁：setup 中取锁、卸载释放——供「挂载即整屏/模态」的界面
 *（SettingsView / TagManagerModal / ImportModal，均 v-if 或路由挂载）一行接入。
 */
export function useUiLockWhileMounted() {
  const release = acquireUiLock()
  onBeforeUnmount(release)
}

export function useEditorBus() {
  return { createRequest, requestNewServer, importRequest, requestImport, editorOpen, setEditorOpen, uiLock }
}
