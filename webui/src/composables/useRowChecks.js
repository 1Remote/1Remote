/**
 * 表格行勾选（自 ServerTable.vue 拆出）：checked 勾选集（始终只存服务器 id）+ Shift 范围/
 * Ctrl 切换 + 表头三态全选 + 文件夹行勾选（含子孙）+ 数据变化剔除（裸点击=纯光标，不在此单元）。
 *
 * - 裸点击=纯光标（不改勾选集）、Ctrl/⌘=切换、Shift=范围（锚点 anchorIdx=上次点击行序号；
 *   裸点击也更新锚点——先点 A 再 Shift 点 B 自然选中区间）。勾选入口：行内复选框 /
 *   Ctrl+点击 / Shift+点击 / Ctrl+A / 表头与文件夹复选框。表头全选=「当前视图可见服务器行」
 *   （合并而非替换——保留文件夹勾选展开的隐藏子孙 id；要连子孙一起选请勾文件夹行复选框）。
 * - 文件夹勾选：全部子孙服务器 id（folderPath 等于该文件夹路径或以 '路径/' 开头，深层
 *   子文件夹一并命中）加入/移出 checked，并限定同数据源（防「全部数据」根跨库同名路径互串）。
 *   各文件夹行三态（checked/indeterminate/count）以 servers 域一次算全层并缓存到依赖变化。
 * - 剔除口径：挂过滤后数据列表（servers getter，标签/搜索/SSE 重载域）而非视图列表
 *   （sorted）——文件夹勾选展开的子孙 id 不在当前视图的直接子级列表里，按视图剔除会在
 *   进入文件夹的一瞬清光子孙勾选；「服务器仍在收到的过滤列表中即保留」，跨层级导航勾选
 *   持续存在，被删除/过滤掉的行即时剔除，批量条计数始终对着真实存在的服务器。
 * - sorted 变化时作废 Shift 范围锚点（树切换/搜索过滤后旧行号已无意义，选区重新锚定）。
 *   cursorId 等纯视觉焦点不属本单元，由调用方在自身 watch 中处理。
 */
import { computed, ref, watch, watchEffect } from 'vue'
import { isInSubtreeOf } from './folders.js'

/**
 * @param {Object} opts
 * @param {import('vue').ComputedRef<Array>} opts.sorted 排序后的可见服务器列表（范围选择的行号域）
 * @param {() => Array} opts.servers 本组件收到的过滤后服务器列表（勾选保留/剔除的判定域）
 * @param {() => Array} opts.folders 当前层级文件夹行 [{name, path, dsName, count}]
 */
