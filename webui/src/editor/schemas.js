/**
 * 连接编辑器 schema —— 全部 9 协议（RDP / SSH / SFTP / FTP / VNC / Telnet /
 * Serial / RemoteApp / APP）。键名 = PROTOCOLS 的 key = json 的 Protocol 鉴别值。
 *
 * 准确性规则：
 *  - 每个字段 `key` 都是 C# 属性名的逐字拷贝（PascalCase，编辑器 json 域直通，勿改拼写），
 *    对照来源（Ui/Model/Protocol/）：
 *      Base/ProtocolBase.cs（DisplayName/Tags/TreeNodes/IconBase64/ColorHex/Note/...）
 *      Base/ProtocolBaseWithAddressPort.cs（Address/Port/AlternateCredentials/IsPingBeforeConnect/...，
 *        继承链上 Serial 只到 ProtocolBase —— 无 Address/Port/IsPingBeforeConnect）
 *      Base/ProtocolBaseWithAddressPortUserPwd.cs（UserName/Password/AskPasswordWhenConnect/
 *        InheritedCredentialName/UsePrivateKeyForConnect/PrivateKey）
 *      Base/Credential.cs（AlternateCredentials 子表单行字段）
 *      RDP.cs / SSH.cs / SFTP.cs / FTP.cs / Vnc.cs / Telnet.cs / Serial.cs /
 *      RdpApp.cs / AppProtocol.cs(LocalApp) / AppArgument.cs（协议专属字段与 ClassVersion）
 *  - SELECT 枚举选项的 value 用枚举成员整数值：Newtonsoft 默认把枚举序列化为数字
 *    （ToJsonString 未挂 StringEnumConverter），GET /config 回读的就是数字。
 *    两个例外（值域为字符串，非枚举整数）：
 *      Serial 的 DataBits/StopBits/Parity/FlowControl 是 C# string 属性（Serial.cs 根本
 *      没有对应枚举），WPF 用 ComboBox 绑定 Serial.cs 的 string[] 集合 → SELECT 的
 *      value = 集合里的字符串原文（"8"/"1"/"NONE"/"XON/XOFF"...）。
 *      AppArgument.Type 挂了 [JsonConverter(StringEnumConverter)]（AppArgument.cs:52）
 *      → json 里是成员名字符串（"Normal"/"Secret"...），非数字。
 *  - visibleWhen 条件对齐 WPF 编辑器 XAML 的可见性触发（RdpFormView.xaml 等）。
 *  - `defaults` 为新建初值，对照各 C# 构造函数与字段初始化器；只列 schema 内字段。
 *    约定：false / 空串 / 空数组的初值省略（表单 falsy 渲染与 C# 初值一致，且 POST 后
 *    Newtonsoft 反序列化会在 new 实例上落回同样的 C# 默认值），只列 true 开关、
 *    SELECT 初值、非空字符串与数值初值。
 *    例外类（必须显式列 false）：C# 属性为 `[DefaultValue(true)]` +
 *    `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]` 而字段初始化器
 *    为 false 时（RDP.cs 的 EnableDiskDrives/EnableRedirectDrivesPlugIn/EnableRedirectCameras），
 *    json 缺失该字段会被 Populate 语义改写为 DefaultValue(true)——与 WPF 新建（false）相悖，
 *    因此这三个开关必须显式写入 defaults 为 false。
 *    Task 6 的 DefaultValue 审计结论（新增 5 类 + AppArgument，逐类 grep [DefaultValue]）：
 *      Vnc.cs / Telnet.cs / Serial.cs / RdpApp.cs / AppProtocol.cs(LocalApp) 自身均无
 *      [DefaultValue] 特性；继承链上与本批协议相关的有两处，均已落入 defaults：
 *      ① LocalApp ctor 显式 IsPingBeforeConnect=false（AppProtocol.cs:24），而基类该属性
 *        为 [DefaultValue(true)]+Populate（ProtocolBaseWithAddressPort.cs:74）→ json 缺失时
 *        会被 Populate 成 true → APP 的 defaults 必须显式 IsPingBeforeConnect:false；
 *      ② AppArgument.AddBlankAfterKey 字段初始化器为 false，但挂 [DefaultValue(true)]+Populate
 *        （AppArgument.cs:96-98）→ 子表单新行缺该键会被 Populate 成 true（WPF 新建行是
 *        false）→ ArgumentList 的 subform.rowDefaults 必须显式 false。
 *        （同文件 Key=[DefaultValue("")]+Populate 与初始化器一致、AddBlankAfterValue=
 *        [DefaultValue(true)]+Populate 与初始化器 true 一致，均无分歧。）
 *
 * TreeNodes（所属文件夹路径）有意不入 schema：Plan 2 网页端的文件夹归属仍由左侧树
 * 拖拽完成（与 WPF 一致），编辑器对 TreeNodes 值原样透传不丢失；树选择器归 Plan 4。
 * IsAutoAlternateAddressSwitching（ProtocolBaseWithAddressPort.cs:83-90，备用地址自动切换）
 * 同样有意不入 schema：WPF 在备用地址 UI 暴露该开关，web 子表单暂未等价实现，
 * 值原样透传不丢失；待后续任务补备用地址 UI 时一并接入。
 */
import { FIELD } from './fieldTypes.js'

// ---------------------------------------------------------------------------
// SELECT 选项表（value = C# 枚举成员整数值，labelKey → editor.o.*（Task 11，
// 双语键见 locales；文案对齐 WPF zh-cn.xaml 的 server_editor_* 系列）。
// 注释标注枚举定义位置。
// ---------------------------------------------------------------------------

/** ERdpFullScreenFlag（RDP.cs:30）Disable=0 / EnableFullScreen=1 / EnableFullAllScreens=2 */
const RDP_FULL_SCREEN_FLAG_OPTIONS = [
  { value: 0, labelKey: 'editor.o.fullScreen.Disable' },
  { value: 1, labelKey: 'editor.o.fullScreen.EnableFullScreen' },
  { value: 2, labelKey: 'editor.o.fullScreen.EnableFullAllScreens' },
]

