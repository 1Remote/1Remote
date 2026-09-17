/**
 * 编辑器入口总线：App.vue 顶栏「+」下拉（新建/导入）与 ServerListView 的编辑抽屉/
 * 导入模态状态之间的跨层通信——顶栏在 router-view 之外、无法向子视图 emit，
 * 沿用 useServers 的模块级共享 ref 模式（全局约定：不引 Pinia）。
 * 语义：requestNewServer()/requestImport() 各自递增计数；ServerListView watch
 * 计数变化分别打开新建抽屉（归属数据源取其当前树选中，见 openCreate）与导入模态。
 */
import { ref } from 'vue'

const createRequest = ref(0)
const importRequest = ref(0)

function requestNewServer() {
  createRequest.value++
}

function requestImport() {
  importRequest.value++
}

export function useEditorBus() {
  return { createRequest, requestNewServer, importRequest, requestImport }
}
