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
using _1RM.View;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// GET /api/servers/{id}/config 集成测试：克隆+解密+全字段 JSON 直通。
    /// 键大小写约定（两个 casing 域，勿互相归一）：
    /// - 信封字段（id/dataSourceName/protocol/json）camelCase（列表 DTO 域）；
    /// - 内嵌 json 对象的键保持 ToJsonString 的 PascalCase 原样直通——
    ///   CreateFromJsonString 的 jObj.Protocol/jObj.ClassVersion 访问大小写敏感，任何一侧转换都会破坏直通。
    /// 加密约定：种子经 AddServer → Database_InsertServer 内部克隆+加密落库，VmItemList 缓存为加密态；
    /// 端点必须克隆后解密，不得原地解密污染缓存（WPF 编辑器的原地解密是既有缺陷，web 不复制）。
    /// </summary>
    [TestClass]
    public class EditorEndpointsTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();

            // 本类独有种子：带密码，用于验证「GET 返回明文 / 缓存保持密文」的克隆-解密语义
            var gd = _1RM.IoC.Get<GlobalData>();
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local, "TestInit 后本地数据源必须可用");
            gd.AddServer(new RDP
            {
                Id = "editor-rdp-1",
                DisplayName = "ed",
                Address = "1.1.1.1",
                Password = "pw123", // Database_InsertServer 内部克隆+加密，缓存与库中为密文
                Tags = new System.Collections.Generic.List<string>(),
            }, local);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        [TestMethod]
        public async Task GetConfig_ReturnsDecryptedJson_WithPascalCasePassthrough()
        {
            var resp = await _client.GetAsync("/api/servers/editor-rdp-1/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();

            // 信封键 camelCase + 协议值
            StringAssert.Contains(body, "\"protocol\":\"RDP\"");
            StringAssert.Contains(body, "\"dataSourceName\":\"Local\"");
            // 内嵌 json 键 PascalCase 原样直通（ToJsonString 无命名策略）
            StringAssert.Contains(body, "\"DisplayName\":\"ed\"");
            // 克隆+解密后的明文密码（与 WPF 编辑器同级暴露，受 token+回环保护）
            StringAssert.Contains(body, "\"Password\":\"pw123\"");
            // 反序列化选型鉴别字段（CreateFromJsonString 大小写敏感访问）
            StringAssert.Contains(body, "\"ClassVersion\"");
        }

        [TestMethod]
        public async Task GetConfig_UnknownId_Returns404()
        {
            var resp = await _client.GetAsync("/api/servers/not-exist/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task GetConfig_DoesNotPolluteEncryptedCache()
        {
            var resp = await _client.GetAsync("/api/servers/editor-rdp-1/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "\"Password\":\"pw123\""); // 前置：响应确实含明文

            // 缓存非污染：VmItemList 中的原对象仍是加密态（密码 != 明文）
            var gd = _1RM.IoC.Get<GlobalData>();
            ProtocolBaseViewModel? cached;
            lock (gd) // 与端点同款快照纪律：锁内只做查找
            {
                cached = gd.GetItemById("Local", "editor-rdp-1");
            }
            Assert.IsNotNull(cached, "GET 后缓存中应仍有该服务器");
            var server = cached.Server as ProtocolBaseWithAddressPortUserPwd;
            Assert.IsNotNull(server, "种子为 RDP，应具备地址/端口/用户名/密码层级");
            Assert.AreNotEqual("pw123", server.Password,
                "缓存中的密码必须保持加密态——端点不得原地解密 VmItemList 中的对象");

            // 一致性：再次 GET 仍返回明文（每次请求独立克隆+解密，不依赖缓存状态）
            var resp2 = await _client.GetAsync("/api/servers/editor-rdp-1/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, resp2.StatusCode);
            var body2 = await resp2.Content.ReadAsStringAsync();
            StringAssert.Contains(body2, "\"Password\":\"pw123\"");
        }
    }
}
