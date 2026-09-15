/**
 * 协议切换的字段携带（Plan 2 Task 8）——纯函数，无 Vue/naive 依赖，node 可直接测。
 *
 * 编辑器头部协议切换器把当前 json（PascalCase 直通域）改造成目标协议的 json，语义对齐
 * WPF 编辑器「保留 base 类公共字段、丢弃协议专属字段」（计划 Task 8 Step 1）：
 *  - 保留：两个 schema 字段键集的交集字段，且源 json 里实际存在该键（值原样携带）。
 *  - 丢弃：仅源协议 schema 的字段（协议专属，目标类型无对应属性）；json 中两 schema 都
 *    未描述的透传键同样丢弃（协议专属透传，如 SSH 的 ExternalSessionConfigPath），
 *    例外见 PASSTHROUGH_KEEP。
 *  - 补齐：目标 schema 独有（或交集但源 json 未携带）的字段，从目标 defaults 取值；
 *    defaults 未列（省略 falsy 初值的约定）→ 键缺省，由后端反序列化落 C# 默认值。
 *  - 注入：目标协议的 Protocol/ClassVersion 鉴别常量（CreateFromJsonString 的
 *    jObj.Protocol/jObj.ClassVersion 访问大小写敏感，必须由目标 schema 显式注入，
 *    不能依赖源 json 的值）。
 *  - 纯函数纪律：不改动入参 json（浅拷贝每个携带值；行对象/数组在后续编辑时由
 *    SubformList/FormField 的不可变更新约定重建）。
 */

/**
 * 协议无关、且有意不入 schema 的基础类透传键（schemas.js 文件头约定），切换时保留。
 * TreeNodes = 所属文件夹（ProtocolBase.TreeNodes）：切换协议不应把服务器挪回根目录——
 * 严格按「交集键保留」会丢掉它（无任何 schema 描述），造成保存后文件夹归属丢失。
 */
export const PASSTHROUGH_KEEP = ['TreeNodes']

/** 深拷贝单个字段值（数组/对象克隆，标量原样；undefined 返回 undefined）。 */
function cloneValue(v) {
  return v === undefined ? undefined : JSON.parse(JSON.stringify(v))
}

/**
 * schema 的全部顶层字段键集合（跨全部分组；不含 subform 行内字段——行字段随所属数组
 * 整体携带或丢弃）。
 * @returns {Set<string>}
 */
export function schemaFieldKeys(schema) {
  const keys = new Set()
  for (const g of schema?.groups || []) {
    for (const f of g.fields || []) keys.add(f.key)
  }
  return keys
}

/**
 * 把一份源协议的编辑 json 改造为目标协议的 json（见文件头规则）。
 * @param {Object} json 当前编辑中的 json（PascalCase 域；不会被改动）。
 * @param {object} fromSchema 源协议 schema（editor/schemas.js 的 PROTOCOLS[key]）。
 * @param {object} toSchema 目标协议 schema。
 * @returns {Object} 全新的 json 对象（Protocol/ClassVersion 已注入目标常量）。
 */
export function switchProtocol(json, fromSchema, toSchema) {
  const fromKeys = schemaFieldKeys(fromSchema)
  const toKeys = [...schemaFieldKeys(toSchema)]
  const out = {}
  for (const k of PASSTHROUGH_KEEP) {
    if (k in json) out[k] = cloneValue(json[k])
  }
  for (const key of toKeys) {
    if (fromKeys.has(key) && key in json) {
      out[key] = cloneValue(json[key]) // 两协议共有字段：值携带
    } else if (toSchema.defaults && key in toSchema.defaults) {
      out[key] = cloneValue(toSchema.defaults[key]) // 目标独有/源缺失：defaults 补齐
    }
    // 其余：键缺省 → 后端反序列化落 C# 默认值
  }
  out.Protocol = toSchema.protocol
  out.ClassVersion = toSchema.classVersion
  return out
}
