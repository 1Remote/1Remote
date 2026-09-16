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
