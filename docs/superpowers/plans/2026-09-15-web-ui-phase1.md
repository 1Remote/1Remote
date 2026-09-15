# 1Remote Web UI 计划 1：服务骨架与主界面 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立本地 Web 服务骨架（进程内 HTTP + SSE）与 Vue 3 前端工程，实现主界面（数据源树边栏 + 服务器行列表 + 搜索 + 连接触发 + 双轨主题），在 WebView2 中可日常浏览与连接服务器。

**Architecture:** WPF 进程内启动 Kestrel（仅 127.0.0.1，Release 随机端口 + token，DEBUG 固定端口免 token），Minimal API 端点包装现有服务层（GlobalData/KeywordMatchService/GlobalEventHelper）；主窗口内容区新增 WebView2 控件承载 Vue 前端，配置开关可在桌面/网页引擎间切换（回退保险）。前端从扁平 `/api/servers` 自组树与列表，数据变更经 SSE 版本号触发增量刷新。

**Tech Stack:** 后端 .NET 9 Minimal API（FrameworkReference Microsoft.AspNetCore.App）+ MSTest；前端 Vue 3 `<script setup>` + Vite + JavaScript + Naive UI + vue-i18n + @vueuse/core + vuedraggable（拖拽本计划不用，计划 4 用）。

**上游 Spec:** `docs/superpowers/specs/2026-09-15-web-ui-redesign-design.md`（视觉与交互以 spec §2-§8 为准）
**计划系列:** 本计划是 4 个连续计划中的第 1 个（2=编辑器、3=设置/凭据库/i18n 完整体、4=拖拽、导入导出、虚拟滚动 >500 行、列宽/列显持久化等体验精化——spec §3.4 虚拟滚动与 §8.6 列状态持久化归计划 4）。

---

## 全局约定（每个任务都适用）

1. **测试策略**：后端任务 TDD（MSTest 3.6.4，新测试放 `Tests/Service/WebUi/`）。**注意**：原 `Tests/TestInit.cs` 已在 Task 1 清理中删除（死代码，引用已不存在的 API）；Task 2/3 的测试不依赖 IoC，直接写即可；Task 4 将以当前 API 重建精简版 TestInit（`IoC.GetByType` 委托注入 + 实例缓存静态字段 + `MockLanguageService` 模式，参照 `Ui/Ioc.cs:20`）。当前测试基线：10 个测试 8 过 2 败（2 个失败在仓库所有者未跟踪的 TDD 红灯文件 `Tests/Utils/RdpConfigTests.cs`，与本项目无关）。前端任务以"实现 + 手动验证清单 + `npm run build` 通过"为完成标准（spec §9 未引入前端测试框架，有意取舍）。
2. **构建/测试命令**：解决方案根目录运行 `dotnet build` / `dotnet test Tests/Tests.csproj`（若仓库现有测试有特殊运行方式，以能跑通现有测试的方式为准）。前端在 `webui/` 目录运行 `npm run dev`（Vite 5173）与 `npm run build`。
3. **端口约定**：DEBUG：后端固定 `17321`、无 token（仅回环）；Release：随机端口 18000-25000 + 32 位随机 token，页面 URL 以 `?token=` 传递（fragment 不会发往服务器，不能用 `#token=`）。
4. **特殊配置政策**：Web UI 仅支持默认 net9 配置（Debug/Release/StoreDebug/StoreRelease）。`ReleaseNet48`/`ReleaseNet6` 为仓库既有的 broken 配置（TFM 修复前即无法完整编译），本计划不为它们做兼容；`Ui/Service/WebUi/` 下的新代码无需条件编译。
5. **提交规范**：英文 conventional commits（`feat:`/`test:`/`chore:`），每任务至少一次提交，消息末尾加 `Co-Authored-By: Claude <noreply@anthropic.com>`。
6. **代码位置**：后端新代码全部在 `Ui/Service/WebUi/`；前端新工程在仓库根 `webui/`（独立于 Ui.csproj）。
7. **WPF 侧改动最小化**：只动 `AppInit.cs`（启动服务）、`MainWindowView.xaml(.cs)`（WebView2 壳）、`Ui/Service/ConfigurationService.cs` 中的 GeneralConfig（引擎开关）、`GeneralSettingView`（开关下拉）。其余 WPF 代码一律不碰。

## 文件结构总览

```
Ui/
├─ Service/WebUi/               # 本计划全部后端新代码
│  ├─ WebUiServer.cs            # Kestrel 宿主：启动/停止/端口/token（Task 2）
│  ├─ TokenMiddleware.cs        # 鉴权中间件（Task 2）
│  ├─ WebUiEndpoints.cs         # 全部 Minimal API 端点注册（Task 2-8）
│  ├─ WebUiDto.cs               # ServerDto/DataSourceDto/TagDto/AppearanceDto（Task 3）
│  └─ DtoMapper.cs              # 服务层对象 → DTO 映射（Task 3）
├─ wwwroot/                     # 前端构建产物（Task 21 生成，gitignore dist 源不忽略）
└─ （修改）AppInit.cs / View/MainWindowView.xaml(.cs) / Model/GlobalData…不修改
webui/                          # 前端工程（Task 10 起）
├─ package.json / vite.config.js / index.html
├─ src/
│  ├─ main.js / App.vue / router.js
│  ├─ api/index.js              # fetch 封装 + token + SSE（Task 11）
│  ├─ themes/theme.css          # CSS 变量两层（Task 12）
│  ├─ themes/index.js           # 预设/强调色/Naive themeOverrides（Task 12）
│  ├─ locales/zh-CN.json / en-US.json（Task 19）
│  ├─ composables/useServers.js # 数据加载/搜索/SSE/树组装（Task 14）
│  ├─ components/SideTree.vue   # 边栏树+标签区（Task 15）
│  ├─ components/ServerTable.vue# 行列表（Task 16）
│  ├─ components/ServerRow.vue  # 单行（Task 16）
│  └─ views/ServerListView.vue  # 页面组装（Task 13）
Tests/Service/WebUi/            # 本计划全部后端测试
```

---

# Phase A：后端服务骨架（Task 1-9）

### Task 1: 项目引用与包

**Files:**
- Modify: `Ui/Ui.csproj`（PropertyGroup 后新增 ItemGroup）
- Modify: `Tests/Tests.csproj`（加 TestHost 包）

- [ ] **Step 1: Ui.csproj 加 ASP.NET Core 框架引用与 WebView2 包**

在 `</PropertyGroup>`（第 39 行 `CsWinRTAotOptimizerEnabled` 块之后、`<ItemGroup>` PackageReference 区域）加入：

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2792.45" />
</ItemGroup>
```

- [ ] **Step 2: 修复 Tests 工程目标框架（仓库现状：net6 引用 net9 的 Ui 报 NU1201，`dotnet test` 无法还原）**

`Tests/Tests.csproj` 改动：

```xml
<!-- TargetFramework 从 net6.0-windows10.0.17763.0 改为与 Ui 默认配置一致 -->
<TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
```

```xml
<ItemGroup>
  <!-- 新增：Minimal API 管道集成测试宿主（与 net9 对齐） -->
  <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="9.0.0" />
