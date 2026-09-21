// 虚拟文件夹操作：新建/重命名/删除/移入的统一入口，
// SideTree（树右键菜单）与 ServerListView（列表右键/拖拽入文件夹）共用。
// 机制（有意设计）：
// - 新建 = PUT /api/ui-state/tree 向 expansion 字典增键（键即存在，值 true=展开且存在，
//   与 WPF 物化循环 ServerTreeViewModel.cs:873-889 一致，WPF 树自动可见空文件夹）；
// - 重命名 / 文件夹移动（整子树）/ 删除-保留内容（子项上移一级）三个操作同走
//   runPrefixRewrite：受影响服务器逐台 config GET → TreeNodes 前缀重写 → PUT（复用
//   树拖拽循环）+ 字典键前缀重写，仅目标路径与提示文案各异（文件夹移动 =
//   「重命名到新父路径」，见 moveFolder）；
// - 删除 = 空文件夹直接删；内有服务器时弹选择——连服务器一起删（WPF 实际行为）或
//   仅删文件夹、子项（服务器/子文件夹键）上移一级（见 deleteFolder/runDelete）；
// - 移入（服务器）= 目标路径逐台重写 TreeNodes（列表行拖到文件夹行 / 树拖拽同语义）。
// UpdateServer 路径不触发 SSE（已知后端行为），全部操作后显式 reload()（WPF parity 刷新）。
import { h, nextTick, ref } from 'vue'
import { NInput, useDialog, useMessage } from 'naive-ui'
import { useI18n } from 'vue-i18n'
import { api } from '../api'
import { progressToast } from '../utils/progressToast'
import {
  SEP,
  buildTree,
  countHolderServers,
  fullKey,
  holderAt,
  isDescendantPath,
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
  const { servers, datasources, reload, dsWritable } = useServers()
  const { folderPathsByDs, knownExpanded, setLocalKeys, persist } = useTreeState()

  const busy = ref(false) // 逐台 PUT / 键迁移进行中（防重入：SideTree 拖拽与本操作互斥参考）

  // 同级既有文件夹名（新建/重命名查重；物化后的空文件夹也在树模型里，一并算重名）
  function siblingNames(dsName, parent) {
    const holder = holderAt(buildTree(servers.value, datasources.value, folderPathsByDs.value), dsName, parent || '')
    return holder ? holder.folders.map((f) => f.name) : []
  }

  // ---- 对话框配色统一策略（owner 第三轮反馈：重命名弹窗红色误读 / 删除弹窗橙色惊叹号）----
  // - 命名输入（新建/重命名）：完全中性——无图标 + positive 用 default 按钮。accent 实心
  //   positive 在红/橙系强调色主题（如 Wine）下整颗读成红色，被误读为危险操作；
  // - 删除类确认：无图标 + positive 用 error 红（颜色本身承载危险语义，替代 naive
  //   dialog.warning 的橙色 ⚠ 图标——图标与红色按钮双重警示反而喧哗）；
  // - 非破坏性确认（批量连接等）：无图标 + 中性按钮。
  // naive 的 dialog.create 默认带蓝色 ⓘ 图标（iconRenderMap.default = Info），须显式
  // showIcon:false；positive 默认渲染 primary 型，须经 positiveButtonProps.type 覆盖。

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
        showIcon: false, // 命名输入走中性形态（见上方对话框配色策略）
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
        positiveButtonProps: { type: 'default' }, // 中性 OK：accent 实心在红/橙强调色下误读为危险操作
        onPositiveClick: () => submit() || false,
        onNegativeClick: () => settle(null),
        onClose: () => settle(null),
        // 关闭动画结束的安全网（任一 hide 路径）：确保 settle 与监听移除，防泄漏悬挂
        onAfterLeave: () => settle(null),
      })
      // 打开即显式聚焦（不依赖 autofocus 属性的宿主差异，见上）：取最新挂载的对话框输入框。
      // 重命名预填旧名时 select() 全选（第三轮 G18：系统惯例——资源管理器/WPF 重命名
      // 预填文本全选，直接输入即覆盖，免三击；新建分支 initial 为空串，全选无意义不调）
      nextTick(() => {
        const inputs = document.querySelectorAll('.n-dialog input')
        const el = inputs[inputs.length - 1]
        el?.focus()
        if (initial) el?.select()
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

  // 受影响服务器（oldPath 前缀下）：重命名/移动/删除-保留内容共用的目标集口径
  function affectedServers(dsName, oldPath) {
    return servers.value.filter(
      (s) => s.dataSourceName === dsName && (s.folderPath === oldPath || (s.folderPath || '').startsWith(oldPath + '/'))
    )
  }

  // 受影响服务器（oldPath 前缀下）逐台 config GET → TreeNodes 重写 → PUT。
  // 返回 {moved, failed}；newPath 的 null=删除上移语义由 rewriteServerPath 统一
  //（前缀替换与「目标父层+文件夹名+余量」逐路径等价，见 rewriteServerPath）；
  // onProgress 每台处理后回调（进度 toast 原地更新用，可选；原位置跳过的不回调——
  // 两调用方（runPrefixRewrite/树内拖拽）的既有口径均为「已处理台数」不含跳过）。
  // J28：自 SideTree.applyTreeMove 收敛回共享实现——此前树内拖拽持有一份逐行同构
  // 的拷贝（H1 修复时复制），核心写库逻辑双份会在未来单侧修 bug 时静默分叉
  async function rewriteServerPaths(dsName, oldPath, newPath, onProgress) {
    const affected = affectedServers(dsName, oldPath)
    let moved = 0
    let failed = 0
    for (const s of affected) {
      const next = rewriteServerPath(s.folderPath, oldPath, newPath)
      if (next == null || next === s.folderPath) continue
      try {
        const cfg = await api.getServerConfig(s.id, dsName)
        cfg.json.TreeNodes = next ? next.split('/') : [] // 编辑器配置域 PascalCase 直通（勿做命名转换）
        await api.updateServer(s.id, cfg.json, dsName)
        moved++
      } catch (err) {
        console.warn('[folderOps] server rewrite failed:', s.id, err?.message || err)
        failed++
      }
      onProgress?.()
    }
    return { moved, failed }
  }

  // tree-state 键前缀重写并落盘（本地即时物化 + 合并基底 PUT）。J28：同上收敛共享
  async function rewriteKeys(dsName, oldPath, newPath) {
    const { remove, add } = rewriteTreeStateKeys(knownExpanded.value, dsName, oldPath, newPath)
    setLocalKeys(add, remove)
    return persist((m) => {
      for (const k of remove) delete m[k]
      Object.assign(m, add)
    })
  }

  // 前缀重写型操作（重命名 / 文件夹移动 / 删除-保留内容）的公共执行体：
  // 服务器 TreeNodes 前缀重写 → 键前缀迁移 → reload → 三档提示（键落盘失败 /
  // 部分服务器失败 / 成功，fail/ok 为文案取值函数）。serverTo 与 keysTo 分开传：
  // 删除-保留内容时服务器上移到父路径，而键走 null 删除语义（rewriteTreeStateKeys
  // 对 null 有「文件夹自身展开态不随键迁移」的特例，见其注释），其余操作两者同值。
  // 大文件夹逐台 GET+PUT 串行耗时——与批量删除同款 loading 进度 toast（原地更新
  // content，完成态原地转三档终态），避免长操作无反馈疑似卡死
  async function runPrefixRewrite(dsName, oldPath, serverTo, keysTo, { fail, ok }) {
    busy.value = true
    const total = affectedServers(dsName, oldPath).length
    // 空文件夹（0 台受影响）键迁移极快：不弹「0/0」进度，终态直接常规 toast
    const toast = progressToast(message, total, (done) => t('toast.treeWorking', { ok: done, n: total }))
    let done = 0
    try {
      const { failed } = await rewriteServerPaths(dsName, oldPath, serverTo, () => toast.step(++done))
      const persistOk = await rewriteKeys(dsName, oldPath, keysTo)
      await reload()
      if (!persistOk) toast.finish('error', fail())
      else if (failed) toast.finish('error', t('toast.treeMoveFailed', { n: failed }))
      else toast.finish('success', ok())
    } finally {
      busy.value = false
    }
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
    await runPrefixRewrite(dsName, oldPath, newPath, newPath, {
      fail: () => t('tree.folderRenameFailed'),
      ok: () => t('tree.folderRenamed', { name }),
    })
  }

  async function deleteFolder(dsName, oldPath) {
    if (!dsWritable(dsName)) {
      message.warning(t('cv.readOnly'))
      return
    }
    const holder = holderAt(buildTree(servers.value, datasources.value, folderPathsByDs.value), dsName, oldPath)
    const count = holder ? countHolderServers(holder) : 0
    const name = oldPath.split('/').pop()
    // 空文件夹（递归无服务器）：单按钮确认直接删——专用文案
    //（原先复用 deleteFolderConfirm 传 n=0，会显示"其中 0 台服务器…上移一级"的怪句）。
    // autoFocus:false——删除类确认不自动聚焦按钮，Enter 不可误触确认（Esc 仍可取消）
    if (!count) {
      dialog.create({
        title: t('tree.deleteFolder'),
        content: t('tree.deleteFolderEmpty', { name }),
        showIcon: false, // 删除类确认统一形态：无图标 + 红 positive（见文件头配色策略）
        positiveText: t('editor.deleteYes'),
        negativeText: t('editor.cancel'),
        positiveButtonProps: { type: 'error' },
        autoFocus: false,
        onPositiveClick: () => runDelete(dsName, oldPath, false),
      })
      return
    }
    // 有服务器：二选一。WPF 语义核实（ServerTreeViewModel.cs:503-516）：
    // 确认后实际删除文件夹及全部内含服务器（AppData.DeleteServer），但其确认文案写的是
    // "move its contents to parent folder"（文不符实）。web 给两种语义显式选择：
    // positive（红）= WPF 实际行为（连服务器一起删）；negative = WPF 文案所述/web 原行为
    //（仅删文件夹，内容上移一级）。两个按钮都是执行动作、无取消位（第三轮 G10）——
    // Esc / 遮罩 / 右上 ✕ = 不动作退出，文案（deleteFolderHasServers）显式注明这一点，
    // 防"习惯点非红按钮求取消"的用户误执行删除
    dialog.create({
      title: t('tree.deleteFolder'),
      content: t('tree.deleteFolderHasServers', { name, n: count }),
      showIcon: false, // 删除类确认统一形态：无图标 + 红 positive（见文件头配色策略）
      positiveText: t('tree.deleteWithServers'),
      negativeText: t('tree.deleteKeepContents'),
      positiveButtonProps: { type: 'error' },
      autoFocus: false, // 同上：删除确认禁键盘 Enter 触发（Esc 仍可取消）
      onPositiveClick: () => runDelete(dsName, oldPath, true),
      onNegativeClick: () => runDelete(dsName, oldPath, false),
    })
  }

  // withServers=true：删除子树内全部服务器（WPF AppData.DeleteServer 同义）+ 删除整个
  // 子树的 tree-state 键（空子文件夹随文件夹消失，对齐 WPF 整节点移除）；
  // false：子项上移一级（服务器 TreeNodes 重写 + 子文件夹键上移，runPrefixRewrite）
  async function runDelete(dsName, oldPath, withServers) {
    if (busy.value) return
    if (!withServers) {
      // 子项上移一级：服务器重写到父路径（rewriteServerPath 对前缀子路径自动拼回），
      // 键走 null 删除语义（子文件夹键上移、文件夹自身键消失）
      await runPrefixRewrite(dsName, oldPath, parentPath(oldPath), null, {
        fail: () => t('tree.folderDeleteFailed'),
        ok: () => t('tree.folderDeleted'),
      })
      return
    }
    busy.value = true
    // 与批量删除同款逐台进度 toast（大文件夹连删同为长操作），完成态原地转三档终态；
    // 0 台（空文件夹误入此分支）不弹「0/0」进度
    const affected = affectedServers(dsName, oldPath)
    const n = affected.length
    const toast = progressToast(message, n, (done) => t('toast.batchDeleting', { ok: done, n }))
    let failed = 0
    let ok = 0
    try {
      for (const s of affected) {
        try {
          await api.deleteServer(s.id, dsName)
          ok++
        } catch (err) {
          console.warn('[folderOps] server delete failed:', s.id, err?.message || err)
          failed++
        }
        toast.step(ok)
      }
      const exact = fullKey(dsName, oldPath)
      const remove = Object.keys(knownExpanded.value).filter((k) => k === exact || k.startsWith(exact + SEP))
      setLocalKeys({}, remove)
      const persistOk = await persist((m) => {
        for (const k of remove) delete m[k]
      })
      await reload()
      if (!persistOk) toast.finish('error', t('tree.folderDeleteFailed'))
      else if (failed) toast.finish('error', t('tree.folderDeleteServerFailed', { n: failed }))
      else toast.finish('success', t('tree.folderDeleted'))
    } finally {
      busy.value = false
    }
  }

  // H3：目标层同名文件夹的合并确认（resolve true=确认合并）——与 SideTree.confirmFolderMerge
  // 同款（树内拖拽/列表拖拽两条路径共用合并语义，词条共用）；WPF 语义为直接合并
  //（ServerTreeViewModel.cs:637-646），owner 2026-09-21 决策改为先确认再合并
  function confirmFolderMerge(name) {
    return new Promise((resolve) => {
      dialog.create({
        title: t('tree.mergeFolderTitle'),
        content: t('tree.mergeFolderConfirm', { name }),
        showIcon: false, // 非破坏性确认（合并两侧内容）：无图标 + 中性按钮（文件头配色策略）
        positiveText: t('tree.mergeFolderYes'),
        negativeText: t('editor.cancel'),
        positiveButtonProps: { type: 'default' },
        autoFocus: false,
        onPositiveClick: () => resolve(true),
        onNegativeClick: () => resolve(false),
        onClose: () => resolve(false),
        onAfterLeave: () => resolve(false),
      })
    })
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
    // H20：逐台 GET+PUT 的长操作补进行中反馈（远程库下数秒零反馈疑似卡死）——与
    // 批量删除/重命名文件夹同款 progressToast（原地更新+终态转换），此前是长操作
    // 体系里唯一没有进度的一条
    const toast = progressToast(message, list.length, (done) => t('toast.treeWorking', { ok: done, n: list.length }))
    try {
      for (const s of list) {
        if ((s.folderPath || '') === target) {
          toast.step(moved + failed) // 原位置跳过也推进度（口径=已处理台数）
          continue
        }
        try {
          const cfg = await api.getServerConfig(s.id, dsName)
          cfg.json.TreeNodes = segs // 编辑器配置域 PascalCase 直通（勿做命名转换）
          await api.updateServer(s.id, cfg.json, dsName)
          moved++
        } catch (err) {
          console.warn('[folderOps] move failed:', s.id, err?.message || err)
          failed++
        }
        toast.step(moved + failed)
      }
      await reload()
      if (failed) toast.finish('error', t('toast.treeMoveFailed', { n: failed }))
      else if (moved > 0) toast.finish('success', t('toast.treeMoved', { n: moved }))
      else toast.cancel() // 全部原位置放下：无变化，撤下进度不出终态
    } finally {
      busy.value = false
    }
  }

  // 文件夹整体移动（列表文件夹行拖拽 / 列表文件夹拖入树节点共用）：整个子树迁到
  // targetParent 之下——runPrefixRewrite（newPath = targetParent + '/' + 名，
  // 即「重命名到新父路径」，与 renameFolder 同一执行体）。
  // 拒绝项（UI 层已拦，此处执行层再各设一道防御）：只读源 / 移入自身或后代；
  // 原地放下（newPath === path，如树内拖到自身父节点）静默返回，与 moveServersToFolder
  // 同口径。H3：目标父层同名文件夹不再直接拒绝——弹窗确认合并（owner 2026-09-21
  // 决策；确认后 runPrefixRewrite 的前缀重写把两侧服务器/键归一到同一路径，天然合并，
  // 对齐 WPF ServerTreeViewModel.cs:637-646；取消则不动）
  async function moveFolder(dsName, path, targetParent) {
    if (!dsWritable(dsName)) {
      message.warning(t('cv.readOnly'))
      return
    }
    if (busy.value) return
    const name = path.split('/').pop()
    const newPath = targetParent ? targetParent + '/' + name : name
    if (newPath === path || isDescendantPath(path, targetParent || '')) return
    if (siblingNames(dsName, targetParent).includes(name)) {
      if (!(await confirmFolderMerge(name))) return
    }
    await runPrefixRewrite(dsName, path, newPath, newPath, {
      fail: () => t('tree.folderMoveFailed'),
      ok: () => t('tree.folderMoved', { name }),
    })
  }

  // 空文件夹批量删除（owner 2026-09-21 第二轮反馈：勾选几个不用的空文件夹后直接删除——
  // 此前空文件夹复选框禁用无此通道）。确认框由调用方（ServerListView）统一弹一次；
  // 本函数只做执行：逐个「键删除」——空文件夹无服务器，rewriteServerPaths 为 0 台防御性
  // 调用，键走 null 删除语义（子键上移，空文件夹无子键则整体消失），一次 reload 收尾。
  async function deleteEmptyFolders(folders) {
    if (busy.value) return
    if (!folders?.length) return
    busy.value = true
    let failed = 0
    try {
      for (const f of folders) {
        try {
          await rewriteServerPaths(f.dsName, f.path, parentPath(f.path))
          const okKeys = await rewriteKeys(f.dsName, f.path, null)
          if (!okKeys) failed++
        } catch (err) {
          console.warn('[folderOps] delete empty folder failed:', f.path, err?.message || err)
          failed++
        }
      }
      await reload()
      if (failed) message.error(t('tree.folderDeleteFailed'))
      else message.success(t('tree.folderDeleted'))
    } finally {
      busy.value = false
    }
  }

  return {
    busy,
    createFolder,
    renameFolder,
    deleteFolder,
    moveServersToFolder,
    moveFolder,
    // J27/J28 共享原语：confirmFolderMerge（树内/列表两路合并确认共用——双份实现
    // 会在下次调整确认形态时裂成两种）与 rewriteServerPaths/rewriteKeys（树内拖拽
    // applyTreeMove 的执行体，自拷贝收敛；受影响台数在执行体内直接算，不再外发）
    confirmFolderMerge,
    deleteEmptyFolders,
    rewriteServerPaths,
    rewriteKeys,
  }
}
