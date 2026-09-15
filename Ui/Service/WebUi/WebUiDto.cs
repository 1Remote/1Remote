using System.Collections.Generic;

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
}
