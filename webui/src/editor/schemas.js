/**
 * 连接编辑器 schema —— 主流 4 协议（RDP / SSH / SFTP / FTP）。
 * 其余 5 协议（VNC/Telnet/Serial/RdpApp/LocalApp）由 Task 6 补充。
 *
 * 准确性规则：
 *  - 每个字段 `key` 都是 C# 属性名的逐字拷贝（PascalCase，编辑器 json 域直通，勿改拼写），
 *    对照来源（Ui/Model/Protocol/）：
 *      Base/ProtocolBase.cs（DisplayName/Tags/TreeNodes/IconBase64/ColorHex/Note/...）
 *      Base/ProtocolBaseWithAddressPort.cs（Address/Port/AlternateCredentials/IsPingBeforeConnect/...）
 *      Base/ProtocolBaseWithAddressPortUserPwd.cs（UserName/Password/AskPasswordWhenConnect/
 *        InheritedCredentialName/UsePrivateKeyForConnect/PrivateKey）
 *      Base/Credential.cs（AlternateCredentials 子表单行字段）
 *      RDP.cs / SSH.cs / SFTP.cs / FTP.cs（协议专属字段与 ClassVersion）
 *  - SELECT 枚举选项的 value 用枚举成员整数值：Newtonsoft 默认把枚举序列化为数字
 *    （ToJsonString 未挂 StringEnumConverter），GET /config 回读的就是数字。
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
 *
 * TreeNodes（所属文件夹路径）有意不入 schema：Plan 2 网页端的文件夹归属仍由左侧树
 * 拖拽完成（与 WPF 一致），编辑器对 TreeNodes 值原样透传不丢失；树选择器归 Plan 4。
 * IsAutoAlternateAddressSwitching（ProtocolBaseWithAddressPort.cs:83-90，备用地址自动切换）
 * 同样有意不入 schema：WPF 在备用地址 UI 暴露该开关，web 子表单暂未等价实现，
 * 值原样透传不丢失；待后续任务补备用地址 UI 时一并接入。
 */
import { FIELD } from './fieldTypes.js'

// ---------------------------------------------------------------------------
// SELECT 选项表（value = C# 枚举成员整数值，注释标注枚举定义位置）
// ---------------------------------------------------------------------------

/** ERdpFullScreenFlag（RDP.cs:30）Disable=0 / EnableFullScreen=1 / EnableFullAllScreens=2 */
const RDP_FULL_SCREEN_FLAG_OPTIONS = [
  { value: 0 },
  { value: 1 },
  { value: 2 },
]

/** ERdpWindowResizeMode（RDP.cs:21）AutoResize=0 / Stretch=1 / Fixed=2 / StretchFullScreen=3 / FixedFullScreen=4 */
const RDP_WINDOW_RESIZE_MODE_OPTIONS = [
  { value: 0 },
  { value: 1 },
  { value: 2 },
  { value: 3 },
  { value: 4 },
]

/** EDisplayPerformance（RDP.cs:37）Auto=0 / Low=1 / Middle=2 / High=3 */
const RDP_DISPLAY_PERFORMANCE_OPTIONS = [
  { value: 0 },
  { value: 1 },
  { value: 2 },
  { value: 3 },
]

/** EAudioRedirectionMode（RDP.cs:74）RedirectToLocal=0 / LeaveOnRemote=1 / Disabled=2 */
const RDP_AUDIO_REDIRECTION_MODE_OPTIONS = [
  { value: 0 },
  { value: 1 },
  { value: 2 },
]

/** EAudioQualityMode（RDP.cs:81）Dynamic=0 / Medium=1 / High=2 */
const RDP_AUDIO_QUALITY_MODE_OPTIONS = [
  { value: 0 },
  { value: 1 },
  { value: 2 },
]

/** EGatewayMode（RDP.cs:60）AutomaticallyDetectGatewayServerSettings=0 / UseTheseGatewayServerSettings=1 / DoNotUseGateway=2 */
const RDP_GATEWAY_MODE_OPTIONS = [
  { value: 0 },
  { value: 1 },
  { value: 2 },
]

/** EGatewayLogonMethod（RDP.cs:67）Password=0 / SmartCard=1 */
const RDP_GATEWAY_LOGON_METHOD_OPTIONS = [
  { value: 0 },
  { value: 1 },
]

// ---------------------------------------------------------------------------
// 共享分组构造器（协议间复用；Task 6 其余协议同样复用）
// ---------------------------------------------------------------------------

/** 基本信息组：全部协议一致（字段来自 ProtocolBase + ProtocolBaseWithAddressPort）。 */
function basicGroup() {
  return {
    id: 'basic',
    labelKey: 'editor.group.basic',
    fields: [
      { key: 'DisplayName', type: FIELD.TEXT, required: true },
      { key: 'Address', type: FIELD.TEXT, required: true },
      // C# Port 是 string（ProtocolBaseWithAddressPort.cs:49），数字输入但按字符串写回
      { key: 'Port', type: FIELD.NUMBER, required: true, asString: true },
      { key: 'Tags', type: FIELD.TAGS },
      { key: 'IconBase64', type: FIELD.ICON },
      { key: 'ColorHex', type: FIELD.COLOR },
      { key: 'Note', type: FIELD.TEXTAREA },
    ],
  }
}

/** 备用凭据子表单（AlternateCredentials: Credential[]，行字段对照 Base/Credential.cs）。 */
function alternateCredentialsField() {
  return {
    key: 'AlternateCredentials',
    type: FIELD.SUBFORM,
    subform: {
      fields: [
        { key: 'Name', type: FIELD.TEXT },
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
 * 凭据组（字段来自 ProtocolBaseWithAddressPortUserPwd）。
 * @param {{withPrivateKey?: boolean, prepend?: object[]}} opts
 *   withPrivateKey: SSH/SFTP 覆写了 ShowPrivateKeyInput()=true，显示私钥两件套；RDP/FTP 不显示。
 *   prepend: 组首额外字段（RDP 的 Domain/LoadBalanceInfo，对齐 WPF Connection 组顺序）。
 */
function credentialGroup({ withPrivateKey = false, prepend = [] } = {}) {
  const fields = [
    ...prepend,
    { key: 'UserName', type: FIELD.TEXT },
    { key: 'Password', type: FIELD.PASSWORD },
    { key: 'AskPasswordWhenConnect', type: FIELD.SWITCH },
    { key: 'InheritedCredentialName', type: FIELD.CREDENTIAL },
  ]
  if (withPrivateKey) {
    // C# 侧 Password 与 PrivateKey 互斥（写其一会清另一并联动 UsePrivateKeyForConnect），
    // 这里只按开关控制显示，值语义交给后端属性 setter
    fields.push(
      { key: 'UsePrivateKeyForConnect', type: FIELD.SWITCH },
      {
        key: 'PrivateKey',
        type: FIELD.TEXT,
        visibleWhen: { field: 'UsePrivateKeyForConnect', in: [true] },
      },
    )
  }
  fields.push(alternateCredentialsField())
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
      behaviorGroup([
        // SshVersion: int?（SSH.cs:20-27，非枚举非字符串），序列化为数字；
        // WPF 下拉 V1/V2（SshFormView.xaml:137-150，Tag=Int32 1/2）
        {
          key: 'SshVersion',
          type: FIELD.SELECT,
          options: [{ value: 1 }, { value: 2 }],
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
      behaviorGroup([{ key: 'StartupPath', type: FIELD.TEXT }]),
      miscGroup(),
    ],
  },
}
