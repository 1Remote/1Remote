using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

        /// <summary>
        /// 前置辅助：POST 新建一台 RDP 并返回生成的 id。
        /// 请求体内嵌 json 键一律 PascalCase（与 GET config 直通一致），信封键 camelCase。
        /// </summary>
        private static async Task<string> CreateRdpViaPostAsync(string displayName, string? password = null)
        {
            var passwordPart = password == null ? "" : $",\"Password\":\"{password}\"";
            var body = $"{{\"dataSourceName\":\"Local\",\"json\":{{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + $"\"DisplayName\":\"{displayName}\",\"Address\":\"2.2.2.2\",\"Port\":\"3389\"{passwordPart}}}}}";
            var resp = await _client.PostAsync("/api/servers",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"前置创建失败: {await resp.Content.ReadAsStringAsync()}");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        [TestMethod]
        public async Task PostServer_CreatesServer_ReturnsGeneratedId_AndListsIt()
        {
            var body = "{\"dataSourceName\":\"Local\",\"json\":{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + "\"DisplayName\":\"new1\",\"Address\":\"2.2.2.2\",\"Port\":\"3389\"}}";
            var resp = await _client.PostAsync("/api/servers",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var id = doc.RootElement.GetProperty("id").GetString();
            Assert.IsFalse(string.IsNullOrEmpty(id), "POST 成功应返回新生成的 id");
            Assert.IsFalse(id!.StartsWith("TMP_SESSION_"), "落库 id 应为 ULID，而非临时会话前缀");
            Assert.AreNotEqual("new1", id, "id 应为数据库生成的 ULID，不是 DisplayName");

            // 回读：GET config 展示新对象
            var cfg = await _client.GetAsync($"/api/servers/{id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
            StringAssert.Contains(await cfg.Content.ReadAsStringAsync(), "\"DisplayName\":\"new1\"");

            // 列表可见（camelCase 域）
            var list = await _client.GetAsync("/api/servers");
            Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
            StringAssert.Contains(await list.Content.ReadAsStringAsync(), "\"displayName\":\"new1\"");
        }

        [TestMethod]
        public async Task PutServer_UpdatesDisplayName_AndConfigReflectsChange()
        {
            var id = await CreateRdpViaPostAsync("put-src");
            var body = "{\"json\":{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + "\"DisplayName\":\"put-dst\",\"Address\":\"3.3.3.3\",\"Port\":\"3389\"}}";
            var resp = await _client.PutAsync($"/api/servers/{id}?ds=Local",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

            var cfg = await _client.GetAsync($"/api/servers/{id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
            StringAssert.Contains(await cfg.Content.ReadAsStringAsync(), "\"DisplayName\":\"put-dst\"");
        }

        [TestMethod]
        public async Task PostServer_MissingDisplayName_Returns400WithErrors()
        {
            // 注意偏差（代码为准）：ProtocolBaseWithAddressPort.Address setter 会把空 DisplayName
            // 自动跟随为地址值（与 WPF 输入地址自动命名一致），故"缺 DisplayName 但有 Address"
            // 是合法自动命名而非错误。此处同时缺 Address，DisplayName 无从跟随 → 必须报错。
            var body = "{\"dataSourceName\":\"Local\",\"json\":{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + "\"Port\":\"3389\"}}"; // 无 DisplayName、无 Address
            var resp = await _client.PostAsync("/api/servers",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.TryGetProperty("errors", out var errors), "400 响应应含 errors 数组");
            Assert.AreEqual(JsonValueKind.Array, errors.ValueKind);
            Assert.IsTrue(errors.GetArrayLength() >= 1, "errors 非空（WPF IDataErrorInfo 同款 DisplayName 规则）");
        }

        [TestMethod]
        public async Task PostServer_NonNumericPort_Returns400WithErrors()
        {
            var body = "{\"dataSourceName\":\"Local\",\"json\":{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + "\"DisplayName\":\"bad-port\",\"Address\":\"2.2.2.2\",\"Port\":\"abc\"}}";
            var resp = await _client.PostAsync("/api/servers",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.GetProperty("errors").GetArrayLength() >= 1, "Port 非数字应产生校验错误");

            // 预校验原子性：校验失败不得有任何写入
            var list = await _client.GetAsync("/api/servers");
            Assert.IsFalse((await list.Content.ReadAsStringAsync()).Contains("\"displayName\":\"bad-port\""),
                "校验失败的对象不得出现在列表中");
        }

        [TestMethod]
        public async Task DeleteServer_CreatedServer_Returns204_AndRemovesIt()
        {
            var id = await CreateRdpViaPostAsync("del-src");
            var resp = await _client.DeleteAsync($"/api/servers/{id}?ds=Local");
            Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);

            var list = await _client.GetAsync("/api/servers");
            Assert.IsFalse((await list.Content.ReadAsStringAsync()).Contains("\"displayName\":\"del-src\""),
                "删除后列表不得再含该服务器");
            var cfg = await _client.GetAsync($"/api/servers/{id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.NotFound, cfg.StatusCode, "删除后 GET config 应 404");
        }

        [TestMethod]
        public async Task PostServer_Password_IsEncryptedAtRest_PlaintextOnRead()
        {
            var id = await CreateRdpViaPostAsync("roundtrip-pw", password: "pwA");

            // 读路径明文（对称于 GET config 的克隆+解密）
            var cfg = await _client.GetAsync($"/api/servers/{id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
            StringAssert.Contains(await cfg.Content.ReadAsStringAsync(), "\"Password\":\"pwA\"");

            // 静态加密：VmItemList 缓存来自 ReloadAll 重读库，仍为密文
            // （加密发生在 DataSourceBase.Database_InsertServer 的克隆上，调用方只传明文）
            var gd = _1RM.IoC.Get<GlobalData>();
            ProtocolBaseViewModel? cached;
            lock (gd) // 与端点同款快照纪律：锁内只做查找
            {
                cached = gd.GetItemById("Local", id);
            }
            Assert.IsNotNull(cached, "POST 后缓存中应有该服务器");
            var pwdServer = cached.Server as ProtocolBaseWithAddressPortUserPwd;
            Assert.IsNotNull(pwdServer, "种子为 RDP，应具备密码层级");
            Assert.AreNotEqual("pwA", pwdServer.Password,
                "缓存中的密码必须保持加密态——加密由 DataSourceBase 在内部克隆上完成");
        }

        [TestMethod]
        public async Task PutServer_UnknownId_Returns404()
        {
            var body = "{\"json\":{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + "\"DisplayName\":\"whatever\",\"Address\":\"4.4.4.4\",\"Port\":\"3389\"}}";
            var resp = await _client.PutAsync("/api/servers/editor-unknown-id?ds=Local",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task PostServer_UnknownDataSourceName_Returns400()
        {
            var body = "{\"dataSourceName\":\"no-such-ds\",\"json\":{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + "\"DisplayName\":\"ds-404\",\"Address\":\"2.2.2.2\",\"Port\":\"3389\"}}";
            var resp = await _client.PostAsync("/api/servers",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }
    }
}
