/**
 * 字段可见性求值（visibleWhen）——纯函数，无 Vue/naive 依赖，node 可直接测。
 *
 * 求值方约定：由父级（编辑器抽屉，Task 8）在渲染分组时调用——抽屉需要知道字段是否可见
 * 以隐藏整行 DOM；FormField 本身保持纯展示（不读 visibleWhen）。隐藏只影响 UI：
 * json 值保留并原样透传（不删值），保证未编辑字段往返保真（fieldTypes.js 约定）。
 *
 * 语义（对齐 fieldTypes.js 的 FieldCondition 注释与 WPF 编辑器 XAML 触发器）：
 *  - 无 visibleWhen → 恒可见。
 *  - visibleWhen 为单条件对象或条件数组（数组 = 全部满足，AND）。
 *  - in（"依赖取了这些值才可见"）：
 *      可见 ⇔ json[dep] !== undefined && in 数组包含该值。
 *      依赖值 undefined（json 缺失该键）→ 不可见：in 表达"依赖已显式取到某值"，
 *      缺失即从未取到。例：PrivateKey 依赖 UsePrivateKeyForConnect ∈ [true]，
 *      未设置（undefined）与 false 同样隐藏。
 *  - notIn（对齐 WPF "值等于 X 才 Collapsed" 的触发语义）：
 *      可见 ⇔ !(json[dep] !== undefined && notIn 数组包含该值)。
 *      依赖值 undefined → 可见：notIn 表达"等于这些值才隐藏"，缺失/从未设置
 *      不满足隐藏条件。例：{ field:'MstscModeEnabled', notIn:[true] } 在
 *      undefined/false 时显示，仅显式 true 时隐藏——与 WPF Trigger 只在值
 *      等于 true 时 Collapsed 一致。
 *  - 比较一律严格（===）：枚举依赖值在 json 里是 Newtonsoft 序列化的数字
 *    （如 RdpWindowResizeMode: 2），'2' 字符串不匹配 2——schema 侧勿写错类型。
 *  - in 与 notIn 约定二选一；同时给出时按 in 求值（防御性兜底）。
 *  - 条件缺 field 或既无 in 也无 notIn → 视为恒真（空条件不隐藏字段）。
 */

/**
 * @param {FieldDescriptor} field 字段描述符（见 fieldTypes.js）。
 * @param {Object|null|undefined} json 当前编辑的完整 json 对象（PascalCase 域）。
 * @returns {boolean} 字段是否可见。
 */
export function isVisible(field, json) {
  const when = field?.visibleWhen
  if (!when) return true
  const conds = Array.isArray(when) ? when : [when]
  return conds.every((c) => matchCondition(c, json))
}

function matchCondition(cond, json) {
  const dep = cond?.field
  if (!dep) return true
  const value = json ? json[dep] : undefined
  if (Array.isArray(cond.in)) {
    return value !== undefined && cond.in.some((v) => v === value)
  }
  if (Array.isArray(cond.notIn)) {
    return !(value !== undefined && cond.notIn.some((v) => v === value))
  }
  return true
}
