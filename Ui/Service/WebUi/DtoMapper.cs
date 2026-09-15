using System;
using System.Collections.Generic;
using System.Linq;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// 协议层对象 → Web UI DTO 映射。
    /// 注意：禁止引用返回 WPF 类型的属性（如 ProtocolBase.IconImg/BitmapSource），只取字符串与列表。
    /// </summary>
    public static class DtoMapper
    {
        /// <summary>
        /// 从协议对象构造 <see cref="ServerDto"/>。
        /// Address/Port/UserName 分属不同基类层级，按实际类型模式匹配取值，
        /// 不具备该层级的协议（如 Dummy）对应字段留空串。
        /// </summary>
        /// <param name="lastConnectTime">来自 ProtocolBaseViewModel.LastConnectTime（LocalityConnectRecorder 缓存），默认 MinValue 映射为 0</param>
        public static ServerDto FromServer(ProtocolBase server, string dataSourceName, DateTime lastConnectTime = default)
        {
            string address = string.Empty;
            string port = string.Empty;
            string userName = string.Empty;
            if (server is ProtocolBaseWithAddressPortUserPwd withUserPwd)
            {
                address = withUserPwd.Address ?? string.Empty;
                port = withUserPwd.Port ?? string.Empty;
                userName = withUserPwd.UserName ?? string.Empty;
            }
            else if (server is ProtocolBaseWithAddressPort withAddressPort)
            {
                address = withAddressPort.Address ?? string.Empty;
                port = withAddressPort.Port ?? string.Empty;
            }

            return new ServerDto
            {
                Id = server.Id ?? string.Empty,
                DisplayName = server.DisplayName ?? string.Empty,
                Protocol = server.Protocol ?? string.Empty,
                Address = address,
                Port = port,
                UserName = userName,
                Tags = server.Tags?.ToList() ?? new List<string>(),
                Color = server.ColorHex ?? string.Empty,
                IconBase64 = server.IconBase64 ?? string.Empty,
                DataSourceName = dataSourceName ?? string.Empty,
                FolderPath = server.TreeNodes != null ? string.Join("/", server.TreeNodes) : string.Empty,
                LastConnectTime = lastConnectTime <= DateTime.MinValue
                    ? 0
                    // 假定本地墙上时钟（与 LocalityConnectRecorder 写入 DateTime.Now 一致）
                    : new DateTimeOffset(lastConnectTime).ToUnixTimeSeconds(),
                ConnectionState = WebUiConstants.StatusDisconnected,
            };
        }

        /// <summary>
        /// 数据源 → <see cref="DataSourceDto"/>；ServerCount 按缓存的服务器列表统计（剔除分组头与临时会话）。
        /// </summary>
        public static DataSourceDto FromDataSource(DataSourceBase ds)
        {
            return new DataSourceDto
            {
                Name = ds.DataSourceName ?? string.Empty,
                Type = ds.DatabaseType switch
                {
                    DatabaseType.Sqlite => "sqlite",
                    DatabaseType.MySql => "mysql",
                    DatabaseType.PostgreSQL => "pgsql",
                    _ => ds.DatabaseType.ToString().ToLowerInvariant(),
                },
                Status = ds.Status switch
                {
                    EnumDatabaseStatus.OK => WebUiConstants.StatusConnected,
                    EnumDatabaseStatus.LostConnection => WebUiConstants.StatusReconnecting,
                    _ => WebUiConstants.StatusDisconnected,
                },
                Writable = ds.IsWritable,
                ReconnectInfo = ds.ReconnectInfo ?? string.Empty,
                ServerCount = CountCachedServers(ds),
            };
        }

        /// <summary>
        /// 数据源缓存的服务器计数（剔除分组头与临时会话）。
        /// DataSourceBase.GetServers 在 lock(this=ds) 下整体替换 CachedProtocols，
        /// 读侧同步加锁以避免并发重载时枚举抛 InvalidOperationException（HTTP 500）。
        /// 注：上游 Database_DeleteServer 对 CachedProtocols 的 RemoveAll 本就未加锁
        /// （DataSourceBase.Source.cs 既有行为，此处不改动），该窗口由快照读兜底。
        /// </summary>
        private static int CountCachedServers(DataSourceBase ds)
        {
            lock (ds)
            {
                return ds.CachedProtocols.Count(x => x.Server is not Dummy && !x.Server.IsTmpSession());
            }
        }

        /// <summary>
        /// 标签聚合快照：GlobalData.TagList 由 ReloadTagsFromServers 维护（含 IsPinned/计数）。
        /// 快照语义：ReloadTagsFromServers 原子交换 TagList 引用且换出的旧列表不再被修改，
        /// 读侧即使不加锁也安全；此锁仅与 GlobalData 自身的 lock(this)（StopTick/StartTick）串行化。
        /// </summary>
        public static List<TagDto> BuildTags(GlobalData gd)
        {
            lock (gd)
            {
                return gd.TagList
                    .Select(t => new TagDto
                    {
                        Name = t.Name,
                        Count = t.ItemsCount,
                        IsPinned = t.IsPinned,
                    })
                    .ToList();
            }
        }
    }
}
