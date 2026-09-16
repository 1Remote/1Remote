# 1Remote Web UI 用户验收修复计划（13 项）

> 基于 owner 实测反馈的修复批次。设计决策已确认：虚拟文件夹（方案甲）、凭据二选一+独立备用连接组、网页接管标题栏、编辑器单页滚动。

**基线**：dotnet 154/2（2 失败=owner 未跟踪 RdpConfigTests.cs）；npm build 0 错误；i18n 428×14。
**纪律**：沿用 Plan 1-4 全部约定（双 casing 域、i18n 平价+CJK 0、每任务双阶段评审）。

## Task 1: 后端/搜索/验证三处修复（#1 匹配范围、#5 保存刷新、#13 验证开关）
- **#1**：查 `FilterHelpers.MatchServers` → `TagAndKeywordEncodeHelper.MatchKeywords` 实际传入了哪些字段（WPF 主窗口传 DisplayName+SubTitle[=地址]；web 疑似只传名称）——修正为名称+地址双字段匹配（与 WPF 对齐）；前端列表名称/地址单元格加**命中高亮**（后端 search 已返回匹配 server，前端本地对 q 分词做 `<mark>` 高亮；拼音命中无法定位字符位置则只高亮原文命中的部分——与 WPF Launcher 行为一致即可）。
- **#5**：编辑器保存成功 → `useServers.reload()`（UpdateServer 不触发 SSE 是已知后端行为，前端兜底即可，不必改后端强推——避免每次保存全库重读）。同时检查 batch/duplicate/delete 是否都已有刷新（batch 靠 SSE? UpdateServer 系全不触发——统一在编辑器 closed+saved 后 reload）。
- **#13**：调查 `SecondaryVerificationHelper`：PUT 走 `SetEnabled`（async void，写注册表/凭据管理器/文件三级），reveal 检查走 `GetEnabled()`/`VerifyAsyncUi()` 读的是哪个源；复现"勾选后 reveal 仍直通"，修复（可能：SetEnabled 静默失败或 GetEnabled 读静态缓存不刷新）+ MSTest。

## Task 2: 树/列表重构（#2 虚拟文件夹 + #3 全部数据源根）
- **树（SideTree）**：移除服务器叶子；顶部常驻虚拟根"全部数据源"（点击=清除数据源过滤，显示全部库服务器）；数据源根 → 文件夹；右键菜单：新建文件夹（虚拟=向 tree-state expansion 字典写入该路径键）/重命名/删除（子项上移一级）；拖拽到文件夹=移动（既有）。
- **虚拟文件夹机制**：`buildTree` 合并 tree-state 的 expansion 键物化空文件夹（与 WPF BuildView 行为对齐）；新建=PUT /api/ui-state/tree 增键；重命名=遍历受影响服务器改 TreeNodes 前缀+改键；删除=子项 TreeNodes 上移一级+删键。WPF 端自动可见（ServerTreeViewModel.cs:873-889 物化逻辑既有）。
- **列表（ServerTable/ServerListView）**：当前层级显示 文件夹（排前，升/降序按当前排序键）+ 服务器；双击文件夹=进入（面包屑更新）；面包屑支持点上级返回；右键菜单新建文件夹；拖拽：服务器行→文件夹行=移动；双击服务器仍=连接。空文件夹显示（无子项数或显示 0）。
- 后端：无需新端点（tree-state PUT 既有；重命名走逐台 config PUT 循环——复用树拖拽的实现）。

## Task 3: 编辑器重构（#6 排版 + #7 凭据组 + #8 单页 + #9 tile + #10 数据源选择）
- **#8**：取消分组页签 → 单页垂直滚动；保存/取消恒定粘底；分组标题变为锚点分区标题（滚动定位可留后续）。脏标记简化为整体 dirty。
- **#6**：图标预览放大（48px+，点击开 IconPicker）；标签输入单行化（chips 行内溢出滚动）；行标题对齐统一（label 列固定宽，全部一致缩进）；颜色：选择后预览立即生效（图标底色/行预览条），且当前颜色有明确 swatch 展示。
- **#7**：凭据组改为"手动输入 ⇄ 从凭据库选择"二选一切换（选库则填 InheritedCredentialName 并显示所选名；手动则清空引用）——对齐 WPF 双 Tab；`AlternateCredentials` 移出凭据组 → 独立分组"备用连接"（i18n 更名+说明文案：备用地址+身份组合）。
- **#9**：ed-tile（编辑器头图标）默认色 #00000000 在暗色不可见 → 无图标时回退色（协议色或中性色）+色板默认选中一个可见色。
- **#10**：新建模式头部加数据源选择器（编辑模式只读展示）。

## Task 4: 窗口控制（#11 网页接管标题栏）
- **WPF 壳**：MainWindowView 中 WebView2 铺满（去掉 40px 顶边距），WPF 自绘标题栏隐藏；WebView2 支持拖拽（DefaultBackgroundColor/WindowChrome 处理或 CoreWebView2 app-region——验证可行性，兜底：WPF 侧监听 WebView2 上边缘拖拽区域 ForwardWebRequest… 最稳：CSS `app-region: drag`（WebView2 已支持）+ WebMessageReceived 处理 min/max/close 命令；双击 topbar=最大化切换；关闭按钮走 CloseButtonBehavior（最小化到托盘/退出）。
- **Web**：topbar 加 LOGO 图标；右侧 min/max/close 三按钮（Windows 风格 hover 高亮，close hover 红）；topbar 设 app-region:drag（按钮/搜索框 no-drag）；窗口 maximized 状态同步（resize 还原按钮图标切换）。
- 注意：保留 WPF 遮罩（TopLevel mask）与 airspace 处理兼容。

## Task 5: 视觉细节 + 收官（#4 列间距、#12 设置导航、整体回归）
- **#4**：地址列与协议徽章间加间距（协议徽章 margin-left 或列 gap）。
- **#12**：SettingsView 左导航一行一项（nowrap + 省略号）。
- 全门禁回归 + 双端构建 + i18n --check + CJK 0；最终评审（5 任务区间）+ owner 手验清单更新（新树操作/文件夹/编辑器单页/窗口控制）。

## 完成定义
13 项全部闭环（#2 按虚拟文件夹方案、#7 按二选一+备用连接组、#8 单页、#11 网页接管标题栏）；WPF 端兼容（虚拟文件夹在 WPF 树可见；WPF 侧仅动 MainWindow 壳层）；零回归（154+新/2、build×2、i18n 428+新键×14）。

## 修复批次 2（owner 二次验收，11 项，已确认设计）

- **Task A（排查型 bug）**：#1 拖拽失效（排查 window-drag 链路+评估 app-region）；#8 新建/导入入口丢失（App.vue 顶栏回查）；#10 设置导航换行（回查 flex:none 是否生效/选对元素）；#11 验证开关端到端（读取端 GetEnabled→VerifyAsyncUi 链路+真实集成测试）。
- **Task B（列表/树行为+视觉）**：#2 资源管理器式文件夹（选中文件夹=仅直接子级；搜索=递归子孙，已确认）；#3 行左侧色条恢复（opaqueHex 实色，类似 WPF 色条）；#9 高亮改实心强调色背景+对比文字。
- **Task C（编辑器）**：#4 备注 Markdown（引入 marked，编辑⇄预览切换，已确认）；#5 标签输入框与名称框同宽同位带边框；#6 备用连接行默认折叠只显示名称；#7 连续 SWITCH 字段一行 3 个网格。
- 基线：dotnet 154/5（owner WIP）；npm build 0；i18n 451×14。Owner WIP 文件禁触。