/** ERdpWindowResizeMode（RDP.cs:21）AutoResize=0 / Stretch=1 / Fixed=2 / StretchFullScreen=3 / FixedFullScreen=4 */
const RDP_WINDOW_RESIZE_MODE_OPTIONS = [
  { value: 0, labelKey: 'editor.o.resize.AutoResize' },
  { value: 1, labelKey: 'editor.o.resize.Stretch' },
  { value: 2, labelKey: 'editor.o.resize.Fixed' },
  { value: 3, labelKey: 'editor.o.resize.StretchFullScreen' },
  { value: 4, labelKey: 'editor.o.resize.FixedFullScreen' },
]

/** EDisplayPerformance（RDP.cs:37）Auto=0 / Low=1 / Middle=2 / High=3 */
const RDP_DISPLAY_PERFORMANCE_OPTIONS = [
  { value: 0, labelKey: 'editor.o.performance.Auto' },
  { value: 1, labelKey: 'editor.o.performance.Low' },
  { value: 2, labelKey: 'editor.o.performance.Middle' },
  { value: 3, labelKey: 'editor.o.performance.High' },
]

/** EAudioRedirectionMode（RDP.cs:74）RedirectToLocal=0 / LeaveOnRemote=1 / Disabled=2 */
const RDP_AUDIO_REDIRECTION_MODE_OPTIONS = [
  { value: 0, labelKey: 'editor.o.audioMode.RedirectToLocal' },
  { value: 1, labelKey: 'editor.o.audioMode.LeaveOnRemote' },
  { value: 2, labelKey: 'editor.o.audioMode.Disabled' },
]

/** EAudioQualityMode（RDP.cs:81）Dynamic=0 / Medium=1 / High=2 */
const RDP_AUDIO_QUALITY_MODE_OPTIONS = [
  { value: 0, labelKey: 'editor.o.audioQuality.Dynamic' },
  { value: 1, labelKey: 'editor.o.audioQuality.Medium' },
  { value: 2, labelKey: 'editor.o.audioQuality.High' },
]

/** EGatewayMode（RDP.cs:60）AutomaticallyDetectGatewayServerSettings=0 / UseTheseGatewayServerSettings=1 / DoNotUseGateway=2 */
const RDP_GATEWAY_MODE_OPTIONS = [
  { value: 0, labelKey: 'editor.o.gatewayMode.AutoDetect' },
  { value: 1, labelKey: 'editor.o.gatewayMode.UseThese' },
  { value: 2, labelKey: 'editor.o.gatewayMode.DoNotUse' },
]

/** EGatewayLogonMethod（RDP.cs:67）Password=0 / SmartCard=1 */
const RDP_GATEWAY_LOGON_METHOD_OPTIONS = [
  { value: 0, labelKey: 'editor.o.gatewayLogon.Password' },
  { value: 1, labelKey: 'editor.o.gatewayLogon.SmartCard' },
]

/** EVncWindowResizeMode（Vnc.cs:11）Stretch=0 / Fixed=1（ctor 初始化器 Stretch） */
const VNC_WINDOW_RESIZE_MODE_OPTIONS = [
  { value: 0, labelKey: 'editor.o.vncResize.Stretch' },
  { value: 1, labelKey: 'editor.o.vncResize.Fixed' },
]

// Serial 的下列选项不是枚举：Serial.cs 的 DataBits/StopBits/Parity/FlowControl 均为
// string 属性，WPF ComboBox 绑定 Serial.cs 内的 string[] 集合（SelectedItem=字符串），
// json 里就是这些字符串原文，因此 SELECT 的 value 必须是字符串而非枚举整数。

/** Serial.DataBitsCollection（Serial.cs:84） */
const SERIAL_DATA_BITS_OPTIONS = [
  { value: '5' },
  { value: '6' },
  { value: '7' },
  { value: '8' },
]

/** Serial.StopBitsCollection（Serial.cs:93） */
const SERIAL_STOP_BITS_OPTIONS = [
  { value: '1' },
  { value: '2' },
]

/** Serial.ParityCollection（Serial.cs:101） */
const SERIAL_PARITY_OPTIONS = [
  { value: 'NONE' },
  { value: 'ODD' },
  { value: 'EVEN' },
  { value: 'MARK' },
  { value: 'SPACE' },
]

/** Serial.FlowControlCollection（Serial.cs:124） */
const SERIAL_FLOW_CONTROL_OPTIONS = [
  { value: 'NONE' },
  { value: 'XON/XOFF' },
  { value: 'RTS/CTS' },
  { value: 'DSR/DTR' },
]

/**
 * AppArgumentType（AppArgument.cs:17）Normal/Int/Float/File/Secret/Flag/Selection/Const。
 * Type 属性挂了 [JsonConverter(StringEnumConverter)]（AppArgument.cs:52）——本项目唯一
 * 的字符串枚举序列化点，json 里是成员名而非整数，选项 value 用字符串。
 */
const APP_ARGUMENT_TYPE_OPTIONS = [
  { value: 'Normal', labelKey: 'editor.o.appArgType.Normal' },
  { value: 'Int', labelKey: 'editor.o.appArgType.Int' },
  { value: 'Float', labelKey: 'editor.o.appArgType.Float' },
  { value: 'File', labelKey: 'editor.o.appArgType.File' },
  { value: 'Secret', labelKey: 'editor.o.appArgType.Secret' },
  { value: 'Flag', labelKey: 'editor.o.appArgType.Flag' },
  { value: 'Selection', labelKey: 'editor.o.appArgType.Selection' },
  { value: 'Const', labelKey: 'editor.o.appArgType.Const' },
]

// ---------------------------------------------------------------------------
// 共享分组构造器（协议间复用；Task 6 其余协议同样复用）
// ---------------------------------------------------------------------------

/**
 * 基本信息组（字段来自 ProtocolBase + ProtocolBaseWithAddressPort）。
 * @param {{withAddressPort?: boolean}} opts
 *   withAddressPort=false 时去掉 Address/Port 两行：Serial 只继承 ProtocolBase（无此二属性），
 *   LocalApp 的地址端口在专属 connection 组中展示（见 APP schema 注释）。
 */
