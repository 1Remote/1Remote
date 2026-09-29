/**
 * 模态草稿守卫（round13 需求：统一关闭语义）——所有添加/编辑弹窗与抽屉共用：
 * - 干净（无修改）时：点遮罩直接关闭；✕/取消/Esc 直接关闭；
 * - 有输入时：点遮罩与 ✕/取消/Esc 同一语义——直接弹「未保存的更改」丢弃确认
 *   （round14 需求：废弃 message.info 的"请点击保存/取消/关闭按钮"引导文案）；
 * - 忙碌（保存在途）时：一切关闭尝试忽略。
 * * 取代 round12 U15 的"遮罩无条件禁用"：owner round13 定案干净态要能点遮罩关。
 * BUG3 教训内建：确认回调一律语句体（返回 false 会被 naive 判为阻止关闭）。
 * N5 教训内建：丢弃确认开着时 Esc 让位（见 useSettingsEsc 的 .n-dialog 让位）。
 */
import { confirmDialog } from '../utils/confirmDialog.js'

/**
 * @param {Object} deps
 * @param {() => boolean} deps.isDirty 表单是否有未保存修改（快照比对由调用方实现）
 * @param {() => void} deps.doClose 干净关闭动作（如置 show=false）
 * @param {() => boolean} [deps.isBusy] 忙碌判定（保存在途等；真则忽略一切关闭）
 * @param {Object} deps.dialog naive useDialog 实例
 * @param {Object} deps.message naive useMessage 实例
 * @param {(k: string, p?: Object) => string} deps.t i18n t
 */
export function useDraftGuard({ isDirty, doClose, isBusy, dialog, message, t }) {
  let asking = false // 丢弃确认框打开期间防叠框（EditorDrawer.closing 同款守卫）

  function closeIfClean() {
    // 忙碌=已处理（吞掉不动作）；返回 true 让调用方不再走后续分支
    if (isBusy?.()) return true
    if (!isDirty()) {
      doClose()
      return true
    }
    return false
  }

  /** 遮罩点击：干净即关；有输入直接弹「未保存的更改」确认框（round14 需求：
   *  与 ✕/取消/Esc 完全同路径；将来遮罩要差异化行为只改这一处） */
  function onMaskClick() {
    requestClose()
  }

  /** ✕/取消/Esc：干净即关；有输入先确认丢弃 */
  function requestClose() {
    if (asking || isBusy?.()) return
    if (closeIfClean()) return
    asking = true
    confirmDialog(dialog, {
      danger: true,
      title: t('editor.unsavedTitle'),
      content: t('editor.unsavedText'),
      positiveText: t('editor.btnDiscard'),
      negativeText: t('editor.btnKeepEditing'),
      // 语句体：回调返回值不得外泄（false 会阻止确认框关闭，round13 BUG3）
      onPositive: () => {
        doClose()
      },
      onAfterLeave: () => {
        asking = false
      },
    })
  }

  return { onMaskClick, requestClose }
}
