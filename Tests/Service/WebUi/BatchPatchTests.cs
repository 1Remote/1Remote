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
    /// POST /api/servers/batch 集成测试：补丁式批量编辑（patch 中缺失的字段 = 保持不变）。
    /// casing 域约定：patch 键属列表 DTO（camelCase）域，服务端经显式 allow-list 映射到
    /// C# PascalCase 属性；未知键 400 列出（防静默丢弃）。password 为明文（保存时加密）。
    /// batch9 Task C 起 allow-list 覆盖编辑器 schema 的全部标量字段（枚举/文本/数字/开关），
    /// 值经反射类型门校验（bool←Boolean、int/枚举←Integer、string←String）。
    /// 原子性=预校验原子性（任一 id 缺失/任一台校验失败 → 整批不执行、零写入）。
    /// 改动型测试各自经 POST /api/servers 造独立种子，不触碰夹具种子（batch-1/2/3），
    /// 避免用例执行顺序影响断言。
    /// </summary>
    [TestClass]
    public class BatchPatchTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();

            // 本类独有种子：不同 Port/Tags/Address/Password，供只读断言与原子性检查
            var gd = _1RM.IoC.Get<GlobalData>();
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local, "TestInit 后本地数据源必须可用");
            for (var i = 1; i <= 3; i++)
            {
                gd.AddServer(new RDP
                {
                    Id = $"batch-{i}",
                    DisplayName = $"batch-{i}",
                    Address = $"10.0.0.{i}",
                    Port = $"338{i}",
                    Password = $"pw-{i}", // Database_InsertServer 内部克隆+加密，缓存与库中为密文
                    Tags = new List<string> { $"t{i}" },
                }, local);
            }

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestClient();
        }

        /// <summary>前置辅助：POST 新建 3 台 RDP（不同 Address/Port/Password/Tags），返回生成的 id 列表。</summary>
        private static async Task<List<string>> CreateThreeViaPostAsync(string prefix)
        {
            var ids = new List<string>();
            for (var i = 1; i <= 3; i++)
            {
                var body = $"{{\"dataSourceName\":\"Local\",\"json\":{{\"Protocol\":\"RDP\",\"ClassVersion\":\"RDP.V1\","
                           + $"\"DisplayName\":\"{prefix}-{i}\",\"Address\":\"172.16.0.{i}\",\"Port\":\"339{i}\","
                           + $"\"Password\":\"pw-{prefix}-{i}\",\"Tags\":[\"tag-{prefix}-{i}\"]}}}}";
                var resp = await _client.PostAsync("/api/servers",
                    new StringContent(body, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                    $"前置创建失败: {await resp.Content.ReadAsStringAsync()}");
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                ids.Add(doc.RootElement.GetProperty("id").GetString()!);
            }
            return ids;
        }

        private static Task<HttpResponseMessage> PostBatchAsync(string body)
        {
            return _client.PostAsync("/api/servers/batch",
                new StringContent(body, Encoding.UTF8, "application/json"));
        }

        private static async Task<string> GetConfigAsync(string id)
        {
            var resp = await _client.GetAsync($"/api/servers/{id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, $"GET config 失败: {id}");
            return await resp.Content.ReadAsStringAsync();
        }

        [TestMethod]
        public async Task BatchPatch_Port_AppliedToAll_OtherFieldsUntouched()
        {
            var ids = await CreateThreeViaPostAsync("bport");
            var resp = await PostBatchAsync(
                $"{{\"ids\":[\"{ids[0]}\",\"{ids[1]}\",\"{ids[2]}\"],\"patch\":{{\"port\":\"3390\"}}}}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"批量补丁失败: {await resp.Content.ReadAsStringAsync()}");
            StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "\"updated\":3");

            for (var i = 1; i <= 3; i++)
            {
                var cfg = await GetConfigAsync(ids[i - 1]);
                StringAssert.Contains(cfg, "\"Port\":\"3390\"", $"第 {i} 台端口应被批量修改");
                // 未出现在 patch 中的字段保持原值（各自不同的 address 不受影响）
                StringAssert.Contains(cfg, $"\"Address\":\"172.16.0.{i}\"");
            }
        }

        [TestMethod]
        public async Task BatchPatch_Tags_ReplacedOnAll()
        {
            var ids = await CreateThreeViaPostAsync("btags");
            var resp = await PostBatchAsync(
                $"{{\"ids\":[\"{ids[0]}\",\"{ids[1]}\",\"{ids[2]}\"],\"patch\":{{\"tags\":[\"x\"]}}}}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"批量补丁失败: {await resp.Content.ReadAsStringAsync()}");

            // Tags=显式覆盖语义（与 WPF 交集合并的有意偏差）：三台均恰为 ["x"]
            foreach (var id in ids)
            {
                var cfg = await GetConfigAsync(id);
                StringAssert.Contains(cfg, "\"Tags\":[\"x\"]");
                Assert.IsFalse(cfg.Contains("tag-btags"), "原标签应被整体覆盖而非合并");
            }
        }

        [TestMethod]
        public async Task BatchPatch_DisplayNameOnly_AddressAndPasswordUnchanged()
        {
            var ids = await CreateThreeViaPostAsync("bname");
            var resp = await PostBatchAsync(
                $"{{\"ids\":[\"{ids[0]}\",\"{ids[1]}\",\"{ids[2]}\"],\"patch\":{{\"displayName\":\"N\"}}}}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"批量补丁失败: {await resp.Content.ReadAsStringAsync()}");

            for (var i = 1; i <= 3; i++)
            {
                var cfg = await GetConfigAsync(ids[i - 1]);
                StringAssert.Contains(cfg, "\"DisplayName\":\"N\"");
                // 逐字段断言：未 patch 的字段原样保留（含密码明文往返）
                StringAssert.Contains(cfg, $"\"Address\":\"172.16.0.{i}\"");
                StringAssert.Contains(cfg, $"\"Password\":\"pw-bname-{i}\"");
            }
        }

        [TestMethod]
        public async Task BatchPatch_BoolField_ConvertedAndApplied()
        {
            var ids = await CreateThreeViaPostAsync("bbool");
            var resp = await PostBatchAsync(
                $"{{\"ids\":[\"{ids[0]}\"],\"patch\":{{\"askPasswordWhenConnect\":true}}}}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"批量补丁失败: {await resp.Content.ReadAsStringAsync()}");
            var cfg = await GetConfigAsync(ids[0]);
            StringAssert.Contains(cfg, "\"AskPasswordWhenConnect\":true", "bool patch 值应转换并生效");
        }

        [TestMethod]
        public async Task BatchPatch_EmptyPatch_Returns400()
        {
            var resp = await PostBatchAsync("{\"ids\":[\"batch-1\"],\"patch\":{}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "空 patch 必须拒绝");
        }

        [TestMethod]
        public async Task BatchPatch_EmptyOrNullIds_Returns400()
        {
            var resp = await PostBatchAsync("{\"ids\":[],\"patch\":{\"port\":\"3390\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "空 ids 必须拒绝");
            var resp2 = await PostBatchAsync("{\"patch\":{\"port\":\"3390\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp2.StatusCode, "缺失 ids 必须拒绝");
        }

        [TestMethod]
        public async Task BatchPatch_UnknownField_Returns400AndListsIt()
        {
            var resp = await PostBatchAsync(
                "{\"ids\":[\"batch-1\"],\"patch\":{\"hackerField\":\"x\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "未知 patch 键必须拒绝");
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "hackerField", "400 应列出未知键名（防静默丢弃）");
        }

        [TestMethod]
        public async Task BatchPatch_UnknownIdAmongValid_Returns404_NothingExecuted()
        {
            // 预校验原子性：任一 id 无效 → 整批不执行（含合法 id 也不被修改）
            var resp = await PostBatchAsync(
                "{\"ids\":[\"batch-1\",\"no-such-server-id\",\"batch-2\"],\"patch\":{\"port\":\"3399\",\"displayName\":\"should-not-apply\"}}");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode, "含未知 id 的批次应 404");

            // 整批零执行：两台合法服务器的端口/名称仍是种子值
            var cfg1 = await GetConfigAsync("batch-1");
            StringAssert.Contains(cfg1, "\"Port\":\"3381\"");
            StringAssert.Contains(cfg1, "\"DisplayName\":\"batch-1\"");
            var cfg2 = await GetConfigAsync("batch-2");
            StringAssert.Contains(cfg2, "\"Port\":\"3382\"");
            StringAssert.Contains(cfg2, "\"DisplayName\":\"batch-2\"");
        }

        [TestMethod]
        public async Task BatchPatch_DeepFieldAlternateCredentials_Returns400WithMessage()
        {
            // 有意简化：子表单/深层字段（alternateCredentials 等）不进批量 allow-list，
            // 400 + 明确消息引导走单机编辑（PUT /api/servers/{id}）
            var resp = await PostBatchAsync(
                "{\"ids\":[\"batch-1\"],\"patch\":{\"alternateCredentials\":[{\"Name\":\"a\"}]}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "深层字段必须拒绝");
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "alternateCredentials", "错误消息应点名该字段");
            StringAssert.Contains(body, "PUT", "错误消息应引导单机编辑路径");
        }

        [TestMethod]
        public async Task BatchPatch_InvalidPortValue_Returns400_NothingSaved()
        {
            var resp = await PostBatchAsync(
                "{\"ids\":[\"batch-2\"],\"patch\":{\"port\":\"abc\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "非法端口值必须经 WPF 平价校验拒绝");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.GetProperty("errors").GetArrayLength() >= 1);

            // 预校验原子性：校验失败不得有任何写入
            var cfg = await GetConfigAsync("batch-2");
            StringAssert.Contains(cfg, "\"Port\":\"3382\"", "校验失败后端口应保持原值");
        }

        [TestMethod]
        public async Task BatchPatch_NullPatchValue_Returns400_NothingSaved()
        {
            // JSON null 一律拒绝：null 会绕过部分 C# setter 的防护直落库（如 Note→null 字符串，
            // WPF 只会产出 ""），下游序列化/连接路径有 NRE/500 风险。清空须显式用 "" 或 []。
            var resp = await PostBatchAsync("{\"ids\":[\"batch-1\"],\"patch\":{\"note\":null}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "null patch 值必须拒绝");
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "note", "错误消息应点名该字段");

            // 预校验原子性：拒绝后不得有任何写入
            var cfg = await GetConfigAsync("batch-1");
            StringAssert.Contains(cfg, "\"DisplayName\":\"batch-1\"", "拒绝后服务器不得被修改");
            StringAssert.Contains(cfg, "\"Port\":\"3381\"");
        }

        [TestMethod]
        public async Task BatchPatch_FieldMissingOnProtocol_Returns400_NothingSaved()
        {
            // startupPath 只存在于 FTP/SFTP：对 RDP 批量 patch 该字段 → 400 且零写入
            var resp = await PostBatchAsync(
                "{\"ids\":[\"batch-3\"],\"patch\":{\"startupPath\":\"/tmp\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "协议上不存在的字段必须拒绝");
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "startupPath", "错误消息应点名该字段");
            var cfg = await GetConfigAsync("batch-3");
            StringAssert.Contains(cfg, "\"Port\":\"3383\"", "拒绝后不得有任何写入");
        }

        /// <summary>
        /// batch9 Task C：allow-list 扩展后的协议感知键写入往返——RDP 专属的数字
        /// （RdpWidth）、开关（EnableClipboard）、枚举（RdpFullScreenFlag/GatewayMode）经
        /// 反射类型门转换并落到全部所选服务器；未 patch 的字段保持原值。
        /// </summary>
        [TestMethod]
        public async Task BatchPatch_ProtocolAwareKeys_AppliedToAll()
        {
            var ids = await CreateThreeViaPostAsync("bproto");
            var resp = await PostBatchAsync(
                $"{{\"ids\":[\"{ids[0]}\",\"{ids[1]}\",\"{ids[2]}\"]," +
                "\"patch\":{\"rdpWidth\":1024,\"enableClipboard\":false,\"rdpFullScreenFlag\":0,\"gatewayMode\":1,\"gatewayHostName\":\"gw.example.com\"}}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode,
                $"批量补丁失败: {await resp.Content.ReadAsStringAsync()}");
            StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "\"updated\":3");

            foreach (var id in ids)
            {
                var cfg = await GetConfigAsync(id);
                StringAssert.Contains(cfg, "\"RdpWidth\":1024", "int? 键应写入");
                StringAssert.Contains(cfg, "\"EnableClipboard\":false", "bool? 键应写入");
                StringAssert.Contains(cfg, "\"RdpFullScreenFlag\":0", "枚举键应按整数值写入");
                StringAssert.Contains(cfg, "\"GatewayMode\":1", "网关模式枚举应写入");
                StringAssert.Contains(cfg, "\"GatewayHostName\":\"gw.example.com");
                // 未 patch 的字段保持原值
                StringAssert.Contains(cfg, "\"Port\":\"339");
            }
        }

        /// <summary>
        /// batch9 Task C：反射类型门——bool 键收字符串、int? 键收浮点均 400 且零写入
        /// （防 Newtonsoft ToObject 的隐式转换静默改值）。
        /// </summary>
        [TestMethod]
        public async Task BatchPatch_WrongJsonType_Returns400_NothingSaved()
        {
            var resp = await PostBatchAsync(
                "{\"ids\":[\"batch-1\"],\"patch\":{\"enableClipboard\":\"yes\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "bool 键的字符串值必须拒绝");
            StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "enableClipboard", "错误消息应点名该字段");

            var resp2 = await PostBatchAsync(
                "{\"ids\":[\"batch-2\"],\"patch\":{\"rdpWidth\":12.5}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp2.StatusCode, "int? 键的浮点值必须拒绝");

            // 预校验原子性：两次拒绝都零写入
            var cfg1 = await GetConfigAsync("batch-1");
            StringAssert.Contains(cfg1, "\"Port\":\"3381\"", "拒绝后不得有任何写入");
            var cfg2 = await GetConfigAsync("batch-2");
            StringAssert.Contains(cfg2, "\"Port\":\"3382\"");
        }

        /// <summary>
        /// batch9 Task C：扩展键同样受「属性不在该协议类型上 → 按台 400」约束——
        /// openSftpOnConnected 仅 SSH 有，对 RDP 批量 patch → 400 且零写入。
        /// </summary>
        [TestMethod]
        public async Task BatchPatch_ExtendedKeyMissingOnProtocol_Returns400()
        {
            var resp = await PostBatchAsync(
                "{\"ids\":[\"batch-1\"],\"patch\":{\"openSftpOnConnected\":true}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "协议上不存在的扩展键必须拒绝");
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "openSftpOnConnected", "错误消息应点名该字段");
            var cfg = await GetConfigAsync("batch-1");
            StringAssert.Contains(cfg, "\"Port\":\"3381\"", "拒绝后不得有任何写入");
        }
    }
}