function basicGroup({ withAddressPort = true } = {}) {
  const fields = [
    { key: 'DisplayName', type: FIELD.TEXT, required: true },
  ]
  if (withAddressPort) {
    fields.push(
      { key: 'Address', type: FIELD.TEXT, required: true },
      // C# Port 是 string（ProtocolBaseWithAddressPort.cs:49），数字输入但按字符串写回
      { key: 'Port', type: FIELD.NUMBER, required: true, asString: true },
    )
  }
  fields.push(
    { key: 'Tags', type: FIELD.TAGS },
    { key: 'IconBase64', type: FIELD.ICON },
    { key: 'ColorHex', type: FIELD.COLOR },
    // 备注：MARKDOWN 特化（fix-batch2 Task C #4）——编辑 ⇄ 预览切换（MarkdownField）。
    // 批量编辑的 note 仍为 TEXTAREA（BULK_FIELDS，列表 DTO 域扁平字段不参与本次特化）
    { key: 'Note', type: FIELD.MARKDOWN },
  )
  return {
    id: 'basic',
    labelKey: 'editor.group.basic',
    fields,
  }
}

/**
 * 备用连接组（fix-batch1 Task 3 #7，owner 确认）：AlternateCredentials 子表单独立成组
 * （此前挂在凭据组尾部）。每行 = 备用地址和/或登录身份的组合（行字段对照 Base/Credential.cs），
 * 组描述行（descKey）向用户说明该语义。
 */
function alternateGroup() {
  return {
    id: 'alternate',
    labelKey: 'editor.group.alternate',
    descKey: 'editor.group.alternateDesc',
    fields: [alternateCredentialsField()],
  }
}

/** 备用凭据子表单（AlternateCredentials: Credential[]，行字段对照 Base/Credential.cs）。 */
function alternateCredentialsField() {
  return {
    key: 'AlternateCredentials',
    type: FIELD.SUBFORM,
    subform: {
      fields: [
        // required：WPF 备用凭据弹窗 IDataErrorInfo 强制 Name 非空（AlternativeCredentialEditViewModel.cs:275）。
        // 子表单内 UI 只标 *，非空校验由保存流/后端把关（AlternateCredentials 数组反序列化不逐行校验，
        // 空名行会在保存时被整体拒绝或按后端行为处理——Task 8 保存错误提示承接）
        { key: 'Name', type: FIELD.TEXT, required: true },
        { key: 'Address', type: FIELD.TEXT },
        // Credential.Port 是 string（Credential.cs:81），子表单内保持文本不转数字
        { key: 'Port', type: FIELD.TEXT },
        { key: 'UserName', type: FIELD.TEXT },
        { key: 'Password', type: FIELD.PASSWORD },
        { key: 'PrivateKeyPath', type: FIELD.TEXT },
      ],
    },
  }
}

/**
 * 凭据组（字段来自 ProtocolBaseWithAddressPortUserPwd）。fix-batch1 Task 3 #7 + fix-batch3
 * Task A 重构：组内由 EditorDrawer 按 `credRole` 四段渲染（对齐 WPF CredentialView.xaml
 * 的区段顺序，见 EditorDrawer 的 groupBlocks）：
 *  - 'pre'：prepend 字段（RDP 的 Domain/LoadBalanceInfo），位于「凭据来源」二选一切换
 *    之前，manual/vault 两模式恒显（WPF 中它们是凭据区之前的 Connection 组字段，不属于
 *    手动输入凭据块）；
 *  - 'identity'：manual 态的身份字段（UserName/Password/PrivateKey）；
 *  - 'picker'：vault 态的 InheritedCredentialName（凭据库选择器，配提示行）；
 *  - 'option'：AskPasswordWhenConnect（+ 私钥协议的 UsePrivateKeyForConnect），两模式
 *    恒显、排在凭据区最后——WPF 中两个开关行不在 manual 块内，vault 态依旧可见。
 * AlternateCredentials 已移出本组（alternateGroup）。子表单行内字段无 credRole，
 * 不参与凭据组分段。
 * @param {{withPrivateKey?: boolean, prepend?: object[]}} opts
 *   withPrivateKey: SSH/SFTP 覆写了 ShowPrivateKeyInput()=true，显示私钥两件套；RDP/FTP
 *     等不显示。私钥协议的 Password 额外挂 visibleWhen（UsePrivateKeyForConnect=true 时
 *     隐藏，对齐 WPF CredentialView.xaml 的 IsUsePrivateKey=True → Password 行 Collapsed）；
 *     无私钥协议的 Password 不挂条件（依赖开关不存在，恒显）。
 *   prepend: 组首额外字段（自动标 'pre'，RDP 的 Domain/LoadBalanceInfo）。
 */
function credentialGroup({ withPrivateKey = false, prepend = [] } = {}) {
  const password = { key: 'Password', type: FIELD.PASSWORD, credRole: 'identity' }
  if (withPrivateKey) {
    password.visibleWhen = { field: 'UsePrivateKeyForConnect', notIn: [true] }
  }
  const fields = [
    ...prepend.map((f) => ({ ...f, credRole: 'pre' })),
    { key: 'UserName', type: FIELD.TEXT, credRole: 'identity' },
    password,
    { key: 'AskPasswordWhenConnect', type: FIELD.SWITCH, credRole: 'option' },
    { key: 'InheritedCredentialName', type: FIELD.CREDENTIAL, credRole: 'picker' },
  ]
  if (withPrivateKey) {
    // C# 侧 Password 与 PrivateKey 互斥（写其一会清另一并联动 UsePrivateKeyForConnect），
    // 这里只按开关控制显示，值语义交给后端属性 setter
    fields.push(
      { key: 'UsePrivateKeyForConnect', type: FIELD.SWITCH, credRole: 'option' },
      {
        key: 'PrivateKey',
        type: FIELD.TEXT,
        credRole: 'identity',
        visibleWhen: { field: 'UsePrivateKeyForConnect', in: [true] },
      },
    )
  }
  return { id: 'credential', labelKey: 'editor.group.credential', fields }
}

