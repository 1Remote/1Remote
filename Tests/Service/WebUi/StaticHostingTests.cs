using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// 静态托管回归测试：复刻 WebUiServer.Start 的完整管线
    /// （UseDefaultFiles → UseStaticFiles → TokenMiddleware(空 token) → MapAll），
    /// 以临时 ContentRoot + wwwroot 验证：
    /// - GET / → 200 text/html（DefaultFiles 兜到 wwwroot/index.html）
    /// - GET /assets/x.js → 200（StaticFiles 命中）
    /// - GET /api/version → 200 JSON（静态与 API 路由共存，互不抢占）
    /// 不依赖 IoC（仅请求 /api/version，无需 TestInit 种子）。
    /// </summary>
    [TestClass]
    public class StaticHostingTests
    {
        private static HttpClient _client = null!;
        private static string _root = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            // 本类请求 /api/version，端点会触发 WebUiUpdateService.EnsureStarted()——装桩短路
            // 后台更新检查，杜绝测试对 github.com 的真实联网（VersionUpdateTests 同款 DefaultStub；
            // 该类 ClassCleanup 会还原 null，跨类不共享，须各自安装）
            WebUiUpdateService.CheckOverrideForTest = () => Shawn.Utils.VersionHelper.CheckUpdateResult.False();

            _root = Path.Combine(Path.GetTempPath(), "1rm-webui-static-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "wwwroot", "assets"));
            File.WriteAllText(Path.Combine(_root, "wwwroot", "index.html"),
                "<!doctype html><html><head><title>1Remote</title></head><body><div id=app>marker-index</div></body></html>");
            File.WriteAllText(Path.Combine(_root, "wwwroot", "assets", "x.js"), "// marker-asset");

            // 与 WebUiServer.Start 相同的注册顺序（顺序即语义：DefaultFiles 必须先于 StaticFiles）
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _root });
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseMiddleware<TokenMiddleware>(string.Empty); // DEBUG 形态（空 token 放行 /api）
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestClient();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            WebUiUpdateService.CheckOverrideForTest = null; // 还原生产语义（不残留桩给后续测试类）
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
                /* 临时目录清理失败无害（随临时目录生命周期回收） */
            }
        }

        [TestMethod]
        public async Task Root_ReturnsIndexHtml()
        {
            var resp = await _client.GetAsync("/");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            StringAssert.Contains(resp.Content.Headers.ContentType?.ToString() ?? "", "text/html");
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "marker-index");
        }

        [TestMethod]
        public async Task Asset_ReturnsFile()
        {
            var resp = await _client.GetAsync("/assets/x.js");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "marker-asset");
        }

        [TestMethod]
        public async Task ApiVersion_StillJson_AlongsideStaticFiles()
        {
            var resp = await _client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            Assert.AreEqual("application/json", resp.Content.Headers.ContentType?.MediaType);
            var dto = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                await resp.Content.ReadAsStringAsync())!;
            Assert.IsTrue(dto.ContainsKey("version"));
            Assert.AreEqual((long)1, Convert.ToInt64(dto["api"]));
        }
    }
}
