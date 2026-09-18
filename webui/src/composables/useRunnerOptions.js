/**
 * SELECT 动态选项的运行器名缓存（GET /api/settings/runners）：
 * 模块级单例状态——SelectedRunnerName 字段按协议各有一个 FormField 实例（9 协议
 * schema 中的 6 个运行器协议），共享同一次拉取（选项源是全局运行器配置，与表单
 * 数据无关，编辑器生命周期内不需重拉；设置页改完运行器重开编辑器即见新值）。
 * 响应形状 {protocols:{SSH:{selectedRunnerName, runners:[...], macros:[...]}}}——
 * runners 数组 PascalCase + $type 直通（WebUiDataSourceService.ReadRunners），
 * 此处只取每项的 Name 字符串。失败静默置空：空列表 = 下拉只剩「跟随全局设置」
 * 一项，不阻断表单也不弹错误——选项只是便利功能，值可透传不受影响。
 */
import { shallowRef } from 'vue'
import { api } from '../api'

// null = 未加载；加载完成（含失败置 {}）后保持，失败时复位标记允许下次重试
const runnerProtocols = shallowRef(null)
let runnersRequested = false

function ensureRunnersLoaded() {
  if (runnersRequested) return
  runnersRequested = true
  api
    .getRunners()
    .then((resp) => {
      runnerProtocols.value = resp?.protocols && typeof resp.protocols === 'object' ? resp.protocols : {}
    })
    .catch(() => {
      // API 失败（含开发模式后端未起）：置空协议表，字段退化为仅「跟随全局设置」；
      // 复位请求标记让下次读取重试——否则本会话内运行器选项整段失效
      runnerProtocols.value = {}
      runnersRequested = false
    })
}

/**
 * 按协议键取运行器名列表（如 'SSH' → ['KiTTY', 'MyPuTTY', ...]；未知协议/未加载 = 空数组）。
 * 调用即确保拉取已发起（懒加载），数据到达后调用方的 computed 自动重算。
 */
function runnerNames(protocolKey) {
  ensureRunnersLoaded()
  const runners = runnerProtocols.value?.[protocolKey]?.runners
  if (!Array.isArray(runners)) return []
  return runners.map((r) => (typeof r?.Name === 'string' ? r.Name : '')).filter(Boolean)
}

/**
 * 运行器名共享访问器：模块级单例（多个组件/实例调用共享同一次拉取与缓存）。
 * 当前唯一消费方是 FormField 的 SELECT optionsSource 分支（selectOptions）。
 */
export function useRunnerOptions() {
  return { runnerNames }
}
