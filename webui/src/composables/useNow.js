/**
 * 共享秒级时钟（composables/useNow.js）：所有需要"随时间流动重渲染"的展示位
 * （数据源重连倒计时 title 等）共用一个 1s interval 的 now ref——多组件订阅
 * 不叠加定时器；最后一个订阅者销毁时自动停表。
 *
 * 背景（2026-09-22 用户报告）：状态栏重连倒计时此前读后端格式化字符串，仅随
 * 30s 轮询刷新，悬停值最多滞后 30s。改为"后端下发剩余秒数快照 + 前端本地倒数"
 * 后，title 计算依赖 now ref，每秒重算即为实时值（原生 title 悬停打开时快照，
 * 再次悬停即见最新值）。
 */
import { onScopeDispose, ref } from 'vue'

const now = ref(Date.now())
let timer = null
let consumers = 0

/** 订阅共享秒级时钟；组件作用域销毁时自动退订（最后一个退订停表）。 */
export function useNow() {
  consumers++
  if (!timer) {
    timer = setInterval(() => {
      now.value = Date.now()
    }, 1000)
  }
  onScopeDispose(() => {
    consumers = Math.max(0, consumers - 1)
    if (consumers === 0 && timer) {
      clearInterval(timer)
      timer = null
    }
  })
  return now
}
