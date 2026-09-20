// 列表跨层信号（App.vue ↔ ServerTable / SideTree）：组件树相隔多层（顶栏 → router-view
// → ServerListView → ServerTable），逐层 emit 转发噪音大——与 editorBus 同思路的轻量总线。
// 两组职责：搜索框 → 表格的键盘移交；列表行拖拽快照（SideTree 落区判定用）。
import { ref } from 'vue'

// ---- 键盘移交：App.vue 顶栏搜索框按下 ↑/↓ → 表格接管键盘并把光标落到首/末行。
// 每次按都赋新对象（watch 按引用判变，同方向重复按也每次触发）；表格隐藏
//（空态/设置页）时无消费者，信号自然丢弃 ----
export const focusHandoff = ref(null) // { delta } —— 1=↓ 首行方向 / -1=↑ 末行方向
export function handoffTableFocus(delta) {
  focusHandoff.value = { delta }
}

// ---- 列表行拖拽快照：dragover 期间规范禁止读 dataTransfer 数据（只有 types 可见），
// SideTree 的落区判定（同库才高亮/跨库 toast）需要被拖对象的 dataSourceName → 拖拽源
//（ServerTable）在 dragstart/dragend 时挂/摘快照。服务器行与文件夹行各一份；与树内
// 文件夹拖拽（SideTree 自持 dragRow）三条链路互斥，同时至多一个快照非空 ----
export const listDragServer = ref(null)
export const listDragFolder = ref(null)

// ---- 跨库拖拽悬停标记：dragover 拒绝（不 preventDefault → dropEffect=none → drop 不触发）
// 期间由落区侧置位，源侧 dragend 兜底出 toast。树（SideTree.onRowDragOver）与列表
//（ServerTable.onFolderDragOver）两侧共用同一标记——同一操作同一套反馈；回到合法
// 目标或成功 drop 即复位（不误报）。toast 由 SideTree 的 window dragend 监听统一出
//（边栏收起时无监听者，同树侧落区一并消失——已知边界）----
export const CROSS_DS_NONE = ''
export const CROSS_DS_SERVER = 'server'
export const CROSS_DS_FOLDER = 'folder'
export const crossDsHover = ref(CROSS_DS_NONE)
