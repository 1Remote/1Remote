using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/connect 集成测试：POST /api/connect/{id} → 触发 GlobalEventHelper.OnRequestServerConnect
    /// （fromView="WebUi"，与 WPF/托盘/命名管道同一条事件管线）。
    /// 密码交互等仍在桌面端 SessionControlService 处理——测试环境无订阅者，端点内 ?.Invoke 安全。
    /// 注意：OnRequestServerConnect 是静态事件，订阅必须在 finally 中退订，避免泄漏到其它测试类。
    /// </summary>
    [TestClass]
    public class ConnectEndpointTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();

            // 本类独有种子：与 seed-rdp 及其它测试类种子区分
            var gd = _1RM.IoC.Get<GlobalData>();
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local, "TestInit 后本地数据源必须可用");
            gd.AddServer(new RDP
            {
                Id = "connect-rdp-1",
                DisplayName = "connect-target",
                Address = "1.2.3.4",
                Tags = new List<string>(),
            }, local);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        [TestMethod]
        public async Task Connect_UnknownId_Returns404()
        {
            var resp = await _client.PostAsync("/api/connect/not-exist", null);
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        // ---- IsConnectable 过滤器单元测试（纯函数，无需 HTTP/夹具种子）----

        [TestMethod]
        public void IsConnectable_NormalServer_True()
        {
            var rdp = new RDP { Id = "01J8Z", DisplayName = "n", Address = "1.2.3.4" };
            Assert.IsTrue(WebUiEndpoints.IsConnectable(rdp));
        }

        [TestMethod]
        public void IsConnectable_DummyGroupHeader_False()
        {
            // VmItemList 中的分组头（树形列表虚拟节点）不是可连接的真实服务器
            var dummy = new Dummy { Id = "group-header" };
            Assert.IsFalse(WebUiEndpoints.IsConnectable(dummy));
        }

        [TestMethod]
        public void IsConnectable_TmpSession_False()
        {
            // 未设置 Id 的对象（编辑器/临时会话形态）：Id getter 会生成 TMP_SESSION_ 前缀 id
            var fresh = new RDP { DisplayName = "t", Address = "1.2.3.4" };
            Assert.IsTrue(fresh.IsTmpSession(), "前置：未设置 Id 的服务器对象即临时会话");
            Assert.IsFalse(WebUiEndpoints.IsConnectable(fresh));

            // 显式 TMP_SESSION_ 前缀 id 同样不可连接
            var tmp = new RDP { Id = "TMP_SESSION_123", DisplayName = "t2", Address = "1.2.3.4" };
            Assert.IsFalse(WebUiEndpoints.IsConnectable(tmp));
        }

        [TestMethod]
        public async Task Connect_KnownId_FiresEventAndReturnsOk()
        {
            var gd = _1RM.IoC.Get<GlobalData>();
            ProtocolBase expected = gd.VmItemList.First(x => x.Server.Id == "connect-rdp-1").Server;

            var fired = false;
            ProtocolBase? firedServer = null;
            string? firedFromView = null;

            // 端点在返回响应前同步 Invoke 委托（见 WebUiEndpoints 实现），
            // TestServer 下 PostAsync 返回时处理器已执行完毕，故无需同步原语
            GlobalEventHelper.OnRequestServerConnectDelegate handler =
                (in ProtocolBase server, in string fromView, in string assignTabToken, in string assignRunnerName, in string assignCredentialName) =>
                {
                    fired = true;
                    firedServer = server;
                    firedFromView = fromView;
                };
            GlobalEventHelper.OnRequestServerConnect += handler;
            try
            {
                var resp = await _client.PostAsync("/api/connect/connect-rdp-1", null);
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
                var body = await resp.Content.ReadAsStringAsync();
                StringAssert.Contains(body, "started");

                Assert.IsTrue(fired, "OnRequestServerConnect 应被触发");
                Assert.AreSame(expected, firedServer, "事件应携带 VmItemList 中的同一服务器实例");
                Assert.AreEqual("WebUi", firedFromView);
            }
            finally
            {
                // 静态事件：退订，防止处理器泄漏到其它测试类
                GlobalEventHelper.OnRequestServerConnect -= handler;
            }
        }
    }
}
