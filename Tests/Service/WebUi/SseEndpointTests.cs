using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Model;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/events SSE 集成测试：TestServer + HttpCompletionOption.ResponseHeadersRead 增量读取。
    /// 端点在写出 ": connected"（即响应头可达）前已完成 OnReloadAll 订阅，
    /// 测试线程同步调用 ReloadAll(true) 会触发处理器并唤醒写循环 → reload 事件应立即到达
    /// （唤醒机制验证：不能等满 15s 心跳周期才推送）。
    /// OnReloadAll 是 public 字段，可用 GetInvocationList().Length 确定性观测订阅/退订。
    /// </summary>
    [TestClass]
    public class SseEndpointTests
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

        /// <summary>带超时的逐行读取：连接挂起时最多等 timeout，避免测试卡死。</summary>
        private static async Task<string?> ReadLineAsync(StreamReader reader, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await reader.ReadLineAsync(cts.Token);
        }

        private static int SubscriberCount(GlobalData gd)
        {
            return gd.OnReloadAll?.GetInvocationList().Length ?? 0;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(25);
            }
        }

        /// <summary>断开并等待服务端退订（RequestAborted → 循环退出 → finally -=）。</summary>
        private static async Task DisconnectAndDrainAsync(HttpResponseMessage resp, CancellationTokenSource cts, GlobalData gd, int baseline)
        {
            cts.Cancel();
            resp.Dispose();
            // 不断言（本方法也用于普通清理）；订阅数是否回落由测试 2 显式断言
            await WaitUntilAsync(() => SubscriberCount(gd) == baseline, TimeSpan.FromSeconds(5));
        }

        [TestMethod]
        public async Task Events_SendsConnectedThenImmediateReloadEvent()
        {
            var gd = _1RM.IoC.Get<GlobalData>();
            var baseline = SubscriberCount(gd);

            using var cts = new CancellationTokenSource();
            var resp = await _client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
            try
            {
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
                StringAssert.Contains(resp.Content.Headers.ContentType?.ToString() ?? "", "text/event-stream");

                var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);

                // 响应头已到达 = 端点已订阅且首行注释已写出
                var first = await ReadLineAsync(reader, TimeSpan.FromSeconds(5));
                Assert.AreEqual(": connected", first);

                // 触发一次重载：ReloadAll(true) 同步 Invoke OnReloadAll（含 SSE 处理器）
                Assert.IsTrue(gd.ReloadAll(true), "强制重载应成功");

                // 唤醒机制：reload 事件应立即到达（≤5s），而非等满 15s 心跳周期
                var sawEvent = false;
                var sawData = false;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
                while (DateTimeOffset.UtcNow < deadline && !(sawEvent && sawData))
                {
                    var line = await ReadLineAsync(reader, TimeSpan.FromSeconds(5));
                    Assert.IsNotNull(line, "读到 reload 事件前连接不应结束");
                    if (line == "event: reload") sawEvent = true;
                    else if (line == "data: 1") sawData = true;
                }
                Assert.IsTrue(sawEvent, "应立即收到 event: reload 行");
                Assert.IsTrue(sawData, "应收到 data: 1 行（本连接内第一次重载）");
            }
            finally
            {
                await DisconnectAndDrainAsync(resp, cts, gd, baseline);
            }
        }

        [TestMethod]
        public async Task Events_UnsubscribesOnDisconnect_NextConnectionStartsFresh()
        {
            var gd = _1RM.IoC.Get<GlobalData>();
            var baseline = SubscriberCount(gd);

            // 连接 A：响应头到达时端点已完成 +=（订阅数 +1）
            HttpResponseMessage respA;
            using (var ctsA = new CancellationTokenSource())
            {
                respA = await _client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, ctsA.Token);
                try
                {
                    Assert.AreEqual(HttpStatusCode.OK, respA.StatusCode);
                    Assert.AreEqual(baseline + 1, SubscriberCount(gd), "连接后 OnReloadAll 订阅数应 +1");
                }
                finally
                {
                    await DisconnectAndDrainAsync(respA, ctsA, gd, baseline);
                }
            }
            Assert.AreEqual(baseline, SubscriberCount(gd), "断开后端点应退订 OnReloadAll");

            // 连接 B：版本计数独立从 0 起算——A 连接期内发生过的重载不影响 B 的 data 序号
            using var ctsB = new CancellationTokenSource();
            var respB = await _client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, ctsB.Token);
            try
            {
                var stream = await respB.Content.ReadAsStreamAsync(ctsB.Token);
                using var reader = new StreamReader(stream);
                var first = await ReadLineAsync(reader, TimeSpan.FromSeconds(5));
                Assert.AreEqual(": connected", first);

                Assert.IsTrue(gd.ReloadAll(true));
                var sawEvent = false;
                var sawData = false;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
                while (DateTimeOffset.UtcNow < deadline && !(sawEvent && sawData))
                {
                    var line = await ReadLineAsync(reader, TimeSpan.FromSeconds(5));
                    Assert.IsNotNull(line, "读到 reload 事件前连接不应结束");
                    if (line == "event: reload") sawEvent = true;
                    else if (line == "data: 1") sawData = true;
                }
                Assert.IsTrue(sawEvent && sawData, "连接 B 应收到自己的 event: reload / data: 1（每连接独立计数）");
            }
            finally
            {
                await DisconnectAndDrainAsync(respB, ctsB, gd, baseline);
            }
        }
    }
}
