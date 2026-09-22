// 列状态：ServerTable 列宽 + 显隐的持久化。
// localStorage '1r-cols' = { colKey: { w: px数|null, hidden: bool } }——仅本地（有意简化：
// 不入后端，WPF 侧 GridSplitter 宽度存 LocalityListViewService 也是机器本地视图状态）。
// 可隐藏列：name/addr/proto/note/folder/time（note 为备注列）；
// 固定最小集：check/status/tags/act（复选/状态/标签/操作列不允许隐藏——勾选与行内操作
// 是列表交互的地基）。
import { ref } from 'vue'

export const HIDEABLE_COLS = ['name', 'addr', 'proto', 'note', 'folder', 'time']

const state = ref(readAll())

function readAll() {
  try {
    const s = JSON.parse(localStorage.getItem('1r-cols') || 'null')
    if (s && typeof s === 'object') {
      // 防御：只保留合法键与合法值形状，损坏数据当空
      const out = {}
      for (const k of HIDEABLE_COLS) {
        const v = s[k]
        if (v && typeof v === 'object') {
          out[k] = {
            w: Number.isFinite(v.w) && v.w >= 40 ? Math.round(v.w) : null,
            hidden: v.hidden === true,
          }
        }
      }
      return out
    }
  } catch {
    /* 损坏数据当空 */
  }
  return {}
}

function persist() {
  try {
    localStorage.setItem('1r-cols', JSON.stringify(state.value))
  } catch {
    /* 隐私模式等写入失败可忽略 */
  }
}

export function useColumns() {
  const isHidden = (k) => state.value[k]?.hidden === true
  const widthOf = (k) => state.value[k]?.w ?? null
  function setHidden(k, hidden) {
    if (!HIDEABLE_COLS.includes(k)) return
    state.value = { ...state.value, [k]: { w: widthOf(k), hidden } }
    persist()
  }
  /** w = 像素数；null = 重置（回到默认 flex 比例） */
  function setWidth(k, w) {
    if (!HIDEABLE_COLS.includes(k)) return
    state.value = { ...state.value, [k]: { w: w == null ? null : Math.max(40, Math.round(w)), hidden: isHidden(k) } }
    persist()
  }
  /** 全部列宽重置（round8 P12：列菜单入口——此前「双击重置」只存在于不可聚焦的表头拖拽热区，
      键盘用户无法调宽看全文、列被拖坏也无恢复通道） */
  function resetWidths() {
    const next = { ...state.value }
    for (const k of HIDEABLE_COLS) next[k] = { w: null, hidden: isHidden(k) }
    state.value = next
    persist()
  }
  return { colState: state, HIDEABLE_COLS, isHidden, widthOf, setHidden, setWidth, resetWidths }
}
