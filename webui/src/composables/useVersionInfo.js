/**
 * 版本与更新检测信息：拉取 GET /api/version（version/buildDate/
 * update 域——后端 WebUiUpdateService 缓存快照，首次命中幂等触发检查 + 每小时复查）。
 *
 * 消费方（App.vue ⚙ 红点 / SettingsView 导航「关于」红点 / AboutGroup 关于页）各自
 * 调用本组合式函数——刻意不做跨组件共享单例：端点轻量（读静态缓存），各自拉取免去
 * provide/inject 的时序耦合，代价是打开设置页时多一次请求，可接受（简单优先）。
 *
 * 检查未完成的补偿：后端首检在后台 Task 里进行，本次响应可能仍是 checking=true——
 * 30s 后重拉，直到拿到定论（available/checking=false）或耗尽重试（10 次 ≈ 5 分钟）。
 * 后端每小时的周期复查不主动推送，红点在长驻会话中不随后续发布翻新，属已知简化。
 */
import { onScopeDispose, readonly, ref } from 'vue'
import { api } from '../api'

const RETRY_DELAY_MS = 30_000
const MAX_RETRIES = 10

export function useVersionInfo() {
  const version = ref('')
  const buildDate = ref('')
  const update = ref(
    /** @type {{available:boolean,checking:boolean,newVersion:string,newVersionUrl:string,breaking:boolean}|null} */ (
      null
    )
  )
  const loaded = ref(false)

  let retries = 0
  let retryTimer = null
  async function load() {
    clearTimeout(retryTimer)
    try {
      const v = await api.version()
      version.value = v?.version || ''
      buildDate.value = v?.buildDate || ''
      update.value = v?.update || null
      loaded.value = true
      if (v?.update?.checking && retries < MAX_RETRIES) {
        retries += 1
        retryTimer = setTimeout(load, RETRY_DELAY_MS)
      }
    } catch {
      /* 后端不可达时保持空值（红点不显示、关于页显示占位） */
    }
  }
  load()

  // 组件作用域销毁（设置页关闭等）时停掉重试计时器，避免孤儿请求
  onScopeDispose(() => clearTimeout(retryTimer))

  return {
    version: readonly(version),
    buildDate: readonly(buildDate),
    update: readonly(update),
    loaded: readonly(loaded),
  }
}
