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
                ServerCount = ds.CachedProtocols.Count(x => x.Server is not Dummy && !x.Server.IsTmpSession()),
            };
        }

        /// <summary>
        /// 标签聚合快照：GlobalData.TagList 由 ReloadTagsFromServers 维护（含 IsPinned/计数）。
        /// </summary>
        public static List<TagDto> BuildTags(GlobalData gd)
        {
            lock (gd) // 对齐 GlobalData 的锁策略
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
