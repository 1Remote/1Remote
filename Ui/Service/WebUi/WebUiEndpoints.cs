using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shawn.Utils.Wpf.Image;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// Web UI 后端（进程内 Kestrel）全部 HTTP 端点的 minimal API 注册入口。
    /// 本类为 partial，按业务域拆分为同目录多个文件，本文件（主文件）只持有：
    /// ─ <see cref="MapAll"/>：按固定顺序调用各分域 Map* 完成全部路由注册（WebUiServer 启动时调用）；
    /// ─ 共享助手：<see cref="IsConnectable"/> / <see cref="DeriveConnectionState"/> /
    ///   <see cref="BuildActiveServerIdSet"/>（跨多个分域复用；前两者还被 DtoMapper、
    ///   WebUiEditorService 等类与 Tests 直接引用，须保持 public）。
    /// 分域文件（各自持有该域的路由注册方法与仅该域使用的私有助手）：
    /// ─ WebUiEndpoints.Servers.cs     —— /api/servers*（列表/单机配置/CRUD/批量补丁/导入/导出）、
    ///                                     /api/search、/api/connect/{id}、/api/events（SSE 推送）
    /// ─ WebUiEndpoints.Credentials.cs —— /api/credentials*（names/list/save/reveal/delete）
    /// ─ WebUiEndpoints.DataSources.cs —— /api/datasources*（列表/新建/更新/删除/测试连接）
    /// ─ WebUiEndpoints.Settings.cs    —— /api/settings/*（general/verify/launcher/runners/appearance）
    /// ─ WebUiEndpoints.Tags.cs        —— /api/tags*（聚合列表/manage 置顶/rename/delete）
    /// ─ WebUiEndpoints.Aux.cs         —— /api/version、/api/icons*（内置列表/exe 提取）、
    ///                                     /api/serial/options、/api/ui-state/*（tree/list-order）
    /// </summary>
    public static partial class WebUiEndpoints
    {
        /// <summary>
        /// Web UI 侧的服务器过滤判定：跳过分组头 Dummy（树形列表虚拟节点）与
        /// 临时会话（TMP_SESSION_ 前缀或空 Id，即编辑器中尚未落库的对象）。
        /// /api/servers、/api/search、/api/connect 共用同一语义，避免各处过滤条件漂移。
        /// </summary>
        public static bool IsConnectable(ProtocolBase server)
        {
            return server is not Dummy && !server.IsTmpSession();
        }

        /// <summary>
        /// 连接状态派生（纯函数，Plan 4 Task 1）：activeServerIds 为当前 1Remote 托管会话占用的
        /// 服务器 Id 集合（<see cref="BuildActiveServerIdSet"/> 快照），serverId 命中 → connected。
        /// 语义收窄：Unhosted 会话（外部 mstsc.exe、RunWithHosting=false 的 LocalApp）不进连接字典，
        /// 显示 disconnected——状态含义是「该服务器是否有 1Remote 托管的活动会话」，不代表远端可达性。
        /// </summary>
        public static string DeriveConnectionState(IEnumerable<string> activeServerIds, string? serverId)
        {
            if (string.IsNullOrEmpty(serverId))
                return WebUiConstants.StatusDisconnected;
            foreach (var id in activeServerIds)
            {
                if (id == serverId)
                    return WebUiConstants.StatusConnected;
            }
            return WebUiConstants.StatusDisconnected;
        }

        /// <summary>
        /// 快照当前活动会话的服务器 Id 集合：遍历 SessionControlService.ConnectionId2Hosts
        /// （ConcurrentDictionary，Kestrel 线程直接枚举安全），只读 host.ProtocolServer.Id 属性
        /// （连接用 Clone() 是 MemberwiseClone，Id 保留，故与列表 vm.Server.Id 同源可匹配），
        /// 绝不在 Host 对象上调用方法——Host 是 WPF UserControl，跨线程调用非法。
        /// 测试宿主刻意不注册 SessionControlService（注册会令其订阅 OnRequestServerConnect，
        /// /api/connect 集成测试会触发真实连接流程），TryGet 为 null → 空集 → 全部 disconnected，
        /// 与「空连接字典」观测一致。
        /// </summary>
        private static HashSet<string> BuildActiveServerIdSet()
        {
            var set = new HashSet<string>();
            var sessions = IoC.TryGet<SessionControlService>();
            if (sessions == null)
                return set;
            foreach (var host in sessions.ConnectionId2Hosts.Values)
            {
                var id = host?.ProtocolServer?.Id;
                if (!string.IsNullOrEmpty(id))
                    set.Add(id);
            }
            return set;
        }

        /// <summary>
        /// 注册全部 Web API 路由。调用顺序与拆分前的单一内联实现逐条一致（每行注释标明
        /// 对应路由，行序 = 注册序）；minimal API 按模板+谓词匹配，此处保持原顺序仅为
        /// 让历史 diff 与评审平凡化，不改变任何匹配行为。
        /// </summary>
        public static void MapAll(WebApplication app)
        {
            MapVersion(app);             // GET    /api/version
            MapServersList(app);         // GET    /api/servers
            MapDataSources(app);         // GET    /api/datasources
                                           // POST   /api/datasources
                                           // PUT    /api/datasources/{name}
                                           // DELETE /api/datasources/{name}
                                           // POST   /api/datasources/{name}/test
            MapSettingsRunners(app);     // GET    /api/settings/runners
                                           // PUT    /api/settings/runners
            MapTagsList(app);            // GET    /api/tags
            MapServersSearch(app);       // GET    /api/search
            MapServersConnect(app);      // POST   /api/connect/{id}
            MapServersConfig(app);       // GET    /api/servers/{id}/config
            MapServersCrud(app);         // POST   /api/servers
                                           // PUT    /api/servers/{id}
                                           // DELETE /api/servers/{id}
            MapServersBatch(app);        // POST   /api/servers/batch
            MapServersImportExport(app); // POST   /api/servers/import
                                           // GET    /api/servers/export
            MapIcons(app);               // GET    /api/icons
            MapSerialOptions(app);       // GET    /api/serial/options
            MapCredentials(app);         // GET    /api/credentials/names
                                           // GET    /api/credentials
                                           // POST   /api/credentials
                                           // PUT    /api/credentials/{name}
                                           // DELETE /api/credentials/{name}
                                           // POST   /api/credentials/{name}/reveal
            MapSettingsGeneral(app);     // GET    /api/settings/general
                                           // PUT    /api/settings/general
            MapSettingsVerify(app);      // POST   /api/settings/verify
            MapSettingsLauncher(app);    // GET    /api/settings/launcher
                                           // PUT    /api/settings/launcher
            MapTagsManage(app);          // GET    /api/tags/manage
                                           // PUT    /api/tags/manage
                                           // POST   /api/tags/rename
                                           // DELETE /api/tags/{name}
            MapIconsExtractFromExe(app); // POST   /api/icons/extract-from-exe
            MapServersEvents(app);       // GET    /api/events（SSE）
            MapSettingsAppearance(app);  // GET    /api/settings/appearance
                                           // PUT    /api/settings/appearance
            MapUiStateTree(app);         // GET    /api/ui-state/tree
                                           // PUT    /api/ui-state/tree
            MapUiStateListOrder(app);    // GET    /api/ui-state/list-order
                                           // POST   /api/ui-state/list-order
        }
    }
}
