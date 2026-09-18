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
    /// POST /api/servers/batch/peek 集成测试（fix batch8 #8；batch9 Task C 键集扩展）：
    /// 批量编辑的共享值回读。响应为逐台 {id, ...camelCase: value}——键集从 BatchPatchFieldMap
    /// 同源派生（全部 schema 标量键，扣 3 个加密键 password/privateKey/gatewayPassword 与
    /// 8 个列表 DTO 已覆盖键 displayName/note/tags/colorHex/iconBase64/address/port/userName）；
    /// 协议不适用字段为 null（如 RDP 无 StartupPath）。
    /// 安全断言：键集不含任何加密键且响应不含创建口令明文。
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
        public async Task Peek_ReturnsDerivedNonSensitiveShape_ValuesMatch()
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

            // 安全断言二（键集派生规则）：加密键与列表 DTO 已覆盖键绝不出现，schema 扩展键必须出现
            var forbiddenKeys = new HashSet<string>
            {
                // 加密键（BatchPeekSensitiveKeys）
                "password", "privateKey", "gatewayPassword",
                // 列表 DTO 已覆盖键（前端从 /api/servers 取共享值，peek 不重传）
                "displayName", "note", "tags", "colorHex", "iconBase64", "address", "port", "userName",
            };
            var requiredKeys = new HashSet<string>
            {
                "id", "askPasswordWhenConnect", "inheritedCredentialName",
                "startupAutoCommand", "startupPath", "rdpFileAdditionalSettings",
                // batch9 Task C 扩展：协议 schema 标量键（RDP 侧代表集）
                "rdpWidth", "rdpHeight", "enableClipboard", "rdpFullScreenFlag",
                "displayPerformance", "gatewayMode", "mstscModeEnabled",
                "commandBeforeConnected", "selectedRunnerName", "alwaysOpenInNewTabWindow",
            };
            foreach (var item in items.EnumerateArray())
            {
                var keys = new HashSet<string>();
                foreach (var p in item.EnumerateObject()) keys.Add(p.Name);
                foreach (var forbidden in forbiddenKeys)
                    Assert.IsFalse(keys.Contains(forbidden), $"peek 不得包含键 '{forbidden}'（实际: {string.Join(", ", keys)}）");
                foreach (var required in requiredKeys)
                    Assert.IsTrue(keys.Contains(required), $"peek 应包含扩展键 '{required}'（实际: {string.Join(", ", keys)}）");

                Assert.IsTrue(item.GetProperty("id").GetString() == ids[0] || item.GetProperty("id").GetString() == ids[1],
                    "记录 id 必须属于请求的 ids");
                // 值断言：创建时显式写入的字段回读一致
                Assert.IsTrue(item.GetProperty("askPasswordWhenConnect").GetBoolean(), "AskPasswordWhenConnect 应回读 true");
                Assert.AreEqual("cred-peek", item.GetProperty("inheritedCredentialName").GetString());
                Assert.AreEqual("redirectclipboard:i:0", item.GetProperty("rdpFileAdditionalSettings").GetString());
                // 扩展键值断言：RDP ctor 默认（RdpWidth=800/EnableClipboard=true）应原样回读
                Assert.AreEqual(800, item.GetProperty("rdpWidth").GetInt32(), "RdpWidth 应回读 RDP ctor 默认 800");
                Assert.IsTrue(item.GetProperty("enableClipboard").GetBoolean(), "EnableClipboard 应回读默认 true");
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
