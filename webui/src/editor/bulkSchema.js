/**
 * 批量编辑的 schema 视图（协议感知批量编辑）——纯函数模块，无 Vue 依赖。
 *
 * 语义（owner 需求，对齐 WPF 批量编辑）：
 *  - 勾选全为同一协议 P → 批量表单 = PROTOCOLS[P].groups 完整视图（与单机编辑同 schema、
 *    同 labelKey/placeholderKey/helpUrl，控件经 FormField/SwitchItem 复用，输入方式与非
 *    批量一致——免费达成）；
 *  - 勾选混合协议 → 组/字段级交集：组 = 所有所选协议 schema 中 id 相同的组；字段 = 组内
 *    key 相同且「值语义可比」（fieldSignature：type + SELECT 选项值表 + optionsSource）
 *    的字段。类型不一致（如某协议是 TEXT 另一个是 SELECT）、动态选项源不同（如
 *    SelectedRunnerName 的 'runners:SSH' vs 'runners:SFTP'——运行器名按协议解析，跨协议
 *    统一写会落一个对部分协议无效的名字）的字段跳过并在 dev 下记录。
 *  - SUBFORM 字段（AlternateCredentials/ArgumentList）一律不进批量：后端
 *    BatchPatchDeepFields 400 引导单机编辑（与 WPF 哨兵机制同类限制），前端直接不渲染。
 *  - 凭据组（credential）拍平：单机编辑的「手动 ⇄ 凭据库」二选一切换是单台 json 的状态机
 *    （切换动作带清空语义），批量域每字段独立「保持不变/覆盖」，两态字段并列展示——按
 *    credRole 四段顺序（pre → identity → picker → option）拍平为普通字段行。
 *  - visibleWhen 在批量域忽略（有意偏差）：条件可见性依赖单台 json 的依赖字段取值，
 *    N 台取值不同时「显/隐」无法统一裁决；隐藏字段的值本就不参与批量语义（未覆盖 =
 *    保持不变），恒展示让用户能显式统一写入（如 RdpWidth 在 AutoResize 模式下不消费，
 *    但批量统一分辨率时正好需要写它）。
 *
 * 与后端契约：字段的批量 patch 键 = camelKey(key)（PascalCase schema 键首字母小写——
 * allow-list 内全部键首字母为单个大写，无连续大写开头，该变换是双射）；对齐
 * WebUiEditorService.BatchPatchFieldMap 的 69 键 allow-list（大小写不敏感，camelCase 为
 * 规范形式）。peek 回读键同名（BatchPeekListDtoCoveredKeys 之外的键均回读）。
 */
import { PROTOCOLS } from './schemas.js'
import { FIELD } from './fieldTypes.js'

/**
 * PascalCase schema 键 → camelCase patch 键（首字母小写；allow-list 键集上为双射）。
 * @param {string} key
 */
export function camelKey(key) {
  return key ? key.charAt(0).toLowerCase() + key.slice(1) : key
}

/**
 * 列表 DTO（/api/servers，camelCase）已携带共享值的键：camelKey → DTO 字段名。
 * 这些键的共享值直接从 bulkServers prop 计算，不依赖 peek（后端 peek 同样跳过它们——
 * 键集两端对齐，见 WebUiEditorService.BatchPeekListDtoCoveredKeys；注意 colorHex 在
 * DTO 里叫 color）。
 */
export const BULK_DTO_KEYS = {
  displayName: 'displayName',
  note: 'note',
  tags: 'tags',
  colorHex: 'color',
  iconBase64: 'iconBase64',
  address: 'address',
  port: 'port',
  userName: 'userName',
}

/**
 * 敏感键（camelKey 域）：列表接口与 peek 都不回读（password 全系加密、privateKey 是
 * SSH 密文、gatewayPassword 是 RDP 密文，DataService.EncryptToDatabaseLevel），批量域
 * 只能以「覆盖」方式写入统一值——对齐 WebUiEditorService.BatchPeekSensitiveKeys。
 */
export const BULK_SENSITIVE_KEYS = new Set(['password', 'privateKey', 'gatewayPassword'])

/**
 * N 台同键取值的「共享值」三元组（批量表单逐字段的展示口径）：
 *  - known=true：值域已知（列表 DTO 键或 peek 回读）；same = N 台 JSON 严格全等
 *    （与 patch.js 同口径，数组整体比较）；same 时 value 为首台深拷贝（以已序列化的
 *    首台串反解——与响应式源/schema 常量脱钩），各不相同 → value=undefined（无意义）；
 *  - 敏感键/未回读键不进本函数，由调用方直接给 {known:false, same:false, value:undefined}。
 * @param {Array} values N 台同键取值（列表 DTO 域或 peek 回读域）
 */
