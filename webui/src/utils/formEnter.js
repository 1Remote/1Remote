/**
 * 模态表单回车=保存（各设置分组模态共用的样板，语义统一描述）：
 * 输入框聚焦回车提交；按钮（回车=原生 click）/textarea（回车=换行）/n-select（回车=选中
 * 选项）聚焦时留给原生行为；IME 组合中的回车（选字）不触发。
 * 调用方绑定 @keydown="onFormEnter($event, save)"；fn 自带守卫（校验未过/保存中不动作）。
 */
export function onFormEnter(e, fn) {
  if (e.key !== 'Enter' || e.isComposing) return
  if (e.target?.closest?.('button, textarea, .n-select')) return
  e.preventDefault()
  fn()
}
