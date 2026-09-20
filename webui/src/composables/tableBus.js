// 列表跨层信号（App.vue ↔ ServerTable / SideTree）：组件树相隔多层（顶栏 → router-view
// → ServerListView → ServerTable），逐层 emit 转发噪音大——与 editorBus 同思路的轻量总线。
// - handoffTableFocus：搜索框 ↑/↓ 把键盘焦点移交给表格（focusHandoff 带 seq 自增，
//   同方向重复按也每次触发；表格隐藏（空态/设置页）时无消费者，信号自然丢弃）
// - listDragServer：列表服务器行拖拽中的服务器快照。dragover 期间规范禁止读
//   dataTransfer 数据（只有 types 可见），SideTree 的落区判定（同库才高亮/跨库 toast）
//   需要被拖服务器的 dataSourceName → 拖拽源（ServerTable）start/end 时在此挂快照；
//   与树内文件夹拖拽（SideTree 自持 dragRow）天然互斥，同时只会有一个非空。
// - listDragFolder：列表文件夹行拖拽中的文件夹快照（{ dsName, path, name }），
//   同一思路的文件夹版——SideTree 据此把树节点判为「移入」目标（moveFolder 链路）。
//   三条拖拽链路（服务器行/列表文件夹行/树内文件夹）互斥，同时至多一个快照非空。
import { ref } from 'vue'

let focusSeq = 0
export const focusHandoff = ref(null) // { seq, delta } —— delta 1=↓ 首行方向 / -1=↑ 末行方向
export function handoffTableFocus(delta) {
  focusHandoff.value = { seq: ++focusSeq, delta }
}

export const listDragServer = ref(null)
export const listDragFolder = ref(null)