export function useRowChecks({ sorted, servers, folders }) {
  const checked = ref(new Set())
  let anchorIdx = -1

  const allChecked = computed(() => sorted.value.length > 0 && sorted.value.every((s) => checked.value.has(s.id)))
  const someChecked = computed(() => !allChecked.value && sorted.value.some((s) => checked.value.has(s.id)))
  const allCb = ref(null) // 表头三态复选框：indeterminate 需写 DOM 属性
  watchEffect(() => {
    if (allCb.value) allCb.value.indeterminate = someChecked.value
  })

  function toggleChecked(id) {
    const next = new Set(checked.value)
    next.has(id) ? next.delete(id) : next.add(id)
    checked.value = next
  }

  // 行点击的勾选分支（Ctrl/Shift；裸点击不改勾选集——单击仅设光标行，勾选只由
  // 复选框/Ctrl+点击/Shift+点击/Ctrl+A 触发。光标落位等行级副作用归调用方，先于本调用执行）
  function rowClickSelect(ev, idx, id) {
    if (ev.shiftKey && anchorIdx >= 0) {
      const lo = Math.min(anchorIdx, idx)
      const hi = Math.max(anchorIdx, idx)
      checked.value = new Set(sorted.value.slice(lo, hi + 1).map((s) => s.id))
    } else if (ev.ctrlKey || ev.metaKey) {
      toggleChecked(id)
      anchorIdx = idx
    } else {
      anchorIdx = idx // 裸点击=纯光标：不增删勾选，仅把 Shift 范围锚点移到本行
    }
  }
  // 行内复选框点击：切换 + 锚点落位
  function onToggleSelect(id, idx) {
    toggleChecked(id)
    anchorIdx = idx
  }

  // 半选/未选 → 勾全部可见行（合并语义：保留隐藏子孙；Ctrl+A 同款复用）
  function addAllVisible() {
    const next = new Set(checked.value)
    for (const s of sorted.value) next.add(s.id)
    checked.value = next
  }
  function toggleAll() {
    if (allChecked.value) {
      clearChecked()
      return
    }
    addAllVisible()
  }
  function clearChecked() {
    checked.value = new Set()
    checkedFolders.value = new Map()
    anchorIdx = -1
  }

  // ---- 文件夹行勾选（批量操作含子孙）----
  function folderDescendantIds(f) {
    // 勾选=全部子孙（isInSubtreeOf 单一语义源；显示语义是 isDirectChildOf——两者有意不同）
    return servers()
      .filter((s) => s.dataSourceName === f.dsName && isInSubtreeOf(s.folderPath, f.path))
      .map((s) => s.id)
  }
  // 空文件夹勾选（owner 2026-09-21 第二轮反馈）：无子孙服务器的文件夹复选框此前禁用，
  // 无法勾选几个不用的空文件夹后直接删除。checkedFolders 只收空文件夹（键与 rowKey
  // 同构 'f:ds:path'，值存 {dsName,path} 供批量删除直接消费）；有子孙的文件夹仍走
  // 「勾选=级联全部子孙服务器」（owner H38 注记「选文件夹=勾选所有子级」的语义）。
  const checkedFolders = ref(new Map())

  // 各文件夹行三态派生（checked=全选 / indeterminate=半选 / count=子孙数）。
  // 空文件夹（count=0）的 checked 来自 checkedFolders（可勾选待批量删除）。
  // Map 键与调用方 rowKey 同构（'f:ds:path'）；O(文件夹×服务器) 一次算全层并缓存到依赖
  // 变化——若逐行内联计算会随虚拟滚动窗口反复重算
  const folderChecks = computed(() => {
    const m = new Map()
    for (const f of folders()) {
      const ids = folderDescendantIds(f)
      let n = 0
      for (const id of ids) if (checked.value.has(id)) n++
      const key = 'f:' + f.dsName + ':' + f.path
      m.set(key, {
        checked: ids.length > 0 ? n === ids.length : checkedFolders.value.has(key),
        indeterminate: n > 0 && n < ids.length,
        count: ids.length,
      })
    }
    return m
  })
  // 勾选文件夹 = 全部子孙 id 加入 checked（未选/半选态点击都补全），已全选 = 移除全部子孙。
  // checked 始终只存服务器 id，计数与批量编辑/导出自然作用于全集。
  // 空文件夹（0 子孙）= 切换 checkedFolders（勾选待批量删除的文件夹本身）
  function onFolderToggleCheck(f) {
    const ids = folderDescendantIds(f)
    if (!ids.length) {
      const key = 'f:' + f.dsName + ':' + f.path
      const next = new Map(checkedFolders.value)
      if (next.has(key)) next.delete(key)
      else next.set(key, { dsName: f.dsName, path: f.path })
      checkedFolders.value = next
      return
    }
    const uncheck = ids.every((id) => checked.value.has(id))
    const next = new Set(checked.value)
    for (const id of ids) uncheck ? next.delete(id) : next.add(id)
    checked.value = next
    anchorIdx = -1 // 文件夹勾选无行号语义，作废 Shift 范围锚点（下次点击重新锚定）
  }

  // sorted 变化作废 Shift 范围锚点（视图行号已变，选区必须重新锚定）
  watch(sorted, () => {
    anchorIdx = -1
  })
  // 空文件夹勾选的剔除与显示同谓词（round8 P4/M20）：文件夹行消失（不在当前层级
  // folderChecks）或不再为空（count>0，被填入了服务器）即从勾选集中剔除——此前剔除
  // 只清服务器集，显示层已按三态语义如实显示未勾，但 checkedFolders 残留会让批量条
  // 仍计「1 个空文件夹」、确认框仍承诺「删除空文件夹」，执行时其内容被静默上移
  watch(folderChecks, (m) => {
    if (!checkedFolders.value.size) return
    const next = new Map(checkedFolders.value)
    let changed = false
    for (const key of checkedFolders.value.keys()) {
      const st = m.get(key)
      if (!st || st.count > 0) {
        next.delete(key)
        changed = true
      }
    }
    if (changed) checkedFolders.value = next
  })
  // 数据变化剔除已不存在的勾选（口径见文件头：servers 域而非视图域）
  watch(servers, (list) => {
    if (!checked.value.size) return
    const ids = new Set(list.map((s) => s.id))
    const kept = [...checked.value].filter((id) => ids.has(id))
    if (kept.length !== checked.value.size) checked.value = new Set(kept)
  })

  return {
    checked,
    checkedFolders,
    allChecked,
    someChecked,
    allCb,
    rowClickSelect,
    onToggleSelect,
    addAllVisible,
    toggleAll,
    clearChecked,
    folderChecks,
    onFolderToggleCheck,
  }
}
