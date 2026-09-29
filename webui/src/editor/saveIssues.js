/**
 * 编辑器保存问题总线（round11 U7/U25）——provide/inject 跨层传递，替代深层 props 链
 * （EditorDrawer → FormField → SubformList 行 → 行内 KV 编辑器共 4 层）。
 *
 * 背景：两类"星号承诺了校验、实际不拦"的静默缺陷——
 * - U7：备用凭据子表单行内 Name 标必填星，但前端校验只查顶层字段、后端也不逐行校验，
 *   空名行直接入库；
 * - U25：键值行编辑器（KeyValueLines/KvMapField）"只填值没填名"的半行在序列化时被
 *   静默丢弃（序列化输出是字符串/对象，半行无法表示，父级无从发现）。
 *
 * 机制：EditorDrawer 作 provide 方，持有 issues（字段键 → true，半行类问题自报）与
 * validate（保存尝试后置 true，行内必填标红的门控——对齐设置模态 P5「红星预告要求、
 * 红字留给真实违规」惯例：未尝试保存不标红）；行内组件 inject 自报/取门控。
 * 无提供方（组件被单独使用/测试）时各 API 安全退化为无操作。
 */
import { inject, reactive, ref } from 'vue'

export const SAVE_ISSUES_KEY = Symbol('editorSaveIssues')

/** provide 方（EditorDrawer）：issues 由保存校验消费；validate 由行内组件消费。 */
export function createSaveIssues() {
  return {
    /** 字段键 → true（自报类问题，如键值半行）；保存前统一清零重报，防陈年残留 */
    issues: reactive({}),
    /** 保存尝试门控：置 true 后子表单行内必填空值才标红 */
    validate: ref(false),
  }
}

/** inject 方（行内组件）：report 自报布尔问题；issues 读自报表（BulkEditForm 等
 *  校验方消费）；validateNow 读/写标红门控（保存尝试置 true）。无提供方时安全退化。 */
export function useSaveIssues() {
  const ctx = inject(SAVE_ISSUES_KEY, null)
  return {
    report(key, has) {
      if (!ctx) return
      if (has) ctx.issues[key] = true
      else delete ctx.issues[key]
    },
    issues: ctx ? ctx.issues : reactive({}),
    validateNow: ctx ? ctx.validate : ref(false),
  }
}
