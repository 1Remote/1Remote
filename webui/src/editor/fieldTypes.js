/**
 * 连接编辑器——字段类型常量与字段描述符（field descriptor）形状约定。
 *
 * 双 casing 域约定（全前端统一约定）：
 *  - 列表 DTO（/api/servers）是 camelCase；
 *  - 编辑器配置 json（GET /api/servers/{id}/config 的 json、POST/PUT body 内嵌 json）是
 *    PascalCase 原样直通（ProtocolBase.ToJsonString 无命名策略；CreateFromJsonString 的
 *    jObj.Protocol / jObj.ClassVersion 访问大小写敏感）。
 *  因此字段描述符的 `key` 一律为 C# 属性名的逐字拷贝（PascalCase），前端不做任何命名转换。
 *
 * i18n 约定：
 *  - 全部文案键使用 editor.* 命名空间：字段 → editor.f.{PascalCaseKey}（schemas.js
 *    加载时对缺省者统一兜底注入，见其「字段 labelKey 兜底」段）；分组 → editor.group.*；
 *    SELECT 选项 → editor.o.{枚举短名}.{成员名}（schemas.js 选项常量逐项标注）；
 *    SWITCH 的控件列说明文字 → switchTextKey（editor.o.*，
 *    availabilityDetection/checkAddressAvailable 为例外命名，见 FieldDescriptor）。
 *  - labelKey / placeholderKey 均为可选，缺失时 FormField 回退显示：字段 → key 原样
 *    （PascalCase）；SELECT 选项 → String(value)；分组 → group.id。
 *  - 有意不译的选项：Serial 的 DataBits/StopBits/Parity/FlowControl 选项值本身即
 *    技术字面量（'8'/'NONE'/'XON/XOFF'…，C# string 集合原样直通），保留 String(value)。
 *  - locales 14 语言键集一致（convert-locales.mjs --check 校验）。
 */
