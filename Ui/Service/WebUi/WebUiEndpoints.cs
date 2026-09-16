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
    public static class WebUiEndpoints
    {
        /// <summary>
        /// 外观取值域（spec §4）。校验大小写不敏感，存储时归一：
        /// themeMode/accent 归一小写、fontSize 归一大写，GET 返回值即规范形式。
        /// </summary>
        private static readonly HashSet<string> ValidThemeModes = new(StringComparer.OrdinalIgnoreCase)
            { "dark", "light", "system" };
        private static readonly HashSet<string> ValidAccents = new(StringComparer.OrdinalIgnoreCase)
            { "blue", "violet", "pink", "red", "orange", "green", "slate" };
        private static readonly HashSet<string> ValidFontSizes = new(StringComparer.OrdinalIgnoreCase)
            { "S", "M", "L", "XL" };

        /// <summary>
        /// Web UI 侧的服务器过滤判定：跳过分组头 Dummy（树形列表虚拟节点）与
        /// 临时会话（TMP_SESSION_ 前缀或空 Id，即编辑器中尚未落库的对象）。
        /// /api/servers、/api/search、/api/connect 共用同一语义，避免各处过滤条件漂移。
        /// </summary>
        public static bool IsConnectable(ProtocolBase server)
        {
            return server is not Dummy && !server.IsTmpSession();
        }

        /// <summary>
        /// 连接状态派生（纯函数，Plan 4 Task 1）：activeServerIds 为当前 1Remote 托管会话占用的
        /// 服务器 Id 集合（<see cref="BuildActiveServerIdSet"/> 快照），serverId 命中 → connected。
        /// 语义收窄：Unhosted 会话（外部 mstsc.exe、RunWithHosting=false 的 LocalApp）不进连接字典，
        /// 显示 disconnected——状态含义是「该服务器是否有 1Remote 托管的活动会话」，不代表远端可达性。
        /// </summary>
        public static string DeriveConnectionState(IEnumerable<string> activeServerIds, string? serverId)
        {
            if (string.IsNullOrEmpty(serverId))
                return WebUiConstants.StatusDisconnected;
            foreach (var id in activeServerIds)
            {
                if (id == serverId)
                    return WebUiConstants.StatusConnected;
            }
            return WebUiConstants.StatusDisconnected;
        }

        /// <summary>
        /// 快照当前活动会话的服务器 Id 集合：遍历 SessionControlService.ConnectionId2Hosts
        /// （ConcurrentDictionary，Kestrel 线程直接枚举安全），只读 host.ProtocolServer.Id 属性
        /// （连接用 Clone() 是 MemberwiseClone，Id 保留，故与列表 vm.Server.Id 同源可匹配），
        /// 绝不在 Host 对象上调用方法——Host 是 WPF UserControl，跨线程调用非法。
        /// 测试宿主刻意不注册 SessionControlService（注册会令其订阅 OnRequestServerConnect，
        /// /api/connect 集成测试会触发真实连接流程），TryGet 为 null → 空集 → 全部 disconnected，
        /// 与「空连接字典」观测一致。
        /// </summary>
        private static HashSet<string> BuildActiveServerIdSet()
        {
            var set = new HashSet<string>();
            var sessions = IoC.TryGet<SessionControlService>();
            if (sessions == null)
                return set;
            foreach (var host in sessions.ConnectionId2Hosts.Values)
            {
                var id = host?.ProtocolServer?.Id;
                if (!string.IsNullOrEmpty(id))
                    set.Add(id);
            }
            return set;
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
                    // 活动会话 Id 快照：仅并发字典的属性读，无 IO，锁内安全（Plan 4 Task 1）
                    var activeIds = BuildActiveServerIdSet();
                    var list = gd.VmItemList
                        .Where(vm => IsConnectable(vm.Server))
                        .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime,
                            DeriveConnectionState(activeIds, vm.Server.Id)))
                        .ToList();
                    return Results.Json(list); // 先物化快照再序列化，锁内不做 IO
                }
            });

            // 数据源列表：本地 + 附加数据源（config = 连接参数视图，无密码）
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

            // 新建数据源（Plan 3 Task 3）：WPF-parity 持久化 = ConfigurationService.AdditionalDataSource
            // .Add + Save() + DataSourceService.AddOrUpdateDataSource（连接失败不回滚，WPF CmdAdd 同款——
            // 返回 201 + 实际 status，前端可先调 /test 验证）。重名 → 409；校验失败 → 400 零写入。
            app.MapPost("/api/datasources", (DataSourceSaveRequest? body) =>
            {
                var result = WebUiDataSourceService.Create(body);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.Json(new
                    {
                        dataSource = result.Dto,
                        connectError = result.ConnectError,
                    }, statusCode: 201),
                    DataSourceMutationStatus.Conflict => Results.Json(new { errors = result.Errors }, statusCode: 409),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 更新数据源连接参数：Local → 400（SQLite 路径不暴露）；字段缺失 = 保持；password 空 = 保持
            // （Mysql/Pgsql Password setter 收 "" 会清空）；保存后 AddOrUpdateDataSource 重连。
            app.MapPut("/api/datasources/{name}", (string name, DataSourceConfigRequest? body) =>
            {
                var result = WebUiDataSourceService.Update(name, body?.Config);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.Json(new
                    {
                        dataSource = result.Dto,
                        connectError = result.ConnectError,
                    }),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 删除数据源：Local → 400；serverCount>0 且未确认 → 409 {serverCount}（Web 侧守卫，WPF
            // CmdDelete 无检查直接删）；keepServers=true = 用户已确认，镜像 WPF：移除数据源，
            // 服务器不迁移留在库文件中（重新添加该数据源即可找回）。
            app.MapDelete("/api/datasources/{name}", (string name, bool? keepServers) =>
            {
                var result = WebUiDataSourceService.Delete(name, keepServers == true);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.NoContent(),
                    DataSourceMutationStatus.Conflict => Results.Json(
                        new { error = result.Errors.FirstOrDefault(), serverCount = result.ServerCount }, statusCode: 409),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 测试连接：sqlite = Database_SelfCheck（含错误详情）；mysql/pgsql = 静态 TestConnection，
            // config 未带密码时沿用已存密码。带 config = 按 config 测试（保存前验证向导流程）。
            app.MapPost("/api/datasources/{name}/test", (string name, DataSourceConfigRequest? body) =>
            {
                var result = WebUiDataSourceService.Test(name, body?.Config);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.Json(new
                    {
                        ok = result.IsOk,
                        status = result.StatusText,
                        detail = result.Detail,
                    }),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = new[] { result.Detail } }),
                };
            });

            // 运行器配置（Plan 3 Task 3）：整体往返 ProtocolSettings（含 SelectedRunnerName），
            // runners 数组 PascalCase + $type 直通（与 ProtocolConfigurationService 的 Newtonsoft
            // 持久化同一路径）。GET 6 协议；PUT 缺失协议 = 保持，未知协议键/runners 空 → 400 零写入。
            app.MapGet("/api/settings/runners", () =>
            {
                var pcs = IoC.Get<ProtocolConfigurationService>();
                return Results.Json(new { protocols = WebUiDataSourceService.ReadRunners(pcs) });
            });

            app.MapPut("/api/settings/runners", (RunnersSaveRequest? body) =>
            {
                var pcs = IoC.Get<ProtocolConfigurationService>();
                var result = WebUiDataSourceService.ApplyRunners(pcs, body?.Protocols);
                if (!result.IsOk)
                    return Results.BadRequest(new { errors = result.Errors });
                return Results.Json(new { protocols = WebUiDataSourceService.ReadRunners(pcs) });
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
                // 活动会话 Id 快照在锁外构建（语义同 /api/servers；仅并发字典属性读）
                var activeIds = BuildActiveServerIdSet();
                return Results.Json(matched
                    .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime,
                        DeriveConnectionState(activeIds, vm.Server.Id)))
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

            // 导入（Plan 4 Task 2）：multipart/form-data（file + ?ds=，ds 缺省 Local）。
            // 格式按上传文件扩展名嗅探（.json/.csv/.rdp/.db）→ 解析 → 逐台插入（凭据提取走 Dapper
            // 批量重载的按 Hash 自动提取，见 WebUiImportExportService 类注释）→ {added, skipped, errors}。
            // 未知格式/未知数据源/只读数据源/解析失败 → 400 {errors}；上传文件落临时目录，处理完即删。
            app.MapPost("/api/servers/import", async (HttpContext ctx, string? ds) =>
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
                if (string.IsNullOrWhiteSpace(originalName)) originalName = "import" + Path.GetExtension(file.FileName) ?? "import";
                var tmpPath = Path.Combine(tmpDir, originalName);
                try
                {
                    Directory.CreateDirectory(tmpDir);
                    await using (var fs = File.Create(tmpPath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    var result = WebUiImportExportService.Import(ds, kind, tmpPath);
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

            // 内置图标列表：ServerIcons 单例（Ui 程序集 .g.resources 内嵌 PNG，有序 base64，无名称）。
            // 装载在单例构造的 Task.Factory.StartNew 后台执行——只依赖程序集资源与 GDI/WPF 位图转换，
            // 不需要 WPF Application（测试宿主同样可用）；桌面进程在启动期（LauncherWindowView）已调
            // ServerIcons.Init() 预热，通常早已就绪。本端点对首访竞态做有界等待（空列表最多等 3s），
            // 随后快照返回——拷贝期间后台仍可能并发 Add（抛 InvalidOperationException），重试一次吸收。
            app.MapGet("/api/icons", () =>
            {
                var icons = ServerIcons.Instance.IconsBase64;
                var deadline = Environment.TickCount64 + 3000;
                while (icons.Count == 0 && Environment.TickCount64 < deadline)
                {
                    Thread.Sleep(50);
                }
                return Results.Json(new { icons = CopyListWithRetry(icons) });
            });

            // 凭据库名称列表：供编辑器「继承凭据」下拉。GetCredentials 自带缓存判定
            // （NeedRead 时读库）与 lock(this)（数据源实例锁，与 GlobalData 的锁无关），无需 lock(gd)。
            // 未知数据源 → 404（只读名称清单，读库失败语义与 WPF 一致：状态异常时返回缓存）。
            app.MapGet("/api/credentials/names", (string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var dataSource = IoC.Get<DataSourceService>().GetDataSource(dataSourceName);
                if (dataSource == null)
                    return Results.NotFound();
                var names = dataSource.GetCredentials().Select(x => x.Name).ToList();
                return Results.Json(new { names });
            });

            // 凭据库列表（Plan 3 Task 1）：{credentials:[{name,address,port,userName,refCount}]}。
            // 安全红线：绝不包含密码/私钥路径——明文查看只能走 reveal 端点（本地二次验证）。
            // 引用计数按该数据源下的服务器扫描（InheritedCredentialName + AlternateCredentials[].Name）。
            // 未知数据源 → 404（与 /api/credentials/names 语义一致）。
            app.MapGet("/api/credentials", (string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var dataSource = IoC.Get<DataSourceService>().GetDataSource(dataSourceName);
                if (dataSource == null)
                    return Results.NotFound();
                return Results.Json(new { credentials = WebUiCredentialService.List(dataSource) });
            });

            // 新建凭据：body {ds?, credential:{Name*,Address,Port,UserName,Password,PrivateKeyPath}}。
            // credential 域 Password/PrivateKeyPath 为明文——加密由 Database_InsertCredential 在内部
            // 克隆上完成；Name 非空+唯一（忽略大小写，WPF 编辑器同款）+长度≤100，违例 400 {errors}；
            // 只读数据源前置 400（InsertCredential 对只读库静默返回 Success，必须先查 IsWritable）。
            app.MapPost("/api/credentials", (CredentialSaveRequest? body) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(body?.Ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : body!.Ds!;
                return MapCredentialSaveResult(WebUiCredentialService.Create(dataSourceName, body?.Credential));
            });

            // 更新凭据（按名寻址，与 WPF 凭据编辑一致）：整体替换语义；nameBefore=路由名驱动
            // 引用服务器联动改名/字段同步（Dapper 事务）；重命名目标名做与新建相同的唯一校验。
            app.MapPut("/api/credentials/{name}", (string name, string? ds, CredentialSaveRequest? body) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                return MapCredentialSaveResult(WebUiCredentialService.Update(dataSourceName, name, body?.Credential));
            });

            // 删除凭据：引用服务器的 InheritedCredentialName 由 Dapper 事务联动清空（WPF 既有行为）；
            // 成功 204；未知名 → 404；只读 → 400（DeleteCredential 对只读库自身会 Fail，此处前置拦截统一语义）。
            app.MapDelete("/api/credentials/{name}", (string name, string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var result = WebUiCredentialService.Delete(dataSourceName, name);
                return result.Status switch
                {
                    EditorSaveStatus.Ok => Results.NoContent(),
                    EditorSaveStatus.NotFound => Results.NotFound(),
                    EditorSaveStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });

            // 明文查看凭据：本地二次验证（未开启时 VerifyAsyncUi 直通 true，与 WPF 一致）→
            // 通过后克隆+解密返回 {password, privateKeyPath}；同一数据源 30s 内免再次验证
            // （服务端静态时间戳）。验证失败/用户取消 → 403；未知名 → 404；未知数据源 → 400。
            // 注意 async 端点直接 await（二次验证是 async Task<bool?>，不能用同步 dispatch 包裹）。
            app.MapPost("/api/credentials/{name}/reveal", async (string name, string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var result = await WebUiCredentialService.Reveal(dataSourceName, name);
                return result.Status switch
                {
                    CredentialRevealStatus.Ok => Results.Json(new
                    {
                        password = result.Password,
                        privateKeyPath = result.PrivateKeyPath,
                    }),
                    CredentialRevealStatus.NotFound => Results.NotFound(),
                    CredentialRevealStatus.Forbidden => Results.StatusCode(403),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 常规设置（Plan 3 Task 2）：读 GeneralConfig 白名单字段 + SecondaryVerificationHelper
            // （requireSecondaryVerification 的真实状态不在 GeneralConfig，见 WebUiSettingsService 类注释）。
            // PUT 为部分更新（缺失键=保持不变），任一键非法 400 零写入；language 变更同步调
            // LanguageService.SetLanguage 让 WPF 即时生效（未注册时静默跳过）。
            app.MapGet("/api/settings/general", async () =>
            {
                var cs = IoC.Get<ConfigurationService>();
                return Results.Json(await WebUiSettingsService.ReadGeneralAsync(cs));
            });

            app.MapPut("/api/settings/general", async (GeneralSettingsUpdateRequest? body) =>
            {
                var result = await WebUiSettingsService.ApplyGeneralAsync(IoC.Get<ConfigurationService>(), body);
                return result.Status switch
                {
                    SettingsApplyStatus.Ok => Results.Json(result.Dto),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 启动器设置：热键为 WPF 枚举，线格式 = 成员名（"ControlAlt"/"M"），PUT 亦接受 "Ctrl+Alt"
            // 显示形态。写后重注册热键：注册失败且 launcherEnabled → 409（配置已保存，与 WPF
            // “内存先行落值 + 警告”语义对齐，见 WebUiSettingsService.ApplyLauncher 注释）。
            app.MapGet("/api/settings/launcher", () =>
            {
                var cs = IoC.Get<ConfigurationService>();
                return Results.Json(WebUiSettingsService.ReadLauncher(cs));
            });

            app.MapPut("/api/settings/launcher", (LauncherSettingsUpdateRequest? body) =>
            {
                var result = WebUiSettingsService.ApplyLauncher(IoC.Get<ConfigurationService>(), body);
                return result.Status switch
                {
                    SettingsApplyStatus.Ok => Results.Json(result.Dto),
                    SettingsApplyStatus.HotkeyConflict => Results.Json(new { error = "hotkey conflict", settings = result.Dto }, statusCode: 409),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 标签管理（Plan 3 Task 2）：列表（该数据源聚合）/ 置顶 / 重命名 / 删除。
            // rename/delete 复刻 TagActionHelper 的核心循环（详见 WebUiSettingsService）。
            app.MapGet("/api/tags/manage", (string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var dataSource = IoC.Get<DataSourceService>().GetDataSource(dataSourceName);
                if (dataSource == null)
                    return Results.NotFound();
                return Results.Json(WebUiSettingsService.ListManageTags(dataSource));
            });

            app.MapPut("/api/tags/manage", (TagPinRequest? body) =>
            {
                var result = WebUiSettingsService.SetTagPinned(body?.Ds, body?.Name, body?.Pinned);
                return result.Status switch
                {
                    TagManageStatus.Ok => Results.Json(result.Item),
                    TagManageStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            app.MapPost("/api/tags/rename", (TagRenameRequest? body) =>
            {
                var result = WebUiSettingsService.RenameTag(body?.Ds, body?.From, body?.To);
                return result.Status switch
                {
                    TagManageStatus.Ok => Results.Json(result.Rename),
                    TagManageStatus.NotFound => Results.NotFound(),
                    TagManageStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });

            app.MapDelete("/api/tags/{name}", (string name, string? ds) =>
            {
                var result = WebUiSettingsService.DeleteTag(ds, name);
                return result.Status switch
                {
                    TagManageStatus.Ok => Results.NoContent(),
                    TagManageStatus.NotFound => Results.NotFound(),
                    TagManageStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });

            // exe 图标提取：与 WPF 图标选择器同一路径（IconPopupDialogViewModel.CmdSelectImage 的
            // .exe 分支：ExtractAssociatedIcon → CreateBitmapSourceFromHIcon），base64 转换复用
            // 编辑器保存时的 BitmapSource.ToBase64()（→ System.Drawing Bitmap → PNG）。
            // 路径缺失/非 .exe → 404（与 VM 分支语义一致：仅 .exe 受理）；其余失败（提取/转换异常）→ 500。
            app.MapPost("/api/icons/extract-from-exe", (ExtractIconRequest? body) =>
            {
                var path = body?.Path?.Trim();
                if (string.IsNullOrWhiteSpace(path))
                    return Results.BadRequest(new { error = "body must contain a 'path' string" });
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                    return Results.NotFound();
                try
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (icon == null)
                        return Results.NotFound();
                    var img = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        new Int32Rect(0, 0, icon.Width, icon.Height),
                        BitmapSizeOptions.FromEmptyOptions());
                    img.Freeze(); // Kestrel 线程上创建即用；Freeze 解除 Dispatcher 线程亲和，转换可安全进行
                    return Results.Json(new { iconBase64 = img.ToBase64() });
                }
                catch (FileNotFoundException)
                {
                    return Results.NotFound(); // 检查与提取之间文件被删：按缺失语义 404
                }
                catch (Exception)
                {
                    return Results.Json(new { error = "failed to extract icon from exe" }, statusCode: 500);
                }
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
                            await ctx.Response.WriteAsync(": heartbeat\n\n", ctx.RequestAborted);
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

            // 外观设置（Web UI 专属字段，独立于 WPF ThemeConfig，持久化到 1Remote.json，spec §4）。
            // GET 返回当前值（旧配置缺字段时由 Configuration 属性初始化器给默认 dark/blue/M）
            app.MapGet("/api/settings/appearance", () =>
            {
                var cs = IoC.Get<ConfigurationService>();
                return Results.Json(ReadAppearance(cs));
            });

            // PUT 校验通过后走 ConfigurationService.Save()（既有保存路径，落盘 1Remote.json），
            // 返回 200 + 归一后的存储值；任一字段非法即 400，不写入任何值。
            // font 为自由取值（字体族名不枚举校验）：空串 = 跟随系统，非空 trim 后存储
            app.MapPut("/api/settings/appearance", (AppearanceDto? dto) =>
            {
                var themeMode = dto?.ThemeMode?.Trim() ?? string.Empty;
                var accent = dto?.Accent?.Trim() ?? string.Empty;
                var fontSize = dto?.FontSize?.Trim() ?? string.Empty;
                var font = dto?.Font?.Trim() ?? string.Empty;
                if (!ValidThemeModes.Contains(themeMode))
                    return Results.BadRequest(new { error = $"invalid themeMode '{themeMode}', expected one of: dark, light, system" });
                if (!ValidAccents.Contains(accent))
                    return Results.BadRequest(new { error = $"invalid accent '{accent}', expected one of: blue, violet, pink, red, orange, green, slate" });
                if (!ValidFontSizes.Contains(fontSize))
                    return Results.BadRequest(new { error = $"invalid fontSize '{fontSize}', expected one of: S, M, L, XL" });

                var cs = IoC.Get<ConfigurationService>();
                cs.WebUiThemeMode = themeMode.ToLowerInvariant();
                cs.WebUiAccent = accent.ToLowerInvariant();
                cs.WebUiFontSize = fontSize.ToUpperInvariant();
                cs.WebUiFontFamily = font;
                cs.Save();
                return Results.Json(ReadAppearance(cs));
            });

            // 服务器树状态：LocalityTreeViewService（静态类，无 IoC）两个字典的读写代理。
            // expanded 键为文件夹全路径——各段之间用 ServerTreeViewModel.FullPathSeparator
            // （" ]=+=+=+=>[ "）连接，如 "LocalDataSource ]=+=+=+=>[ Folder1"，不是 "->"；
            // order 键为节点 Id（与 WPF 树 BuildView/LoadLocalCaches 消费一致）。
            // 键对本 API 为不透明透传（前端需按同一分隔符构造）；PUT 为整体替换（幂等），落盘 .locality/.tree_view.json
            app.MapGet("/api/ui-state/tree", () =>
            {
                var s = LocalityTreeViewService.Settings;
                // 拷贝快照再序列化：Settings 为静态缓存，WPF 侧有两种写入方式——重载时整体替换字典
                // （SaveExpansionStates/LoadExpansionStates），节点展开/折叠时原地写入
                // （TreeNode.IsExpanded: Settings.TreeNodeExpansionStates[FullPath] = value）。
                // 原地写入与字典拷贝并发会抛 InvalidOperationException（枚举中集合被修改），
                // 拷贝失败重试一次；仍失败（持续并发写）交由上层 500
                return Results.Json(new TreeStateDto
                {
                    Expanded = CopyDictionaryWithRetry(s.TreeNodeExpansionStates),
                    Order = CopyDictionaryWithRetry(s.CustomNodeOrder),
                });
            });

            app.MapPut("/api/ui-state/tree", (TreeStateDto? dto) =>
            {
                if (dto == null)
                    return Results.BadRequest(new { error = "body must be a JSON object" });
                var s = LocalityTreeViewService.Settings;
                s.TreeNodeExpansionStates = dto.Expanded ?? new Dictionary<string, bool>();
                s.CustomNodeOrder = dto.Order ?? new Dictionary<string, int>();
                // Save() 序列化 Settings 时，WPF 侧节点展开的原地写入窗口同样存在——
                // Save 内部 RetryHelper.Try(3 次) 会吸收该瞬时失败后重试，无需在此额外处理
                LocalityTreeViewService.Save();
                // 响应拷贝与 GET 同理：WPF 原地写入窗口下并发拷贝需重试兜底
                return Results.Json(new TreeStateDto
                {
                    Expanded = CopyDictionaryWithRetry(s.TreeNodeExpansionStates),
                    Order = CopyDictionaryWithRetry(s.CustomNodeOrder),
                });
            });
        }

        private static AppearanceDto ReadAppearance(ConfigurationService cs)
        {
            return new AppearanceDto
            {
                ThemeMode = cs.WebUiThemeMode,
                Accent = cs.WebUiAccent,
                FontSize = cs.WebUiFontSize,
                Font = cs.WebUiFontFamily,
            };
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

        /// <summary>
        /// 凭据保存（POST/PUT）结果 → HTTP 映射：与 MapSaveResult 同款分类，
        /// 但 Ok 载荷为凭据名（按名寻址资源）：Ok→200 {name}；其余同上。
        /// </summary>
        private static IResult MapCredentialSaveResult(EditorSaveResult result)
        {
            return result.Status switch
            {
                EditorSaveStatus.Ok => Results.Json(new { name = result.ServerId }),
                EditorSaveStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                EditorSaveStatus.NotFound => Results.NotFound(),
                _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
            };
        }

        /// <summary>
        /// 拷贝静态图标缓存列表：ServerIcons 后台装载任务会向 IconsBase64 原地 Add，
        /// 并发拷贝（构造器枚举）可能因集合被修改抛 InvalidOperationException，重拷一次；
        /// 再失败说明持续装载中，让异常冒泡交由上层 500（与 CopyDictionaryWithRetry 同款语义）。
        /// </summary>
        private static List<string> CopyListWithRetry(List<string> source)
        {
            try
            {
                return new List<string>(source);
            }
            catch (InvalidOperationException)
            {
                return new List<string>(source);
            }
        }

        /// <summary>
        /// 拷贝静态缓存字典：WPF 侧节点展开会原地写入（非整体替换），并发拷贝可能因
        /// 集合被修改抛 InvalidOperationException，重拷一次；再失败说明持续并发写，
        /// 让异常冒泡交由上层 500。
        /// </summary>
        private static Dictionary<TKey, TValue> CopyDictionaryWithRetry<TKey, TValue>(Dictionary<TKey, TValue> source)
            where TKey : notnull
        {
            try
            {
                return new Dictionary<TKey, TValue>(source);
            }
            catch (InvalidOperationException)
            {
                return new Dictionary<TKey, TValue>(source);
            }
        }
    }
}
