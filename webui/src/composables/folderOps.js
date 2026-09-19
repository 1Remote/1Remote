// 虚拟文件夹操作：新建/重命名/删除/移入的统一入口，
// SideTree（树右键菜单）与 ServerListView（列表右键/拖拽入文件夹）共用。
// 机制（有意设计）：
// - 新建 = PUT /api/ui-state/tree 向 expansion 字典增键（键即存在，值 true=展开且存在，
//   与 WPF 物化循环 ServerTreeViewModel.cs:873-889 一致，WPF 树自动可见空文件夹）；
// - 重命名 = 受影响服务器逐台 config GET → TreeNodes 前缀重写 → PUT（复用树拖拽循环）
//   + 字典键前缀重写；
// - 删除 = 空文件夹直接删；内有服务器时弹选择——连服务器一起删（WPF 实际行为）或
//   仅删文件夹、子项（服务器/子文件夹键）上移一级（见 deleteFolder/runDelete）；
// - 移入 = 目标路径逐台重写 TreeNodes（列表行拖到文件夹行 / 树拖拽同语义）。
// UpdateServer 路径不触发 SSE（已知后端行为），全部操作后显式 reload()（WPF parity 刷新）。
import { h, nextTick, ref } from 'vue'
import { NInput, useDialog, useMessage } from 'naive-ui'
import { useI18n } from 'vue-i18n'
import { api } from '../api'
import {
  SEP,
  buildTree,
  countHolderServers,
  fullKey,
  holderAt,
  parentPath,
  rewriteServerPath,
  rewriteTreeStateKeys,
} from './folders'
import { useServers } from './useServers'
import { useTreeState } from './useTreeState'

