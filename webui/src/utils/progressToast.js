// 逐台串行长操作（批量删除 / 文件夹重命名·移动·删除 / 树内拖拽迁移）的进度 toast。
// loading 句柄原地更新 content（避免长操作无反馈疑似卡死），终态原地转 success/warning/error；
// 总数为 0（空文件夹/原位放下等）不弹「0/0」进度，终态直接退回常规 toast。
// destroy 定时收尾：非 success 终态驻留 5s（失败信息需要时间读），success 2.5s。

/**
 * @param {ReturnType<typeof import('naive-ui').useMessage>} message naive message 实例
 * @param {number} total 受影响条目总数（0 = 跳过进度句柄）
 * @param {(done: number, total: number) => string} text 进度文案（每步与初值共用）
 */
export function progressToast(message, total, text) {
  const progress = total ? message.loading(text(0, total), { duration: 0 }) : null
  return {
    /** 每完成一台（无论成败）推进度文案；done 口径由调用方决定 */
    step(done) {
      if (progress) progress.content = text(done, total)
    },
    /** 无变化提前撤下（原位放下等）：销毁进度，不出终态文案 */
    cancel() {
      progress?.destroy()
    },
    /** 终态：'success' | 'warning' | 'error'；无进度句柄时退回常规 toast（warning 并入 error） */
    finish(type, content) {
      if (progress) {
        progress.type = type
        progress.content = content
        setTimeout(() => progress.destroy(), type === 'success' ? 2500 : 5000)
      } else if (type === 'success') message.success(content)
      else message.error(content)
    },
  }
}
