using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    [TestClass]
    public class ReadEndpointsTests
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

        [TestMethod]
        public async Task GetServers_ReturnsJsonArray_WithSeedServer()
        {
            var resp = await _client.GetAsync("/api/servers");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "seed-rdp");
            StringAssert.Contains(body, "\"id\"");
        }

        [TestMethod]
        public async Task GetDatasources_ReturnsArrayWithLocal()
        {
            var resp = await _client.GetAsync("/api/datasources");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "Local");
        }

        [TestMethod]
        public async Task GetTags_ReturnsJsonArray()
        {
            var resp = await _client.GetAsync("/api/tags");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }
    }
}
