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
    /// WebUiEndpoints 分域：辅助域（版本/图标/串口建议/本地视图状态，均为小而独立的只读或代理端点）。
    /// ─ GET  /api/version               应用与 API 版本 + 构建日期 + 新版本检测状态（update 域）
    /// ─ GET  /api/icons                 内置图标列表（ServerIcons 单例快照）
    /// ─ POST /api/icons/extract-from-exe 从 .exe 提取图标（与 WPF 图标选择器同路径）
    /// ─ GET  /api/serial/options        Serial 编辑器下拉建议（端口/波特率）
    /// ─ GET/PUT /api/ui-state/tree      服务器树状态（展开/排序，本地缓存代理）
    /// ─ GET/POST /api/ui-state/list-order 列表自定义顺序（本地缓存代理）
    /// 注册顺序由主文件 MapAll 统一编排（与拆分前一致）。
    /// </summary>
    public static partial class WebUiEndpoints
    {
        internal static void MapVersion(WebApplication app)
        {
            // fix batch6 Task D #12：除既有 version/api 外，增补 buildDate（AppVersion.BuildDate
            // 原文，前端按 WPF AboutPageViewModel:98 语义自行去时区后缀显示）与 update 域
            // （WebUiUpdateService 缓存快照；DoNotCheckNewVersion 时 available 恒 false）。
            // 首次命中 EnsureStarted() 幂等触发检查 + 每小时复查 Timer（端点线程不等网络）。
            app.MapGet("/api/version", () =>
            {
                WebUiUpdateService.EnsureStarted();
                var u = WebUiUpdateService.GetStatus();
                return Results.Json(new
                {
                    version = _1RM.AppVersion.Version,
                    api = 1,
                    buildDate = _1RM.AppVersion.BuildDate,
                    update = new
                    {
                        available = u.Available,
                        checking = u.Checking,
                        newVersion = u.NewVersion,
                        newVersionUrl = u.NewVersionUrl,
                        breaking = u.Breaking,
                    },
                });
            });
        }

        internal static void MapIcons(WebApplication app)
        {
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
        }

        internal static void MapSerialOptions(WebApplication app)
        {
            // Serial 编辑器的可输入下拉建议（fix batch4 Task B）：对齐 WPF SerialFormView 的
            // AutoCompleteComboBox 数据源——端口 = 后端机器 SerialPort.GetPortNames()（Serial.cs:157），
            // 波特率 = Serial.cs BitRates 常量表（Serial.cs:71）。new Serial() 构造无副作用
            // （ProtocolBase ctor 仅赋协议名/版本，Serial ctor 额外取首个端口名作默认值）。
            // 前端拉取失败时静默退化为纯文本输入（见 FormField.vue 的建议缓存）。
            app.MapGet("/api/serial/options", () =>
            {
                var serial = new Serial();
                return Results.Json(new { ports = serial.SerialPorts, baudRates = serial.BitRates });
            });
        }

        internal static void MapIconsExtractFromExe(WebApplication app)
        {
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
        }

        internal static void MapUiStateTree(WebApplication app)
        {
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

        internal static void MapUiStateListOrder(WebApplication app)
        {
            // 列表自定义顺序（Plan 4 Task 4）：LocalityListViewService.ServerCustomOrder 的读写代理。
            // GET 返回 {ids: 按序号升序的 id 列表, order: {id: 序号快照}}（静态缓存拷贝 + 重试，
            // 与 /api/ui-state/tree 同款并发防护）；POST {ids} = 整库新顺序全量替换（复用 WPF 的
            // ServerCustomOrderSave：清空重填 + 同步 vm.CustomOrder + 落盘 .locality/.list_view.json），
            // 未知 id 跳过，响应 {ids} = 实际保存顺序。该字典是机器本地视图状态，不触发 SSE。
            app.MapGet("/api/ui-state/list-order", () =>
            {
                var order = CopyDictionaryWithRetry(LocalityListViewService.Settings.ServerCustomOrder);
                return Results.Json(new
                {
                    ids = order.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList(),
                    order,
                });
            });

            app.MapPost("/api/ui-state/list-order", (ListOrderRequest? body) =>
            {
                var ids = body?.Ids;
                if (ids == null || ids.Count == 0)
                    return Results.BadRequest(new { error = "body must contain a non-empty 'ids' array" });
                var gd = IoC.Get<GlobalData>();
                var vms = new List<ProtocolBaseViewModel>();
                lock (gd) // 快照语义同 /api/servers：锁内只做查找
                {
                    foreach (var id in ids.Distinct())
                    {
                        var vm = gd.VmItemList.FirstOrDefault(x => x.Id == id && IsConnectable(x.Server));
                        if (vm != null)
                            vms.Add(vm);
                    }
                }
                LocalityListViewService.ServerCustomOrderSave(vms); // 清空重填（WPF 同款全量替换）
                return Results.Json(new { ids = vms.Select(v => v.Id).ToList() });
            });
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