/** 杂项组：extraFields 在前，IsPingBeforeConnect（ProtocolBaseWithAddressPort.cs:76）收尾。 */
function miscGroup(extraFields = []) {
  return {
    id: 'misc',
    labelKey: 'editor.group.misc',
    fields: [...extraFields, { key: 'IsPingBeforeConnect', type: FIELD.SWITCH }],
  }
}

/** 行为组（SSH/SFTP/FTP 专属行为字段的容器）。 */
function behaviorGroup(fields) {
  return { id: 'behavior', labelKey: 'editor.group.behavior', fields }
}

// ---------------------------------------------------------------------------
// RDP 专属分组
// ---------------------------------------------------------------------------

/** 全屏附属开关（连接栏等）：RdpFullScreenFlag != Disable 时可见（RdpFormView.xaml:98）。 */
const VISIBLE_WHEN_FULL_SCREEN = { field: 'RdpFullScreenFlag', notIn: [0] }

/** RdpWidth/RdpHeight：RdpWindowResizeMode ∈ {Stretch, Fixed} 时可见（RdpFormView.xaml:173-186）。 */
const VISIBLE_WHEN_RESOLUTION = { field: 'RdpWindowResizeMode', in: [1, 2] }

/** 网关明细：GatewayMode != DoNotUseGateway 且非 mstsc 模式（RdpFormView.xaml:472-484）。 */
const VISIBLE_WHEN_GATEWAY_DETAIL = [
  { field: 'GatewayMode', notIn: [2] },
  { field: 'MstscModeEnabled', notIn: [true] },
]

function rdpDisplayGroup() {
  return {
    id: 'display',
    labelKey: 'editor.group.display',
    fields: [
      { key: 'RdpFullScreenFlag', type: FIELD.SELECT, options: RDP_FULL_SCREEN_FLAG_OPTIONS },
      { key: 'IsConnWithFullScreen', type: FIELD.SWITCH, visibleWhen: VISIBLE_WHEN_FULL_SCREEN },
      { key: 'IsFullScreenWithConnectionBar', type: FIELD.SWITCH, visibleWhen: VISIBLE_WHEN_FULL_SCREEN },
      { key: 'IsPinTheConnectionBarByDefault', type: FIELD.SWITCH, visibleWhen: VISIBLE_WHEN_FULL_SCREEN },
      { key: 'RdpWindowResizeMode', type: FIELD.SELECT, options: RDP_WINDOW_RESIZE_MODE_OPTIONS },
      { key: 'RdpWidth', type: FIELD.NUMBER, visibleWhen: VISIBLE_WHEN_RESOLUTION },
      { key: 'RdpHeight', type: FIELD.NUMBER, visibleWhen: VISIBLE_WHEN_RESOLUTION },
      // 非 mstsc 模式才显示缩放两件套（RdpFormView.xaml:200-224）
      {
        key: 'IsScaleFactorFollowSystem',
        type: FIELD.SWITCH,
        visibleWhen: { field: 'MstscModeEnabled', notIn: [true] },
      },
      {
        key: 'ScaleFactorCustomValue',
        type: FIELD.NUMBER,
        visibleWhen: [
          { field: 'MstscModeEnabled', notIn: [true] },
          { field: 'IsScaleFactorFollowSystem', notIn: [true] },
        ],
      },
      { key: 'DisplayPerformance', type: FIELD.SELECT, options: RDP_DISPLAY_PERFORMANCE_OPTIONS },
    ],
  }
}

function rdpMstscGroup() {
  return {
    id: 'mstsc',
    labelKey: 'editor.group.mstsc',
    fields: [
      { key: 'MstscModeEnabled', type: FIELD.SWITCH },
      {
        key: 'RdpFileAdditionalSettings',
        type: FIELD.TEXTAREA,
        visibleWhen: { field: 'MstscModeEnabled', in: [true] },
      },
    ],
  }
}

function rdpAdvancedGroup() {
  return {
    id: 'advanced',
    labelKey: 'editor.group.advanced',
    fields: [
      { key: 'IsAdministrativePurposes', type: FIELD.SWITCH },
      { key: 'AudioRedirectionMode', type: FIELD.SELECT, options: RDP_AUDIO_REDIRECTION_MODE_OPTIONS },
      // 音质仅在重定向到本机(0)时可见（RdpFormView.xaml:407）
      {
        key: 'AudioQualityMode',
        type: FIELD.SELECT,
        options: RDP_AUDIO_QUALITY_MODE_OPTIONS,
        visibleWhen: { field: 'AudioRedirectionMode', in: [0] },
      },
      // 9 个 Enable* 开关（RDP.cs resource switch 区段，顺序对齐 RdpFormView.xaml:433-441）
      { key: 'EnableClipboard', type: FIELD.SWITCH },
      { key: 'EnableKeyCombinations', type: FIELD.SWITCH },
      { key: 'EnableAudioCapture', type: FIELD.SWITCH },
      { key: 'EnablePorts', type: FIELD.SWITCH },
      { key: 'EnablePrinters', type: FIELD.SWITCH },
      { key: 'EnableSmartCardsAndWinHello', type: FIELD.SWITCH },
      { key: 'EnableDiskDrives', type: FIELD.SWITCH },
      { key: 'EnableRedirectDrivesPlugIn', type: FIELD.SWITCH },
      { key: 'EnableRedirectCameras', type: FIELD.SWITCH },
    ],
  }
}

