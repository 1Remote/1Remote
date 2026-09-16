using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Service.DataSource;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/search 集成测试：过滤语义必须与 WPF 主窗口一致
    /// （#tag / 空格分隔多关键字 / 拼音均由服务端执行，见 spec §3.1）。
    /// 复用 TestInit 的 IoC 实例库与基础种子 seed-rdp（tag=seed-tag）。
    /// </summary>
    [TestClass]
    public class SearchEndpointTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();

            // 本类独有种子（无标签）：与 seed-rdp 区分，用于过滤断言
            var gd = _1RM.IoC.Get<GlobalData>();
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local, "TestInit 后本地数据源必须可用");
            gd.AddServer(new RDP
            {
                Id = "search-ssh-1",
                DisplayName = "search-ssh-1",
                Address = "2.2.2.2",
                Tags = new List<string>(),
            }, local);
            gd.AddServer(new RDP
            {
                Id = "search-rdp-2",
                DisplayName = "search-rdp-2",
                Address = "3.3.3.3",
                Tags = new List<string>(),
            }, local);
            gd.AddServer(new RDP
            {
                Id = "search-cn-1",
                DisplayName = "生产服务器", // 拼音断言用：shengchanfuwuqi / 首字母 scfwq
                Address = "4.4.4.4",
                Tags = new List<string>(),
            }, local);
            // 地址匹配断言用：名称不含任何数字，地址含 192.168 —— 名称与地址命中可区分
            gd.AddServer(new RDP
            {
                Id = "search-addr-1",
                DisplayName = "addr-probe",
                Address = "192.168.1.5",
                Tags = new List<string>(),
            }, local);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        [TestMethod]
        public async Task Search_ByTagKeyword_ReturnsOnlyTaggedServer()
        {
            // '#' 需 URL 编码为 %23；seed-rdp 带 seed-tag 标签，本类种子均无标签
            var resp = await _client.GetAsync("/api/search?q=%23seed-tag");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "seed-rdp");
            Assert.IsFalse(body.Contains("search-ssh-1"), "无该标签的服务器不应命中");
            Assert.IsFalse(body.Contains("search-rdp-2"), "无该标签的服务器不应命中");
        }

        [TestMethod]
        public async Task Search_ByMultipleKeywords_AllKeywordsMustMatch()
        {
            // 与 WPF 主过滤一致：空格分隔的多关键字须全部命中（KeywordMatchService），
            // search-ssh-1 同时含 "search" 与 "ssh"，其余种子只含 "search" 或都不含
            var resp = await _client.GetAsync("/api/search?q=search%20ssh");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "search-ssh-1");
            Assert.IsFalse(body.Contains("search-rdp-2"), "只含 'search' 不含 'ssh' 的服务器不应命中");
            Assert.IsFalse(body.Contains("seed-rdp"), "不含任何关键字的服务器不应命中");
        }

        [TestMethod]
        public async Task Search_ByPinyin_MatchesChineseName()
        {
            // 首字母：sc → 生产服务器(shengchanfuwuqi / scfwq)；
            // fixture 固定启用 Pinyin/PinyinInitials provider，断言跨 locale 确定
            var resp = await _client.GetAsync("/api/search?q=sc");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "search-cn-1");
            Assert.IsFalse(body.Contains("search-ssh-1"), "非中文服务器不应命中首字母查询");
            Assert.IsFalse(body.Contains("seed-rdp"), "非中文服务器不应命中首字母查询");

            // 全拼：shengchan → 生产服务器
            var resp2 = await _client.GetAsync("/api/search?q=shengchan");
            Assert.AreEqual(HttpStatusCode.OK, resp2.StatusCode);
            var body2 = await resp2.Content.ReadAsStringAsync();
            StringAssert.Contains(body2, "search-cn-1");
            Assert.IsFalse(body2.Contains("seed-rdp"), "非中文服务器不应命中拼音查询");
        }

        [TestMethod]
        public async Task Search_ByAddress_MatchesServerWhoseAddressContainsKeyword()
        {
            // WPF 主窗口过滤一致：关键字同时匹配 DisplayName 与 SubTitle(=Address:Port)。
            // search-addr-1 名称 "addr-probe" 不含 "192"，地址 "192.168.1.5" 含 —— 仅地址命中也须返回
            var resp = await _client.GetAsync("/api/search?q=192");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "search-addr-1");
            Assert.IsFalse(body.Contains("search-ssh-1"), "地址不含关键字且名称不含关键字的服务器不应命中");
            Assert.IsFalse(body.Contains("seed-rdp"), "地址不含关键字且名称不含关键字的服务器不应命中");
        }

        [TestMethod]
        public async Task Search_NameAndAddressTokensCombined_AllKeywordsMustMatch()
        {
            // 多关键字（空格分隔）可分别命中名称与地址：name 含 "addr"、地址含 "168"
            var resp = await _client.GetAsync("/api/search?q=addr%20168");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "search-addr-1");
            Assert.IsFalse(body.Contains("search-ssh-1"), "两个关键字都须命中（名称或地址任一）");
            Assert.IsFalse(body.Contains("seed-rdp"), "两个关键字都须命中（名称或地址任一）");
        }

        [TestMethod]
        public async Task Search_NoMatch_ReturnsEmptyArray()
        {
            var resp = await _client.GetAsync("/api/search?q=zzz-no-match-xyz");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.AreEqual("[]", body.Trim());
        }

        [TestMethod]
        public async Task Search_EmptyOrWhitespaceQuery_ReturnsAll()
        {
            // 与 WPF 主过滤一致：空过滤 = 不筛选 = 全部可见
            var resp = await _client.GetAsync("/api/search");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "seed-rdp");
            StringAssert.Contains(body, "search-ssh-1");
            StringAssert.Contains(body, "search-rdp-2");

            // 纯空白同样视为空
            var resp2 = await _client.GetAsync("/api/search?q=%20");
            Assert.AreEqual(HttpStatusCode.OK, resp2.StatusCode);
            var body2 = await resp2.Content.ReadAsStringAsync();
            StringAssert.Contains(body2, "seed-rdp");
            StringAssert.Contains(body2, "search-ssh-1");
            StringAssert.Contains(body2, "search-rdp-2");
        }
    }
}
