using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// 连接状态派生测试：
    /// - 纯函数 WebUiEndpoints.DeriveConnectionState：空活动集 / 命中 / 多会话同服务器 / 他人活跃 / null serverId。
    /// - /api/servers、/api/search 端点接线：测试宿主刻意不注册 SessionControlService
    ///   （注册会令其订阅 OnRequestServerConnect，/api/connect 集成测试将触发真实连接流程），
    ///   端点内 TryGet 返回 null → 活动集为空 → 全部 disconnected——与「空连接字典」同观测，
    ///   以此断言 connectionState 字段确实随列表下发。
    /// - 真实会话生命周期驱动的 SSE 通知无法无头测试：HostBase 是抽象 WPF UserControl
    ///   （含菜单/资源引用），构造伪 Host 注入字典不现实——SSE 触发列为 owner 手动验收项
    ///   （Web UI 连接一台 → 行内绿点点亮；断开 → 熄灭，时延≈一次 reload 推送）。
    /// </summary>
    [TestClass]
    public class ConnectionStatusTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        // ---- 纯函数：DeriveConnectionState ----

        [TestMethod]
        public void Derive_EmptyActiveSet_Disconnected()
        {
            Assert.AreEqual(WebUiConstants.StatusDisconnected,
                WebUiEndpoints.DeriveConnectionState(Array.Empty<string>(), "seed-rdp"));
        }

        [TestMethod]
        public void Derive_ActiveSetContainsId_Connected()
        {
            Assert.AreEqual(WebUiConstants.StatusConnected,
                WebUiEndpoints.DeriveConnectionState(new[] { "seed-rdp" }, "seed-rdp"));
        }

        [TestMethod]
        public void Derive_MultipleSessionsSameServer_Connected()
        {
            // 非 OnlyOneInstance 服务器可开多会话：字典中多个 host 指向同一 server Id，仍命中
            Assert.AreEqual(WebUiConstants.StatusConnected,
                WebUiEndpoints.DeriveConnectionState(new[] { "other", "seed-rdp", "seed-rdp" }, "seed-rdp"));
        }

        [TestMethod]
        public void Derive_OnlyOtherServersActive_TargetDisconnected()
        {
            Assert.AreEqual(WebUiConstants.StatusDisconnected,
                WebUiEndpoints.DeriveConnectionState(new[] { "a", "b" }, "seed-rdp"));
        }

        [TestMethod]
        public void Derive_NullOrEmptyServerId_Disconnected()
        {
            Assert.AreEqual(WebUiConstants.StatusDisconnected,
                WebUiEndpoints.DeriveConnectionState(new[] { "seed-rdp" }, null));
            Assert.AreEqual(WebUiConstants.StatusDisconnected,
                WebUiEndpoints.DeriveConnectionState(new[] { "seed-rdp" }, ""));
        }

        // ---- DtoMapper 接线：connectionState 参数 ----

        [TestMethod]
        public void FromServer_WithConnectionState_PassesThrough()
        {
            var rdp = new _1RM.Model.Protocol.RDP { Id = "cs-1", DisplayName = "cs", Address = "1.1.1.1" };
            var dto = DtoMapper.FromServer(rdp, "Local", connectionState: WebUiConstants.StatusConnected);
            Assert.AreEqual(WebUiConstants.StatusConnected, dto.ConnectionState);
        }

        // ---- 端点接线：空活动集基线（全 disconnected）----

        [TestMethod]
        public async Task GetServers_NoActiveSession_AllDisconnected()
        {
            var resp = await _client.GetAsync("/api/servers");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "seed-rdp"); // 前置：列表非空
            StringAssert.Contains(body, "\"connectionState\":\"disconnected\"");
            // "connected" 与 "disconnected" 前缀不同，Contains 不受子串干扰，可直接断言不出现
            Assert.IsFalse(body.Contains("\"connectionState\":\"connected\""),
                "无活动会话时列表中不得出现 connected 状态");
        }

        [TestMethod]
        public async Task Search_NoActiveSession_AllDisconnected()
        {
            var resp = await _client.GetAsync("/api/search?q=seed");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "seed-rdp"); // 前置：搜索命中种子
            StringAssert.Contains(body, "\"connectionState\":\"disconnected\"");
            Assert.IsFalse(body.Contains("\"connectionState\":\"connected\""),
                "无活动会话时搜索结果中不得出现 connected 状态");
        }
    }
}