function rdpGatewayGroup() {
  return {
    id: 'gateway',
    labelKey: 'editor.group.gateway',
    fields: [
      { key: 'GatewayMode', type: FIELD.SELECT, options: RDP_GATEWAY_MODE_OPTIONS },
      { key: 'GatewayHostName', type: FIELD.TEXT, visibleWhen: VISIBLE_WHEN_GATEWAY_DETAIL },
      { key: 'GatewayLogonMethod', type: FIELD.SELECT, options: RDP_GATEWAY_LOGON_METHOD_OPTIONS, visibleWhen: VISIBLE_WHEN_GATEWAY_DETAIL },
      // 用户名/密码：登录方式 != SmartCard 时可见（RdpFormView.xaml:503/512）
      { key: 'GatewayUserName', type: FIELD.TEXT, visibleWhen: [...VISIBLE_WHEN_GATEWAY_DETAIL, { field: 'GatewayLogonMethod', notIn: [1] }] },
      { key: 'GatewayPassword', type: FIELD.PASSWORD, visibleWhen: [...VISIBLE_WHEN_GATEWAY_DETAIL, { field: 'GatewayLogonMethod', notIn: [1] }] },
    ],
  }
}

// ---------------------------------------------------------------------------
// Task 6 协议专属分组/字段（VNC/Telnet/Serial/RdpApp/LocalApp）
// ---------------------------------------------------------------------------

/**
 * ArgumentList 子表单（LocalApp.ArgumentList: AppArgument[]，行字段对照 AppArgument.cs:36）。
 *  - Type 选项为字符串成员名（StringEnumConverter，见常量注释）。
 *  - 行内未列字段（Selections: Dictionary<string,string>，Selection/Const 型参数的取值表）
 *    无法用静态字段描述符表达 → 由 SubformList 行编辑原样保留（Task 7 约定：行对象原地
 *    修改，不重建），本 schema 只列出可安全编辑的标量字段。
 *  - Value 在 WPF 里按 Type 切换渲染（Secret=密码框/Flag=勾选/Selection=下拉，见
 *    ArgumentListControl.xaml:126-145）；静态描述符无法按行内另一字段的值切换控件类型，
 *    web 统一按 TEXT 渲染（Task 11/后续可由 SubformList 按 row.Type==='Secret' 特判加掩码），
 *    值语义（Flag 存 "1"/"" 等）不受影响。
 *  - rowDefaults：SubformList 新增行初值，对照 AppArgument 字段初始化器；
 *    AddBlankAfterKey 显式 false —— [DefaultValue(true)]+Populate 陷阱（见文件头审计②）。
 */
function appArgumentListField() {
  return {
    key: 'ArgumentList',
    type: FIELD.SUBFORM,
    subform: {
      rowDefaults: {
        Type: 'Normal',
        IsNullable: true,
        AddBlankAfterKey: false,
        AddBlankAfterValue: true,
      },
      fields: [
        { key: 'Type', type: FIELD.SELECT, options: APP_ARGUMENT_TYPE_OPTIONS },
        { key: 'Name', type: FIELD.TEXT },
        { key: 'Key', type: FIELD.TEXT },
        { key: 'Value', type: FIELD.TEXT },
        { key: 'IsNullable', type: FIELD.SWITCH },
        { key: 'AddBlankAfterKey', type: FIELD.SWITCH },
        { key: 'AddBlankAfterValue', type: FIELD.SWITCH },
        { key: 'Description', type: FIELD.TEXT },
      ],
    },
  }
}

/** Serial 连接参数组（Serial.cs；SerialPort/BitRate 为 C# string，WPF 用可自由输入的 AutoCompleteComboBox）。 */
function serialGroup() {
  return {
    id: 'serial',
    labelKey: 'editor.group.serial',
    fields: [
      // WPF 下拉数据源是后端机器的 SerialPort.GetPortNames()（Serial.cs:157），
      // web 无法枚举远端 COM 口 → 文本输入；IDataErrorInfo 要求非空
      { key: 'SerialPort', type: FIELD.TEXT, required: true },
      // WPF 为 BitRates 列表的可输入组合框（Serial.cs:71），允许自定义波特率 → 文本输入；
      // IDataErrorInfo 要求非空且可 long.Parse
      { key: 'BitRate', type: FIELD.NUMBER, required: true, asString: true },
      { key: 'DataBits', type: FIELD.SELECT, options: SERIAL_DATA_BITS_OPTIONS },
      { key: 'StopBits', type: FIELD.SELECT, options: SERIAL_STOP_BITS_OPTIONS },
      { key: 'Parity', type: FIELD.SELECT, options: SERIAL_PARITY_OPTIONS },
      { key: 'FlowControl', type: FIELD.SELECT, options: SERIAL_FLOW_CONTROL_OPTIONS },
    ],
  }
}

/**
 * LocalApp 连接字段组（WPF LocalAppFormView.xaml:63-182 的 Connection 区）。
 * WPF 按 CheckMacroRequirement（LocalAppFormViewModel.cs:224）动态显隐：仅当 ArgumentList
 * 某行的 Value 含对应宏（%1RM_HOSTNAME%/%1RM_PORT%/%1RM_USERNAME%/%1RM_PASSWORD%/
 * %1RM_PRIVATE_KEY_PATH%，定义于 ProtocolBaseWithAddressPort(UserPwd).cs）时才显示对应字段，
 * 且宏消失时 WPF 会清空该字段值。静态字段级 visibleWhen 只能依赖单字段取值、无法扫描
 * ArgumentList 内容 → 决策：web 上始终显示这五个字段（简化），未使用的字段留空即等价
 * （后端只在宏替换时消费这些值）；与 WPF 的该显隐差异为有意简化，记录在案。
 * AlternateCredentials 跟随 WPF：LocalApp 继承 ProtocolBaseWithAddressPortUserPwd，
 * WPF 在 Connection 区尾部展示备用凭据列表（LocalAppFormView.xaml:163）→ web 移入独立
 * 备用连接组（alternateGroup，fix-batch1 Task 3 #7）。
 */
function localAppConnectionGroup() {
  return {
    id: 'connection',
    labelKey: 'editor.group.connection',
    fields: [
      { key: 'Address', type: FIELD.TEXT },
      { key: 'Port', type: FIELD.NUMBER, asString: true },
      { key: 'UserName', type: FIELD.TEXT },
      { key: 'Password', type: FIELD.PASSWORD },
      { key: 'PrivateKey', type: FIELD.TEXT },
    ],
  }
}

// ---------------------------------------------------------------------------
// 协议 schema
// ---------------------------------------------------------------------------

