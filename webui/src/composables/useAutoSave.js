import { ref } from 'vue'

/**
 * 设置项自动保存守卫（全设置去保存按钮，改完即存）。
 *
 * 消费方：GeneralGroup / LauncherGroup / RunnerGroup——各组在自己的 saveFn 里构造
 * PUT 载荷（General 为差量 patch，Launcher/Runner 为全量表单），本组合式函数只负责
 * 触发纪律与并发合并，不管载荷内容：
 * - saveNow(patch)：开关/下拉等离散控件——立即保存；
 * - saveDebounced(patch)：文本/数字输入——静默 debounceMs 后保存（默认 500ms）；
 * - 并发守卫：保存进行中再来的变更合并进 pending（Object.assign，后改覆盖先改、
 *   异键共存——差量与全量载荷均安全），完成后 flush 最新合并态；
 * - 反馈策略：成功不 toast（改一项弹一次会烦人），仅失败 toast——
 *   失败回调 onError 由各组自带文案与 detail 提取；
 * - saving：供离散控件绑短暂 loading 态；
 * - dispose：组件卸载时冲刷仍在 debounce 里的最后一次变更（切分组不丢输入）。
 *
 * patch 可省略：省略时 saveFn 收到的 body 为待合并的空对象，载荷由 saveFn 每次发送
 * 时自行读取当前表单构造（全量组的最简用法，天然取到最新值）。
 */
export function useAutoSave(saveFn, { debounceMs = 500, onError = () => {} } = {}) {
  const saving = ref(false)
  let pending = null // null=无待发；对象=待发载荷（后改覆盖先改）
  let timer = null
  let flushing = false

  function enqueue(patch) {
    pending = Object.assign(pending || {}, patch)
  }

  async function flush() {
    if (flushing || pending === null) return
    flushing = true
    saving.value = true
    const body = pending
    pending = null
    try {
      await saveFn(body)
    } catch (e) {
      onError(e)
    } finally {
      flushing = false
      saving.value = false
    }
    if (pending !== null) flush() // 保存期间又累积的变更：继续冲刷最新态
  }

  function saveNow(patch) {
    clearTimeout(timer)
    timer = null
    enqueue(patch)
    flush()
  }

  function saveDebounced(patch) {
    clearTimeout(timer)
    enqueue(patch)
    timer = setTimeout(flush, debounceMs)
  }

  function dispose() {
    clearTimeout(timer)
    timer = null
    if (pending !== null) flush()
  }

  /** 是否有未发出的变更（保存请求自身的飞行期不算）——回填守卫用：飞行中用户又改了表单则跳过响应回填，防丢字。 */
  const hasPending = () => pending !== null

  return { saving, saveNow, saveDebounced, dispose, hasPending }
}