export function sharedValueOf(values) {
  const first = JSON.stringify(values[0])
  const same = values.every((v) => JSON.stringify(v) === first)
  // 全 undefined 的 same 是合法结果（值本就缺失），first 为 undefined 时无法反解 → undefined
  return { known: true, same, value: same && first !== undefined ? JSON.parse(first) : undefined }
}

/** 凭据组拍平的字段顺序（credRole 四段，对齐 EditorDrawer.groupBlocks 的分段序）。 */
const CRED_ROLE_ORDER = ['pre', 'identity', 'picker', 'option']

/**
 * 字段的「值语义签名」：交集判定用。type + SELECT 选项值表（顺序敏感）+ optionsSource。
 * 不含 labelKey/placeholderKey/visibleWhen/required——文案与显隐不影响写值语义
 * （混合协议下 placeholder 取首协议文案，为记录在案的有意取舍）。
 */
function fieldSignature(f) {
  return JSON.stringify({
    type: f.type,
    optionsSource: f.optionsSource || null,
    options: (f.options || []).map((o) => o.value),
  })
}

function isCredentialGroup(g) {
  return g.id === 'credential' && g.fields.some((f) => f.key === 'InheritedCredentialName')
}

/** dev 告警守卫：可选链兼容 node 直跑（vite 构建态 import.meta.env 恒有值）。 */
function isDev() {
  return typeof import.meta !== 'undefined' && import.meta.env?.DEV === true
}

/** 凭据组字段按 credRole 四段拍平；非凭据组原样。 */
function flattenCredentialGroup(g) {
  if (!isCredentialGroup(g)) return g.fields
  return CRED_ROLE_ORDER.flatMap((role) => g.fields.filter((f) => f.credRole === role))
}

/**
 * 批量表单的组视图。
 * @param {Array<string>|Set<string>} protocolKeys 所选服务器的协议集（PROTOCOLS 键，DTO protocol 原文）
 * @returns {{groups: Array, defaults: Object|null, mixed: boolean}}
 *   groups：字段已滤 SUBFORM、凭据组已拍平的组列表（描述符为 schema 原对象的浅拷贝组，
 *   字段对象本身共享 schema 引用——批量渲染只读描述符，不改写）；
 *   defaults：全同协议时该协议的 defaults（覆盖态空值初始化用），混合协议为 null
 *   （交集字段无协议级默认值可言）；
 *   mixed：是否混合协议（前端横幅提示）。
 */
export function bulkSchemaView(protocolKeys) {
  const wanted = [...new Set(protocolKeys)].filter((k) => PROTOCOLS[k])
  const mixed = wanted.length > 1
  // 组序按 PROTOCOLS 键序（RDP 在前）而非勾选顺序——混合交集的组序确定可复现
  const ordered = Object.keys(PROTOCOLS).filter((k) => wanted.includes(k))

  if (!mixed) {
    const schema = ordered.length ? PROTOCOLS[ordered[0]] : null
    return {
      groups: schema
        ? schema.groups.map((g) => ({
            ...g,
            fields: flattenCredentialGroup(g).filter((f) => f.type !== FIELD.SUBFORM),
          }))
        : [],
      defaults: schema ? schema.defaults : null,
      mixed: false,
    }
  }

  const schemas = ordered.map((k) => PROTOCOLS[k])
  const [first, ...rest] = schemas
  const groups = []
  for (const g of first.groups) {
    const others = rest.map((s) => s.groups.find((x) => x.id === g.id))
    if (others.some((og) => !og)) continue // 组 id 不全共有 → 整组不进批量
    const fields = []
    for (const f of flattenCredentialGroup(g)) {
      if (f.type === FIELD.SUBFORM) continue
      const common = others.every((og) => {
        const of = og.fields.find((x) => x.key === f.key)
        return of != null && fieldSignature(of) === fieldSignature(f)
      })
      if (common) fields.push(f)
      else if (isDev()) console.warn('[bulkSchema] field dropped from mixed-protocol intersection:', f.key)
    }
    if (fields.length) groups.push({ ...g, fields })
  }
  return { groups, defaults: null, mixed: true }
}
