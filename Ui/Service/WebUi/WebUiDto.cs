using System.Collections.Generic;
using System.Text.Json;

namespace _1RM.Service.WebUi
{
    /// <summary>Web UI 端点使用的状态字符串常量（避免魔术字符串散落各处）。</summary>
    public static class WebUiConstants
    {
        public const string StatusDisconnected = "disconnected";
        public const string StatusConnected = "connected";
        public const string StatusConnecting = "connecting";
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
        public string SubTitle { get; set; } = string.Empty;   // ProtocolBase.SubTitle（WPF 列表地址列原文，如 Serial "COM1(9600)"）
        public string Note { get; set; } = string.Empty;      // 备注（Markdown 源文本，供列表行悬停预览）
        public List<string> Tags { get; set; } = new();
        public string Color { get; set; } = string.Empty;      // 服务器自定义色 hex
        public string IconBase64 { get; set; } = string.Empty;
        public string DataSourceName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty; // "a/b"，根为空串
        public long LastConnectTime { get; set; }              // Unix 秒，0=从未连接
        public string ConnectionState { get; set; } = WebUiConstants.StatusDisconnected; // connected/connecting/disconnected，由活动/进行中会话派生（WebUiEndpoints.DeriveConnectionState）
    }

    public class DataSourceDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;        // sqlite/mysql/pgsql
        public string Status { get; set; } = string.Empty;      // connected/disconnected/reconnecting
        public bool Writable { get; set; } = true;
        public string ReconnectInfo { get; set; } = string.Empty;
        public int ServerCount { get; set; }
        /// <summary>
        /// 连接参数视图（camelCase 序列化）：sqlite = {path}；mysql/pgsql = {host, port, databaseName, userName}。
        /// 安全红线：绝不包含密码（明文密码只在 POST/PUT 请求方向存在，读方向无）。
        /// </summary>
        public object? Config { get; set; }
    }

    public class TagDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsPinned { get; set; }
    }

    /// <summary>
    /// /api/settings/appearance DTO（Web UI 专属外观，独立于 WPF ThemeConfig）。
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
    /// POST /api/servers/batch/peek 请求体（批量编辑共享值回读，fix batch8 #8）：
    /// {ids, ds?}——ids 为目标服务器 id 数组（ds 省略 = Local）。响应为逐台非敏感字段
    /// 载荷（allow-list 派生的 camelCase 键序列），绝不包含 password 类加密字段。
    /// </summary>
    public class BatchPeekRequest
    {
        public List<string>? Ids { get; set; }
        public string? Ds { get; set; }
    }

    /// <summary>
    /// POST /api/ui-state/list-order 请求体（列表行拖拽排序）。
    /// ids = 整库服务器 id 按新顺序排列（全量替换 LocalityListViewService.ServerCustomOrder，
    /// 与 WPF ServerListPageView 拖拽落点调 ServerCustomOrderSave 传入完整可见列表同语义）；
    /// 未知 id 逐个跳过（不整体失败）。响应 {ids} = 实际保存的顺序。
    /// </summary>
    public class ListOrderRequest
    {
        public List<string>? Ids { get; set; }
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
    /// POST /api/files/pick 请求体（batch9 Task B #9，泛化自 batch8 的 pick-exe）：全 {可选}。
    ///  - filter：文件对话框过滤器，WPF SelectFileHelper 线格式（"label|*.ext1;*.ext2|label2|*.*"）；
    ///    缺省回退 "exe|*.exe"（原 pick-exe 调用方语义，exe 选择器仍是主流用法）。
    ///  - title：对话框标题（WPF CmdSelectScript 传 "Select a script"）；缺省用系统默认。
    ///  - path：当前值（仅用于推导对话框初始目录，取其目录名；缺失/无效时用系统默认位置）。
    /// 响应 {path} = 用户选中的文件全路径；用户取消 → 404。
    /// </summary>
    public class PickFileRequest
    {
        public string? Filter { get; set; }
        public string? Title { get; set; }
        public string? Path { get; set; }
    }

    /// <summary>
    /// POST /api/scripts/test 请求体（batch9 Task B #9）：{command}。command 为编辑器中
    /// 「连接前/断开后脚本」的单行命令文本（与 WPF CmdTestScript 直传 Server 属性一致，
    /// 不做任何改写）。响应 {file, arguments, exitCode, timedOut, output, error}（见端点注释）。
    /// </summary>
    public class ScriptTestRequest
    {
        public string? Command { get; set; }
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
        /// <summary>写路径走 SecondaryVerificationHelper.SetEnabledAsync（可等待，注册表/凭据管理器机器状态，返回前完成并刷新缓存）。</summary>
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
    /// 在内部克隆上完成。PUT 语义（fix batch8 Task E #17）：Password/PrivateKeyPath 空=保持
    /// 原值（列表/编辑 API 不回显明文，web 表单无从预填）；其余字段整体替换（空=清空）。
    /// Address/Port 为 API 兼容保留——凭据库表单已对齐 WPF 凭据库弹窗（showHost:false）
    /// 不再提交这两项，且 Dapper UpdateCredential 落库前本就强制清空（vault 不使用）。
    /// </summary>
    public class CredentialSaveRequest
    {
        public string? Ds { get; set; }
        public CredentialInputDto? Credential { get; set; }
    }

    public class CredentialInputDto
    {
        public string? Name { get; set; }
        public string? Address { get; set; }        // API 兼容保留（web 表单不提交，落库前被清空）
        public string? Port { get; set; }           // 同上
        public string? UserName { get; set; }
        public string? Password { get; set; }       // Update：null=保持原值；空串=显式清除；非空=新值（batch9 Task D ⑯）
        public string? PrivateKeyPath { get; set; } // 同 Password（Create 一律按空串=无处理）
    }

    /// <summary>
    /// POST /api/datasources 请求体。type: sqlite|mysql|pgsql（postgresql 同义）；
    /// name 缺省时 sqlite 从 config.path 文件名推导；重名（忽略 CurrentCulture 大小写，WPF 同款）→ 409。
    /// config.password 仅 mysql/pgsql：POST 为新建语义传明文（必填，WPF 弹窗同款）；PUT 空/缺失 = 保持。
    /// </summary>
    public class DataSourceSaveRequest
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
        public DataSourceConfigInput? Config { get; set; }
    }

    /// <summary>PUT /api/datasources/{name} 请求体：{config:{...}}（字段缺失 = 保持不变）。</summary>
    public class DataSourceConfigRequest
    {
        public DataSourceConfigInput? Config { get; set; }
    }

    /// <summary>
    /// 数据源连接参数（camelCase 绑定）。port 1-65535；password 仅写方向存在——
    /// PUT 语义：null/空串 = 保持原密码（Mysql/Pgsql Password setter 收 "" 会清空）。
    /// </summary>
    public class DataSourceConfigInput
    {
        public string? Path { get; set; }            // sqlite
        public string? Host { get; set; }            // mysql/pgsql
        public int? Port { get; set; }               // mysql/pgsql
        public string? DatabaseName { get; set; }    // mysql/pgsql
        public string? UserName { get; set; }        // mysql/pgsql
        public string? Password { get; set; }        // mysql/pgsql（明文，写方向）
    }

    /// <summary>
    /// PUT /api/settings/runners 请求体：{protocols:{SSH:{selectedRunnerName, runners:[...]}}, ...}。
    /// protocols 值的顶层键 camelCase（selectedRunnerName/runners，反序列化大小写不敏感）；
    /// runners 数组为 Newtonsoft PascalCase + $type 直通域（与 GET 原样往返，勿做命名转换）。
    /// 缺失的协议 = 保持不变；未知协议键 → 400。
    /// </summary>
    public class RunnersSaveRequest
    {
        public JsonElement? Protocols { get; set; }
    }
}
