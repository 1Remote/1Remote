using System.Collections.Generic;
using System.Linq;
using _1RM.Model.Protocol.Base;

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
        public static ServerDto FromServer(ProtocolBase server, string dataSourceName)
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
                LastConnectTime = 0, // Task 4 从 LocalityConnectRecorder 填充
                ConnectionState = "disconnected",
            };
        }
    }
}
