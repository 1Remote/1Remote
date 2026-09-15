/**
 * 连接编辑器——字段类型常量与字段描述符（field descriptor）形状约定。
 *
 * 双 casing 域约定（docs/superpowers/plans/2026-09-16-web-ui-phase2.md 全局约定 #3）：
 *  - 列表 DTO（/api/servers）是 camelCase（Plan 1 契约）；
 *  - 编辑器配置 json（GET /api/servers/{id}/config 的 json、POST/PUT body 内嵌 json）是
 *    PascalCase 原样直通（ProtocolBase.ToJsonString 无命名策略；CreateFromJsonString 的
 *    jObj.Protocol / jObj.ClassVersion 访问大小写敏感）。
 *  因此字段描述符的 `key` 一律为 C# 属性名的逐字拷贝（PascalCase），前端不做任何命名转换。
 *
 * i18n 约定（本阶段 schemas.js 不携带 labelKey，Task 11 统一补齐）：
 *  - 全部文案键使用 editor.* 命名空间（如 'editor.rdp.width'）；
 *  - labelKey / placeholderKey / 分组 labelKey 均为可选，缺失时 FormField 回退显示：
 *    字段 → key 原样（PascalCase）；SELECT 选项 → String(value)；分组 → group.id；
 *  - locales JSON 条目由 Task 11 创建（双语平价），在此之前编辑器显示回退文案属预期行为。
 */
export const FIELD = {
  TEXT: 'text',
  NUMBER: 'number',
  SELECT: 'select',
  SWITCH: 'switch',
  TAGS: 'tags',
  PASSWORD: 'password',
  TEXTAREA: 'textarea',
  ICON: 'icon',
  COLOR: 'color',
  CREDENTIAL: 'credential',
  SUBFORM: 'subform',
}

/**
 * @typedef {Object} FieldOption SELECT 的单个选项。
 * @property {number|string|boolean} value
 *   选项值，必须与 json 中该字段的实际值一致。注意：C# 枚举经 Newtonsoft 默认序列化为
 *   数字（本项目未使用 StringEnumConverter，ToJsonString 直接输出枚举整数），因此枚举型
 *   SELECT 的 value 必须用枚举成员的整数值（对照枚举定义，如 RDP.cs 顶部的
 *   ERdpWindowResizeMode / ERdpFullScreenFlag 等），否则 GET 回读的数字无法与选项匹配。
 * @property {string} [labelKey] 选项文案 i18n 键（editor.*），缺失时回退 String(value)。
 */

/**
 * @typedef {Object} FieldCondition 可见性条件。
 * 只控制显示/隐藏——隐藏时 json 值保留并原样透传（不删值），保证未编辑字段往返保真。
 * @property {string} field 同一份 json 内的依赖字段 key（PascalCase）。
 * @property {Array} [in] 依赖字段当前值 ∈ in 时可见。
 * @property {Array} [notIn] 依赖字段当前值 ∉ notIn 时可见（对齐 WPF
 *   "等于 X 才 Collapsed" 的触发语义，可正确处理 null/undefined）。
 *   in 与 notIn 二选一。
 */

/**
 * @typedef {Object} FieldDescriptor 单个字段描述符。
 * @property {string} key json 字段名 = C# 属性名逐字拷贝（PascalCase，勿手拼/猜测）。
 * @property {string} type FIELD 常量之一。
 * @property {string} [labelKey] 字段文案 i18n 键（editor.*），缺失时回退显示 key 原样。
 * @property {FieldOption[]} [options] SELECT 必填且非空。
 * @property {FieldCondition|FieldCondition[]} [visibleWhen]
 *   单条件对象或条件数组（数组 = 全部满足，AND）。值域见各枚举定义。
 * @property {boolean} [required] 必填校验（红框 + i18n 消息，语义对齐 WPF IDataErrorInfo：
 *   DisplayName / Address / Port 等）。
 * @property {string} [placeholderKey] 占位文案 i18n 键，缺失则无占位。
 * @property {{fields: FieldDescriptor[]}} [subform]
 *   SUBFORM 行字段描述（行内字段递归使用同一描述符形状，渲染深度限 2，见 SubformList）。
 *   字段本身对应 json 中的数组（如 AlternateCredentials: Credential[]）。
 * @property {boolean} [asString]
 *   仅 NUMBER 使用：C# 属性实为 string（如 Port='3389'）——按数字输入渲染，
 *   写回 json 时转为字符串，保持 WPF 的存储格式。
 */

/**
 * @typedef {Object} FieldGroup 分组（编辑器分组页签的一页）。
 * @property {string} id 分组标识（basic/credential/display/...，同时是页签回退文案）。
 * @property {string} [labelKey] 分组标题 i18n 键（editor.group.*），缺失时回退显示 id。
 * @property {FieldDescriptor[]} fields 组内字段（同组内 key 不重复）。
 */
