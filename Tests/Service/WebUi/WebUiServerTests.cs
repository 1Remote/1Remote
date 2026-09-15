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
    }
}
