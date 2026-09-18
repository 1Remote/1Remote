using System.Collections.Generic;
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
using _1RM.Service.DataSource;
using _1RM.Service.WebUi;
using _1RM.View;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// POST /api/servers/batch/peek 集成测试（fix batch8 #8）：批量编辑的共享值回读。
    /// 响应为逐台固定 6 键（camelCase 列表 DTO 域）的非敏感载荷——id +
    /// askPasswordWhenConnect/inheritedCredentialName/startupAutoCommand/startupPath/
    /// rdpFileAdditionalSettings；协议不适用字段为 null（如 RDP 无 StartupPath）。
    /// 安全断言：键集精确相等（无 password/privateKey 等加密键）且响应不含创建口令明文。
    /// 错误语义与 batch 补丁对齐：ids 空/缺失 400；任一 id 未知 404。
    /// 用例经 POST /api/servers 造独立种子，不触碰其它测试类的夹具种子。
    /// </summary>
    [TestClass]
    public class BatchPeekTests
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
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestClient();
        }

        /// <summary>前置辅助：POST 新建 RDP 并携带批量回读相关字段的显式值，返回新 id。</summary>
        private static async Task<string> CreateRdpAsync(string prefix, int i, string rdpAdditional)
        {
            var body = $"{{\"dataSourceName\":\"Local\",\"json\":{{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                       + $"\"DisplayName\":\"{prefix}-{i}\",\"Address\":\"172.17.0.{i}\",\"Port\":\"339{i}\","
                       + $"\"Password\":\"pw-{prefix}-{i}\","
                       + $"\"AskPasswordWhenConnect\":true,"
                       + $"\"InheritedCredentialName\":\"cred-{prefix}\","
                       + $"\"RdpFileAdditionalSettings\":\"{rdpAdditional}\"}}}}";
            var resp = await _client.PostAsync("/api/servers",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"前置创建失败: {await resp.Content.ReadAsStringAsync()}");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        private static Task<HttpResponseMessage> PostPeekAsync(string body)
        {
            return _client.PostAsync("/api/servers/batch/peek",
                new StringContent(body, Encoding.UTF8, "application/json"));
        }

        [TestMethod]
        public async Task Peek_ReturnsFixedNonSensitiveShape_ValuesMatch()
        {
            var ids = new List<string>
            {
                await CreateRdpAsync("peek", 1, "redirectclipboard:i:0"),
                await CreateRdpAsync("peek", 2, "redirectclipboard:i:0"),
            };
            var resp = await PostPeekAsync($"{{\"ids\":[\"{ids[0]}\",\"{ids[1]}\"],\"ds\":\"Local\"}}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"回读失败: {await resp.Content.ReadAsStringAsync()}");
            var raw = await resp.Content.ReadAsStringAsync();

            // 安全断言一：响应不得携带创建口令明文（加密字段绝不回读）
            Assert.IsFalse(raw.Contains("pw-peek"), "peek 响应不得包含密码明文");

            using var doc = JsonDocument.Parse(raw);
            var items = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Array, items.ValueKind, "响应应为数组");
            Assert.AreEqual(2, items.GetArrayLength(), "两台各一条记录");
            var expectedKeys = new HashSet<string>
            {
                "id", "askPasswordWhenConnect", "inheritedCredentialName",
                "startupAutoCommand", "startupPath", "rdpFileAdditionalSettings",
            };
            foreach (var item in items.EnumerateArray())
            {
                // 安全断言二：键集精确相等（无 password/privateKey 等任何额外键，防字段漂移）
                var keys = new HashSet<string>();
                foreach (var p in item.EnumerateObject()) keys.Add(p.Name);
                Assert.IsTrue(keys.SetEquals(expectedKeys),
                    "逐台载荷键集必须恰为 6 个非敏感键，实际: " + string.Join(", ", keys));

                Assert.IsTrue(item.GetProperty("id").GetString() == ids[0] || item.GetProperty("id").GetString() == ids[1],
                    "记录 id 必须属于请求的 ids");
                // 值断言：创建时显式写入的字段回读一致
                Assert.IsTrue(item.GetProperty("askPasswordWhenConnect").GetBoolean(), "AskPasswordWhenConnect 应回读 true");
                Assert.AreEqual("cred-peek", item.GetProperty("inheritedCredentialName").GetString());
                Assert.AreEqual("redirectclipboard:i:0", item.GetProperty("rdpFileAdditionalSettings").GetString());
                // 协议不适用字段：RDP 无 StartupAutoCommand/StartupPath → null 占位
                Assert.AreEqual(JsonValueKind.Null, item.GetProperty("startupAutoCommand").ValueKind,
                    "RDP 无 StartupAutoCommand，应以 null 占位而非缺键");
                Assert.AreEqual(JsonValueKind.Null, item.GetProperty("startupPath").ValueKind,
                    "RDP 无 StartupPath，应以 null 占位而非缺键");
            }
        }

        [TestMethod]
        public async Task Peek_UnknownIdAmongValid_Returns404()
        {
            var id = await CreateRdpAsync("peek404", 1, "x:i:1");
            var resp = await PostPeekAsync($"{{\"ids\":[\"{id}\",\"no-such-server-id\"]}}");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode, "含未知 id 的回读应整批 404");
        }

        [TestMethod]
        public async Task Peek_EmptyOrNullIds_Returns400()
        {
            var resp = await PostPeekAsync("{\"ids\":[]}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "空 ids 必须拒绝");
            var resp2 = await PostPeekAsync("{}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp2.StatusCode, "缺失 ids 必须拒绝");
        }
    }
}
