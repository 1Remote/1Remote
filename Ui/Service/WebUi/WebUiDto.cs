using System.Collections.Generic;
using System.Text.Json;

namespace _1RM.Service.WebUi
{
    /// <summary>Web UI 端点使用的状态字符串常量（避免魔术字符串散落各处）。</summary>
    public static class WebUiConstants
    {
        public const string StatusDisconnected = "disconnected";
        public const string StatusConnected = "connected";
        public const string StatusReconnecting = "reconnecting";
    }

    /// <summary>
    /// 服务器列表条目 DTO：仅含可 JSON 序列化的纯 CLR 类型（零 WPF 依赖），
    /// 供 Web UI HTTP 端点使用，避免直接序列化协议层对象（含 BitmapSource 等不可序列化属性）。
    /// </summary>
    public class ServerDto
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string Color { get; set; } = string.Empty;      // 服务器自定义色 hex
        public string IconBase64 { get; set; } = string.Empty;
        public string DataSourceName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty; // "a/b"，根为空串
        public long LastConnectTime { get; set; }              // Unix 秒，0=从未连接
        public string ConnectionState { get; set; } = WebUiConstants.StatusDisconnected; // 预留（spec §3.4）
    }

    public class DataSourceDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;        // sqlite/mysql/pgsql
        public string Status { get; set; } = string.Empty;      // connected/disconnected/reconnecting
        public bool Writable { get; set; } = true;
        public string ReconnectInfo { get; set; } = string.Empty;
        public int ServerCount { get; set; }
    }

    public class TagDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsPinned { get; set; }
    }

    /// <summary>
    /// /api/settings/appearance DTO（Web UI 专属外观，独立于 WPF ThemeConfig，spec §4）。
    /// themeMode: dark|light|system；accent: blue|violet|pink|red|orange|green|slate；fontSize: S|M|L|XL；
    /// font: 字体族名（自由取值不枚举，空串 = 跟随系统，非空存储前 trim）。
    /// </summary>
    public class AppearanceDto
    {
        public string ThemeMode { get; set; } = string.Empty;
        public string Accent { get; set; } = string.Empty;
        public string FontSize { get; set; } = string.Empty;
        public string Font { get; set; } = string.Empty;
    }

    /// <summary>
    /// /api/ui-state/tree DTO：LocalityTreeViewService 两个字典的忠实投影。
    /// expanded: 文件夹节点全路径 → 是否展开（TreeNodeExpansionStates）。全路径各段之间的分隔符是
    /// ServerTreeViewModel.FullPathSeparator，即 " ]=+=+=+=>[ "，如 "LocalDataSource ]=+=+=+=>[ Folder1"
    /// ——不是 "->"；键对本 API 为不透明透传，前端后续需按同一分隔符构造。
    /// order: 节点 Id → 自定义序号（CustomNodeOrder，排序模式=Custom 时生效）。
    /// PUT 为整体替换语义。字典键不做大小写转换（路径/Id 原样透传）。
    /// </summary>
    public class TreeStateDto
    {
        public Dictionary<string, bool> Expanded { get; set; } = new();
        public Dictionary<string, int> Order { get; set; } = new();
    }

    /// <summary>
    /// POST/PUT /api/servers 请求体。信封键 camelCase；内嵌 json 为编辑器配置对象——
    /// 其键保持 ToJsonString 的 PascalCase 原样直通（两个 casing 域，勿互相归一）：
    /// 用 JsonElement 内嵌可原样保留键名大小写，GetRawText() 后交 ItemCreateHelper 反序列化。
    /// json 必须含 Protocol/ClassVersion 鉴别字段（访问大小写敏感）。
    /// </summary>
    public class ServerSaveRequest
    {
        public string? DataSourceName { get; set; } // 仅 POST 使用；PUT/DELETE 经 ?ds= 查询参数
        public JsonElement? Json { get; set; }
    }

    /// <summary>
    /// POST /api/servers/batch 请求体（补丁式批量编辑）。信封键 camelCase；
    /// patch 键属列表 DTO（camelCase）域——与编辑器配置 json（PascalCase 直通）是两个 casing 域，
    /// 服务端经显式 allow-list（WebUiEditorService.BatchPatchFieldMap）映射到 C# PascalCase 属性，
    /// 未知键 400 列出（防静默丢弃）。patch 中缺失的字段 = 保持不变；password 为明文（保存时加密）。
    /// ds 省略 = Local；ids 空数组/缺失 → 400。
    /// </summary>
    public class BatchPatchRequest
    {
        public List<string>? Ids { get; set; }
        public string? Ds { get; set; }
        public JsonElement? Patch { get; set; }
    }

    /// <summary>
    /// POST /api/icons/extract-from-exe 请求体：{path}。path 为 exe 的绝对路径；
    /// 与 WPF 图标选择器同一分支语义（IconPopupDialogViewModel：仅 .exe 走 ExtractAssociatedIcon）。
    /// </summary>
    public class ExtractIconRequest
    {
        public string? Path { get; set; }
    }

    /// <summary>
    /// GET /api/settings/general 响应（camelCase 序列化）。
    /// 安全白名单域：只暴露非破坏性字段——开机自启（注册表）、便携模式、SQLite 路径不在此列。
    /// requireSecondaryVerification 不在 GeneralConfig（那是 XAML 控件名）：真实状态在
    /// SecondaryVerificationHelper（凭据管理器/注册表/locality 文件），读 GetEnabled()、
    /// 写 SetEnabled(bool)（async void，fire-and-forget）。
    /// </summary>
    public class GeneralSettingsDto
    {
        public string Language { get; set; } = string.Empty;      // GeneralConfig.CurrentLanguageCode，小写码（"zh-cn"）
        public int CloseButtonBehavior { get; set; }              // GeneralConfig.EnumCloseButtonBehavior：0=Exit, 1=Minimize
        public bool ConfirmBeforeClosingSession { get; set; }
        public bool ShowSessionIconInSessionWindow { get; set; }
        public int LogLevel { get; set; }                         // SimpleLogHelper.EnumLogLevel：0=Debug..5=Disabled
        public bool TabWindowCloseButtonOnLeft { get; set; }
        public bool TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow { get; set; }
        public bool CopyPortWhenCopyAddress { get; set; }
        public bool DoNotCheckNewVersion { get; set; }
        public bool RequireSecondaryVerification { get; set; }    // SecondaryVerificationHelper.GetEnabled()
    }

    /// <summary>
    /// PUT /api/settings/general 请求体：白名单部分更新——键出现（非 null）才写，缺失 = 保持不变；
    /// 未知键静默忽略（白名单外不写）。language 大小写不敏感并归一小写（web 端 "zh-CN" → "zh-cn"）；
    /// closeButtonBehavior/logLevel 校验枚举定义值，违例 400 且零写入。
    /// </summary>
    public class GeneralSettingsUpdateRequest
    {
        public string? Language { get; set; }
        public int? CloseButtonBehavior { get; set; }
        public bool? ConfirmBeforeClosingSession { get; set; }
        public bool? ShowSessionIconInSessionWindow { get; set; }
        public int? LogLevel { get; set; }
        public bool? TabWindowCloseButtonOnLeft { get; set; }
        public bool? TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow { get; set; }
        public bool? CopyPortWhenCopyAddress { get; set; }
        public bool? DoNotCheckNewVersion { get; set; }
        /// <summary>写路径走 SecondaryVerificationHelper.SetEnabled（async void，注册表/凭据管理器机器状态）。</summary>
        public bool? RequireSecondaryVerification { get; set; }
    }

    /// <summary>
    /// GET /api/settings/launcher 响应。hotKeyModifiers/hotKeyKey 为 WPF 枚举：
    /// 线格式 = 枚举成员名（"ControlAlt"、"Shift"、"M"、"F1"）；显示形态（"Ctrl + Alt"）由前端自行拼装。
    /// </summary>
    public class LauncherSettingsDto
    {
        public bool LauncherEnabled { get; set; }
        public string HotKeyModifiers { get; set; } = string.Empty;   // HotkeyModifierKeys 成员名
        public string HotKeyKey { get; set; } = string.Empty;         // System.Windows.Input.Key 成员名
        public bool ShowCredentials { get; set; }
        public bool AllowSaveInfoInQuickConnect { get; set; }
    }

    /// <summary>
    /// PUT /api/settings/launcher 请求体：部分更新（非 null 才写）。hotKeyModifiers 除成员名外
    /// 亦接受显示形态（"Ctrl+Alt"/"win + ctrl"，token 顺序无关）；hotKeyKey 接受 Key 成员名（大小写不敏感）；
    /// None/未知取值 400 且零写入。
    /// </summary>
    public class LauncherSettingsUpdateRequest
    {
        public bool? LauncherEnabled { get; set; }
        public string? HotKeyModifiers { get; set; }
        public string? HotKeyKey { get; set; }
        public bool? ShowCredentials { get; set; }
        public bool? AllowSaveInfoInQuickConnect { get; set; }
    }

    /// <summary>GET /api/tags/manage?ds= 列表条目：数据源范围内的标签聚合。</summary>
    public class TagManageItemDto
    {
        public string Name { get; set; } = string.Empty; // 规范化小写（与 GlobalData.TagList 约定一致）
        public int Count { get; set; }                   // 该数据源下带此标签的服务器数
        public bool Pinned { get; set; }                 // LocalityTagService 置顶状态（机器本地，跨数据源共享）
    }

    /// <summary>PUT /api/tags/manage 请求体：置顶/取消置顶（pinned = 目标值，幂等非翻转）。</summary>
    public class TagPinRequest
    {
        public string? Ds { get; set; }
        public string? Name { get; set; }
        public bool? Pinned { get; set; }
    }

    /// <summary>
    /// POST /api/tags/rename 请求体：{ds, from, to}。from/to 均经 RectifyTagName 规范化
    /// （去 #、空格→-、小写）；to 为空/与 from 相同/该数据源下已存在 → 400；from 不存在 → 404。
    /// </summary>
    public class TagRenameRequest
    {
        public string? Ds { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
    }

    /// <summary>
    /// GET /api/credentials 列表条目（信封/列表域 camelCase 序列化）。
    /// 安全红线：绝不包含 Password/PrivateKeyPath——明文查看只能走 reveal 端点（二次验证）。
    /// refCount = 该数据源下引用此凭据名的服务器数（InheritedCredentialName + AlternateCredentials[].Name）。
    /// </summary>
    public class CredentialListItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int RefCount { get; set; }
    }

    /// <summary>
    /// POST/PUT /api/credentials 请求体。ds 仅 POST 使用（PUT/DELETE/reveal 经 ?ds= 查询参数）。
    /// credential 域字段与 WPF Credential 模型一致（PascalCase，绑定大小写不敏感）；
    /// Password/PrivateKeyPath 为明文——加密由 DataSourceBase.Database_Insert/UpdateCredential
    /// 在内部克隆上完成；PUT 为整体替换语义（空字段=清空，与 WPF 编辑弹窗一致，非“保持不变”）。
    /// </summary>
    public class CredentialSaveRequest
    {
        public string? Ds { get; set; }
        public CredentialInputDto? Credential { get; set; }
    }

    public class CredentialInputDto
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Port { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? PrivateKeyPath { get; set; }
    }
}
