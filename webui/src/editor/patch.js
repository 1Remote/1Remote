/**
 * 批量编辑补丁构造（Plan 2 Task 10）：纯函数，node 可直接断言（无 Vue 依赖）。
 *
 * 语义（与后端 WebUiEditorService.ApplyBatchPatch 的「缺失字段 = 保持不变」契约配对）：
 *  - diffPatch(initial, current) 返回仅含「current 与 initial 不一致」的键；
 *  - 相等判定 = 逐键严格 JSON 相等（JSON.stringify 比对）：标量按值、数组整体比对
 *    （不逐元素合并——批量域的 tags 是显式覆盖语义，与后端一致）、对象按键序序列化比对；
 *  - undefined 与「键不存在」视为相等（两者 JSON.stringify 结果同为 undefined）；
 *    null 与 undefined 不相等（null 是可写入 patch 的明确值）；
 *  - current 侧值为 undefined 的键不会出现在结果里——批量 patch 域不表达「删除」，
 *    没有值可写即等于「保持不变」。
 */

/**
 * @param {Object|null|undefined} initial 基准值（各键的共享初值；键缺省 = 无基准）
 * @param {Object|null|undefined} current 当前值
 * @returns {Object} 仅含变化键的补丁对象（值取 current 侧；无变化返回 {}）
 */
export function diffPatch(initial, current) {
  const out = {}
  const a = initial || {}
  const b = current || {}
  for (const key of new Set([...Object.keys(a), ...Object.keys(b)])) {
    if (!(key in b)) continue // current 无此键 = 无可写值 = 保持不变（见文件头语义）
    if (JSON.stringify(a[key]) !== JSON.stringify(b[key])) {
      out[key] = b[key]
    }
  }
  return out
}