</ItemGroup>
```

注意：MSTest 2.2.7 若在 net9 下报兼容错误，将 `MSTest.TestAdapter`/`MSTest.TestFramework` 升级到 `3.6.4`、`Microsoft.NET.Test.Sdk` 升级到 `17.12.0`（测试代码无需改动）。验证：`dotnet test Tests/Tests.csproj` 能还原并跑通**现有**测试（这是后续所有任务测试循环的前提；现有测试若有个别与本次改动无关的既有失败，记录并保持原状，不算本任务失败）。

- [ ] **Step 3: 验证编译**

Run: `dotnet build Ui/Ui.csproj`
Expected: Build succeeded（无新 warning 关于 FrameworkReference）

- [ ] **Step 4: Commit**

```bash
git add Ui/Ui.csproj Tests/Tests.csproj
git commit -m "chore: add AspNetCore framework ref, WebView2 and TestHost packages"
```

### Task 2: WebUiServer 宿主 + Token 中间件 + /api/version

**Files:**
- Create: `Ui/Service/WebUi/WebUiServer.cs`
- Create: `Ui/Service/WebUi/TokenMiddleware.cs`
- Create: `Ui/Service/WebUi/WebUiEndpoints.cs`
- Test: `Tests/Service/WebUi/WebUiServerTests.cs`

- [ ] **Step 1: 写失败测试**

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Service.WebUi
{
    [TestClass]
    public class WebUiServerTests
    {
        private static HttpClient CreateClient(string? token)
        {
            // 直接构建与生产相同的管道（token 规则一致），用 TestServer 驱动
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            var tokenInPipe = token; // null 表示"服务端未启用 token"（DEBUG 形态）
            app.UseMiddleware<_1RM.Service.WebUi.TokenMiddleware>(tokenInPipe ?? "");
            app.MapGet("/api/version", () => new { version = "test", api = 1 });
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            var client = app.GetTestClient();
            return client;
        }

        [TestMethod]
        public async Task VersionEndpoint_NoTokenRequired_Returns200()
        {
            using var client = CreateClient(null);
            var resp = await client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_MissingToken_Returns401()
        {
            using var client = CreateClient("secret123");
            var resp = await client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_CorrectBearer_Returns200()
        {
            using var client = CreateClient("secret123");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "secret123");
            var resp = await client.GetAsync("/api/version");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public async Task WithTokenEnabled_QueryToken_Returns200()
        {
            using var client = CreateClient("secret123");
            var resp = await client.GetAsync("/api/version?token=secret123");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test Tests/Tests.csproj --filter WebUiServerTests`
Expected: 编译失败（`_1RM.Service.WebUi.TokenMiddleware` 不存在）

- [ ] **Step 3: 实现 TokenMiddleware**