export const PROTOCOLS = {
  /** RDP（样板 schema）：Ui/Model/Protocol/RDP.cs，ctor 见 RDP.cs:115-119。 */
  RDP: {
    protocol: 'RDP',
    classVersion: 'RDP.V1',
    defaults: {
      ColorHex: '#00000000',
      Port: '3389',
      UserName: 'Administrator',
      IsPingBeforeConnect: true,
      RdpFullScreenFlag: 1,
      IsFullScreenWithConnectionBar: true,
      RdpWindowResizeMode: 0,
      RdpWidth: 800,
      RdpHeight: 600,
      IsScaleFactorFollowSystem: true,
      ScaleFactorCustomValue: 100,
      DisplayPerformance: 0,
      AudioRedirectionMode: 0,
      AudioQualityMode: 0,
      EnableClipboard: true,
      EnableKeyCombinations: true,
      // 显式 false（勿按"省略 false"约定删）：三者 C# 字段初始化器为 false，但挂了
      // [DefaultValue(true)] + DefaultValueHandling.Populate——json 缺失该字段时
      // Newtonsoft 会 Populate 为 true，导致网页新建默认开启磁盘/即插即用/摄像头重定向，
      // 与 WPF 新建（false）分歧（安全相关）。显式写入 false 使 POST json 携带明确值。
      EnableDiskDrives: false,
      EnableRedirectDrivesPlugIn: false,
      EnableRedirectCameras: false,
      GatewayMode: 2,
      GatewayLogonMethod: 0,
    },
    groups: [
      basicGroup(),
      credentialGroup({
        // Domain/LoadBalanceInfo（RDP.cs:129/137）位于 WPF Connection 组的凭据区之前
        prepend: [
          { key: 'Domain', type: FIELD.TEXT },
          { key: 'LoadBalanceInfo', type: FIELD.TEXT },
        ],
      }),
      alternateGroup(),
      rdpDisplayGroup(),
      rdpMstscGroup(),
      rdpAdvancedGroup(),
      rdpGatewayGroup(),
      miscGroup([
        // mstsc 模式下该控件高级设置不生效，WPF 整组隐藏（RdpFormView.xaml:525）
        {
          key: 'RdpControlAdditionalSettings',
          type: FIELD.TEXTAREA,
          visibleWhen: { field: 'MstscModeEnabled', notIn: [true] },
        },
      ]),
    ],
  },

  /** SSH：Ui/Model/Protocol/SSH.cs，ctor 见 SSH.cs:14-18（ClassVersion 为插值串 "Putty.SSH.V1"）。 */
  SSH: {
    protocol: 'SSH',
    classVersion: 'Putty.SSH.V1',
    defaults: {
      Port: '22',
      UserName: 'root',
      IsPingBeforeConnect: true,
      SshVersion: 2,
    },
    groups: [
      basicGroup(),
      credentialGroup({ withPrivateKey: true }),
      alternateGroup(),
      behaviorGroup([
        // SshVersion: int?（SSH.cs:20-27，非枚举非字符串），序列化为数字；
        // WPF 下拉 V1/V2（SshFormView.xaml:137-150，Tag=Int32 1/2）
        {
          key: 'SshVersion',
          type: FIELD.SELECT,
          options: [
            { value: 1, labelKey: 'editor.o.sshVersion.V1' },
            { value: 2, labelKey: 'editor.o.sshVersion.V2' },
          ],
        },
        { key: 'StartupAutoCommand', type: FIELD.TEXT },
        { key: 'OpenSftpOnConnected', type: FIELD.SWITCH },
        { key: 'ExternalKittySessionConfigPath', type: FIELD.TEXT },
      ]),
      // ExternalSessionConfigPath（ExternalKitty 的回退取值属性）透传不编辑
      miscGroup(),
    ],
  },

  /** SFTP：Ui/Model/Protocol/SFTP.cs，ctor 见 SFTP.cs:15-19。 */
  SFTP: {
    protocol: 'SFTP',
    classVersion: 'SFTP.V1',
    defaults: {
      Port: '22',
      UserName: 'root',
      IsPingBeforeConnect: true,
      StartupPath: '/',
    },
    groups: [
      basicGroup(),
      credentialGroup({ withPrivateKey: true }),
      alternateGroup(),
      behaviorGroup([{ key: 'StartupPath', type: FIELD.TEXT }]),
      miscGroup(),
    ],
  },

  /** FTP：Ui/Model/Protocol/FTP.cs，ctor 见 FTP.cs:14-17。 */
  FTP: {
    protocol: 'FTP',
    classVersion: 'FTP.V1',
    defaults: {
      Port: '21',
      IsPingBeforeConnect: true,
      StartupPath: '/',
    },
    groups: [
      basicGroup(),
      // FTP 未覆写 ShowPrivateKeyInput()（基类默认 false），无私钥两件套
      credentialGroup(),
      alternateGroup(),
      behaviorGroup([{ key: 'StartupPath', type: FIELD.TEXT }]),
      miscGroup(),
    ],
  },

  /** VNC：Ui/Model/Protocol/Vnc.cs，ctor 见 Vnc.cs:18-22（Protocol="VNC"，UserName 置空）。 */
  VNC: {
    protocol: 'VNC',
    classVersion: 'VNC.V1',
    defaults: {
      ColorHex: '#00000000',
      Port: '5900',
      IsPingBeforeConnect: true,
      VncWindowResizeMode: 0,
    },
    groups: [
      basicGroup(),
      // WPF CredentialView 对 VNC 同样渲染 UserName 行（CredentialView.xaml:123 无条件，
      // VNC.ShowUserNameInput()=false 只影响凭据库新增弹窗的必填项，Vnc.cs:55）；
      // ShowPrivateKeyInput()=false（Vnc.cs:65）→ 无私钥两件套，与 FTP 同构
      credentialGroup(),
      alternateGroup(),
      {
        id: 'display',
        labelKey: 'editor.group.display',
        fields: [
          // EVncWindowResizeMode?（可空枚举，json 为数字），初始化器 Stretch=0（Vnc.cs:24）
          { key: 'VncWindowResizeMode', type: FIELD.SELECT, options: VNC_WINDOW_RESIZE_MODE_OPTIONS },
        ],
      },
      miscGroup(),
    ],
  },

  /**
   * Telnet：Ui/Model/Protocol/Telnet.cs，ctor 见 Telnet.cs:13-16。
   * 注意基类是 ProtocolBaseWithAddressPort（Telnet.cs:10）——模型里没有
   * UserName/Password/AskPasswordWhenConnect/InheritedCredentialName/PrivateKey，
   * 无凭据组（手动/库二选一无从谈起），备用凭据列表独立成备用连接组
   *（WPF TelnetFormView.xaml:35 也只挂备用凭据列表）。
   */
  Telnet: {
    protocol: 'Telnet',
    classVersion: 'Putty.Telnet.V1',
    defaults: {
      ColorHex: '#00000000',
      Port: '23',
      IsPingBeforeConnect: true,
    },
    groups: [
      basicGroup(),
      alternateGroup(),
      // WPF 优势组只有 StartupAutoCommand（TelnetFormView.xaml:39-50）；
      // ExternalKittySessionConfigPath/ExternalSessionConfigPath 模型存在但表单未暴露 → 透传
      behaviorGroup([{ key: 'StartupAutoCommand', type: FIELD.TEXT }]),
      miscGroup(),
    ],
  },

  /**
   * Serial：Ui/Model/Protocol/Serial.cs，ctor 见 Serial.cs:16-19。
   * 基类是 ProtocolBase（Serial.cs:13）——没有 Address/Port/AlternateCredentials/
   * IsPingBeforeConnect（后三者在 ProtocolBaseWithAddressPort 上），故 basic 组去掉地址端口、
   * 无 misc 组；StartupAutoCommand 属性存在但 WPF 表单已将其注释隐藏（SerialFormView.xaml:38-46）→ 透传。
   */
  Serial: {
    protocol: 'Serial',
    classVersion: 'Putty.Serial.V1',
    defaults: {
      ColorHex: '#00000000',
      // ctor 取后端机器第一个 COM 口兜底 "COM1"（Serial.cs:18），web 新建用同一兜底值
      SerialPort: 'COM1',
      BitRate: '9600',
      DataBits: '8',
      StopBits: '1',
      Parity: 'NONE',
      FlowControl: 'XON/XOFF',
    },
    groups: [
      basicGroup({ withAddressPort: false }),
      serialGroup(),
      // KiTTY 会话配置（Serial.cs:159，WPF SerialFormView.xaml:91-102 展示）
      behaviorGroup([{ key: 'ExternalKittySessionConfigPath', type: FIELD.TEXT }]),
    ],
  },

  /**
   * RemoteApp：Ui/Model/Protocol/RdpApp.cs，ctor 见 RdpApp.cs:14-18
   * （Protocol 鉴别值是 "RemoteApp" 而非类名 "RdpApp"，ClassVersion="RemoteApp.V1"）。
   * 音频两枚举复用 RDP.cs 的 EAudioRedirectionMode/EAudioQualityMode（同一类型），整数序列化。
   * RdpApp.cs 自身无 [DefaultValue] 特性（文件头审计），默认值取字段初始化器。
   */
  RemoteApp: {
    protocol: 'RemoteApp',
    classVersion: 'RemoteApp.V1',
    defaults: {
      ColorHex: '#00000000',
      Port: '3389',
      UserName: 'Administrator',
      IsPingBeforeConnect: true,
      AudioRedirectionMode: 0,
      AudioQualityMode: 0,
    },
    groups: [
      basicGroup(),
      // WPF RdpAppFormView 挂 CredentialView + 备用凭据列表；ShowPrivateKeyInput 基类默认 false
      credentialGroup(),
      alternateGroup(),
      {
        // IDataErrorInfo：RemoteApplicationName/RemoteApplicationProgram 必填（RdpApp.cs:140-153）
        id: 'remote',
        labelKey: 'editor.group.remote',
        fields: [
          { key: 'RemoteApplicationName', type: FIELD.TEXT, required: true },
          { key: 'RemoteApplicationProgram', type: FIELD.TEXT, required: true },
        ],
      },
      {
        id: 'display',
        labelKey: 'editor.group.display',
        fields: [
          { key: 'AudioRedirectionMode', type: FIELD.SELECT, options: RDP_AUDIO_REDIRECTION_MODE_OPTIONS },
          // 音质仅在重定向到本机(0)时可见（RdpAppFormView.xaml:118，与 RDP 表单同规则）
          {
            key: 'AudioQualityMode',
            type: FIELD.SELECT,
            options: RDP_AUDIO_QUALITY_MODE_OPTIONS,
            visibleWhen: { field: 'AudioRedirectionMode', in: [0] },
          },
        ],
      },
      {
        id: 'mstsc',
        labelKey: 'editor.group.mstsc',
        fields: [{ key: 'RdpFileAdditionalSettings', type: FIELD.TEXTAREA }],
      },
      miscGroup(),
    ],
  },

  /**
   * APP（LocalApp）：Ui/Model/Protocol/AppProtocol.cs，ctor 见 AppProtocol.cs:17-25
   * （Protocol="APP"，ClassVersion="APP.V1"）。
   *  - LocalApp 继承 ProtocolBaseWithAddressPortUserPwd（AppProtocol.cs:15），五个连接字段
   *    均在模型中；ctor 把 Address/Port/UserName/Password/PrivateKey 与 IsPingBeforeConnect
   *    全部置空/置 false。
   *  - 连接字段组见 localAppConnectionGroup() 注释：WPF 按 ArgumentList 宏引用动态显隐，
   *    web 简化为始终显示（有意偏差）。
   *  - Arguments 属性已标 [Obsolete]（AppProtocol.cs:57-64）→ 透传不编辑。
   *  - AskPasswordWhenConnect/InheritedCredentialName/UsePrivateKeyForConnect 模型存在但
   *    WPF LocalApp 表单未暴露 → 透传。
   */
  APP: {
    protocol: 'APP',
    classVersion: 'APP.V1',
    defaults: {
      ColorHex: '#00000000',
      // 显式 false（勿按"省略 false"约定删）：ctor 置 false（AppProtocol.cs:24），而基类
      // 该属性挂 [DefaultValue(true)]+Populate（ProtocolBaseWithAddressPort.cs:74）——
      // json 缺失该字段时会被 Populate 成 true，与 WPF 新建（false）相悖（文件头审计①）
      IsPingBeforeConnect: false,
    },
    groups: [
      basicGroup({ withAddressPort: false }),
      {
        id: 'exe',
        labelKey: 'editor.group.exe',
        fields: [
          // IDataErrorInfo：ExePath 必填（AppProtocol.cs:206-211）
          { key: 'ExePath', type: FIELD.TEXT, required: true },
          { key: 'RunWithHosting', type: FIELD.SWITCH },
          // 自定义协议显示名（LocalAppFormView.xaml:52-60，可选项）
          { key: 'AppProtocolDisplayName', type: FIELD.TEXT },
        ],
      },
      {
        id: 'arguments',
        labelKey: 'editor.group.arguments',
        fields: [appArgumentListField()],
      },
      localAppConnectionGroup(),
      alternateGroup(),
      miscGroup(),
    ],
  },
}

