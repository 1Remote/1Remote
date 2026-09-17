/**
 * AUTOCOMPLETE 的远程串口建议缓存（/api/serial/options）：模块级单例状态——
 * Serial 编辑器的 SerialPort/BitRate 是两个 FormField 实例，共享同一次拉取
 *（建议源是后端机器的 COM 口/波特率表，与表单数据无关，无需按数据源隔离或重拉）。
 * shallowRef 保持响应式：数据到达后各实例的 acOptions 自动重算（数组只整组替换，
 * 浅响应足够）。失败静默置空：空建议 = 纯文本输入，不阻断表单也不弹错误——建议
 * 只是便利功能，无「必须从列表中选择」的语义，退化后功能完整。
 */
import { shallowRef } from 'vue'
import { api } from '../api'

const serialPortSuggestions = shallowRef([])
const serialBaudRateSuggestions = shallowRef([])
let serialOptionsRequested = false

function ensureSerialOptionsLoaded() {
  if (serialOptionsRequested) return
  serialOptionsRequested = true
  api.serialOptions()
    .then((resp) => {
      serialPortSuggestions.value = Array.isArray(resp?.ports) ? resp.ports : []
      serialBaudRateSuggestions.value = Array.isArray(resp?.baudRates) ? resp.baudRates : []
    })
    .catch(() => {
      // API 失败（含开发模式后端未起）：留空建议，字段退化为纯文本输入；
      // 复位请求标记让下次读取重试——否则本会话内建议功能整段失效
      serialPortSuggestions.value = []
      serialBaudRateSuggestions.value = []
      serialOptionsRequested = false
    })
}

/** 按建议源取候选（'serial-ports' | 'serial-baud-rates'；未知源 = 空列表）。 */
function serialSuggestions(source) {
  ensureSerialOptionsLoaded()
  if (source === 'serial-ports') return serialPortSuggestions.value
  if (source === 'serial-baud-rates') return serialBaudRateSuggestions.value
  return []
}

/**
 * 串口建议共享访问器：模块级单例（多个组件/实例调用共享同一次拉取与缓存）。
 * 当前唯一消费方是 FormField 的 AUTOCOMPLETE 分支（acOptions）。
 */
export function useSerialOptions() {
  return { serialSuggestions }
}
