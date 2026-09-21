// 批量连接（J33：ServerListView 批量条与 TagManagerModal「连接全部」共用的执行体——
// 两处此前的同构拷贝（阈值确认+串行循环+双 toast）在 H10 修复时被迫打了两遍补丁，
// 注释自认「两处必须一致」；收敛单实现后同类修复只需改一处）。
// 语义（与两处原实现逐项一致）：
// - 超过 BATCH_CONNECT_THRESHOLD 台先弹确认（非破坏性：无图标+中性按钮+autoFocus:false
//   ——H10：不自动聚焦确认按钮，Enter 肌肉记忆不再直接确认拉起 N 个会话）；
// - 逐台串行 await api.connect（并发轰炸后端/桌面端连接管线不友好）；
// - 成功/失败双 toast（部分成功两条都出）。
import { useDialog, useMessage } from 'naive-ui'
import { useI18n } from 'vue-i18n'
import { api } from '../api'
import { BATCH_CONNECT_THRESHOLD } from './useServers'

export function useBatchConnect() {
  const { t } = useI18n()
  const message = useMessage()
  const dialog = useDialog()

  function confirmConnect(n) {
    return new Promise((resolve) => {
      dialog.create({
        title: t('batchConnect.confirmTitle'),
        content: t('batchConnect.confirmText', { n }),
        // 非破坏性确认：无图标 + 中性按钮（folderOps 文件头三档策略）
        showIcon: false,
        positiveText: t('batch.connect'),
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

  async function batchConnect(ids) {
    if (!ids?.length) return
    if (ids.length > BATCH_CONNECT_THRESHOLD && !(await confirmConnect(ids.length))) return
    let ok = 0
    for (const id of ids) {
      try {
        await api.connect(id)
        ok++
      } catch (e) {
        console.warn('[useBatchConnect] connect failed:', id, e?.message || e)
      }
    }
    if (ok) message.success(t('toast.batchConnectStarted', { n: ok }))
    if (ok < ids.length) message.error(t('toast.batchConnectFailed', { n: ids.length - ok }))
  }

  return { batchConnect }
}
