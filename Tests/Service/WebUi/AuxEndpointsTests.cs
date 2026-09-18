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
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// 编辑器辅助端点集成测试：
    /// - GET /api/icons：内置图标 base64 列表（ServerIcons 单例，程序集内嵌 PNG，装载不依赖 WPF Application）；
    /// - GET /api/credentials/names：凭据库名称列表（GetCredentials 缓存判定 → 读库），未知数据源 404；
    /// - POST /api/icons/extract-from-exe：exe 关联图标提取为 PNG base64（与 WPF 图标选择器同一路径），
    ///   缺失文件/非 exe → 404；
    /// - POST /api/files/pick：通用文件选择器（batch9 Task B #9，泛化自 batch8 pick-exe），
    ///   经 FilePicker 注入点 stub 测形状（选中 200 {path} / 取消 404 / filter+title+初始目录
    ///   透传 / 无 body 直通），真实 STA 弹窗不在单测范围。
    /// </summary>
    [TestClass]
    public class AuxEndpointsTests
    {
        private static HttpClient _client = null!;
        private const string SeedCredentialName = "aux-cred-1";

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();

            // 凭据种子：经正规 Database_InsertCredential 落库（内部克隆+加密，与 WPF 凭据管理同一入口）。
            // 端点走 GetCredentials()（NeedRead 缓存判定，凭据表首次读取必重读库）应能看到该名称。
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local, "TestInit 后本地数据源必须可用");
            var ret = local.Database_InsertCredential(new Credential
            {
                Name = SeedCredentialName,
                Address = "5.5.5.5",
                Port = "22",
                UserName = "aux-user",
                Password = "aux-pw",
            });
            Assert.IsTrue(ret.IsSuccess, "凭据种子插入失败: " + ret.ErrorInfo);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        [TestMethod]
        public async Task GetIcons_ReturnsNonEmptyPngBase64Array()
        {
            var resp = await _client.GetAsync("/api/icons");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.TryGetProperty("icons", out var icons), "响应应含 icons 数组");
            Assert.AreEqual(JsonValueKind.Array, icons.ValueKind);

            // ServerIcons 从程序集 .g.resources 装载内置 PNG（装载只需程序集资源 + GDI 位图转换，
            // 不需要 WPF Application），测试宿主同样应装载成功——非空是诚实断言而非环境巧合
            Assert.IsTrue(icons.GetArrayLength() > 0, "内置图标列表不应为空（Ui 程序集内嵌约 200 个 PNG）");

            foreach (var icon in icons.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.String, icon.ValueKind, "每个元素应为 base64 字符串");
                var s = icon.GetString()!;
                Assert.IsFalse(string.IsNullOrEmpty(s), "base64 字符串不应为空");
                // 装载器统一经 ToBitmapSource().ToBase64()（ImageFormat.Png）→ 解码后必须是 PNG
                var bytes = Convert.FromBase64String(s);
                Assert.IsTrue(bytes.Length > 8, "PNG 数据应非平凡");
                Assert.AreEqual(0x89, bytes[0], "PNG 魔数首字节 0x89");
                Assert.AreEqual(0x50, bytes[1], "PNG 魔数次字节 'P'");
            }
        }

        [TestMethod]
        public async Task GetCredentialNames_ReturnsArrayWithInsertedName()
        {
            var resp = await _client.GetAsync("/api/credentials/names?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.TryGetProperty("names", out var names), "响应应含 names 数组");
            Assert.AreEqual(JsonValueKind.Array, names.ValueKind);

            var found = false;
            foreach (var name in names.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.String, name.ValueKind);
                if (name.GetString() == SeedCredentialName) found = true;
            }
            Assert.IsTrue(found, $"names 应包含种子凭据 '{SeedCredentialName}'（GetCredentials 需读到库中凭据）");
        }

        [TestMethod]
        public async Task GetCredentialNames_UnknownDataSource_Returns404()
        {
            var resp = await _client.GetAsync("/api/credentials/names?ds=no-such-ds");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task ExtractIcon_FromNotepadExe_ReturnsPngBase64()
        {
            // System32\notepad.exe 在所有 Windows 上存在；不存在时（异常宿主）退化为验证缺失路径语义
            var notepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            var resp = await PostExtractAsync(notepad);
            if (!File.Exists(notepad))
            {
                Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode, "环境无 notepad.exe 时应 404");
                return;
            }
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.TryGetProperty("iconBase64", out var iconBase64), "响应应含 iconBase64");
            var b64 = iconBase64.GetString();
            Assert.IsFalse(string.IsNullOrEmpty(b64), "iconBase64 不应为空");
            var bytes = Convert.FromBase64String(b64!);
            Assert.IsTrue(bytes.Length > 8, "PNG 数据应非平凡");
            Assert.AreEqual(0x89, bytes[0], "PNG 魔数首字节 0x89");
            Assert.AreEqual(0x50, bytes[1], "PNG 魔数次字节 'P'");
        }

        [TestMethod]
        public async Task ExtractIcon_MissingFile_Returns404()
        {
            var missing = Path.Combine(Path.GetTempPath(), "no-such-1rm-" + Guid.NewGuid().ToString("N") + ".exe");
            Assert.IsFalse(File.Exists(missing), "前置：路径确实不存在");
            var resp = await PostExtractAsync(missing);
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task ExtractIcon_NotAnExe_Returns404()
        {
            // 与 WPF 图标选择器同一分支语义：仅 .exe 走 ExtractAssociatedIcon，其余类型不受理
            var txt = Path.Combine(Path.GetTempPath(), "not-exe-1rm-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(txt, "not an exe");
            try
            {
                var resp = await PostExtractAsync(txt);
                Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
            }
            finally
            {
                File.Delete(txt);
            }
        }

        // ------------------------------------------------------------------
        // POST /api/files/pick（batch9 Task B #9）：注入 stub 测形状——真实弹窗
        // 在测试宿主无人关闭会让请求永久挂起，端点把弹窗收敛在 FilePicker 注入点后，
        // 这里注入确定性 stub 覆盖 200/404/filter+title 透传/无 body 路径（生产 STA 实现
        // 由桌面进程运行时覆盖，对话框行为本身不在单测范围）。
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task PickFile_StubPicked_ReturnsPathAndDerivesInitialDir()
        {
            var picked = @"C:\apps\putty.exe";
            WebUiEndpoints.FilePicker = (filter, title, initialDir) =>
            {
                // 端点归一：filter 透传（exe 调用方传原值）、title 缺省 → null、
                // body.path 的目录推导在端点内完成，stub 收到的已是 initialDirectory
                Assert.AreEqual("exe|*.exe", filter, "filter 应原样透传（WPF SelectFileHelper 线格式）");
                Assert.IsNull(title, "body 无 title 时应归一为 null（系统默认标题）");
                Assert.AreEqual(@"C:\apps", initialDir, "端点应把 body.path 的目录部分作为 initialDirectory 传入");
                return picked;
            };
            try
            {
                var body = JsonSerializer.Serialize(new { filter = "exe|*.exe", path = @"C:\apps\old.exe" });
                var resp = await _client.PostAsync("/api/files/pick",
                    new StringContent(body, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                Assert.IsTrue(doc.RootElement.TryGetProperty("path", out var p), "响应应含 path");
                Assert.AreEqual(picked, p.GetString());
            }
            finally
            {
                WebUiEndpoints.FilePicker = null; // 还原生产实现（静态注入点，避免污染其它测试）
            }
        }

        [TestMethod]
        public async Task PickFile_ScriptFilterAndTitle_PassThrough()
        {
            // 脚本选择调用方（WPF CmdSelectScript 同参）：filter 含多段 + 第二段 *|*.*，title 直传
            var scriptFilter = "script|*.bat;*.cmd;*.ps1;*.py|*|*.*";
            WebUiEndpoints.FilePicker = (filter, title, initialDir) =>
            {
                Assert.AreEqual(scriptFilter, filter, "多段 filter 应原样透传");
                Assert.AreEqual("Select a script", title, "title 应原样透传");
                return @"C:\scripts\run.bat";
            };
            try
            {
                var body = JsonSerializer.Serialize(new { filter = scriptFilter, title = "Select a script" });
                var resp = await _client.PostAsync("/api/files/pick",
                    new StringContent(body, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                Assert.IsTrue(doc.RootElement.TryGetProperty("path", out var p), "响应应含 path");
                Assert.AreEqual(@"C:\scripts\run.bat", p.GetString());
            }
            finally
            {
                WebUiEndpoints.FilePicker = null;
            }
        }

        [TestMethod]
        public async Task PickFile_StubCancelled_Returns404()
        {
            WebUiEndpoints.FilePicker = (_, _, _) => null; // 用户取消：SelectFileHelper 返回 null → 404
            try
            {
                var resp = await PostPickAsync(null);
                Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
            }
            finally
            {
                WebUiEndpoints.FilePicker = null;
            }
        }

        [TestMethod]
        public async Task PickFile_NoBody_StillInvokesPicker()
        {
            // body 整体可省（filter/title/path 均仅用于对话框参数）：无 body 时不 400，弹窗照常（此处 stub 直通）
            WebUiEndpoints.FilePicker = (filter, title, initialDir) =>
            {
                Assert.IsNull(filter, "无 body.filter 时端点归一为 null（生产实现内回退 exe|*.exe）");
                Assert.IsNull(title, "无 body.title 时应归一为 null");
                Assert.IsNull(initialDir, "无 body.path 时 initialDirectory 应为 null");
                return @"C:\apps\kitty.exe";
            };
            try
            {
                var resp = await _client.PostAsync("/api/files/pick",
                    new StringContent(string.Empty, Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            }
            finally
            {
                WebUiEndpoints.FilePicker = null;
            }
        }

        private static async Task<HttpResponseMessage> PostPickAsync(string? path)
        {
            var body = path == null ? "{}" : JsonSerializer.Serialize(new { path });
            return await _client.PostAsync("/api/files/pick",
                new StringContent(body, Encoding.UTF8, "application/json"));
        }

        private static async Task<HttpResponseMessage> PostExtractAsync(string path)
        {
            var body = JsonSerializer.Serialize(new { path });
            return await _client.PostAsync("/api/icons/extract-from-exe",
                new StringContent(body, Encoding.UTF8, "application/json"));
        }
    }
}
