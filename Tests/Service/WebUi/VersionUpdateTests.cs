using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shawn.Utils;
using Tests;
using _1RM.Service;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// WebUiUpdateService + GET /api/version 扩展（fix batch6 Task D #12）测试：
    /// ─ GetStatus 形状（available/newVersion/newVersionUrl/breaking/checking）；
    /// ─ DoNotCheckNewVersion 抑制语义（available 恒 false，即使缓存里有新版本）；
    /// ─ 缓存为事件式语义（未发现新版本的检查不清除已缓存的新版本——WPF OnNewVersionRelease 同款）；
    /// ─ EnsureStarted 幂等（重复调用只触发一次检查）且尊重 DoNotCheckNewVersion；
    /// ─ /api/version 响应形状（version/api/buildDate/update 域）。
    /// 网络隔离（绝不真联网）：VersionHelper.CheckUpdate 会真访问 UpdateCheckUrls——全部
    /// 检查路径经注入桩（checkOverride 参数 / CheckOverrideForTest 静态桩，同
    /// WebUiSettingsService.VerifyAccessAsync 的 verifier 模式）短路；ClassInitialize 即装桩，
    /// 端点测试经 TestServer 命中 MapVersion → EnsureStarted → 后台 Task 同样走桩。
    /// </summary>
    [TestClass]
    public class VersionUpdateTests
    {
        private static HttpClient _client = null!;
        private static ConfigurationService _cs = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();
            _cs = _1RM.IoC.Get<ConfigurationService>();

            // 默认桩（无新版本）：任何经端点触发的后台检查都绝不真联网；个别用例按需覆写、
            // 用例结束后还原回默认桩（绝不留 null——null = 真实网络检查）
            WebUiUpdateService.CheckOverrideForTest = DefaultStub;

            // 端点测试用的 TestServer（与 AuxEndpointsTests 同款最小管线）
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestClient();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            WebUiUpdateService.ResetForTest();
            WebUiUpdateService.CheckOverrideForTest = null; // 还原生产语义（真实检查；本类内所有桩均已复位）
        }

        [TestInitialize]
        public void ResetServiceState()
        {
            // 静态服务状态在用例间累积：每个用例从初始态出发，互不依赖执行顺序
            WebUiUpdateService.ResetForTest();
        }

        private static VersionHelper.CheckUpdateResult DefaultStub() => VersionHelper.CheckUpdateResult.False();

        private static VersionHelper.CheckUpdateResult Newer(bool breaking = false) =>
            new(true, "9.9.9", "https://example.com/1remote-9.9.9", breaking);

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000)
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }
            return condition();
        }

        [TestMethod]
        public void GetStatus_Initial_IsUnavailableAndIdle()
        {
            var s = WebUiUpdateService.GetStatus();
            Assert.IsFalse(s.Available, "初始态应无新版本");
            Assert.IsFalse(s.Checking, "初始态应无进行中的检查");
            Assert.AreEqual("", s.NewVersion);
            Assert.AreEqual("", s.NewVersionUrl);
            Assert.IsFalse(s.Breaking);
        }

        [TestMethod]
        public void ExecuteCheck_NewerResult_CachedAndReported()
        {
            WebUiUpdateService.ExecuteCheck(() => Newer(breaking: true));
            var s = WebUiUpdateService.GetStatus();
            Assert.IsTrue(s.Available, "发现新版本后 available 应为 true");
            Assert.AreEqual("9.9.9", s.NewVersion);
            Assert.AreEqual("https://example.com/1remote-9.9.9", s.NewVersionUrl);
            Assert.IsTrue(s.Breaking, "破坏性更新标记应透传（WPF '!' 语义）");
            Assert.IsFalse(s.Checking, "同步 ExecuteCheck 完成后 checking 应为 false");
        }

        [TestMethod]
        public void ExecuteCheck_NoNewer_DoesNotClearCachedNewer()
        {
            // 事件式缓存语义（对齐 WPF OnNewVersionRelease）：未发现新版本的检查不清除
            // 已缓存的新版本——避免站点瞬时不可达令红点闪烁消失
            WebUiUpdateService.ExecuteCheck(() => Newer());
            WebUiUpdateService.ExecuteCheck(() => VersionHelper.CheckUpdateResult.False());
            var s = WebUiUpdateService.GetStatus();
            Assert.IsTrue(s.Available, "未发现新版本的检查不应清除已缓存的新版本");
            Assert.AreEqual("9.9.9", s.NewVersion);
        }

        [TestMethod]
        public void GetStatus_DoNotCheckNewVersion_AlwaysUnavailable()
        {
            WebUiUpdateService.ExecuteCheck(() => Newer());
            Assert.IsTrue(WebUiUpdateService.GetStatus().Available, "前置：禁用前缓存里已有新版本");

            var prev = _cs.General.DoNotCheckNewVersion;
            _cs.General.DoNotCheckNewVersion = true;
            try
            {
                var s = WebUiUpdateService.GetStatus();
                Assert.IsFalse(s.Available, "DoNotCheckNewVersion 时 available 必须恒 false");
                Assert.AreEqual("", s.NewVersion, "禁用时 newVersion 应清空");
                Assert.AreEqual("", s.NewVersionUrl);
                Assert.IsFalse(s.Breaking);
                Assert.IsFalse(s.Checking);
            }
            finally
            {
                _cs.General.DoNotCheckNewVersion = prev; // 共享单例配置，恢复以免影响其它测试类
            }
        }

        [TestMethod]
        public void EnsureStarted_Disabled_DoesNotStartCheck()
        {
            var fired = 0;
            WebUiUpdateService.CheckOverrideForTest = () => { Interlocked.Increment(ref fired); return Newer(); };

            var prev = _cs.General.DoNotCheckNewVersion;
            _cs.General.DoNotCheckNewVersion = true;
            try
            {
                WebUiUpdateService.EnsureStarted();
                Thread.Sleep(200); // 若误启动，后台 Task 会在此窗口内命中桩
                Assert.AreEqual(0, fired, "禁用检查时 EnsureStarted 不应触发任何检查（WPF 早退语义）");
                Assert.IsFalse(WebUiUpdateService.GetStatus().Available);

                // 用户后续启用：下一次调用（/api/version 每次命中都会调）仍可启动
                _cs.General.DoNotCheckNewVersion = false;
                WebUiUpdateService.EnsureStarted();
                Assert.IsTrue(WaitUntil(() => fired >= 1), "启用后 EnsureStarted 应触发首次检查");
                Assert.IsTrue(WaitUntil(() => !WebUiUpdateService.GetStatus().Checking), "检查应完成");
                Assert.IsTrue(WebUiUpdateService.GetStatus().Available, "桩结果应进入缓存");
            }
            finally
            {
                _cs.General.DoNotCheckNewVersion = prev;
                WebUiUpdateService.CheckOverrideForTest = DefaultStub;
            }
        }

        [TestMethod]
        public void EnsureStarted_IsIdempotent_SingleCheck()
        {
            var fired = 0;
            WebUiUpdateService.CheckOverrideForTest = () => { Interlocked.Increment(ref fired); return VersionHelper.CheckUpdateResult.False(); };
            WebUiUpdateService.EnsureStarted();
            WebUiUpdateService.EnsureStarted();
            WebUiUpdateService.EnsureStarted();
            Assert.IsTrue(WaitUntil(() => fired >= 1), "首次调用应触发检查");
            Assert.IsTrue(WaitUntil(() => !WebUiUpdateService.GetStatus().Checking), "检查应完成");
            Thread.Sleep(200); // 若幂等性破坏，多余的启动会在此窗口内再次命中桩
            Assert.AreEqual(1, fired, "重复调用 EnsureStarted 绝不能重复触发网络检查（/api/version 高频命中）");
            WebUiUpdateService.CheckOverrideForTest = DefaultStub;
        }

        [TestMethod]
        public async Task VersionEndpoint_ResponseShape()
        {
            WebUiUpdateService.CheckOverrideForTest = DefaultStub;
            var resp = await _client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.String, root.GetProperty("version").ValueKind, "version 应为字符串");
            Assert.AreEqual(_1RM.AppVersion.Version, root.GetProperty("version").GetString());
            Assert.AreEqual(1, root.GetProperty("api").GetInt32(), "api 版本号保持 1（只增字段不破坏兼容）");
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("buildDate").ValueKind, "buildDate 应为字符串（AppVersion.BuildDate 原文，可能为空串）");
            Assert.AreEqual(_1RM.AppVersion.BuildDate, root.GetProperty("buildDate").GetString());

            var update = root.GetProperty("update");
            Assert.AreEqual(JsonValueKind.Object, update.ValueKind, "响应应含 update 对象");
            AssertIsBoolean(update.GetProperty("available"), "available");
            Assert.IsFalse(update.GetProperty("available").GetBoolean(), "桩返回无新版本 → available=false");
            AssertIsBoolean(update.GetProperty("checking"), "checking");
            Assert.AreEqual(JsonValueKind.String, update.GetProperty("newVersion").ValueKind);
            Assert.AreEqual("", update.GetProperty("newVersion").GetString());
            Assert.AreEqual(JsonValueKind.String, update.GetProperty("newVersionUrl").ValueKind);
            Assert.AreEqual("", update.GetProperty("newVersionUrl").GetString());
            AssertIsBoolean(update.GetProperty("breaking"), "breaking");
            Assert.IsFalse(update.GetProperty("breaking").GetBoolean());
            WebUiUpdateService.CheckOverrideForTest = DefaultStub;
        }

        /// <summary>布尔形状断言：JsonValueKind.True/False 均为合法布尔（值语义由 GetBoolean 单独断言）。</summary>
        private static void AssertIsBoolean(JsonElement e, string name)
        {
            Assert.IsTrue(e.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{name} 应为布尔（实际 {e.ValueKind}）");
        }

        [TestMethod]
        public async Task VersionEndpoint_NewerAvailable_ExposedInUpdate()
        {
            // 端点 → EnsureStarted → 后台检查（桩）→ 缓存；fired≥1 且 checking 结束后再断言 update 域透传
            var fired = 0;
            WebUiUpdateService.CheckOverrideForTest = () => { Interlocked.Increment(ref fired); return Newer(breaking: true); };
            var resp = await _client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            Assert.IsTrue(WaitUntil(() => Volatile.Read(ref fired) >= 1 && !WebUiUpdateService.GetStatus().Checking),
                "EnsureStarted 触发的后台检查应完成（fired 兼防 Task 尚未调度导致的 Checking 假阴性）");

            using var doc = JsonDocument.Parse(await (await _client.GetAsync("/api/version")).Content.ReadAsStringAsync());
            var update = doc.RootElement.GetProperty("update");
            Assert.IsTrue(update.GetProperty("available").GetBoolean(), "桩发现新版本 → available=true");
            Assert.AreEqual("9.9.9", update.GetProperty("newVersion").GetString());
            Assert.AreEqual("https://example.com/1remote-9.9.9", update.GetProperty("newVersionUrl").GetString());
            Assert.IsTrue(update.GetProperty("breaking").GetBoolean());
            WebUiUpdateService.CheckOverrideForTest = DefaultStub;
        }
    }
}