export function useFolderOps() {
  const { t } = useI18n()
  const message = useMessage()
  const dialog = useDialog()
  const { servers, datasources, reload } = useServers()
  const { folderPathsByDs, knownExpanded, setLocalKeys, persist } = useTreeState()

  const busy = ref(false) // 逐台 PUT / 键迁移进行中（防重入：SideTree 拖拽与本操作互斥参考）

  const dsWritable = (dsName) => datasources.value.find((d) => d.name === dsName)?.writable !== false

  // 同级既有文件夹名（新建/重命名查重；物化后的空文件夹也在树模型里，一并算重名）
  function siblingNames(dsName, parent) {
    const holder = holderAt(buildTree(servers.value, datasources.value, folderPathsByDs.value), dsName, parent || '')
    return holder ? holder.folders.map((f) => f.name) : []
  }

  // 名称输入对话框（naive dialog + NInput 渲染函数）：resolve(名称) | resolve(null)；
  // onPositiveClick 返回 false 保持打开（校验失败就地提示）。
  // 回车=确认走三层冗余（宿主 WebView2 的焦点到达路径存在环境差异，不依赖任何单一环节）：
  // - NInput 元素级 onKeydown：焦点在输入框内时（正常路径）keydown 冒泡到 NInput 根元素触发；
  // - window 级 keydown 兜底：对话框打开期间无论焦点在哪（body/其他控件），回车一律走
  //   同一 submit；焦点在按钮/textarea/n-select（回车=原生 click/换行/选中语义）时让位；
  // - 打开即显式 focus()：不依赖 autofocus 属性对动态插入 DOM 的生效（HTML 规范允许
  //   每 document 只 flush 一次，宿主实现存在差异）。
  // IME 组合输入中的回车（候选选字）各级一致忽略；无效名时就地提示并保持打开。
  // settle 幂等：同一次回车先在输入框级触发、再冒泡到 window 级重复触发，与按钮/关闭
  // 路径竞争时只生效一次；window 监听随 settle 移除，onAfterLeave 兜底任何关闭路径。
  function promptName(title, initial) {
    return new Promise((resolve) => {
      const name = ref(initial || '')
      let settled = false
      let dia = null
      const settle = (v) => {
        if (settled) return
        settled = true
        window.removeEventListener('keydown', onWinEnter, { capture: true })
        resolve(v)
      }
      const submit = () => {
        if (settled) return true
        const v = name.value.trim()
        if (!v || v.includes('/')) {
          message.warning(t('tree.folderNameInvalid'))
          return false
        }
        settle(v)
        return true
      }
      function onWinEnter(e) {
        if (e.key !== 'Enter' || e.isComposing || e.repeat) return
        if (e.target?.closest?.('button, textarea, .n-select, [contenteditable]')) return
        // capture 阶段拦截并截断传播：先于表格等 bubble 监听（preventDefault 只取消默认
        // 行为、不阻断监听器），不 stopPropagation 的话焦点全失败（tableFocused 仍 true）
        // 时同一次回车还会被 ServerTable 的 Enter 连接消费——提交对话框的同时连接光标行
        e.preventDefault()
        e.stopPropagation()
        if (submit()) dia?.destroy()
      }
      window.addEventListener('keydown', onWinEnter, { capture: true })
      dia = dialog.create({
        title,
        content: () =>
          h(NInput, {
            value: name.value,
            placeholder: t('tree.folderNamePlaceholder'),
            autofocus: true,
            'onUpdate:value': (v) => {
              name.value = v
            },
            onKeydown: (e) => {
              if (e.key === 'Enter' && !e.isComposing && submit()) dia.destroy()
            },
          }),
        positiveText: t('common.ok'),
        negativeText: t('editor.cancel'),
        onPositiveClick: () => submit() || false,
        onNegativeClick: () => settle(null),
        onClose: () => settle(null),
        // 关闭动画结束的安全网（任一 hide 路径）：确保 settle 与监听移除，防泄漏悬挂
        onAfterLeave: () => settle(null),
      })
      // 打开即显式聚焦（不依赖 autofocus 属性的宿主差异，见上）：取最新挂载的对话框输入框
      nextTick(() => {
        const inputs = document.querySelectorAll('.n-dialog input')
        inputs[inputs.length - 1]?.focus()
      })
    })
  }

  async function createFolder(dsName, parent) {
    if (!dsWritable(dsName)) {
      message.warning(t('cv.readOnly'))
      return
    }
    const name = await promptName(t('tree.newFolder'), '')
    if (!name) return
    if (siblingNames(dsName, parent).includes(name)) {
      message.warning(t('tree.folderNameExists'))
      return
    }
    const key = fullKey(dsName, parent ? parent + '/' + name : name)
    setLocalKeys({ [key]: true }) // 键即存在且展开 → 树/列表即时物化，不等 PUT 往返
    const ok = await persist((m) => {
      m[key] = true
    })
    if (!ok) {
      setLocalKeys({}, [key]) // 回滚本地物化
      message.error(t('tree.folderCreateFailed'))
      return
    }
    message.success(t('tree.folderCreated', { name }))
    reload() // WPF parity refresh（tree-state 已本地更新；servers 重取兜底）
  }

  // 受影响服务器（oldPath 前缀下）逐台 config GET → TreeNodes 重写 → PUT。
  // 返回失败台数；newPathFn(null=删除上移) 语义由 rewriteServerPath 统一。
  async function rewriteServerPaths(dsName, oldPath, newPath) {
    const affected = servers.value.filter(
      (s) => s.dataSourceName === dsName && (s.folderPath === oldPath || (s.folderPath || '').startsWith(oldPath + '/'))
    )
    let failed = 0
    for (const s of affected) {
      const next = rewriteServerPath(s.folderPath, oldPath, newPath)
      if (next == null || next === s.folderPath) continue
      try {
        const cfg = await api.getServerConfig(s.id, dsName)
        cfg.json.TreeNodes = next ? next.split('/') : [] // 编辑器配置域 PascalCase 直通（勿做命名转换）
        await api.updateServer(s.id, cfg.json, dsName)
      } catch (err) {
        console.warn('[folderOps] server rewrite failed:', s.id, err?.message || err)
        failed++
      }
    }
    return failed
  }

  // tree-state 键前缀重写并落盘（本地即时物化 + 合并基底 PUT）
  async function rewriteKeys(dsName, oldPath, newPath) {
    const { remove, add } = rewriteTreeStateKeys(knownExpanded.value, dsName, oldPath, newPath)
    setLocalKeys(add, remove)
    return persist((m) => {
      for (const k of remove) delete m[k]
      Object.assign(m, add)
    })
  }

  async function renameFolder(dsName, oldPath) {
    if (!dsWritable(dsName)) {
      message.warning(t('cv.readOnly'))
      return
    }
    if (busy.value) return
    const oldName = oldPath.split('/').pop()
    const parent = parentPath(oldPath)
    const name = await promptName(t('tree.renameFolder'), oldName)
    if (!name || name === oldName) return
    if (siblingNames(dsName, parent).includes(name)) {
      message.warning(t('tree.folderNameExists'))
      return
    }
    const newPath = parent ? parent + '/' + name : name
    busy.value = true
    try {
      const failed = await rewriteServerPaths(dsName, oldPath, newPath)
      const ok = await rewriteKeys(dsName, oldPath, newPath)
      await reload()
      if (!ok) message.error(t('tree.folderRenameFailed'))
      else if (failed) message.error(t('toast.treeMoveFailed', { n: failed }))
      else message.success(t('tree.folderRenamed', { name }))
    } finally {
      busy.value = false
    }
  }

  async function deleteFolder(dsName, oldPath) {
    if (!dsWritable(dsName)) {
      message.warning(t('cv.readOnly'))
      return
    }
    const holder = holderAt(buildTree(servers.value, datasources.value, folderPathsByDs.value), dsName, oldPath)
    const count = holder ? countHolderServers(holder) : 0
    const name = oldPath.split('/').pop()
    // 空文件夹（递归无服务器）：单按钮确认直接删——专用文案（batch10 Task A #2：
    // 原先复用 deleteFolderConfirm 传 n=0，会显示"其中 0 台服务器…上移一级"的怪句）
    if (!count) {
      dialog.warning({
        title: t('tree.deleteFolder'),
        content: t('tree.deleteFolderEmpty', { name }),
        positiveText: t('editor.deleteYes'),
        negativeText: t('editor.cancel'),
        onPositiveClick: () => runDelete(dsName, oldPath, false),
      })
      return
    }
    // 有服务器：二选一。WPF 语义核实（ServerTreeViewModel.cs:503-516）：
    // 确认后实际删除文件夹及全部内含服务器（AppData.DeleteServer），但其确认文案写的是
    // "move its contents to parent folder"（文不符实）。web 给两种语义显式选择：
    // positive（红）= WPF 实际行为（连服务器一起删）；negative = WPF 文案所述/web 原行为
    //（仅删文件夹，内容上移一级）；右上 ✕ / 取消链接 = 不动作。
    dialog.warning({
      title: t('tree.deleteFolder'),
      content: t('tree.deleteFolderHasServers', { name, n: count }),
      positiveText: t('tree.deleteWithServers'),
      negativeText: t('tree.deleteKeepContents'),
      positiveButtonProps: { type: 'error' },
      onPositiveClick: () => runDelete(dsName, oldPath, true),
      onNegativeClick: () => runDelete(dsName, oldPath, false),
    })
  }

  // withServers=true：删除子树内全部服务器（WPF AppData.DeleteServer 同义）+ 删除整个
  // 子树的 tree-state 键（空子文件夹随文件夹消失，对齐 WPF 整节点移除）；
  // false：子项上移一级（原语义——服务器 TreeNodes 重写 + 子文件夹键上移）
  async function runDelete(dsName, oldPath, withServers) {
    if (busy.value) return
    busy.value = true
    try {
      let ok = true
      if (withServers) {
        const affected = servers.value.filter(
          (s) =>
            s.dataSourceName === dsName && (s.folderPath === oldPath || (s.folderPath || '').startsWith(oldPath + '/'))
        )
        let failed = 0
        for (const s of affected) {
          try {
            await api.deleteServer(s.id, dsName)
          } catch (err) {
            console.warn('[folderOps] server delete failed:', s.id, err?.message || err)
            failed++
          }
        }
        const exact = fullKey(dsName, oldPath)
        const remove = Object.keys(knownExpanded.value).filter((k) => k === exact || k.startsWith(exact + SEP))
        setLocalKeys({}, remove)
        ok = await persist((m) => {
          for (const k of remove) delete m[k]
        })
        await reload()
        if (!ok) message.error(t('tree.folderDeleteFailed'))
        else if (failed) message.error(t('tree.folderDeleteServerFailed', { n: failed }))
        else message.success(t('tree.folderDeleted'))
      } else {
        // 子项上移一级：newPath = 父路径（rewriteServerPath 对前缀子路径自动拼回）
        const failed = await rewriteServerPaths(dsName, oldPath, parentPath(oldPath))
        ok = await rewriteKeys(dsName, oldPath, null)
        await reload()
        if (!ok) message.error(t('tree.folderDeleteFailed'))
        else if (failed) message.error(t('toast.treeMoveFailed', { n: failed }))
        else message.success(t('tree.folderDeleted'))
      }
    } finally {
      busy.value = false
    }
  }

  // 服务器移入文件夹（列表行拖到文件夹行；path='' = 移到数据源根）
  async function moveServersToFolder(list, dsName, path) {
    if (!dsWritable(dsName)) {
      message.warning(t('cv.readOnly'))
      return
    }
    if (busy.value) return
    const segs = path ? path.split('/') : []
    const target = segs.join('/')
    let moved = 0
    let failed = 0
    busy.value = true
    try {
      for (const s of list) {
        if ((s.folderPath || '') === target) continue
        try {
          const cfg = await api.getServerConfig(s.id, dsName)
          cfg.json.TreeNodes = segs // 编辑器配置域 PascalCase 直通（勿做命名转换）
          await api.updateServer(s.id, cfg.json, dsName)
          moved++
        } catch (err) {
          console.warn('[folderOps] move failed:', s.id, err?.message || err)
          failed++
        }
      }
      await reload()
      if (failed) message.error(t('toast.treeMoveFailed', { n: failed }))
      else if (moved > 0) message.success(t('toast.treeMoved', { n: moved }))
    } finally {
      busy.value = false
    }
  }

  return { busy, createFolder, renameFolder, deleteFolder, moveServersToFolder }
}
