using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shawn.Utils.Wpf.Image;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// WebUiEndpoints 分域：服务器域。
    /// ─ GET    /api/servers             列表（只读快照，含连接状态）
    /// ─ GET    /api/search              搜索（与 WPF 主窗口过滤同一语义）
    /// ─ POST   /api/connect/{id}        触发连接（桌面端会话管线）
    /// ─ GET    /api/servers/{id}/config 可编辑配置（克隆+解密全字段明文）
    /// ─ POST   /api/servers             新建
    /// ─ PUT    /api/servers/{id}        更新（整体替换）
    /// ─ DELETE /api/servers/{id}        删除
    /// ─ POST   /api/servers/batch       批量补丁编辑
    /// ─ POST   /api/servers/batch/peek  批量回读非敏感字段（共享值计算，只读）
    /// ─ POST   /api/servers/import      导入（json/csv/rdp/db）
    /// ─ GET    /api/servers/export      导出（解密 JSON 下载）
    /// ─ GET    /api/events              SSE 数据版本推送（服务器/标签重载通知）
    /// 注册顺序由主文件 MapAll 统一编排（与拆分前一致）。
    /// </summary>
    public static partial class WebUiEndpoints
    {
        /// <summary>服务器列表（只读快照）与搜索、连接的注册方法。各自职责见路由旁注释。</summary>
        internal static void MapServersList(WebApplication app)
        {
            // 服务器列表（只读快照）：跳过分组头 Dummy 与临时会话
            app.MapGet("/api/servers", () =>
            {
                var gd = IoC.Get<GlobalData>();
                // 快照语义：ReloadAll 原子交换 VmItemList 引用且换出的旧列表不再被修改，
                // 读侧即使不加锁也安全；此锁仅与 GlobalData 自身的 lock(this)（StopTick/StartTick）串行化。
                // 注意：加载数据的 GetServers 锁的是 DataSourceService/DataSourceBase 实例，不是 GlobalData。
                lock (gd)
                {
                    // 活动会话 Id 快照：仅并发字典的属性读，无 IO，锁内安全（Plan 4 Task 1）；
                    // connecting 快照 = 连接请求进行中（前置脚本/凭据对话期间），状态点先转琥珀再翻绿
                    var activeIds = BuildActiveServerIdSet();
                    var connectingIds = BuildConnectingServerIdSet();
                    var list = gd.VmItemList
                        .Where(vm => IsConnectable(vm.Server))
                        .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime,
                            DeriveConnectionState(activeIds, vm.Server.Id, connectingIds)))
                        .ToList();
                    return Results.Json(list); // 先物化快照再序列化，锁内不做 IO
                }
            });
        }

        internal static void MapServersSearch(WebApplication app)
        {
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
                // 活动会话 Id 快照在锁外构建（语义同 /api/servers；仅并发字典属性读）
                var activeIds = BuildActiveServerIdSet();
                var connectingIds = BuildConnectingServerIdSet();
                return Results.Json(matched
                    .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime,
                        DeriveConnectionState(activeIds, vm.Server.Id, connectingIds)))
                    .ToList());
            });
        }

        internal static void MapServersConnect(WebApplication app)
        {
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
        }

        internal static void MapServersConfig(WebApplication app)
        {
            // 可编辑配置：克隆+解密后的全字段明文 JSON（与 WPF 编辑器同级暴露，受 token+回环保护）。
            // 两个 casing 域勿互相归一：信封键（id/dataSourceName/protocol/json）camelCase；
            // 内嵌 json 对象的键保持 ToJsonString 的 PascalCase 原样直通——CreateFromJsonString 的
            // jObj.Protocol/jObj.ClassVersion 访问大小写敏感，任何一侧做命名转换都会破坏直通。
            // 注：json 用 System.Text.Json 的 JsonElement 内嵌（JsonDocument.Parse 后 Clone 脱离文档生命周期）；
            // 不能用 Newtonsoft JObject——STJ 会把 JProperty/JToken 枚举成空数组结构（实测）。
            app.MapGet("/api/servers/{id}/config", (string id, string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var json = WebUiEditorService.GetEditableConfig(dataSourceName, id);
                if (json == null)
                    return Results.NotFound();

                using var doc = JsonDocument.Parse(json);
                var configJson = doc.RootElement.Clone();
                return Results.Json(new
                {
                    id,
                    dataSourceName,
                    protocol = configJson.TryGetProperty("Protocol", out var protocol) ? protocol.GetString() : "",
                    json = configJson, // 内嵌 JSON 对象（非字符串），前端免二次解析
                });
            });
        }

        internal static void MapServersCrud(WebApplication app)
        {
            // 新建服务器：body {dataSourceName, json}。json 为编辑器配置全量（PascalCase 直通），
            // 服务端 ItemCreateHelper 反序列化 → WPF 平价校验（不过则 400 {errors}，零写入）→ AddServer。
            // 加密由 DataSourceBase 在内部克隆上完成（调用方传明文）；响应 {id}=插入路径回写的 ULID。
            app.MapPost("/api/servers", (ServerSaveRequest? body) =>
            {
                var json = body?.Json;
                if (json == null || json.Value.ValueKind != JsonValueKind.Object)
                    return Results.BadRequest(new { errors = new[] { "body must contain a 'json' object" } });
                var dataSourceName = string.IsNullOrWhiteSpace(body!.DataSourceName)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : body.DataSourceName;
                return MapSaveResult(WebUiEditorService.Create(dataSourceName, json.Value.GetRawText()));
            });

            // 更新服务器：整体替换语义（前端回传加载时的完整 json）。DataSource 为 [JsonIgnore]，
            // 服务端从缓存原对象回填后走 GlobalData.UpdateServer（与 WPF 编辑器保存同一路径）。
            app.MapPut("/api/servers/{id}", (string id, string? ds, ServerSaveRequest? body) =>
            {
                var json = body?.Json;
                if (json == null || json.Value.ValueKind != JsonValueKind.Object)
                    return Results.BadRequest(new { errors = new[] { "body must contain a 'json' object" } });
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                return MapSaveResult(WebUiEditorService.Update(dataSourceName, id, json.Value.GetRawText()));
            });

            // 删除服务器：204 无内容；未知 id → 404；写库失败 → 500
            app.MapDelete("/api/servers/{id}", (string id, string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var result = WebUiEditorService.Delete(dataSourceName, id);
                return result.Status switch
                {
                    EditorSaveStatus.Ok => Results.NoContent(),
                    EditorSaveStatus.NotFound => Results.NotFound(),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });
        }

        internal static void MapServersBatch(WebApplication app)
        {
            // 批量补丁编辑：POST /api/servers/batch，body {ids, ds?, patch}。patch 键为 camelCase（列表
            // DTO 域），经显式 allow-list 映射到 C# 属性；缺失字段=保持不变；未知键 400 列出；
            // 深层字段（alternateCredentials 等子表单）400 引导单机编辑（Plan 2 简化）。
            // 原子性=预校验原子性：任一 id 缺失(404)或任一台校验失败(400) → 整批零执行。
            app.MapPost("/api/servers/batch", (BatchPatchRequest? body) =>
            {
                var patch = body?.Patch;
                if (patch == null || patch.Value.ValueKind != JsonValueKind.Object)
                    return Results.BadRequest(new { errors = new[] { "body must contain a 'patch' object" } });
                var dataSourceName = string.IsNullOrWhiteSpace(body!.Ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : body.Ds;
                var result = WebUiEditorService.ApplyBatchPatch(dataSourceName, body.Ids, patch.Value.GetRawText());
                return result.Status switch
                {
                    // updated 取服务端实际保存台数（Ok 载荷经 ServerId 字符串载体带回）——
                    // 不复用 body.Ids.Count（重复 id 已在服务端去重，二者可能不等）
                    EditorSaveStatus.Ok => Results.Json(new { updated = int.Parse(result.ServerId) }),
                    EditorSaveStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    EditorSaveStatus.NotFound => Results.NotFound(),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });
        }

        internal static void MapServersBatchPeek(WebApplication app)
        {
            // 批量回读（fix batch8 #8；batch9 Task C 键集扩展）：POST /api/servers/batch/peek，
            // body {ids, ds?}。批量编辑表单打开时回读列表 DTO 不携带的全部非敏感标量键
            // （askPasswordWhenConnect/rdpWidth 等协议 schema 字段——键集与 batch 补丁的
            // allow-list 同源派生，扣 3 个加密键与 8 个列表 DTO 已覆盖键），供前端计算 N 台
            // 共享值；不含任何加密字段（安全论证见 WebUiEditorService.PeekBatch）。只读端点：
            // ids 空 → 400；任一 id 未知 → 404（与 batch 补丁的整批拒绝语义对齐）。
            app.MapPost("/api/servers/batch/peek", (BatchPeekRequest? body) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(body?.Ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : body!.Ds;
                var result = WebUiEditorService.PeekBatch(dataSourceName, body?.Ids);
                return result.Status switch
                {
                    EditorSaveStatus.Ok => Results.Json(result.Items),
                    EditorSaveStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    EditorSaveStatus.NotFound => Results.NotFound(),
                    _ => Results.StatusCode(500), // DbError 等不可达分支（只读操作），防御性映射
                };
            });
        }

        internal static void MapServersImportExport(WebApplication app)
        {
            // 导入（Plan 4 Task 2）：multipart/form-data（file + ?ds=，ds 缺省 Local）。
            // 格式按上传文件扩展名嗅探（.json/.csv/.rdp/.db）→ 解析 → 逐台插入（凭据提取走 Dapper
            // 批量重载的按 Hash 自动提取，见 WebUiImportExportService 类注释）→ {added, skipped, errors}。
            // 未知格式/未知数据源/只读数据源/解析失败 → 400 {errors}；上传文件落临时目录，处理完即删。
            // folder（可选，'/' 分隔，batch10 Task A #1）：目标文件夹——webui 文件夹内入口导入时传
            // 当前文件夹路径，导入服务器 TreeNodes 改写为该路径拆分；缺省 = 不改写（保留解析器产出：
            // JSON 导出文件自带源库路径则保留、CSV/RDP 无路径则落根——与 WPF 导入一致）。
            app.MapPost("/api/servers/import", async (HttpContext ctx, string? ds, string? folder) =>
            {
                if (!ctx.Request.HasFormContentType)
                    return Results.BadRequest(new { errors = new[] { "request must be multipart/form-data with a 'file' part" } });
                IFormFile? file;
                try
                {
                    file = (await ctx.Request.ReadFormAsync()).Files.FirstOrDefault(f => f.Name == "file");
                }
                catch (Exception)
                {
                    file = null; // malformed multipart body
                }
                if (file == null || file.Length == 0)
                    return Results.BadRequest(new { errors = new[] { "multipart body must contain a non-empty 'file' part" } });

                var kind = WebUiImportExportService.DetectImportKind(file.FileName);
                if (kind == ImportFileKind.Unknown)
                    return Results.BadRequest(new { errors = new[] { $"unsupported file type '{file.FileName}' (expected .json/.csv/.rdp/.db)" } });

                // 各解析器按“路径”工作（mRemoteNG/RdpConfig/sqlite 连接串），先落临时目录再处理；
                // 目录内保留上传文件原名——RdpConfig.FromRdpFile 用文件名作服务器 DisplayName（WPF 平价）
                var tmpDir = Path.Combine(Path.GetTempPath(), "1rm-webui-import-" + Guid.NewGuid().ToString("N"));
                var originalName = string.Join("_",
                    Path.GetFileName(file.FileName).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                if (string.IsNullOrWhiteSpace(originalName))
                {
                    var ext = Path.GetExtension(file.FileName);
                    originalName = string.IsNullOrEmpty(ext) ? "import" : "import" + ext;
                }
                var tmpPath = Path.Combine(tmpDir, originalName);
                try
                {
                    Directory.CreateDirectory(tmpDir);
                    await using (var fs = File.Create(tmpPath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    var result = WebUiImportExportService.Import(ds, kind, tmpPath, folder);
                    return result.Status switch
                    {
                        ExportStatus.Ok => Results.Json(new { added = result.Added, skipped = result.Skipped, errors = result.Errors }),
                        _ => Results.BadRequest(new { errors = result.Errors }),
                    };
                }
                finally
                {
                    try { Directory.Delete(tmpDir, true); } catch (Exception) { /* 随临时目录清理 */ }
                }
            });

            // 导出（Plan 4 Task 2）：?ids=a,b,c（跨数据源，按每台自身数据源取）。
            // 验证门与 WPF 导出平价：任一涉及数据源在 30s 窗口外 → VerifyAsyncUi（未开启时直通 true），
            // 非 true → 403（窗口与凭据 reveal 共用，见 WebUiImportExportService.ExportAsync）。
            // 通过 → 解密克隆列表的 Indented JSON，UTF8 attachment 下载（Content-Disposition）。
            // ids 空/含未知 id/含只读数据源服务器 → 400（WPF CanExecute 要求全部可编辑同语义）。
            app.MapGet("/api/servers/export", async (string? ids) =>
            {
                var idList = (ids ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                var result = await WebUiImportExportService.ExportAsync(idList);
                return result.Status switch
                {
                    ExportStatus.Ok => Results.File(
                        Encoding.UTF8.GetBytes(result.Json),
                        "application/json",
                        result.FileName),
                    ExportStatus.Forbidden => Results.StatusCode(403),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });
        }

        internal static void MapServersEvents(WebApplication app)
        {
            // SSE 数据版本推送：连接期间订阅 GlobalData.OnReloadAll，每次重载推送 event: reload，
            // data 为本连接内重载次数（每连接独立从 0 起计）——前端收到后重新拉取 /api/servers 等即可，
            // 无重载时每 15s 发一拍心跳保活；断开（RequestAborted）在 finally 中退订。
            // 即时性：用 SemaphoreSlim 唤醒替代固定 Task.Delay 轮询——若每轮睡满 15s，
            // 重载事件最迟要等满一个心跳周期才发出，无法满足前端"秒级自动刷新"的诉求。
            // 心跳是具名 ping 事件而非注释行：SSE 注释对 EventSource API 不可见，前端看门狗需要
            // 可观测的存活信号（静默 45s 判连接已死并强制重建——后端重启窗口内代理会返回 502，
            // 按规范 EventSource 永久失败不再自动重连，须靠前端自愈 + 心跳监测兜底）；
            // 未监听 ping 的消费方按规范忽略该事件，行为不受影响。
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
                // OnReloadAll 是 public 字段（非 event），+=/-= 编译为非原子读改写；
                // SSE 是其首个多线程订阅方（Kestrel 请求线程），并发开/关页签会丢失订阅或退订，
                // 故与其它端点一致用 lock(gd) 串行化。Invoke 侧无需加锁（委托调用列表是不可变快照）。
                lock (gd)
                {
                    gd.OnReloadAll += OnReload;
                }
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
                            // 心跳具名事件（见方法头注释：前端看门狗判活依据；注释行对 EventSource 不可见）。
                            // data 行不可省：SSE 规范 dispatch 步骤对空 data buffer 直接 return 不派发事件——
                            // 只有 event 行的心跳前端永远收不到，看门狗会误杀健康连接（评审 E1）
                            await ctx.Response.WriteAsync("event: ping\ndata: 1\n\n", ctx.RequestAborted);
                        }
                    }
                }
                catch (OperationCanceledException) { /* 客户端断开，正常结束 */ }
                finally
                {
                    lock (gd) // 同上：字段 -= 非原子，防止并发断开时丢失退订导致处理器泄漏
                    {
                        gd.OnReloadAll -= OnReload;
                    }
                    // 不 Dispose wakeup：退订与并发执行中的 OnReload 之间存在窗口，
                    // Dispose 后到达的 Release 会抛 ObjectDisposedException 并打断 ReloadAll 调用方；
                    // SemaphoreSlim 无非托管资源，交给 GC 即可
                }
            });
        }

        /// <summary>保存（POST/PUT）结果 → HTTP 映射：Ok→200 {id}；BadRequest→400 {errors}；NotFound→404；DbError→500。</summary>
        private static IResult MapSaveResult(EditorSaveResult result)
        {
            return result.Status switch
            {
                EditorSaveStatus.Ok => Results.Json(new { id = result.ServerId }),
                EditorSaveStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                EditorSaveStatus.NotFound => Results.NotFound(),
                _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
            };
        }
    }
}
