/**
 * 编辑器入口总线：App.vue 顶栏「+」下拉（新建/导入）与 ServerListView 的编辑抽屉/
 * 导入模态状态之间的跨层通信——顶栏在 router-view 之外、无法向子视图 emit，
 * 沿用 useServers 的模块级共享 ref 模式（全局约定：不引 Pinia）。
 * 语义：requestNewServer()/requestImport() 各自递增计数；ServerListView watch
 * 计数变化分别打开新建抽屉（归属数据源取其当前树选中，见 openCreate）与导入模态。
 * 反向承载编辑抽屉占用态：editorOpen——ServerListView 在 editor 状态变化时
 * setEditorOpen 同步，App.vue 消费（编辑期间锁定顶栏搜索/「+」/⚙，避免编辑中
 * 切换上下文导致误导航/丢焦点）。
 */
import { ref } from 'vue'

const createRequest = ref(0)
const importRequest = ref(0)
// 编辑抽屉占用态：true = 抽屉打开（新建/编辑/复制/批量任一模式）
const editorOpen = ref(false)

function requestNewServer() {
  createRequest.value++
}

function requestImport() {
  importRequest.value++
}

function setEditorOpen(open) {
  editorOpen.value = !!open
}

export function useEditorBus() {
  return { createRequest, requestNewServer, importRequest, requestImport, editorOpen, setEditorOpen }
}