`Ui/Service/WebUi/TokenMiddleware.cs`：

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace _1RM.Service.WebUi
{
    /// <summary>Release 形态下校验 token（Bearer 或 ?token=）；token 为空串（DEBUG 形态）时放行。</summary>
    public class TokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _token;

        public TokenMiddleware(RequestDelegate next, string token)
        {
            _next = next;
            _token = token;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!string.IsNullOrEmpty(_token))
            {
                var provided = context.Request.Query["token"].ToString();
                if (string.IsNullOrEmpty(provided))
                {
                    var auth = context.Request.Headers.Authorization.ToString();
                    if (auth.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
                        provided = auth["Bearer ".Length..].Trim();
                }
                if (provided != _token)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await _next(context);
        }
    }
}
```

- [ ] **Step 4: 实现 WebUiServer 与版本端点**

`Ui/Service/WebUi/WebUiServer.cs`：

```csharp
using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace _1RM.Service.WebUi
{
    /// <summary>进程内本地 Web 服务：仅监听回环地址。DEBUG 固定端口免 token，Release 随机端口 + token。</summary>
    public static class WebUiServer
    {
        private static WebApplication? _app;
        public static int Port { get; private set; }
        public static string Token { get; private set; } = string.Empty;
        public static bool IsRunning => _app != null;

        public static void Start()
        {
            if (_app != null) return;
#if DEBUG
            Port = 17321;
            Token = string.Empty;
#else
            Port = Random.Shared.Next(18000, 25000);
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
#endif
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders(); // 避免与 SimpleLogHelper 重复
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, Port));
            var app = builder.Build();
            app.UseMiddleware<TokenMiddleware>(Token);
            WebUiEndpoints.MapAll(app);
            _app = app;
            _ = app.RunAsync(); // 后台运行，不阻塞 WPF 启动
        }

        public static async Task StopAsync()
        {
            if (_app == null) return;
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
```

`Ui/Service/WebUi/WebUiEndpoints.cs`（本任务先只有 version，后续任务在此文件追加）：

```csharp
using _1RM.AppVersion;

namespace _1RM.Service.WebUi
{
    public static class WebUiEndpoints
    {
        public static void MapAll(WebApplication app)
        {
            app.MapGet("/api/version", () => Results.Json(new
            {
                version = _1RM.AppVersion.Version,
                api = 1,
            }));
        }
    }
}
```

注意：版本号取值参照 `Ui/AppVersion.cs` 中现有的版本常量名（执行时打开该文件确认实际类名/属性名并修正 `_1RM.AppVersion.Version`）。

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet test Tests/Tests.csproj --filter WebUiServerTests`
Expected: 4 个测试 PASS

- [ ] **Step 6: Commit**

```bash
git add Ui/Service/WebUi/ Tests/Service/WebUi/
git commit -m "feat(webui): in-process kestrel server with token middleware and version endpoint"
```

### Task 3: DTO 与映射器

**Files:**
- Create: `Ui/Service/WebUi/WebUiDto.cs`
- Create: `Ui/Service/WebUi/DtoMapper.cs`
- Test: `Tests/Service/WebUi/DtoMapperTests.cs`

背景：列表页数据源是 `GlobalData.VmItemList`（元素 `ProtocolBaseViewModel`，其 `.Server` 为协议对象）。DTO 必须零 WPF 类型（无 BitmapSource 等）。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Collections.Generic;
using System.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;

namespace Tests.Service.WebUi
{
    [TestClass]
    public class DtoMapperTests
    {
        // 纯函数测试，不需要 TestInit/IoC
        [TestMethod]
        public void Map_RdpServer_FieldsComplete()
        {
            var rdp = new RDP
            {
                Id = "01J8Z",
                DisplayName = "WIN-PROD-01",
                Address = "192.168.1.10",
                Port = "3389",
            };
            rdp.Tags = new List<string> { "生产环境", "monitor" };
            rdp.ColorHex = "#2c5aff"; // 若实际属性名不同（如 Color），以 ProtocolBase.cs 为准修正
            rdp.TreeNodes = new List<string> { "生产环境", "Web 集群" };

            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(rdp, dataSourceName: "Local");

            Assert.AreEqual("WIN-PROD-01", dto.DisplayName);
            Assert.AreEqual("RDP", dto.Protocol);
            Assert.AreEqual("192.168.1.10", dto.Address);
            Assert.AreEqual(2, dto.Tags.Count);
            Assert.AreEqual("生产环境/Web 集群", dto.FolderPath);
            Assert.AreEqual("Local", dto.DataSourceName);
            Assert.AreEqual("disconnected", dto.ConnectionState); // 预留字段默认值
        }

        [TestMethod]
        public void Map_NullTreeNodes_RootFolder()
        {
            var ssh = new SSH { Id = "x", DisplayName = "h", Address = "1.1.1.1" };
            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(ssh, "Local");
            Assert.AreEqual(string.Empty, dto.FolderPath);
        }
    }
}
```

（执行时打开 `Ui/Model/Protocol/Base/ProtocolBase.cs` 核对 Tags/TreeNodes/颜色/IconBase64 的真实属性名并修正测试。）

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test Tests/Tests.csproj --filter DtoMapperTests`
Expected: 编译失败（DtoMapper 不存在）

- [ ] **Step 3: 实现 WebUiDto.cs**

```csharp
using System.Collections.Generic;

namespace _1RM.Service.WebUi
{
    public class ServerDto
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string Color { get; set; } = string.Empty;      // 服务器自定义色 hex
        public string IconBase64 { get; set; } = string.Empty;
        public string DataSourceName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty; // "a/b"，根为空串
        public long LastConnectTime { get; set; }              // Unix 秒，0=从未连接
        public string ConnectionState { get; set; } = "disconnected"; // 预留（spec §3.4）
    }

    public class DataSourceDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;        // sqlite/mysql/pgsql
        public string Status { get; set; } = string.Empty;      // connected/disconnected/reconnecting
        public bool Writable { get; set; } = true;
        public string ReconnectInfo { get; set; } = string.Empty;
        public int ServerCount { get; set; }
    }

    public class TagDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsPinned { get; set; }
    }
}
```

- [ ] **Step 4: 实现 DtoMapper.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using _1RM.Model;
using _1RM.Model.Protocol.Base;

namespace _1RM.Service.WebUi
{
    public static class DtoMapper
    {
        public static ServerDto FromServer(ProtocolServerBase server, string dataSourceName)
        {
            return new ServerDto
            {
                Id = server.Id,
                DisplayName = server.DisplayName,
                Protocol = server.Protocol,
                Address = server.Address ?? string.Empty,
                Port = server.Port ?? string.Empty,
                UserName = server.UserName ?? string.Empty,
                Tags = server.Tags?.ToList() ?? new List<string>(),
                Color = server.ColorHex ?? string.Empty,
                IconBase64 = server.IconBase64 ?? string.Empty,
                DataSourceName = dataSourceName,
                FolderPath = server.TreeNodes != null ? string.Join("/", server.TreeNodes) : string.Empty,
                LastConnectTime = 0, // Task 4 从 LocalityConnectRecorder 填充
                ConnectionState = "disconnected",
            };
        }
    }
}
```

（`server.Address/Port/UserName/ColorHex/IconBase64` 的真实属性名以 `ProtocolBaseWithAddressPort.cs` / `ProtocolBase.cs` 为准——RDP 与 SSH 的这些属性分散在基类，执行时核对。）

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet test Tests/Tests.csproj --filter DtoMapperTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Ui/Service/WebUi/WebUiDto.cs Ui/Service/WebUi/DtoMapper.cs Tests/Service/WebUi/DtoMapperTests.cs
git commit -m "feat(webui): server/DataSource/Tag DTOs with mapper from protocol layer"
```

### Task 4: 只读数据端点（servers / datasources / tags）

**Files:**
- Modify: `Ui/Service/WebUi/WebUiEndpoints.cs`
- Modify: `Ui/Service/WebUi/DtoMapper.cs`（补 LastConnectTime、数据源/标签映射）
- Test: `Tests/Service/WebUi/ReadEndpointsTests.cs`

数据来源（执行时核对类成员名）：
- 服务器清单：`IoC.Get<GlobalData>().VmItemList`（每项 `.Server` 与 `.DataSource`）；最近连接时间：`IoC.Get<LocalityConnectRecorder>()` 的记录（若 `ProtocolBaseViewModel` 上已有 `LastConnectTime` 则直接用）。
- 数据源：`IoC.Get<DataSourceService>()`（`LocalDataSource` + `AdditionalSources`，各源 `Status/IsWritable/ReconnectInfo`）。
- 标签：`IoC.Get<GlobalData>()` 的标签聚合（`GlobalData_Tag.cs`），置顶来自 `LocalityTagService`。

- [ ] **Step 1: 重建精简版 TestInit 测试夹具（原文件已在 Task 1 清理中删除）**

新建 `Tests/TestInit.cs`（用当前 API，勿参考 git 历史旧版——旧版引用的 API 已不存在）：

```csharp
// 新增静态字段（类顶部）：
private static KeywordMatchService? _keywordMatchService;
private static GlobalData? _globalData;
private static DataSourceService? _dataSourceService;

// GetByType lambda 中追加（缓存返回，注意 KeywordMatchService 为无参构造、非泛型）：
if (type == typeof(KeywordMatchService))
    return _keywordMatchService ??= new KeywordMatchService();
if (type == typeof(DataSourceService) && _dataSourceService != null)
    return _dataSourceService; // 若已有注册行则改为同样走缓存
if (type == typeof(GlobalData))
    return _globalData ??= new GlobalData(); // 构造参数以 GlobalData.cs 为准
```

并在 `Init()` 末尾初始化数据链路（`VmItemList` 仅由 `ReloadAll()` 填充，且 `ReloadAll` 要求先 `SetDataSourceService` 注入；`LocalDataSource` 为 get-only，须经 `InitLocalDataSource` 打开 SQLite）并植入种子数据：

```csharp
_dataSourceService = _dataSourceService ?? new DataSourceService();
var sqliteConfig = new SqliteSource { Name = "Local", Path = Path.Combine(Path.GetTempPath(), "tests-1rm.db") };
_dataSourceService.InitLocalDataSource(sqliteConfig); // 成员名以 DataSourceService.cs 为准
_globalData = _globalData ?? new GlobalData();
_globalData.SetDataSourceService(_dataSourceService); // 若方法名不同以 GlobalData.cs 为准
_globalData.ReloadAll(true);
_globalData.AddServer(new RDP { Id = "seed-rdp", DisplayName = "seed-rdp", Address = "1.1.1.1" },
    _dataSourceService.LocalDataSource); // AddServer 为双参数 (ProtocolBase, DataSourceBase)
```

**Step 2: 写失败测试**（模式同 Task 2：TestServer 挂真实端点；`[ClassInitialize]` 先 `TestInit.Init()` 再 `app.StartAsync()`）

```csharp
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    [TestClass]
    public class ReadEndpointsTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init(); // 初始化 SQLite 测试库 + mock IoC
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 与 Task 2 一致，避免启动竞态
            _client = app.GetTestClient();
        }

        [TestMethod]
        public async Task GetServers_ReturnsJsonArray()
        {
            var resp = await _client.GetAsync("/api/servers");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "\"id\"");
        }

        [TestMethod]
        public async Task GetDatasources_ReturnsArrayWithLocal()
        {
            var resp = await _client.GetAsync("/api/datasources");
            var body = await resp.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "Local");
        }

        [TestMethod]
        public async Task GetTags_ReturnsJsonArray()
        {
            var resp = await _client.GetAsync("/api/tags");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }
    }
}
```

- [ ] **Step 3: 跑测试确认失败**

Run: `dotnet test Tests/Tests.csproj --filter ReadEndpointsTests`
Expected: FAIL（路由不存在 → 404）

- [ ] **Step 4: 实现端点**（`WebUiEndpoints.MapAll` 追加）

```csharp
app.MapGet("/api/servers", () =>
{
    var gd = IoC.Get<GlobalData>();
    lock (gd.VmItemList) // 与现有刷新锁一致，执行时对齐 GlobalData 的锁策略
    {
        var list = gd.VmItemList
            .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSource))
            .ToList();
        return Results.Json(list);
    }
});

app.MapGet("/api/datasources", () =>
{
    var dss = IoC.Get<DataSourceService>();
    var all = new[] { dss.LocalDataSource }.Concat(dss.AdditionalSources.Values);
    return Results.Json(all.Select(DtoMapper.FromDataSource));
});

app.MapGet("/api/tags", () =>
{
    var gd = IoC.Get<GlobalData>();
    return Results.Json(DtoMapper.BuildTags(gd));
});
```

`DtoMapper` 补充 `FromDataSource(DataSourceBase)` 与 `BuildTags(GlobalData)`（成员名以 `Service/DataSource/DataSourceService.cs`、`Model/GlobalData_Tag.cs` 为准）。注意：端点在 Kestrel 线程访问 `VmItemList`，若 `FromServer` 内部触及 WPF 依赖（如 `IconImg`）则只取 `IconBase64` 字符串字段，绝不触碰 `BitmapSource` 属性。

- [ ] **Step 5: 跑测试确认通过** → `dotnet test Tests/Tests.csproj --filter ReadEndpointsTests` PASS

- [ ] **Step 6: Commit** `feat(webui): read-only endpoints for servers, datasources, tags`

### Task 5: 搜索端点（服务端 KeywordMatchService 复用）

**Files:**
- Modify: `Ui/Service/WebUi/WebUiEndpoints.cs`
- Test: `Tests/Service/WebUi/SearchEndpointTests.cs`

语义（spec §3.1 修正）：拼音/多关键词/`#tag` 语法全部在服务端执行——复用 `KeywordMatchService` 与 `TagAndKeywordEncodeHelper`，与 WPF 主窗口过滤同一逻辑，前端不自写过滤器。

- [ ] **Step 1: 写失败测试**

```csharp
// 关键断言：中文关键词"生产"能命中 "WIN-PROD-01"（若其 tag 为 生产环境）；
// 多关键词空格分隔交集；无匹配返回空数组。
[TestMethod]
public async Task Search_ByTagKeyword_FiltersServers() { /* 植入 2 台服务器，1 台带 tag，断言只返回它 */ }
[TestMethod]
public async Task Search_NoMatch_ReturnsEmpty() { /* 断言 200 + [] */ }
```

（测试数据植入方式参考 `Tests/Service/DataServiceTests.cs` 现有做法——直接调 `IoC.Get<GlobalData>().AddServer()` 或数据库层插入。）

- [ ] **Step 2: 跑测试确认失败**

- [ ] **Step 3: 实现端点**

```csharp
app.MapGet("/api/search", (string q) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.Json(Array.Empty<object>());
    var gd = IoC.Get<GlobalData>();
    var matcher = IoC.Get<KeywordMatchService>();
    // 复用主窗口的匹配路径：执行时打开 View/ServerView/ServerPageViewModelBase.cs
    // 的 CalcServerVisibleAndFilter（或 TagAndKeywordEncodeHelper.MatchKeywords），
    // 提取其中"#tag 解码 + 多关键词匹配"为可静态调用的辅助方法供两端共用，
    // 不要复制粘贴实现（DRY）。
    var matched = FilterHelpers.MatchServers(gd.VmItemList, matcher, q);
    return Results.Json(matched.Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSource)).ToList());
});
```

注意：若现有过滤逻辑与 ViewModel 耦合无法直接复用，则提取纯函数到 `Ui/Service/WebUi/FilterHelpers.cs`（输入 vm 列表 + 关键词，输出匹配列表），并让 `ServerPageViewModelBase` 改调它（重构保持行为，现有测试兜底）。

- [ ] **Step 4: 跑测试确认通过**（含手动对照：与 WPF 主窗口同一关键词结果一致）

- [ ] **Step 5: Commit** `feat(webui): search endpoint reusing KeywordMatchService`

### Task 6: 连接端点

**Files:**
- Modify: `Ui/Service/WebUi/WebUiEndpoints.cs`
- Test: `Tests/Service/WebUi/ConnectEndpointTests.cs`

行为：`POST /api/connect/{id}` → 在 `GlobalData.VmItemList` 找到目标 → 触发 `GlobalEventHelper.OnRequestServerConnect(server, fromView: "WebUi")`（事件签名以 `Ui/Model/GlobalEventHelper.cs` 为准）。缺密码等交互弹窗仍走本地 WPF 管线（现有 `SessionControlService.Connect()` 内部逻辑），本计划不改动它——Web 发起、桌面弹窗补密，行为与评估报告 §4.6 一致。

- [ ] **Step 1: 写失败测试**

```csharp
[TestMethod]
public async Task Connect_UnknownId_Returns404()
{
    var resp = await _client.PostAsync("/api/connect/not-exist", null);
    Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
}

[TestMethod]
public async Task Connect_KnownId_FiresEvent()
{
    var fired = false;
    _1RM.Model.GlobalEventHelper.OnRequestServerConnect += (s, v, t, r, c) => fired = true;
    // 签名以 GlobalEventHelper.cs 实际为准修正 lambda 参数
    var resp = await _client.PostAsync("/api/connect/" + _testServerId, null);
    Assert.IsTrue(fired);
}
```

- [ ] **Step 2: 跑测试确认失败** → **Step 3: 实现**

```csharp
app.MapPost("/api/connect/{id}", (string id) =>
{
    var vm = IoC.Get<GlobalData>().VmItemList.FirstOrDefault(x => x.Server.Id == id);
    if (vm == null) return Results.NotFound();
    GlobalEventHelper.OnRequestServerConnect?.Invoke(vm.Server, "WebUi");
    return Results.Ok(new { started = true });
});
```

- [ ] **Step 4: 跑测试确认通过** → **Step 5: Commit** `feat(webui): connect endpoint firing OnRequestServerConnect`

### Task 7: SSE 数据版本推送

**Files:**
- Modify: `Ui/Service/WebUi/WebUiEndpoints.cs`
- Test: `Tests/Service/WebUi/SseEndpointTests.cs`

行为：`GET /api/events` 建立长连接；订阅 `GlobalData.OnReloadAll`（执行时核对该事件的真实签名与触发点 `GlobalData_Timer.cs`）；每次数据重载推 `event: reload\ndata: {version}\n\n`；每 15 秒发 `: heartbeat` 注释行保活；断开时取消订阅。

- [ ] **Step 1: 写失败测试**（TestServer 下用 `HttpCompletionOption.ResponseHeadersRead` 读流，断言收到一条 `reload` 事件——测试中手动调用一次 `GlobalData.ReloadAll(true)` 触发）

- [ ] **Step 2: 跑测试确认失败** → **Step 3: 实现**

```csharp
app.MapGet("/api/events", async (HttpContext ctx) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    var gd = IoC.Get<GlobalData>();
    var version = 0;
    void OnReload() => Interlocked.Increment(ref version);
    gd.OnReloadAll += OnReload; // 注意：OnReloadAll 是 GlobalData 的实例字段（非静态）
    try
    {
        await ctx.Response.WriteAsync(": connected\n\n", ctx.RequestAborted);
        var lastSent = -1;
        while (!ctx.RequestAborted.IsCancellationRequested)
        {
            if (version != lastSent)
            {
                lastSent = version;
                await ctx.Response.WriteAsync($"event: reload\ndata: {version}\n\n", ctx.RequestAborted);
            }
            else
            {
                await ctx.Response.WriteAsync(": heartbeat\n\n", ctx.RequestAborted);
            }
            await Task.Delay(15000, ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { /* 客户端断开 */ }
    finally { gd.OnReloadAll -= OnReload; }
});
```

（`OnReloadAll` 若为带参事件或委托类型不同，按实际签名调整；轮询 `version` 变量的方式避免了跨线程写流的复杂度。）

- [ ] **Step 4: 跑测试确认通过** → **Step 5: Commit** `feat(webui): SSE endpoint pushing data version on reload`

### Task 8: 外观与树状态端点

**Files:**
- Modify: `Ui/Service/WebUi/WebUiEndpoints.cs`、`Ui/Service/WebUi/WebUiDto.cs`（AppearanceDto）
- Test: `Tests/Service/WebUi/AppearanceEndpointTests.cs`

- `GET/PUT /api/settings/appearance`：读写主题设置 `{ themeMode: "dark"|"light"|"system", accent: "blue"|…, fontSize: "S"|"M"|"L"|"XL", font: "" }`（font 为界面字体族，空串=跟随系统）。持久化复用 `IoC.Get<ConfigurationService>()`——**已落地为 Configuration 平铺字段 `WebUiThemeMode/WebUiAccent/WebUiFontSize/WebUiFontFamily`**（Task 8 实现选择，不动 WPF 现有 ThemeConfig）；PUT 后同时写 `1Remote.json`（调现有 Save）。PUT 为全量替换语义：四个字段缺一不可。
- `GET/PUT /api/ui-state/tree`：代理 `LocalityTreeViewService` 的展开状态与自定义顺序（序列化为 `{ expanded: [path…], order: {path: [children…]} }`；执行时核对 `LocalityTreeViewService` 的公开成员，直接 JSON 化其字典）。

- [ ] **Step 1: 写失败测试**（GET 返回默认值 dark/blue；PUT 后 GET 回读一致；断言 `1Remote.json` 落盘）
- [ ] **Step 2: 跑测试确认失败** → **Step 3: 实现**（照上述契约）→ **Step 4: 跑测试确认通过**
- [ ] **Step 5: Commit** `feat(webui): appearance and tree-state persistence endpoints`

### Task 9: WebView2 壳与引擎开关（手动验证任务）

**Files:**
- Modify: `Ui/View/MainWindowView.xaml`（内容区最外层 Grid 新增 WebView2 控件）
- Modify: `Ui/View/MainWindowView.xaml.cs`（导航逻辑）
- Modify: `Ui/Service/ConfigurationService.cs` 的 GeneralConfig（加 `UiEngine` 字段，默认 `"Desktop"`）
- Modify: `Ui/View/Settings/General/GeneralSettingView.xaml(.cs)`（加"界面引擎"下拉：Desktop/Web）
- Modify: `Ui/AppInit.cs`（启动时 `WebUiServer.Start()`，仅当 `UiEngine == "Web"` 或 DEBUG）

- [ ] **Step 1: GeneralConfig 加字段**

```csharp
// Ui/Service/ConfigurationService.cs → GeneralConfig 类
public string UiEngine { get; set; } = "Desktop"; // "Desktop" | "Web"
```

- [ ] **Step 2: MainWindowView.xaml 加 WebView2**

在最外层 Grid 的**最后一个子元素**位置（覆盖在现有三层 ContentControl 之上）加：

```xml
<wpf:WebView2 x:Name="WebUI" Visibility="Collapsed"
              xmlns:wpf="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"/>
```

- [ ] **Step 3: MainWindowView.xaml.cs 导航**

```csharp
public void ShowWebUi()
{
#if DEBUG
    WebUI.Source = new Uri("http://localhost:5173"); // Vite 开发服务器
#else
    WebUI.Source = new Uri($"http://127.0.0.1:{WebUiServer.Port}/?token={WebUiServer.Token}");
#endif
    WebUI.Visibility = Visibility.Visible;
}
public void HideWebUi() { WebUI.Visibility = Visibility.Collapsed; WebUI.Source = null; }
```

引擎切换时：`UiEngine == "Web"` → `ShowWebUi()`，否则 `HideWebUi()`；设置页改下拉后立即生效。

- [ ] **Step 4: AppInit 启动服务**

在 `InitOnLaunch()`（托盘初始化附近）加：

```csharp
try
{
    _1RM.Service.WebUi.WebUiServer.Start();
    if (IoC.Get<_1RM.Service.Configuration>().General.UiEngine == "Web") // 属性名为 General（类型 GeneralConfig）
        IoC.Get<MainWindowView>().ShowWebUi();
}
catch (Exception e) { SimpleLogHelper.Error(e); /* Web 服务失败不阻断桌面版 */ }
```

- [ ] **Step 5: 手动验证**

1. DEBUG 下运行 `dotnet build` + 启动 1Remote → 设置页切引擎为 Web → 内容区显示 Vite 页面（此时前端工程还没建，显示 5173 拒绝连接属预期）
2. 切回 Desktop → WPF 列表完好无损
3. `dotnet test Tests/Tests.csproj` 全绿（无回归）

- [ ] **Step 6: Commit** `feat(webui): WebView2 shell in main window with engine switch`

---

# Phase B：前端工程（Task 10-21）

> 前端任务的完成标准：功能实现 + `npm run dev` 手动验证清单逐项通过 + `npm run build` 零报错。后端须已按 Task 2-8 在 DEBUG 模式运行（`17321`，Vite proxy 转发）。

### Task 10: 前端工程初始化

**Files:**
- Create: `webui/package.json`、`webui/vite.config.js`、`webui/index.html`、`webui/src/main.js`、`webui/src/App.vue`
- Modify: `.gitignore`（加 `webui/node_modules/`、`webui/dist/`）

- [ ] **Step 1: 脚手架**

```bash
cd D:/DemoProject/1Remote
npm create vite@latest webui -- --template vue
cd webui && npm install
npm install naive-ui vue-router@4 vue-i18n@9 @vueuse/core vuedraggable@next
```

- [ ] **Step 2: vite.config.js（dev 代理到后端）**

```javascript
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: { '/api': 'http://127.0.0.1:17321' },
  },
  build: { outDir: 'dist' },
})
```

- [ ] **Step 3: 验证**

Run: `npm run dev` → 浏览器开 `http://localhost:5173`
Expected: Vite 默认页正常（后端未起不影响静态页）

- [ ] **Step 4: Commit** `chore(webui): scaffold vite + vue3 project with naive-ui`

### Task 11: api 封装（fetch + token + SSE）

**Files:**
- Create: `webui/src/api/index.js`

```javascript
// token：生产模式由外壳 URL ?token=xxx 注入；开发模式（Vite 代理直连 DEBUG 后端）不需要
let token = ''
{
  const q = new URLSearchParams(location.search).get('token')
  if (q) {
    token = q
    sessionStorage.setItem('1r-token', token)
    history.replaceState(null, '', location.pathname) // 清掉 URL 中的 token
  } else {
    token = sessionStorage.getItem('1r-token') || ''
  }
}

async function request(path, { method = 'GET', body } = {}) {
  const headers = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  const resp = await fetch(path, { method, headers, body: body ? JSON.stringify(body) : undefined })
  if (resp.status === 401) throw new Error('unauthorized')
  if (!resp.ok) throw new Error(`${resp.status} ${path}`)
  return resp.json()
}

export const api = {
  version: () => request('/api/version'),
  servers: () => request('/api/servers'),
  datasources: () => request('/api/datasources'),
  tags: () => request('/api/tags'),
  search: (q) => request(`/api/search?q=${encodeURIComponent(q)}`),
  connect: (id) => request(`/api/connect/${id}`, { method: 'POST' }),
  getAppearance: () => request('/api/settings/appearance'),
  saveAppearance: (a) => request('/api/settings/appearance', { method: 'PUT', body: a }),
  getTreeState: () => request('/api/ui-state/tree'),
  saveTreeState: (s) => request('/api/ui-state/tree', { method: 'PUT', body: s }),
}

/** 订阅数据版本；返回取消函数。onReload 在每次 reload 事件时回调。 */
export function subscribeEvents(onReload) {
  const es = new EventSource('/api/events' + (token ? `?token=${token}` : ''))
  es.addEventListener('reload', onReload)
  return () => es.close()
}
```

- [ ] **Step 1: 实现**（如上） → **Step 2: 验证**：DEBUG 后端运行时，浏览器 console 执行 `(await import('./src/api')).api.version()` 返回版本 JSON → **Step 3: Commit** `feat(webui): api client with token handling and SSE subscription`

### Task 12: 主题系统（双轨 CSS 变量 + Naive themeOverrides + 持久化）

**Files:**
- Create: `webui/src/themes/theme.css`
- Create: `webui/src/themes/index.js`

- [ ] **Step 1: theme.css——两层变量**（变量名即 spec §4 契约，全部前端样式的唯一取色来源）

```css
:root { --accent: #2c5aff; --accent-hover: #4d73ff; --accent-container: #16203a; --accent-text: #7c9bff; }
[data-theme="dark"] {
  --bg: #0f1011; --bg-panel: #131417; --bg-elevated: #1a1b1f; --bg-hover: #17181c;
  --border: #26272b; --border-strong: #2c2e33;
  --text-1: #e8e9ea; --text-2: #c9cdd4; --text-3: #8b8f98; --text-4: #565a63;
  --danger: #ef6a6a; --success: #26a269; --warning: #f0b25f;
}
[data-theme="light"] {
  --bg: #fafafa; --bg-panel: #ffffff; --bg-elevated: #ffffff; --bg-hover: #f4f4f5;
  --border: #e4e4e7; --border-strong: #d4d4d8;
  --text-1: #18181b; --text-2: #3f3f46; --text-3: #71717a; --text-4: #a1a1aa;
  --danger: #dc2626; --success: #16a34a; --warning: #d97706;
}
[data-accent="blue"]   { --accent: #2c5aff; --accent-hover: #4d73ff; --accent-container: #16203a; --accent-text: #7c9bff; }
[data-accent="violet"] { --accent: #8b5cf6; --accent-hover: #a78bfa; --accent-container: #1d1626; --accent-text: #b8a6f7; }
[data-accent="pink"]   { --accent: #ec4899; --accent-hover: #f472b6; --accent-container: #26141e; --accent-text: #f5a8cd; }
[data-accent="red"]    { --accent: #ef4444; --accent-hover: #f87171; --accent-container: #26120f; --accent-text: #f5a09a; }
[data-accent="orange"] { --accent: #f97316; --accent-hover: #fb923c; --accent-container: #241d12; --accent-text: #f0b25f; }
[data-accent="green"]  { --accent: #10b981; --accent-hover: #34d399; --accent-container: #12241a; --accent-text: #5fd38d; }
[data-accent="slate"]  { --accent: #64748b; --accent-hover: #7c8ba1; --accent-container: #1b2028; --accent-text: #9aa8bb; }
/* 亮色基底下的 accent-container 用色需在实现时按可读性微调（低饱和浅底） */
```

- [ ] **Step 2: themes/index.js**

```javascript
import { reactive, computed } from 'vue'
import { darkTheme, lightTheme } from 'naive-ui'
import { api } from '../api'

export const ACCENTS = ['blue', 'violet', 'pink', 'red', 'orange', 'green', 'slate']
// 强调色 → Naive UI primaryColor 映射（与 theme.css 中 --accent 保持一致）
export const ACCENT_HEX = {
  blue: '#2c5aff', violet: '#8b5cf6', pink: '#ec4899', red: '#ef4444',
  orange: '#f97316', green: '#10b981', slate: '#64748b',
}
export const CLASSIC_THEMES = { // spec §4：旧 9 主题 → 预设组合
  Light: { themeMode: 'light', accent: 'blue' }, Dark: { themeMode: 'dark', accent: 'blue' },
  Wine: { themeMode: 'light', accent: 'red' }, Forest: { themeMode: 'dark', accent: 'green' },
  Greystone: { themeMode: 'light', accent: 'slate' }, Asphalt: { themeMode: 'dark', accent: 'slate' },
  Soil: { themeMode: 'light', accent: 'orange' }, SecretKey: { themeMode: 'light', accent: 'violet' },
  PRemoteM: { themeMode: 'dark', accent: 'violet' },
}

// reactive 保证切换强调色时 Naive 组件同步刷新（spec §11.3 风险点的处理）
export const themeState = reactive({ themeMode: 'dark', accent: 'blue', fontSize: 'M', systemDark: true })

export function applyTheme() {
  const resolved = themeState.themeMode === 'system'
    ? (themeState.systemDark ? 'dark' : 'light') : themeState.themeMode
  document.documentElement.dataset.theme = resolved
  document.documentElement.dataset.accent = themeState.accent
  const sizes = { S: '12px', M: '13px', L: '14px', XL: '15px' }
  document.documentElement.style.fontSize = sizes[themeState.fontSize] || '13px'
}

export function setAppearance(patch) {
  Object.assign(themeState, patch)
  applyTheme()
  // PUT 为全量替换：必须始终携带全部四个字段（含 font），否则遗漏字段会被清空
  api.saveAppearance({ themeMode: themeState.themeMode, accent: themeState.accent,
    fontSize: themeState.fontSize, font: themeState.font })
    .catch(() => {}) // 桌面后端未运行（纯浏览器预览）时静默
}

export async function initTheme() {
  const mq = matchMedia('(prefers-color-scheme: dark)')
  themeState.systemDark = mq.matches
  mq.addEventListener('change', (e) => { themeState.systemDark = e.matches; applyTheme() })
  try { Object.assign(themeState, await api.getAppearance()) } catch { /* 默认值 */ }
  applyTheme()
}

/** Naive UI 主题（含 overrides），供 n-config-provider 绑定 —— computed 保持响应式 */
export function useNaiveTheme() {
  return computed(() => {
    const resolved = themeState.themeMode === 'system'
      ? (themeState.systemDark ? 'dark' : 'light') : themeState.themeMode
    return {
      theme: resolved === 'dark' ? darkTheme : lightTheme,
      overrides: { common: { primaryColor: ACCENT_HEX[themeState.accent], primaryColorHover: ACCENT_HEX[themeState.accent] } },
    }
  })
}
```

- [ ] **Step 3: main.js 挂载**（`createApp` 前 `initTheme()`，`n-config-provider :theme="naiveTheme()"` 包裹 App——见 Task 13）
- [ ] **Step 4: 验证**：console 执行 `setAppearance({accent:'green'})` → 全页按钮/链接变绿且刷新后保持（后端已存） → **Step 5: Commit** `feat(webui): dual-track theme system with classic presets`

### Task 13: 布局骨架（顶栏/边栏/内容区/状态栏 + 路由）

**Files:**
- Create: `webui/src/App.vue`、`webui/src/router.js`、`webui/src/views/ServerListView.vue`、`webui/src/views/PlaceholderView.vue`

- [ ] **Step 1: router.js**（`/` → ServerList；`/settings`、`/editor` 等 → Placeholder，显示"计划 2/3 提供"）

- [ ] **Step 2: App.vue 骨架**（n-config-provider + n-message-provider 包裹；grid：顶栏 44px 固定 + 下方 flex（边栏 + 内容区）；顶栏含 logo、搜索框插槽、新建/排序/列按钮、设置图标）

```vue
<script setup>
import { useNaiveTheme } from './themes'
import { useRouter } from 'vue-router'
const router = useRouter()
const naive = useNaiveTheme()
</script>
<template>
  <n-config-provider :theme="naive.theme" :theme-overrides="naive.overrides" style="height: 100vh">
    <n-message-provider>
      <div class="shell">
        <header class="topbar">
          <div class="logo">1Remote</div>
          <!-- Task 13 先放只读搜索框占位；Task 17 接线防抖搜索与 Ctrl K -->
          <div class="searchbox">⌕ {{ $t('search.placeholder') }}</div>
          <div class="topbar-actions">
            <n-button quaternary size="small" @click="router.push('/settings')">⚙</n-button>
          </div>
        </header>
        <div class="main">
          <router-view />
        </div>
      </div>
    </n-message-provider>
  </n-config-provider>
</template>
<style scoped>
.shell { display: grid; grid-template-rows: 44px 1fr; height: 100vh; background: var(--bg); color: var(--text-1); }
.topbar { display: flex; align-items: center; gap: 10px; padding: 0 12px; border-bottom: 1px solid var(--border); background: var(--bg-panel); }
.main { display: flex; min-height: 0; }
</style>
```

（真实实现按 spec §3.1 样张完善：搜索框在顶栏中部、新建/排序/列按钮、状态栏在内容区底部。）

- [ ] **Step 3: 验证**：`npm run dev` 看到空骨架（顶栏+空内容区），切 `?theme=light`（临时调试手段）无样式错乱 → **Step 4: Commit** `feat(webui): app shell with topbar and router`

### Task 14: useServers 组合式函数（数据/搜索/SSE/树组装）

**Files:**
- Create: `webui/src/composables/useServers.js`

```javascript
import { ref } from 'vue'
import { api, subscribeEvents } from '../api'

// 模块级共享状态（spec §9.1：不用 Pinia）
const servers = ref([])
const datasources = ref([])
const tags = ref([])
const loading = ref(false)
let unsubscribe = null
let pollTimer = null

async function loadAll() {
  loading.value = true
  try {
    [servers.value, datasources.value, tags.value] =
      await Promise.all([api.servers(), api.datasources(), api.tags()])
  } finally { loading.value = false }
}

export function useServers() {
  if (!unsubscribe) {
    loadAll()
    unsubscribe = subscribeEvents(() => loadAll()) // SSE 版本变化 → 拉全量
    // 数据源连接状态（重连倒计时等）不触发 OnReloadAll，低频轮询兜底（顺带刷新边栏状态点）
    pollTimer = setInterval(async () => {
      try { datasources.value = await api.datasources() } catch { /* 后端未运行 */ }
    }, 30000)
  }
  return { servers, datasources, tags, loading, reload: loadAll }
}

/** 扁平列表 → 边栏树结构：[{datasource, status, servers: [], folders: [{name, path, servers, folders}]}] */
export function buildTree(servers) {
  const roots = []
  for (const ds of new Set(servers.map(s => s.dataSourceName))) {
    const dsServers = servers.filter(s => s.dataSourceName === ds)
    const root = { name: ds, folders: [], servers: [] }
    for (const s of dsServers) {
      const parts = s.folderPath ? s.folderPath.split('/') : []
      let node = root
      for (const p of parts) {
        let f = node.folders.find(x => x.name === p)
        if (!f) { f = { name: p, path: (node.path ? node.path + '/' : '') + p, folders: [], servers: [] }; node.folders.push(f) }
        node = f
      }
      node.servers.push(s)
    }
    roots.push(root)
  }
  return roots
}
```

- [ ] **Step 1: 实现** → **Step 2: 验证**：Vue devtools 中 `servers` 有数据、SSE 触发后（桌面端改一台服务器）列表自动刷新 → **Step 3: Commit** `feat(webui): useServers composable with tree builder and SSE refresh`

### Task 15: 边栏树组件（SideTree）

**Files:**
- Create: `webui/src/components/SideTree.vue`

要点（spec §3.2/§3.3）：数据源=根节点（名称+类型+状态点：绿/灰/红+重连倒计时）；文件夹树（递归组件，展开箭头+文件夹名+计数；叶子=服务器行只读显示图标+名称，双击=连接）；「标签」区（chips+计数，点击=过滤，`+ 管理`按钮→toast"计划 3"）；底部「收起边栏」。展开状态经 `api.getTreeState/saveTreeState` 持久化（防抖 500ms）。选中节点（数据源根/文件夹）向父组件 emit `{dataSourceName, folderPath}` 过滤列表。

- [ ] **Step 1: 实现**（n-tree 或自写递归皆可——推荐 n-tree：`block-line`、`selected-keys`、`on-update:selected`、`render-label` 定制图标/计数/状态点；服务器叶子用 `nodeType` 区分双击行为）
- [ ] **Step 2: 验证清单**：多数据源并列显示且状态点正确；点击根节点/文件夹过滤列表；双击叶子服务器触发连接 toast；展开状态刷新后保持 → **Step 3: Commit** `feat(webui): sidebar tree with datasource roots, folders and tags`

### Task 16: 行列表组件（ServerTable + ServerRow）

**Files:**
- Create: `webui/src/components/ServerTable.vue`
- Create: `webui/src/components/ServerRow.vue`
- Create: `webui/src/components/ProtocolBadge.vue`、`webui/src/components/StatusDot.vue`

要点（spec §3.4，对齐已确认样张 v2）：自写轻量表格（div 行 + flex 列——列配置数组驱动）；列=复选框/状态(预留,恒 `—`)/图标+名称/地址/协议徽章/标签/文件夹(仅根视图)/最近连接(Intl.RelativeTimeFormat)/hover 操作(▸ 连接 ✎ 编辑→toast 计划 2)；表头点击排序（名称/地址自然 IP/协议/最近连接，升降切换）；多选 Ctrl/Shift；行右键菜单（n-dropdown：连接/编辑/复制地址/删除→toast 计划 2，快捷键提示对齐 spec §8.2）；图标 22px 圆角块（`IconBase64`，底色=服务器 Color 的低饱和变体，无图标用协议默认图标 + 协议色）；批量操作条（选中 ≥1 时显示于面包屑行右侧：连接/批量编辑/导出/取消）。

自然 IP 排序（客户端版，对齐 `SubTitleSortByNaturalIp` 语义）：按 `.` 分段逐段数值比较。

- [ ] **Step 1: 实现 StatusDot/ProtocolBadge（协议→色映射表：RDP 蓝/SSH 绿/SFTP 橙/FTP 黄/VNC 紫/Telnet 青/Serial 灰/APP 灰蓝/RdpApp 蓝）**
- [ ] **Step 2: 实现 ServerRow（含 hover 操作、右键菜单 emit）**
- [ ] **Step 3: 实现 ServerTable（列配置、排序、多选、批量条、空态插槽）**
- [ ] **Step 4: 验证清单**：排序四列正确（IP 段比较）；Ctrl/Shift 多选与批量条出现/消失；右键菜单项与快捷键提示显示；状态列全部为 `—`；图标/颜色条渲染 → **Step 5: Commit** `feat(webui): server table with sort, multi-select and context menu`

### Task 17: 搜索与 Ctrl K

**Files:**
- Modify: `webui/src/App.vue`（顶栏搜索框）、`webui/src/composables/useServers.js`（搜索状态）

要点：防抖 200ms；空串=清除过滤；非空调 `api.search`，得到 id 集合后与当前树过滤**取交集**展示；`Ctrl K`（全局 keydown，preventDefault）聚焦搜索框，`Esc` 清空；搜索进行中输入框显示 spinner。

- [ ] **Step 1: 实现** → **Step 2: 验证**：拼音首字母（如 `sc` 匹配 `生产环境` tag 的服务器）与 WPF 主窗口结果一致；`#tag` 语法生效 → **Step 3: Commit** `feat(webui): debounced server-side search with ctrl+k`

### Task 18: 连接动作

**Files:**
- Modify: `ServerTable/ServerRow`（触发点接线）

要点：双击行 / Enter（选中行时）/ hover ▸ 按钮 / 右键菜单"连接"→ `api.connect(id)` → toast「已发起连接：{名称}」（成功后桌面端弹出会话窗口）；失败（404/异常）toast 错误。

- [ ] **Step 1: 实现** → **Step 2: 验证**：DEBUG 下对一台真实 RDP/SSH 服务器发起连接，桌面端会话窗口打开；连续连接两台进入同一 Tab 窗口（沿用现有 `_lastTabToken` 行为） → **Step 3: Commit** `feat(webui): connect actions via local api`

### Task 19: i18n 骨架（zh-CN / en-US）

**Files:**
- Create: `webui/src/locales/zh-CN.json`、`webui/src/locales/en-US.json`、`webui/src/locales/index.js`
- Modify: `main.js`（挂 vue-i18n）、全部组件（文案换 `t('…')`）

要点：默认语言 `navigator.language` 前两位匹配，无匹配回退 zh-CN；语言键命名沿用 WPF 资源键风格（为计划 3 的 14 语言 XAML→JSON 转换铺路）；**收尾检查：组件中不得残留硬编码用户可见文案**（spec §7）。

- [ ] **Step 1: 实现** → **Step 2: 验证**：切换浏览器语言（或 console `$i18n.locale='en-US'`）全部文案切换；`grep -rn "[\u4e00-\u9fa5]" src --include="*.vue"` 仅命中注释 → **Step 3: Commit** `feat(webui): i18n skeleton with zh-CN and en-US`

### Task 20: 空状态 / 骨架屏 / toast / 窄窗收起

**Files:**
- Modify: `ServerListView.vue`、`SideTree.vue`

要点：加载中=骨架行（6 行灰块脉动）；空库=引导卡片（"新建第一台服务器"按钮→toast 计划 2、"导入 mRemoteNG"→toast 计划 4、Launcher 热键提示）；空文件夹=轻提示；全部操作反馈走 n-message（右上角）；`useWindowSize().width < 900` 时边栏自动收起为 44px 图标条（spec §8.7）。

- [ ] **Step 1: 实现** → **Step 2: 验证**：首屏骨架→数据替换无跳动；缩窄窗口到 899px 边栏收起 → **Step 3: Commit** `feat(webui): empty states, skeleton, toast and responsive collapse`

### Task 21: 生产构建集成与端到端验收

**Files:**
- Modify: `Ui/Ui.csproj`（wwwroot 内容复制）
- Create: `webui/README.md`（开发/构建双流程说明）
- Modify: `.gitignore`

- [ ] **Step 1: 构建产物接入**

```bash
cd webui && npm run build   # 产出 webui/dist/
```

`Ui/Ui.csproj` 加：

```xml
<ItemGroup>
  <Content Include="..\webui\dist\**\*" Link="wwwroot\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

`WebUiEndpoints.MapAll` 加静态托管（`app.UseDefaultFiles(); app.UseStaticFiles();`——放在 Token 中间件之后；`WebApplication.CreateBuilder()` 默认 ContentRoot 即 exe 目录，wwwroot 需显式 `builder.WebHost.UseWebRoot` 或 `UseContentRoot(AppContext.BaseDirectory)` 指向输出目录，执行时验证）。

- [ ] **Step 2: 端到端验收（Release 形态手动清单）**

1. `dotnet build -c Release` → 启动 1Remote → 设置切引擎 Web → 内容区加载出主界面（Release 走 wwwroot + token）
2. 边栏树与 WPF 版树一致（多数据源/文件夹/计数）
3. 点根节点看全库列表，「文件夹」列出现；点文件夹该列消失、面包屑正确
4. 搜索拼音与 WPF 一致；Ctrl K / Esc 键盘流可用
5. 双击连接 → 桌面会话窗口打开（RDP 与 SSH 各验证一台）
6. 主题：深/浅/跟随系统切换 + 7 强调色 + 经典预设生效并持久化，重启后保持（设置页 UI 属计划 3，本计划经浏览器 devtools console 验证：`setAppearance(CLASSIC_THEMES.Wine)`——themes 模块需在 window 上暴露调试入口）
7. 桌面端修改服务器（WPF 引擎下编辑）→ Web 端 10 秒内自动刷新（SSE 重载推送；数据源状态点经 30s 低频轮询更新）
8. 引擎切回 Desktop → WPF 界面完全正常（回退保险）
9. `dotnet test Tests/Tests.csproj` 全绿；`npm run build` 零报错
10. 窗口 <900px 边栏收起；语言中英切换完整

- [ ] **Step 3: Commit** `feat(webui): production build integration and e2e checklist`

---

## 计划 1 完成定义（对照 spec §12）

- 主界面（树+行列表+搜索+连接）在 WebView2 中可用，与 WPF 引擎可随时互切（回退保险）
- 双轨主题 + 经典预设 + 字号生效并持久化
- zh-CN/en-US 双语无硬编码文案
- 后端端点有 MSTest 覆盖（token/DTO/只读/搜索/连接/SSE/外观）
- 状态列（`connectionState`）预留位就绪（恒 `—`）
- 编辑器/设置/凭据库/拖拽/导入导出不在本计划（分属计划 2/3/4，入口以 toast 占位）


