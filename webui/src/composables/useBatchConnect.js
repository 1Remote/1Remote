// 批量连接（J33：ServerListView 批量条与 TagManagerModal「连接全部」共用的执行体——
// 两处此前的同构拷贝（阈值确认+串行循环+双 toast）在 H10 修复时被迫打了两遍补丁，
// 注释自认「两处必须一致」；收敛单实现后同类修复只需改一处）。
// 语义：
// - 超过 BATCH_CONNECT_THRESHOLD 台先弹确认（非破坏性：无图标+中性按钮+autoFocus:false
//   ——H10：不自动聚焦确认按钮，Enter 肌肉记忆不再直接确认拉起 N 个会话）；
// - 逐台串行 await api.connect（并发轰炸后端/桌面端连接管线不友好）；
// - 逐台进度 toast + 终态三档（round11 U2：与批量删除/移动同款反馈；此前无任何
//   进行中提示，重复点击会把同批服务器整轮重复连接——桌面端每台弹出双窗口）；
// - 模块级在途锁：两个入口（批量条/标签弹窗）各自持有本 composable 闭包，锁必须
//   模块级才互斥；确认弹窗阶段也在锁内（连点不会叠出多个确认框），取消即释放。
import { useDialog, useMessage } from 'naive-ui'
import { useI18n } from 'vue-i18n'
import { confirmBool } from '../utils/confirmDialog'
import { api } from '../api'
import { BATCH_CONNECT_THRESHOLD } from './useServers'
import { progressToast } from '../utils/progressToast'

// 模块级单飞锁（两个 useBatchConnect() 实例共享）
let batchRunning = false

export function useBatchConnect() {
  const { t } = useI18n()
  const message = useMessage()
  const dialog = useDialog()

  // round17：Promise+四路 resolve(false) 的同构块收敛为 utils/confirmDialog.confirmBool
  function confirmConnect(n) {
    return confirmBool(dialog, {
      title: t('batchConnect.confirmTitle'),
      content: t('batchConnect.confirmText', { n }),
      // 非破坏性确认：无图标 + 中性按钮（folderOps 文件头三档策略）
      positiveText: t('batch.connect'),
      negativeText: t('editor.cancel'),
    })
  }

  async function batchConnect(ids) {
    if (!ids?.length || batchRunning) return
    batchRunning = true
    try {
      if (ids.length > BATCH_CONNECT_THRESHOLD && !(await confirmConnect(ids.length))) return
      const toast = progressToast(message, ids.length, (done) =>
        t('toast.batchConnecting', { ok: done, n: ids.length })
      )
      let ok = 0
      for (const id of ids) {
        try {
          await api.connect(id)
          ok++
        } catch (e) {
          console.warn('[useBatchConnect] connect failed:', id, e?.message || e)
        }
        toast.step(ok)
      }
      if (ok === ids.length) toast.finish('success', t('toast.batchConnectStarted', { n: ok }))
      // round13 M3：部分成功 warning（与批量删除同口径，红色会让用户误读为整批失败）；
      // 双结果子句分隔符统一「 · 」（M10 句式口径，与批量删除终态一致）
      else if (ok > 0)
        toast.finish(
          'warning',
          t('toast.batchConnectStarted', { n: ok }) + ' · ' + t('toast.batchConnectFailed', { n: ids.length - ok })
        )
      else toast.finish('error', t('toast.batchConnectFailed', { n: ids.length }))
    } finally {
      batchRunning = false
    }
  }

  return { batchConnect }
}