export const FIELD = {
  TEXT: 'text',
  NUMBER: 'number',
  SELECT: 'select',
  SWITCH: 'switch',
  TAGS: 'tags',
  PASSWORD: 'password',
  TEXTAREA: 'textarea',
  /** MARKDOWN：TEXTAREA 的备注特化——渲染 MarkdownField（编辑 ⇄ 预览，marked 渲染）。
   *  值域与 TEXTAREA 相同（字符串原样存取，存储格式不因预览改变）。 */
  MARKDOWN: 'markdown',
  ICON: 'icon',
  COLOR: 'color',
  CREDENTIAL: 'credential',
  SUBFORM: 'subform',
  /** AUTOCOMPLETE：可输入下拉——对齐 WPF AutoCompleteComboBox：
   *  既能从建议列表选择，也能直接键入任意自定义值（如非标准波特率）。值域与 TEXT
   *  相同（字符串原样存取）。建议来源见 FieldDescriptor 的 suggestions/suggestionsSource。 */
  AUTOCOMPLETE: 'autocomplete',
  /** KEY_VALUE_LINES：键值行编辑器——RDP「额外指令」（RdpControlAdditionalSettings）
   *  的特化。json 值是字符串，但语义为"一行一个 属性名:类型:值"（WPF AvalonEdit 文本域，
   *  解析器 RDP.cs SplitAdditionalSettings）。渲染为行数组编辑器（每行 [属性名自动补全]
   *  [值输入][删行]），序列化回 WPF 存储格式（详见 KeyValueLines.vue 头注释）。 */
  KEY_VALUE_LINES: 'key-value-lines',
  /** KV_MAP：字符串字典编辑器——json 值是 {key: value} 对象（PascalCase 域直通，
   *  键名即用户数据原样保留，如 AppArgument.Selections）。渲染为行式
   *  [key 输入][value 输入][删行] 的小表（KvMapField），增删行就地编辑对象。 */
  KV_MAP: 'kv-map',
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
 * @property {FieldOption[]} [options] SELECT 必填且非空（静态选项）。
 * @property {string} [optionsSource]
 *   仅 SELECT 使用：动态选项源标识，与 options 二选一（optionsSource 优先）。当前取值
 *   'runners:<ProtocolKey>'（该协议已配置的运行器名列表，如 'runners:SSH'）——数据经
 *   GET /api/settings/runners 模块级缓存拉取一次（composables/useRunnerOptions.js），
 *   失败静默退化为空列表。用于 SelectedRunnerName（值为运行器名字符串，'' = 跟随全局），
 *   schema 静态 options 无法表达"选项随用户配置变化"的场景。
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
 *   （AUTOCOMPLETE 上仅作标注保留——该类型值域本就是字符串，无行为差异。）
 * @property {Array<string>} [suggestions]
 *   仅 AUTOCOMPLETE 使用：静态建议数组（可输入下拉的候选，按输入包含匹配过滤，
 *   空输入显示全部；建议不约束取值——任意输入仍可保存，对齐 WPF 平价校验在后端）。
 * @property {string} [suggestionsSource]
 *   仅 AUTOCOMPLETE 使用：远程建议源标识，当前取值 'serial-ports'（后端机器 COM 口）
 *   | 'serial-baud-rates'（Serial.cs BitRates 常量表）——经 /api/serial/options
 *   模块级缓存拉取一次（composables/useSerialOptions.js，两字段共享）；失败静默退化
 *   为空建议 = 纯文本输入。与 suggestions 二选一（suggestionsSource 优先）。
 * @property {Array<string>} [kvSuggestions]
 *   仅 KEY_VALUE_LINES 使用：属性名自动补全候选（完整 'name:type:' 串，如
 *   'EnableAutoReconnect:i:'——与 WPF CompletionWindow 插入的文本一致，见
 *   editor/rdpProperties.js 的来源说明）。行内属性名框 n-auto-complete 恒全量
 *   展示（同 AUTOCOMPLETE 的 Serial 模式），候选不约束取值（任意键入原样进 json，
 *   校验与 WPF 平价规则交给后端/连接时解析器）。
 * @property {string} [credRole]
 *   仅凭据组（credentialGroup）字段有值，由 schemas.js 注入，EditorDrawer 的
 *   groupBlocks 按此四段渲染（对齐 WPF CredentialView.xaml 的区段顺序）：
 *   'pre'（二选一切换之前恒显，RDP 的 Domain/LoadBalanceInfo）| 'identity'（manual
 *   态身份字段）| 'picker'（vault 态库选择器）| 'option'（两态恒显的收尾开关）。
 *   其他组字段缺省；子表单行内字段不参与凭据组分段。
 * @property {boolean} [switchWithLabel]
 *   仅 SWITCH 使用：默认开关行标签列留空、控件列为 [switch][描述文字]（对齐 WPF
 *   复选框行 CredentialView.xaml:191-215）；置 true 时标签列显示 labelKey 文案
 *   （例外行——如 IsPingBeforeConnect「可用性检测」，WPF HostView.xaml:29-38
 *   该行标签列有文字）。
 * @property {string} [switchTextKey]
 *   仅 SWITCH 使用：控件列描述文字（紧跟开关右侧）的 i18n 键；缺省回退 labelKey 文案。
 *   与 switchWithLabel 搭配时 = 「标签列文字之外的开关说明文字」。
 * @property {string} [runTitleKey]
 *   仅 SWITCH 使用：switch-run 聚合行的行标题 i18n 键（EditorDrawer 的 blocksOf 把
 *   连续 SWITCH 字段聚成一行，该行标签列的标题文字）。挂在连续开关段的第一个字段上
 *   （EditorDrawer 只读段首字段的值作整行标题，段内后续字段不读取）。对齐 WPF 资源
 *   重定向区的行标题列（server_editor_advantage_resources，RdpFormView.xaml:427）。
 *   缺省 = 该行标签列留空（其余开关组的 WPF 无行标题，维持空列）。
 */

/**
 * @typedef {Object} FieldGroup 分组（编辑器单页滚动中的一个区段）。
 * @property {string} id 分组标识（basic/credential/display/...，同时是标题回退文案）。
 * @property {string} [labelKey] 分组标题 i18n 键（editor.group.*），缺失时回退显示 id。
 * @property {FieldDescriptor[]} fields 组内字段（同组内 key 不重复）。
 */