// ---------------------------------------------------------------------------
// 字段 labelKey 兜底（Task 11 i18n 收尾）：加载时统一补齐，builder 不必逐个写。
// 规则：字段（含 subform 行字段）缺 labelKey 时默认 'editor.f.' + key；显式提供者
// 不覆盖。locales 的 editor.f.* 共 73 键与去重后的字段 key 集合一一对应（9 协议共享
// 基类字段，同名 key 语义一致——如各协议的 UserName 均为「用户名」；子表单行字段与
// 顶层同名字段同键共用：Address/Port/UserName/Password/Name 两处文案相同）。
// 仅遍历 PROTOCOLS（编辑器 json 域）；BULK_FIELDS 属列表 DTO 域，自带
// editor.bulkField.* 键，不在此列。
// ---------------------------------------------------------------------------
for (const schema of Object.values(PROTOCOLS)) {
  for (const group of schema.groups) {
    for (const field of group.fields) {
      if (!field.labelKey) field.labelKey = 'editor.f.' + field.key
      for (const rowField of field.subform?.fields || []) {
        if (!rowField.labelKey) rowField.labelKey = 'editor.f.' + rowField.key
      }
    }
  }
}

// ---------------------------------------------------------------------------
// 批量编辑字段（Plan 2 Task 10）
// ---------------------------------------------------------------------------

