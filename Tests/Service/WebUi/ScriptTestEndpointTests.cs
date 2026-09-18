using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Service.DataSource;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// POST /api/scripts/test 集成测试（batch9 Task B #9）：编辑器脚本行 Test 按钮的后端执行。
    /// 用真实 .bat 脚本走 WPF 同一条拆解/执行路径（DisassembleOneLineScriptCmd → 重定向执行）：
    /// - echo 输出回传 + exitCode=0；
    /// - 非零退出码（exit 3）回传；
    /// - 启动失败（不存在的脚本路径）→ 200 {error}（WPF 同款吞异常语义）；
    /// - 超时（ScriptTestTimeoutMs 调小 + ping 驻留脚本）→ timedOut；
    /// - 空 command → 400。
    /// </summary>
    [TestClass]
    public class ScriptTestEndpointTests
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

        [TestCleanup]
        public void RestoreTimeout()
        {
            WebUiEndpoints.ScriptTestTimeoutMs = 15_000; // 超时注入点是静态的：逐测试还原
        }

        [TestMethod]
        public async Task BatEcho_ReturnsOutputAndZeroExitCode()
        {
            var bat = Path.Combine(Path.GetTempPath(), "1rm-script-test-" + Guid.NewGuid().ToString("N") + ".bat");
            File.WriteAllText(bat, "@echo off\r\necho script-test-ok-123\r\n");
            try
            {
                using var doc = await PostAsync(bat);
                Assert.IsNull(doc.RootElement.GetProperty("error").GetString(), "正常执行不应有 error");
                Assert.AreEqual(0, doc.RootElement.GetProperty("exitCode").GetInt32(), "echo 脚本应正常退出");
                Assert.IsFalse(doc.RootElement.GetProperty("timedOut").GetBoolean());
                var output = doc.RootElement.GetProperty("output").GetString();
                Assert.IsTrue(output?.Contains("script-test-ok-123") == true, $"输出应含 echo 标记，实际: {output}");
                // 拆解字段回传（WPF DisassembleOneLineScriptCmd：存在文件 → 全路径 + 工作目录）
                Assert.AreEqual(Path.GetFullPath(bat), doc.RootElement.GetProperty("file").GetString());
            }
            finally
            {
                File.Delete(bat);
            }
        }

        [TestMethod]
        public async Task BatNonZeroExit_ReturnsExitCode()
        {
            var bat = Path.Combine(Path.GetTempPath(), "1rm-script-test-" + Guid.NewGuid().ToString("N") + ".bat");
            File.WriteAllText(bat, "@echo off\r\nexit 3\r\n");
            try
            {
                using var doc = await PostAsync(bat);
                Assert.AreEqual(3, doc.RootElement.GetProperty("exitCode").GetInt32(), "exit 3 应原样回传（WPF 报退出码语义）");
            }
            finally
            {
                File.Delete(bat);
            }
        }

        [TestMethod]
        public async Task MissingScript_ReturnsStartError()
        {
            // 不存在的脚本：Process.Start 抛 Win32Exception → 端点吞异常回 200 {error}
            //（WPF RunScriptBeforeConnect 的 catch → MessageBoxHelper.ErrorAlert 同语义）
            var missing = Path.Combine(Path.GetTempPath(), "no-such-1rm-script-" + Guid.NewGuid().ToString("N") + ".bat");
            Assert.IsFalse(File.Exists(missing), "前置：路径确实不存在");
            using var doc = await PostAsync(missing);
            Assert.IsFalse(string.IsNullOrEmpty(doc.RootElement.GetProperty("error").GetString()), "启动失败应回填 error");
        }

        [TestMethod]
        public async Task HangingScript_TimesOutAndKills()
        {
            // ping 4 次约 3s；超时注入点调小到 300ms → 必然超时，进程被杀、timedOut=true
            WebUiEndpoints.ScriptTestTimeoutMs = 300;
            var bat = Path.Combine(Path.GetTempPath(), "1rm-script-test-" + Guid.NewGuid().ToString("N") + ".bat");
            File.WriteAllText(bat, "@ping -n 4 127.0.0.1 > nul\r\n");
            try
            {
                using var doc = await PostAsync(bat);
                Assert.IsTrue(doc.RootElement.GetProperty("timedOut").GetBoolean(), "驻留脚本应触发超时");
            }
            finally
            {
                File.Delete(bat);
            }
        }

        [TestMethod]
        public async Task EmptyCommand_Returns400()
        {
            var body = JsonSerializer.Serialize(new { command = "   " });
            var resp = await _client.PostAsync("/api/scripts/test",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        private static async Task<JsonDocument> PostAsync(string command)
        {
            var body = JsonSerializer.Serialize(new { command });
            var resp = await _client.PostAsync("/api/scripts/test",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, "测试执行结果（含非零退出码/失败）按 200 回传");
            return JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        }
    }
}
