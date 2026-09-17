// 虚拟文件夹操作：新建/重命名/删除/移入的统一入口，
// SideTree（树右键菜单）与 ServerListView（列表右键/拖拽入文件夹）共用。
// 机制（owner 确认）：
// - 新建 = PUT /api/ui-state/tree 向 expansion 字典增键（键即存在，值 true=展开且存在，
//   与 WPF 物化循环 ServerTreeViewModel.cs:873-889 一致，WPF 树自动可见空文件夹）；
// - 重命名 = 受影响服务器逐台 config GET → TreeNodes 前缀重写 → PUT（复用树拖拽循环）
//   + 字典键前缀重写；
// - 删除 = 子项（服务器/子文件夹键）上移一级 + 删键；确认框展示直接/递归服务器数；
// - 移入 = 目标路径逐台重写 TreeNodes（列表行拖到文件夹行 / 树拖拽同语义）。
// UpdateServer 路径不触发 SSE（已知后端行为），全部操作后显式 reload()（WPF parity 刷新）。
import { h, ref } from 'vue'
import { NInput, useDialog, useMessage } from 'naive-ui'
import { useI18n } from 'vue-i18n'
import { api } from '../api'
import {
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
  // onPositiveClick 返回 false 保持打开（校验失败就地提示）
  function promptName(title, initial) {
    return new Promise((resolve) => {
      const name = ref(initial || '')
      dialog.create({
        title,
        content: () =>
          h(NInput, {
            value: name.value,
            placeholder: t('tree.folderNamePlaceholder'),
            autofocus: true,
            'onUpdate:value': (v) => {
              name.value = v
            },
          }),
        positiveText: t('common.ok'),
        negativeText: t('editor.cancel'),
        onPositiveClick: () => {
          const v = name.value.trim()
          if (!v || v.includes('/')) {
            message.warning(t('tree.folderNameInvalid'))
            return false
          }
          resolve(v)
        },
        onNegativeClick: () => resolve(null),
        onClose: () => resolve(null),
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
    dialog.warning({
      title: t('tree.deleteFolder'),
      content: t('tree.deleteFolderConfirm', { name, n: count }),
      positiveText: t('editor.deleteYes'),
      negativeText: t('editor.cancel'),
      onPositiveClick: () => runDelete(dsName, oldPath),
    })
  }

  async function runDelete(dsName, oldPath) {
    if (busy.value) return
    busy.value = true
    try {
      // 子项上移一级：newPath = 父路径（rewriteServerPath 对前缀子路径自动拼回）
      const failed = await rewriteServerPaths(dsName, oldPath, parentPath(oldPath))
      const ok = await rewriteKeys(dsName, oldPath, null)
      await reload()
      if (!ok) message.error(t('tree.folderDeleteFailed'))
      else if (failed) message.error(t('toast.treeMoveFailed', { n: failed }))
      else message.success(t('tree.folderDeleted'))
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
