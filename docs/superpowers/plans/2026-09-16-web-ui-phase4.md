# 1Remote Web UI 计划 4：拖拽 / 导入导出 / 收尾精化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use checkbox syntax.

**Goal:** 补齐 spec 承诺的剩余体验项：连接状态点亮（后端）、导入导出、树/列表拖拽、虚拟滚动、列宽持久化、批量连接阈值、键盘 E/Del、静态托管回归测试、构建防护，最终整体评审收官。

**Architecture:** 后端：连接状态从 `SessionControlService` 的会话字典（`_connectionId2Hosts`）派生注入 ServerDto（Kestrel 线程快照读）；导入复用 WPF `ServerPageViewModelBase` 底层导入器（`Utils/mRemoteNG/mRemoteNGImporter`、JSON 反序列化、`Utils/RdpFile/RdpConfig`、旧库 `Utils/PRemoteM/PRemoteMTransferHelper`、Windows 凭据管理器 `Credential.Load`）包装为文件上传端点。前端：vuedraggable 树/行拖拽、useVirtualList 虚拟滚动、localStorage 列状态、确认阈值模态。测试基线 126/2。

**Tech Stack:** 同前。上游：spec §3.4/§3.5/§8/§10。

---

## 全局约定（沿用 Plan 1-3）

- 关键已知事实：`SessionControlService` partial 五文件，`_connectionId2Hosts` ConcurrentDictionary<connectionId, HostBase>（连接字典）——ServerDto.connectionState 从中派生"该服务器是否有活动会话"（含 Host 内 ProtocolServer.Id 匹配）；导入器在 `ServerPageViewModelBase.cs:252-600`（CmdImport 区域，与 VM 耦合部分需剥离复用其核心）；`PRemoteMTransferHelper`/`mRemoteNGImporter`/`RdpConfig` 均为可静态调用的 Utils；树展开持久化键 SEP=`" ]=+=+=+=>[ "`（SideTree 已有 fullKey）；vuedraggable 4.1.0 已装。
- 批量连接阈值：>5 台弹确认（控制器决策项，spec 无此要求）。键盘：E=编辑（编辑器已有）、Del=删除（已有 onDelete，需行级键盘接线）、Ctrl+D=复制。
- 原生文件选择器：**降级不做**——STA/UI 线程约束 + 模态阻塞桌面端（Plan 3 调研确认风险）；文件路径输入保持文本框，导入走 web file input 上传。记录为有意简化。

## 任务

### Task 1: 连接状态后端 + 前端点亮
Files: WebUiEndpoints.cs/DtoMapper.cs + Tests（状态派生单测：连接/断开/多会话）
- DtoMapper.FromServer 增加可选参 `bool connected`（默认 false）；/api/servers、/api/search 端点在 lock(gd) 快照后，从 `IoC.Get<SessionControlService>()` 的连接字典（读其公共属性或新增快照方法——**注意线程安全**：字典本身 Concurrent，遍历安全；不要在 Host 上调用方法）匹配 `vm.Server.Id`。状态值："connected"（1Remote 托管会话活跃——**注意**：Unhosted 会话（外部 mstsc.exe、RunWithHosting=false LocalApp）不进连接字典，将显示离线，语义收窄记录为"1Remote 托管会话状态"；host.Status 属性读取安全可考虑加"connecting"黄态，实现时评估）/"disconnected"。
- 前端：StatusDot 已有 connected 渲染分支——自动点亮。**SSE 通知为必须项（已验证）**：SessionControlService 全链路不触发 GlobalData.ReloadAll，SSE 只订阅 OnReloadAll，前端 30s 轮询只刷 datasources——连接与断开两个方向都不会刷新列表。**实现**：SessionControlService 在会话建立（ConnectWithTab/FullScreen 成功路径）与 HostBase.OnClosed 处，出锁后经后台任务触发 `IoC.Get<GlobalData>().ReloadAll()`（或直接通知 SSE 端点——选 ReloadAll 最简，副作用是 DB 重读，可接受）。**纪律：绝不在 _dictLock（MarkProtocolHostToClose 持锁区）内联触发**（ReloadAll 含 DB IO，WebUiServer.cs:14 已警告 _dictLock+同步组合死锁）——在锁外/finally/Task.Run 中触发。
- 测试：MSTest——打开连接字典注入伪条目（构造 HostBase 不现实→用反射/接口stub 或测静态派生纯函数：输入 serverId 集合+活动id集合→输出状态）。

