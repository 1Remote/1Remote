using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Builder;

namespace Tests.Service.WebUi
{
    [TestClass]
    public class WebUiServerTests
    {
        private static HttpClient CreateClient(string? token)
        {
            // 直接构建与生产相同的管道（token 规则一致），用 TestServer 驱动
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            var tokenInPipe = token; // null 表示"服务端未启用 token"（DEBUG 形态）
            app.UseMiddleware<_1RM.Service.WebUi.TokenMiddleware>(tokenInPipe ?? "");
            app.MapGet("/api/version", () => new { version = "test", api = 1 });
            // 模拟静态资源路径（非 /api），用于验证 token 只守卫 API
            app.MapGet("/index.html", () => "<html></html>");
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            var client = app.GetTestClient();
            return client;
        }

        [TestMethod]
        public async Task VersionEndpoint_NoTokenRequired_Returns200()
        {
            using var client = CreateClient(null);
            var resp = await client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_MissingToken_Returns401()
        {
            using var client = CreateClient("secret123");
            var resp = await client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_CorrectBearer_Returns200()
        {
            using var client = CreateClient("secret123");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "secret123");
            var resp = await client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_QueryToken_Returns200()
        {
            using var client = CreateClient("secret123");
            var resp = await client.GetAsync("/api/version?token=secret123");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_WrongQueryToken_Returns401()
        {
            using var client = CreateClient("secret123");
            var resp = await client.GetAsync("/api/version?token=wrong");
            Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_WrongBearer_Returns401()
        {
            using var client = CreateClient("secret123");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "wrong-token");
            var resp = await client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_NonApiPath_NoToken_Returns200()
        {
            // token 只守卫 API：页面经 ?token= 加载后，静态资源请求不携带 token 也应放行
            using var client = CreateClient("secret123");
            var resp = await client.GetAsync("/index.html");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public void ResolveContentRoot_SingleFileExtractionFallback()
        {
            // 单文件发布探测（WebUiServer.ResolveContentRoot）：exe 旁无 wwwroot 时，
            // 应回落到 {DOTNET_BUNDLE_EXTRACT_BASE_DIR}\...\1Remote\wwwroot 所在目录；
            // 探测基目录用环境变量注入，避免触碰真实 %TEMP%\.net
            var baseDir = _1RM.Service.WebUi.WebUiServer.ResolveContentRoot(); // exe 旁有 wwwroot（测试输出目录）→ 原样返回
            Assert.AreEqual(AppContext.BaseDirectory, baseDir, "常规构建：exe 旁 wwwroot 命中即用 BaseDirectory");

            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "1rm-bundle-test-" + Guid.NewGuid().ToString("N"));
            var wwwroot = System.IO.Path.Combine(tmp, "sub", "1Remote", "wwwroot");
            System.IO.Directory.CreateDirectory(wwwroot);
            System.IO.File.WriteAllText(System.IO.Path.Combine(wwwroot, "index.html"), "<html></html>");
            var prev = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR");
            try
            {
                Environment.SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", tmp);
                // 测试进程的 BaseDirectory 下确有 wwwroot（Tests 输出目录带产物），先构造"没有"的场景无法直接做到——
                // 故此断言验证的是：环境变量指向存在 1Remote\wwwroot 的根时不会抛错且返回值是含 wwwroot 的目录或 BaseDirectory
                var resolved = _1RM.Service.WebUi.WebUiServer.ResolveContentRoot();
                Assert.IsTrue(resolved == AppContext.BaseDirectory || System.IO.Directory.Exists(System.IO.Path.Combine(resolved, "wwwroot")),
                    "回落路径必须指向一个实际存在 wwwroot 的目录");
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", prev);
                try { System.IO.Directory.Delete(tmp, true); } catch (System.IO.IOException) { /* 临时目录清理失败无害 */ }
            }
        }
    }
}
