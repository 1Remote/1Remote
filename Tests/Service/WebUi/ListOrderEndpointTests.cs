using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Tests;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.Locality;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/ui-state/list-order 集成测试（列表行拖拽排序的读写端点）。
    /// POST 全量替换 LocalityListViewService.ServerCustomOrder（WPF ServerCustomOrderSave 同款：
    /// 清空重填 + 同步 vm.CustomOrder + 落盘 .locality/.list_view.json）；GET 返回按序升序的 id 列表。
    /// 未知 id 跳过不整体失败。测试写完即还原空顺序，保证与其它测试类及执行顺序无关。
    /// </summary>
    [TestClass]
    public class ListOrderEndpointTests
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

        private static async Task<(HttpStatusCode Code, string Body)> PostAsync(string uri, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync(uri, content);
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }

        /// <summary>直接经服务还原空顺序（不依赖 HTTP），保证任何失败路径下也能清理。</summary>
        private static void ResetOrderToEmpty()
        {
            LocalityListViewService.ServerCustomOrderSave(Enumerable.Empty<_1RM.View.ProtocolBaseViewModel>());
        }

        [TestMethod]
        public async Task PostThenGet_RoundTripsOrderedIdsAndPersistsToFile()
        {
            try
            {
                // 种子服务器 seed-rdp 存在；另两台临时插入（每类唯一 id 前缀），POST 后删除
                var gd = _1RM.IoC.Get<_1RM.Model.GlobalData>();
                var ds = _1RM.IoC.Get<_1RM.Service.DataSource.DataSourceService>();
                gd.AddServer(new RDP { Id = "listorder-a", DisplayName = "a", Address = "1.1.1.1" }, ds.LocalDataSource!);
                gd.AddServer(new RDP { Id = "listorder-b", DisplayName = "b", Address = "2.2.2.2" }, ds.LocalDataSource!);
                try
                {
                    var (code, body) = await PostAsync("/api/ui-state/list-order",
                        "{\"ids\":[\"listorder-b\",\"seed-rdp\",\"listorder-a\",\"listorder-b\"]}");
                    Assert.AreEqual(HttpStatusCode.OK, code, body);

                    // 响应 = 实际保存顺序（重复 id 已去重、顺序保留首次出现位）
                    var saved = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(body)!;
                    CollectionAssert.AreEqual(
                        new List<string> { "listorder-b", "seed-rdp", "listorder-a" }, saved["ids"]);

                    // GET 按 value 升序还原
                    var gotRaw = await _client.GetStringAsync("/api/ui-state/list-order");
                    var gotIds = JsonConvert.DeserializeAnonymousType(gotRaw, new { ids = new List<string>() })!;
                    CollectionAssert.AreEqual(
                        new List<string> { "listorder-b", "seed-rdp", "listorder-a" }, gotIds.ids);

                    // 落盘验证：.locality/.list_view.json 的 ServerCustomOrder 已全量替换
                    Assert.IsTrue(File.Exists(LocalityListViewService.JsonPath),
                        $"列表状态文件应已生成: {LocalityListViewService.JsonPath}");
                    var settings = JsonConvert.DeserializeObject<LocalityListViewSettings>(
                        File.ReadAllText(LocalityListViewService.JsonPath))!;
                    Assert.AreEqual(3, settings.ServerCustomOrder.Count);
                    Assert.AreEqual(0, settings.ServerCustomOrder["listorder-b"]);
                    Assert.AreEqual(1, settings.ServerCustomOrder["seed-rdp"]);
                    Assert.AreEqual(2, settings.ServerCustomOrder["listorder-a"]);
                }
                finally
                {
                    gd.DeleteServer(new ProtocolBase[] {
                        gd.GetItemById(ds.LocalDataSource!.DataSourceName, "listorder-a")!.Server,
                        gd.GetItemById(ds.LocalDataSource.DataSourceName, "listorder-b")!.Server });
                }
            }
            finally
            {
                ResetOrderToEmpty();
            }
        }

        [TestMethod]
        public async Task Post_SkipsUnknownIdsWithoutFailing()
        {
            try
            {
                var (code, body) = await PostAsync("/api/ui-state/list-order",
                    "{\"ids\":[\"no-such-id\",\"seed-rdp\"]}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                var saved = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(body)!;
                CollectionAssert.AreEqual(new List<string> { "seed-rdp" }, saved["ids"]);
            }
            finally
            {
                ResetOrderToEmpty();
            }
        }

        [TestMethod]
        public async Task Post_EmptyOrNullIds_Returns400()
        {
            var (code, _) = await PostAsync("/api/ui-state/list-order", "{\"ids\":[]}");
            Assert.AreEqual(HttpStatusCode.BadRequest, code);
            var (code2, _) = await PostAsync("/api/ui-state/list-order", "{}");
            Assert.AreEqual(HttpStatusCode.BadRequest, code2);
        }
    }
}
