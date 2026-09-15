using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Tests;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/settings/appearance 与 /api/ui-state/tree 集成测试。
    /// appearance 持久化到 1Remote.json（Configuration 新增 WebUi* 字段，独立于 WPF ThemeConfig，spec §4）；
    /// tree 状态代理静态类 LocalityTreeViewService，落盘 .locality/.tree_view.json（测试 cwd 下生成，可接受）。
    /// 各测试写完即还原默认值/清空，保证与其它测试类及执行顺序无关。
    /// </summary>
    [TestClass]
    public class AppearanceEndpointTests
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

        private static async Task<(HttpStatusCode Code, string Body)> PutAsync(string uri, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _client.PutAsync(uri, content);
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }

        /// <summary>直接经服务还原外观默认值（不依赖 HTTP），保证任何失败路径下也能清理。</summary>
        private static void ResetAppearanceToDefaults()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            cs.WebUiThemeMode = "dark";
            cs.WebUiAccent = "blue";
            cs.WebUiFontSize = "M";
            cs.Save();
        }

        [TestMethod]
        public async Task GetAppearance_Defaults_ReturnsDarkBlueM()
        {
            ResetAppearanceToDefaults(); // 防御其它测试类/执行顺序污染
            var resp = await _client.GetAsync("/api/settings/appearance");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            var dto = JsonConvert.DeserializeObject<AppearanceDto>(body)!;
            Assert.AreEqual("dark", dto.ThemeMode);
            Assert.AreEqual("blue", dto.Accent);
            Assert.AreEqual("M", dto.FontSize);
        }

        [TestMethod]
        public async Task PutAppearance_Valid_Returns200UpdatesGetAndPersistsToFile()
        {
            try
            {
                var (code, body) = await PutAsync("/api/settings/appearance",
                    "{\"themeMode\":\"light\",\"accent\":\"violet\",\"fontSize\":\"XL\"}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                var stored = JsonConvert.DeserializeObject<AppearanceDto>(body)!;
                Assert.AreEqual("light", stored.ThemeMode);
                Assert.AreEqual("violet", stored.Accent);
                Assert.AreEqual("XL", stored.FontSize);

                // GET 返回更新后的值
                var after = JsonConvert.DeserializeObject<AppearanceDto>(
                    await _client.GetStringAsync("/api/settings/appearance"))!;
                Assert.AreEqual("light", after.ThemeMode);
                Assert.AreEqual("violet", after.Accent);
                Assert.AreEqual("XL", after.FontSize);

                // 落盘验证：ProfileJsonPath（测试 cwd 下 1Remote.json）应包含新值，
                // 且反序列化为 Configuration 后字段一致（证明走的是既有保存路径）
                var path = AppPathHelper.Instance.ProfileJsonPath;
                Assert.IsTrue(File.Exists(path), $"配置文件应已生成: {path}");
                var raw = File.ReadAllText(path);
                StringAssert.Contains(raw, "violet");
                var cfg = JsonConvert.DeserializeObject<Configuration>(raw);
                Assert.IsNotNull(cfg);
                Assert.AreEqual("light", cfg!.WebUiThemeMode);
                Assert.AreEqual("violet", cfg.WebUiAccent);
                Assert.AreEqual("XL", cfg.WebUiFontSize);
            }
            finally
            {
                ResetAppearanceToDefaults();
            }
        }

        [TestMethod]
        public async Task PutAppearance_InvalidValues_Return400AndKeepValues()
        {
            try
            {
                // 先写入一组已知合法值
                var (ok, _) = await PutAsync("/api/settings/appearance",
                    "{\"themeMode\":\"light\",\"accent\":\"green\",\"fontSize\":\"L\"}");
                Assert.AreEqual(HttpStatusCode.OK, ok);

                // 非法 themeMode
                var (bad1, body1) = await PutAsync("/api/settings/appearance",
                    "{\"themeMode\":\"neon\",\"accent\":\"green\",\"fontSize\":\"L\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, bad1, body1);

                // 非法 accent
                var (bad2, body2) = await PutAsync("/api/settings/appearance",
                    "{\"themeMode\":\"light\",\"accent\":\"magenta\",\"fontSize\":\"L\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, bad2, body2);

                // 非法 fontSize
                var (bad3, body3) = await PutAsync("/api/settings/appearance",
                    "{\"themeMode\":\"light\",\"accent\":\"green\",\"fontSize\":\"XXL\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, bad3, body3);

                // 值保持不变
                var after = JsonConvert.DeserializeObject<AppearanceDto>(
                    await _client.GetStringAsync("/api/settings/appearance"))!;
                Assert.AreEqual("light", after.ThemeMode);
                Assert.AreEqual("green", after.Accent);
                Assert.AreEqual("L", after.FontSize);
            }
            finally
            {
                ResetAppearanceToDefaults();
            }
        }

        [TestMethod]
        public async Task PutAppearance_CaseInsensitive_NormalizesToCanonicalCase()
        {
            try
            {
                // 大小写宽容 + 归一：ThemeMode/accent 归一小写，fontSize 归一大写
                var (code, body) = await PutAsync("/api/settings/appearance",
                    "{\"themeMode\":\"System\",\"accent\":\"GREEN\",\"fontSize\":\"s\"}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                var stored = JsonConvert.DeserializeObject<AppearanceDto>(body)!;
                Assert.AreEqual("system", stored.ThemeMode);
                Assert.AreEqual("green", stored.Accent);
                Assert.AreEqual("S", stored.FontSize);
            }
            finally
            {
                ResetAppearanceToDefaults();
            }
        }

        [TestMethod]
        public async Task TreeState_PutThenGet_RoundTrips()
        {
            try
            {
                var payload = "{\"expanded\":{\"LocalDataSource->Folder1\":true,\"LocalDataSource->Folder1->Sub\":false},"
                              + "\"order\":{\"srv-1\":1,\"srv-2\":2}}";
                var (code, body) = await PutAsync("/api/ui-state/tree", payload);
                Assert.AreEqual(HttpStatusCode.OK, code, body);

                var stored = JsonConvert.DeserializeObject<TreeStateDto>(body)!;
                Assert.AreEqual(2, stored.Expanded.Count);
                Assert.IsTrue(stored.Expanded["LocalDataSource->Folder1"]);
                Assert.IsFalse(stored.Expanded["LocalDataSource->Folder1->Sub"]);
                Assert.AreEqual(2, stored.Order.Count);
                Assert.AreEqual(1, stored.Order["srv-1"]);
                Assert.AreEqual(2, stored.Order["srv-2"]);

                // GET 返回相同状态
                var got = JsonConvert.DeserializeObject<TreeStateDto>(
                    await _client.GetStringAsync("/api/ui-state/tree"))!;
                Assert.AreEqual(2, got.Expanded.Count);
                Assert.IsTrue(got.Expanded["LocalDataSource->Folder1"]);
                Assert.IsFalse(got.Expanded["LocalDataSource->Folder1->Sub"]);
                Assert.AreEqual(2, got.Order.Count);
                Assert.AreEqual(1, got.Order["srv-1"]);
                Assert.AreEqual(2, got.Order["srv-2"]);

                // 落盘验证：直接反序列化 .locality/.tree_view.json（服务自有存储）
                Assert.IsTrue(File.Exists(LocalityTreeViewService.JsonPath),
                    $"树状态文件应已生成: {LocalityTreeViewService.JsonPath}");
                var settings = JsonConvert.DeserializeObject<LocalityTreeViewSettings>(
                    File.ReadAllText(LocalityTreeViewService.JsonPath))!;
                Assert.IsNotNull(settings);
                Assert.AreEqual(2, settings!.TreeNodeExpansionStates.Count);
                Assert.IsTrue(settings.TreeNodeExpansionStates["LocalDataSource->Folder1"]);
                Assert.IsFalse(settings.TreeNodeExpansionStates["LocalDataSource->Folder1->Sub"]);
                Assert.AreEqual(2, settings.CustomNodeOrder.Count);
                Assert.AreEqual(1, settings.CustomNodeOrder["srv-1"]);
            }
            finally
            {
                // 还原空状态，避免污染静态缓存与其它测试类
                var s = LocalityTreeViewService.Settings;
                s.TreeNodeExpansionStates = new Dictionary<string, bool>();
                s.CustomNodeOrder = new Dictionary<string, int>();
                LocalityTreeViewService.Save();
            }
        }

        [TestMethod]
        public async Task TreeState_OrderValueNotInteger_Returns400()
        {
            // 类型不匹配由最小 API 模型绑定拒绝（400），不会写入半更新状态
            var (code, _) = await PutAsync("/api/ui-state/tree",
                "{\"expanded\":{},\"order\":{\"srv-1\":\"not-a-number\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, code);
        }
    }
}