### Task 2: 导入导出端点 + MSTest
Files: 新 WebUiImportExportService.cs + 端点 + Tests
- `POST /api/servers/import` multipart/form-data：file + ds + kind 自动嗅探（扩展名+内容：.csv→mRemoteNGImporter、.json→1Remote 导出格式（WPF CmdExportJson 的格式——读其导出代码对齐）、.rdp→RdpConfig.FromRdpFile+Windows 凭据管理器读取（TERMSRV/<host> Credential.Load，服务器上有）、.db→PRemoteM 旧库 **或 1Remote sqlite 库双格式**（ServerPageViewModelBase.cs:444-466 与 WPF 平价）。先查目标 ds IsWritable→400；**逐台插入**（映射每台 Result→added/errors，保证粒度）返回 {added, skipped, errors[]}。
- `GET /api/servers/export?ids=a,b,c&ds=` → **先 `await SecondaryVerificationHelper.VerifyAsyncUi()`（与 WPF 导出前强制验证平价，ServerPageViewModelBase.cs:281；通过后 30s 窗口复用 Plan 3 的 RevealVerifiedAtMap 语义）** → 通过则 JSON 文件下载（Content-Disposition；格式=WPF CmdExportSelectedToJson 格式：选中项 Clone+DecryptToConnectLevel → SerializeObject(List<ProtocolBase>, Indented)）；未通过 403。跨数据源 ids：按每台自身 ds 分组导出（WPF 同）。
- 复用剥离：导入核心逻辑若与 VM 耦合（MaskLayer/UI 弹窗），提取纯函数路径（读文件→解析→List<ProtocolBase>→AddServer 循环）；凭据去重插入复用 DapperDataBase 批量导入的凭据提取（调研发现其按 Hash 自动提取——已有）。
- MSTest：JSON 往返（export→import→新增）；rdp 导入（含凭据管理器不可用时的降级——测试环境 TERMSRV 凭据不存在→密码空）；CSV 嗅探。

### Task 3: 前端导入导出 UI + 批量连接阈值 + 键盘 E/Ctrl+D/Del
Files: ServerListView/ServerTable/ServerRow + locales
- 空状态"导入"按钮启用 → 模态（file input + 拖放区 + 进度/结果 toast）；批量条"导出"启用 → api 下载（blob）。
- 批量连接 >5 → n-dialog 确认（显示 N）；TagManagerModal 连接全部同样加阈值。
- 键盘：ServerTable onGlobalKey 加 'e'→编辑选中行（openEdit）、'Delete'→删除确认、'Ctrl+d'→复制（防浏览器书签默认）。光标行或勾选行驱动（勾选优先单台）。

### Task 4: 拖拽精化（树 + 列表行排序）
Files: SideTree.vue/ServerListView/新 api setTreeOrder
- 树拖拽（Plan 1 简化语义落地）：vuedraggable 或原生 HTML5 DnD——节点拖动：悬停文件夹>400ms=移入（高亮容器）；上下边缘 25%=前/后插（指示线）；跨数据源/后代/只读=禁止光标。落点计算 TreeNodes 重排→逐台 PUT config（TreeNodes 字段）或新增批量端点 `POST /api/servers/batch-nodes {moves:[{id,treeNodes}]}`（复用 batch patch 的 treeNodes——**Plan 2 batch 明确拒绝了 treeNodes**→新增专用小端点更干净：直接 PUT each config 的 TreeNodes 字段即可，N 通常小，循环 PUT 可接受）。同级自定义顺序：`PUT /api/ui-state/tree` 已全量替换 CustomNodeOrder（后端就绪无需新端点）——本任务落 TreeNodes 移动 + 顶层同级顺序写回该端点；深层自定义排序透传尊重 WPF 值，记录简化。
- 列表行拖拽：排序模式=Custom 时行拖拽→计算新顺序→`POST /api/ui-state/list-order` {ids:[...]}（LocalityListViewService.ServerCustomOrder 持久化——后端小端点+单测）。非 Custom 模式禁用拖拽。
- i18n + CJK 纪律。

### Task 5: 虚拟滚动 + 列状态持久化 + 静态托管回归测试 + dist 构建防护
Files: ServerTable.vue、webui/src/composables/useColumns.js、Tests/Service/WebUi/StaticHostingTests.cs、Ui/Ui.csproj
- >500 行 useVirtualList（@vueuse/core）：行高固定 36px 容器化（sticky 表头适配——已在滚动容器内，适配 spacer）；<500 直渲染（性能开关常量化）。
- 列宽（GridSplitter 语义简化为可拖列边+双击重置）与列显隐（**新建**"列"下拉菜单控件——当前不存在，顶栏只有 + 与 ⚙，由本任务在表头工具行新建）→ localStorage '1r-cols'（key=列名→{width,hidden}）+ API 镜像可选（仅 localStorage 即可，记录）。
- 静态托管回归测试：TestServer + 临时 wwwroot 目录 + ContentRootPath 设置→GET / 200 text/html、GET /assets/x.js 200、/api/version 仍 JSON（路由共存）。
- dist 构建防护：Ui.csproj BeforeBuild target——若 `..\webui\dist\index.html` 存在但比 `..\webui\src` 任一文件旧→Warning（不 Error，避免开发摩擦）MSB 提示"webui/dist 过期，请 npm run build"。

### Task 6: 最终整体评审 + 收官
- 全门禁：dotnet 126+新/2、双端 build、i18n 14×平价、CJK 0、实机探针（连接状态点亮：POST connect→SSE→列表行绿点；导入导出往返）。
- 最终代码评审（整个 Plan 4 区间）+ memory 更新 + owner 总验收清单（覆盖 Plan 1-4 全部手动项汇总）。
- 完成定义：连接状态点亮（真实会话驱动）；导入（4 格式）导出可用；拖拽树移动+行排序；虚拟滚动；列状态持久化；批量阈值+键盘完整；静态托管有回归测试；全部既有功能零回归。

## 有意简化（记录）
- 原生文件选择器不做（STA 约束，文本框+上传替代）；同级自定义顺序（CustomNodeOrder）Web 端只读尊重不编辑；列状态仅 localStorage 不入后端；rdp 附加设置 CodeMirror 补全仍为 textarea。
