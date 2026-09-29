/**
 * 确认框三档辅助（round11 A2 收敛）。三档语言（folderOps 文件头策略的代码化）此前
 * 只存在于注释约定——9 个文件 16 处手工重复 showIcon:false / positiveButtonProps /
 * autoFocus:false 三个参数，漏写任一项三档即破功（历轮评审反复修的正是这类漏网）。
 *
 * 用法：confirmDialog(dialog, { danger, title, content, positiveText, negativeText,
 *   onPositive, onNegative, onClose, onAfterLeave, ...其余透传 props })
 * - danger: true（删除/丢弃等破坏性确认）→ 无图标 + 红 positive；
 *   缺省 false（命名/合并/批量连接等中性·非破坏确认）→ 无图标 + 默认 positive。
 * - 统一 autoFocus:false：Enter 肌肉记忆不直接命中确认钮（Esc 仍可取消）。
 * - 回调用短名（onPositive/onNegative/onClose/onAfterLeave），映射到 naive 的
 *   onPositiveClick/onNegativeClick/onClose/onAfterLeave。
 * - **回调返回值契约（round13 BUG3 教训）**：naive 的确认/取消/关闭回调若返回
 *   恰好 false 会"阻止关闭"（DialogEnvironment 的 result === false 判定）。调用方
 *   写这些回调时一律用语句体（花括号），不要写 `() => (x.value = false)` 这类
 *   值式箭头——赋值表达式的值就是 false，确认框会永远关不掉。唯一例外是刻意
 *   要保持弹窗打开的场景（如 folderOps 命名输入 `submit() || false` 校验不过不关）。
 */
export function confirmDialog(dialog, opts) {
  const { danger = false, onPositive, onNegative, onClose, onAfterLeave, ...rest } = opts
  // 返回 naive 的 dialog 实例（调用方需要 destroy 等句柄时可用，folderOps 命名输入等）
  return dialog.create({
    showIcon: false,
    positiveButtonProps: { type: danger ? 'error' : 'default' },
    autoFocus: false,
    ...(onPositive ? { onPositiveClick: onPositive } : {}),
    ...(onNegative ? { onNegativeClick: onNegative } : {}),
    ...(onClose ? { onClose } : {}),
    ...(onAfterLeave ? { onAfterLeave } : {}),
    ...rest,
  })
}

/**
 * 布尔确认（round17 收敛）：「new Promise + confirmDialog + onPositive→resolve(true)、
 * onNegative/onClose/onAfterLeave→resolve(false)」的 12 行同构块此前在 useBatchConnect
 * .confirmConnect 与 folderOps.confirmFolderMerge 各持一份。Promise 多次 resolve 幂等，
 * 四个关闭路径都落 resolve(false)。回调为语句体（返回值契约见上）。
 * @returns {Promise<boolean>} true=确认，false=取消/关闭/遮罩
 */
export function confirmBool(dialog, opts) {
  return new Promise((resolve) => {
    confirmDialog(dialog, {
      ...opts,
      onPositive: () => {
        resolve(true)
      },
      onNegative: () => {
        resolve(false)
      },
      onClose: () => {
        resolve(false)
      },
      onAfterLeave: () => {
        resolve(false)
      },
    })
  })
}
