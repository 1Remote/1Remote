using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Model.Protocol;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// Serial 编辑器建议端点：GET /api/serial/options 提供
    /// SerialPort/BitRate 可输入下拉的数据源，对齐 WPF SerialFormView 的
    /// AutoCompleteComboBox（端口 = 后端机器 GetPortNames()，波特率 = Serial.cs
    /// BitRates 常量表）。端点只 new Serial() 直读属性，不依赖 IoC/数据库。
    /// </summary>
    [TestClass]
    public class SerialOptionsTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init(); // 与其他端点测试同一夹具形态（本端点自身不依赖 IoC）

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        [TestMethod]
        public async Task GetSerialOptions_ReturnsPortsArrayAndBaudRateTable()
        {
            var resp = await _client.GetAsync("/api/serial/options");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            // ports：后端机器实际 COM 口（GetPortNames），内容机器相关——只断言形状
            // （string 数组），不断言非空（无串口的机器上合法为空）
            Assert.IsTrue(root.TryGetProperty("ports", out var ports), "响应应含 ports 数组");
            Assert.AreEqual(JsonValueKind.Array, ports.ValueKind);
            foreach (var port in ports.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.String, port.ValueKind, "每个端口名应为字符串");
            }

            // baudRates：Serial.cs BitRates 常量表（1200..256000 共 12 档），确定性断言
            Assert.IsTrue(root.TryGetProperty("baudRates", out var rates), "响应应含 baudRates 数组");
            var list = rates.EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.AreEqual(12, list.Count, "波特率表应与 Serial.cs BitRates 一致（12 档）");
            Assert.IsTrue(list.Contains("9600"), "波特率表应含默认档 9600");
            Assert.IsTrue(list.Contains("115200"), "波特率表应含常用档 115200");
        }

        [TestMethod]
        public void SerialDataSource_MatchesWpfAutoCompleteSources()
        {
            // 端点与 WPF SerialFormView 同源（Serial.cs 属性直读）——常量表形状回归：
            // 12 档字符串且全部可 long.Parse（WPF IDataErrorInfo 对 BitRate 的数值校验语义，
            // Serial.cs:187-193）；SerialPorts 为 List&lt;string&gt;（机器相关，只断言类型/非 null）
            var serial = new Serial();
            Assert.AreEqual(12, serial.BitRates.Length);
            Assert.IsTrue(serial.BitRates.Contains("9600"));
            foreach (var rate in serial.BitRates)
            {
                Assert.IsTrue(long.TryParse(rate, out _), $"波特率档 '{rate}' 应为数字字符串");
            }
            Assert.IsInstanceOfType(serial.SerialPorts, typeof(List<string>));
        }
    }
}
