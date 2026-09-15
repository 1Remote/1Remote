using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    public static class WebUiEndpoints
    {
        /// <summary>
        /// Web UI 侧的服务器过滤判定：跳过分组头 Dummy（树形列表虚拟节点）与
        /// 临时会话（TMP_SESSION_ 前缀或空 Id，即编辑器中尚未落库的对象）。
        /// /api/servers、/api/search、/api/connect 共用同一语义，避免各处过滤条件漂移。
        /// </summary>
        public static bool IsConnectable(ProtocolBase server)
        {
            return server is not Dummy && !server.IsTmpSession();
        }

        public static void MapAll(WebApplication app)
        {
            app.MapGet("/api/version", () => Results.Json(new
            {
                version = _1RM.AppVersion.Version,
                api = 1,
            }));

            // 服务器列表（只读快照）：跳过分组头 Dummy 与临时会话
            app.MapGet("/api/servers", () =>
            {
                var gd = IoC.Get<GlobalData>();
                // 快照语义：ReloadAll 原子交换 VmItemList 引用且换出的旧列表不再被修改，
                // 读侧即使不加锁也安全；此锁仅与 GlobalData 自身的 lock(this)（StopTick/StartTick）串行化。
                // 注意：加载数据的 GetServers 锁的是 DataSourceService/DataSourceBase 实例，不是 GlobalData。
                lock (gd)
                {
                    var list = gd.VmItemList
                        .Where(vm => IsConnectable(vm.Server))
                        .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime))
                        .ToList();
                    return Results.Json(list); // 先物化快照再序列化，锁内不做 IO
                }
            });

            // 数据源列表：本地 + 附加数据源
            app.MapGet("/api/datasources", () =>
            {
                var dss = IoC.Get<DataSourceService>();
                var all = new List<DataSourceBase>();
                if (dss.LocalDataSource != null)
                {
                    all.Add(dss.LocalDataSource);
                }
                all.AddRange(dss.AdditionalSources.Values);
                return Results.Json(all.Select(DtoMapper.FromDataSource).ToList());
            });

            // 标签聚合（含计数与置顶状态）
            app.MapGet("/api/tags", () =>
            {
                var gd = IoC.Get<GlobalData>();
                return Results.Json(DtoMapper.BuildTags(gd));
            });

            // 搜索：与 WPF 主窗口过滤同一套语义（#tag / 空格分隔多关键字 / 拼音，均服务端执行），
            // 空/空白 q 与主窗口空过滤一致 = 不筛选 = 返回全部；前端不得自实现过滤
            app.MapGet("/api/search", (string? q) =>
            {
                var gd = IoC.Get<GlobalData>();
                List<ProtocolBaseViewModel> source;
                lock (gd)
                {
                    // 快照语义同 /api/servers：锁内仅物化列表，
                    // 匹配在锁外执行（MatchKeywords 内部并行，且不触碰 GlobalData 的锁）
                    source = gd.VmItemList
                        .Where(vm => IsConnectable(vm.Server))
                        .ToList();
                }

                var matched = FilterHelpers.MatchServers(source, q);
                return Results.Json(matched
                    .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime))
                    .ToList());
            });

            // 连接：触发与 WPF/托盘/命名管道相同的 OnRequestServerConnect 事件（fromView="WebUi"），
            // 密码交互、会话窗口等仍由桌面端 SessionControlService 管线处理（其内部自行起任务，
            // 故在 Kestrel 线程上同步 Invoke 是安全的）；assign* 参数留空 = 默认行为
            app.MapPost("/api/connect/{id}", (string id) =>
            {
                var gd = IoC.Get<GlobalData>();
                ProtocolBaseViewModel? vm;
                lock (gd) // 快照语义同 /api/servers：锁内只做查找
                {
                    vm = gd.VmItemList.FirstOrDefault(x => x.Server.Id == id && IsConnectable(x.Server));
                }
                if (vm == null) return Results.NotFound();
                GlobalEventHelper.OnRequestServerConnect?.Invoke(vm.Server, fromView: "WebUi");
                return Results.Ok(new { started = true });
            });

            // SSE 数据版本推送：连接期间订阅 GlobalData.OnReloadAll，每次重载推送 event: reload，
            // data 为本连接内重载次数（每连接独立从 0 起计）——前端收到后重新拉取 /api/servers 等即可，
            // 无重载时每 15s 写一行注释心跳保活；断开（RequestAborted）在 finally 中退订。
            // 即时性：用 SemaphoreSlim 唤醒替代固定 Task.Delay 轮询——若每轮睡满 15s，
            // 重载事件最迟要等满一个心跳周期才发出，无法满足前端"秒级自动刷新"的诉求。
            app.MapGet("/api/events", async (HttpContext ctx) =>
            {
                ctx.Response.Headers.ContentType = "text/event-stream";
                ctx.Response.Headers.CacheControl = "no-cache";
                var gd = IoC.Get<GlobalData>();
                var version = 0; // 本连接内已发生的重载次数（OnReload 与写循环分属不同线程，Interlocked 维护）
                var wakeup = new SemaphoreSlim(0, 1);
                void OnReload()
                {
                    Interlocked.Increment(ref version);
                    try
                    {
                        wakeup.Release(); // 唤醒写循环立即推送；信号粘滞，循环未消费期间不重复释放
                    }
                    catch (SemaphoreFullException)
                    {
                        // 上一次唤醒尚未被消费：版本号已合并递增，无需二次唤醒
                    }
                }
                gd.OnReloadAll += OnReload;
                try
                {
                    await ctx.Response.WriteAsync(": connected\n\n", ctx.RequestAborted);
                    var lastSent = 0; // 0 = 连接建立基线：连接前的重载不补发（前端连接后自行全量拉取一次）
                    while (!ctx.RequestAborted.IsCancellationRequested)
                    {
                        // 等待：被唤醒（有重载，立即推送）或 15s 超时（心跳保活）
                        await wakeup.WaitAsync(TimeSpan.FromSeconds(15), ctx.RequestAborted);
                        if (version != lastSent)
                        {
                            lastSent = version;
                            await ctx.Response.WriteAsync($"event: reload\ndata: {version}\n\n", ctx.RequestAborted);
                        }
                        else
                        {
                            await ctx.Response.WriteAsync(": heartbeat\n\n", ctx.RequestAborted);
                        }
                    }
                }
                catch (OperationCanceledException) { /* 客户端断开，正常结束 */ }
                finally
                {
                    gd.OnReloadAll -= OnReload;
                    // 不 Dispose wakeup：退订与并发执行中的 OnReload 之间存在窗口，
                    // Dispose 后到达的 Release 会抛 ObjectDisposedException 并打断 ReloadAll 调用方；
                    // SemaphoreSlim 无非托管资源，交给 GC 即可
                }
            });
        }
    }
}
