/**
 * 编辑器入口总线（Plan 2 Task 8）：App.vue 顶栏「+ 新建」按钮与 ServerListView 的
 * 编辑抽屉状态之间的跨层通信——顶栏在 router-view 之外、无法向子视图 emit，
 * 沿用 useServers 的模块级共享 ref 模式（spec §9.1：不引 Pinia）。
 * 语义：requestNewServer() 递增计数；ServerListView watch 计数变化打开新建抽屉
 * （归属数据源取其当前树选中，见 openCreate）。
 * Plan 4 Task 3：顶栏「+」改为下拉（新建/导入）——requestImport() 同款计数递增，
 * ServerListView watch 后打开导入模态。
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