/**
 * 批量编辑表单的字段描述符：与后端 BatchPatchFieldMap 的 allow-list 一一对应
 * （WebUiEditorService.cs；14 键）。与单机 schema 的 PascalCase 编辑器 json 域不同，
 * 批量 patch 属列表 DTO 域——`key` 直接就是 camelCase patch 键（后端 allow-list 映射到
 * C# 属性），也是批量表单 v-model 的绑定键。控件类型复用 FIELD 体系，FormField 直接渲染。
 *
 * 扩展属性（批量专用，FormField 不读取）：
 *  - dtoKey：列表 DTO（/api/servers，camelCase）中对应字段名，用于计算 N 台共享值；
 *    null = 列表 DTO 无此字段（note/password/inheritedCredentialName/askPasswordWhenConnect
 *    及协议专属三键）——共享值未知，仅能以「覆盖」方式设置统一值；
 *  - protocols：协议专属字段（startupAutoCommand/startupPath/rdpFileAdditionalSettings）
 *    的适用协议集（对照 BatchPatchFieldMap 注释）；所选服务器全部适用才显示该字段，
 *    否则后端会对不适用的那台 400（属性不存在）导致整批失败。
 */
export const BULK_FIELDS = [
  { key: 'displayName', type: FIELD.TEXT, required: true, labelKey: 'editor.bulkField.displayName', dtoKey: 'displayName' },
  { key: 'note', type: FIELD.TEXTAREA, labelKey: 'editor.bulkField.note', dtoKey: null },
  { key: 'tags', type: FIELD.TAGS, labelKey: 'editor.bulkField.tags', dtoKey: 'tags' },
  { key: 'colorHex', type: FIELD.COLOR, labelKey: 'editor.bulkField.colorHex', dtoKey: 'color' },
  { key: 'iconBase64', type: FIELD.ICON, labelKey: 'editor.bulkField.iconBase64', dtoKey: 'iconBase64' },
  { key: 'address', type: FIELD.TEXT, required: true, labelKey: 'editor.bulkField.address', dtoKey: 'address' },
  { key: 'port', type: FIELD.NUMBER, required: true, asString: true, labelKey: 'editor.bulkField.port', dtoKey: 'port' },
  { key: 'userName', type: FIELD.TEXT, labelKey: 'editor.bulkField.userName', dtoKey: 'userName' },
  { key: 'password', type: FIELD.PASSWORD, labelKey: 'editor.bulkField.password', dtoKey: null },
  { key: 'inheritedCredentialName', type: FIELD.CREDENTIAL, labelKey: 'editor.bulkField.inheritedCredentialName', dtoKey: null },
  { key: 'askPasswordWhenConnect', type: FIELD.SWITCH, labelKey: 'editor.bulkField.askPasswordWhenConnect', dtoKey: null },
  { key: 'startupAutoCommand', type: FIELD.TEXT, labelKey: 'editor.bulkField.startupAutoCommand', dtoKey: null, protocols: ['SSH', 'Telnet', 'Serial'] },
  { key: 'startupPath', type: FIELD.TEXT, labelKey: 'editor.bulkField.startupPath', dtoKey: null, protocols: ['SFTP', 'FTP'] },
  { key: 'rdpFileAdditionalSettings', type: FIELD.TEXTAREA, labelKey: 'editor.bulkField.rdpFileAdditionalSettings', dtoKey: null, protocols: ['RDP', 'RemoteApp'] },
]
