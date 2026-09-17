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

## 修复批次 3（owner 三次验收，9 项，已确认设计）

- **Task A（编辑器，#1/#2/#3）**：#1 凭据组对齐 WPF（CredentialView.xaml 实证结构）：`credRole` 元数据分四档——`pre`（RDP Domain/LoadBalanceInfo，凭据来源切换之前恒显）/`identity`（UserName/Password/PrivateKey，仅 manual 模式；Password 在 UsePrivateKeyForConnect=true 时隐藏，WPF 同）/`option`（AskPasswordWhenConnect、SSH/SFTP UsePrivateKeyForConnect，两模式恒显）/`picker`（InheritedCredentialName，仅 vault 模式）；EditorDrawer 渲染顺序 pre→二选一→(identity|picker)→option；9 协议全部核查（Telnet/Serial 无凭据组、APP 的 connection 组不动）。#2 抽屉让位顶栏：全局 `--topbar-h:44px`（App.vue .shell 消费），`.ed-root` 从 inset:0 改 top:var(--topbar-h)，蒙层/面板不再遮挡窗口按钮与拖拽。#3 FormField TAGS 自绘 chips 换 `n-dynamic-tags`（small，与 n-input 视觉天然一致；editor Tags 与 BULK tags 两处同享）。
- **Task B（验证开关 #6）**：根因=开关混入"点保存才提交"表单（WPF 是点击立即生效 + 翻转前先过 Windows 验证，GeneralSettingView.xaml.cs:31-43）。后端新增 `POST /api/settings/verify`（VerifyAsyncUi，200/403；verifier 可注入便于 MSTest，参照 WebUiImportExportService.cs:350 模式）；前端 GeneralGroup 该开关改拨动即生效：先 verify（未开启验证时自动通过）→ 成功后单键 PUT {requireSecondaryVerification} → 失败/取消回弹；退出 Save 差量逻辑（其余字段不变）。
- **Task C（列表/设置/杂项 #4/#5/#7/#8）**：#4 ServerDto+DtoMapper 增 Note 字段（+测试）；ServerRow 名称后有备注时显示 📄 图标，n-popover 悬浮渲染 Markdown（marked+净化抽 utils/markdown.js，与 MarkdownField 共用）；BULK note dtoKey 顺手接 'note'。#5 `.s-groups` 加 `display:flex;flex-direction:column;gap:2px`（真根因：button 默认 inline-block 横向平铺，上批 .s-item flex:none 无效）。#7 顶栏 logo 换真图：Ui/LOGO.ico 256px PNG 已提取至 webui/public/logo_src_256.png（重命名/瘦身 logo.png），App.vue 替换自绘 SVG，favicon.svg 同步替换。#8 App.vue 全局快捷键 Ctrl+K 扩为 K/F。
- **Task D（#9 一键启动）**：仓库根 `dev-webui.cmd`：起 Vite（node_modules 缺失先 npm install）+ dotnet run Debug（net9.0-windows10.0.19041.0）；提示 DEBUG 下 WebView2（UiEngine=Web）直连 localhost:5173 热重载、WPF 引擎时浏览器开 5173。
- 基线：dotnet 154/5（owner WIP：Credential.cs/DapperDataBase*.cs/RdpConfigTests.cs）；npm build 0；i18n 455×14。Owner WIP 文件禁触；顺序执行 A→B→C→D（App.vue/EditorDrawer 有跨任务触碰，禁止并行）。

## 修复批次 4（owner 四次验收，10 项，2026-09-17）

- **Task A（编辑器视觉/结构）**：#1 颜色行重构为单个标准输入组（当前色块+hex 输入+色板收进一个与 n-input 同观的边框容器，对齐其他输入框）；#2 备注预览按钮移到"备注"标签右侧（省垂直空间，mode 状态上提至 FormField）；#3+#4 开关行重构——所有 SWITCH 字段渲染为「ff-label 留空 + 控件列 [开关][紧跟文字描述]」，网格与单行一致，对齐 WPF 复选框行（CredentialView.xaml:193 空 title+checkbox 在输入列）；#6 IsPingBeforeConnect 从 misc 组移到 Address/Port 行正下方（HostView.xaml:29-38 同构：标签列"Availability detection"+开关文字"Check if address is available before connect"，两键从 WPF 14 语言 xaml 移植；7 个带地址协议进 basicGroup、APP 进 connection 组、Telnet 清掉空 misc）；#5 IsAdministrativePurposes zh 文案补 /admini 术语前缀（owner 点名）。
- **Task B（Serial 下拉）**：GET /api/serial/options（ports=SerialPort.GetPortNames()、baudRates=Serial.cs:71 BitRates，注入可测）+ 前端新字段类型 AUTOCOMPLETE（n-auto-complete，可输入可选）用于 SerialPort/BitRate + MSTest。
- **Task C（placeholder 全量对齐 WPF）**：根因=web 未设 placeholder，"Please Input" 是 naive-ui enUS Input 默认值；扫全部表单 XAML 的 Tag=（字面量照抄：Address "e.g. 192.168.0.101"、Password "leave it blank..."、LoadBalanceInfo "tsv://MS Terminal Services Plugin.1.Wortell_sLab_Ses"、Serial "e.g. COM1"/"e.g. 9600"、StartupPath/StartupAutoCommand "e.g. /home/user/Desktop..."、ExePath "e.g. C:/vnc/viewer.exe or %VNC%/viewer.exe" 等；DynamicResource 键从 WPF 14 语言 xaml 移植真翻译：'Leave blank to inherit the default value'、server_editor_remote_app_name_tag 等）→ schemas placeholderKey + FormField 已有 placeholder 通道 + 14 locales。
- **Task D（主界面）**：#1 根路径 Explorer 语义——数据源根选中=仅直接子级（folderPath==='' 服务器+文件夹行；全部数据源根保持全库视图）；行拖入文件夹的移动完成后 reload 列表（即时消失）；#2 Ctrl+F 焦点在 WPF 层时转发——MainWindowView.CommandFocusFilter_OnExecuted（xaml.cs:398）检测 WebUI 可见→ExecuteScriptAsync 调 window.__focusSearch（App.vue 暴露，focus+select），否则走原 WPF 逻辑。
- 基线：dotnet 162/2（2 败=owner RdpConfigTests.cs WIP）；npm build 0；i18n 457×14。顺序 A→B→C→D（schemas.js/FormField 跨任务触碰）。

## 修复批次 5（owner 五次验收，9 项，2026-09-17；备注列已确认"一列兼顾"）

- **Task A（编辑器）**：①ed-head 单行重排：[ed-tile][标题文字][协议 n-select][数据库 n-select/只读 pill][关闭]，标题不再两行堆叠；②Serial/BitRate 建议列表恒显示全部选项（移除输入过滤，保留 get-show 恒 true）；③连续 SWITCH 不再 3 列网格平铺多个 form-field——聚合为**一个** form-field 行（ff-label 留空，全部 [switch][文字] 项在同一个 ff-control 内水平排列 flex-wrap）。
- **Task B（列表语义）**：④"全部数据源"更名"全部数据"（crumb.allDataSources 值更新：zh 全部数据/zh-TW 全部數據/en All data，其余回落 en）；全部数据根=全库递归所有服务器且**不显示文件夹行**（currentFolders 对 all-root 返回空；数据源根/文件夹/搜索语义不变）。⑤备注列（owner 确认一列兼顾）：新"备注"列=纯文本直显（超长省略号）+ 有备注时悬停 n-popover 渲染 markdown（renderMarkdown 复用）；删除批次3加在名称旁的小图标；接入 HIDEABLE_COLS/COL_LABELS 列菜单；文件夹行两处模板（虚拟/非虚拟）补空 cell 对齐。
- **Task C（布局/杂项）**：⑥批量条+表头工具簇（已选 N 台/▶连接/✎批量编辑/⤓导出/≡自定义顺序/▦列菜单）从 ServerTable 移入面包屑行（Teleport 到 crumb-row 内容器，状态留在 ServerTable；▦ tooltip 文案改进）；⑦搜索框 topbar 居中（absolute 居中）；⑧状态栏语言切换按钮改为 当前语言⇄英文（模块级记住非英语选择，按钮显示目标语言名，languageOptions 取名）。
- 基线：dotnet 161/5（5 败=owner WIP）；npm build 0；i18n 472×14。顺序 A→B→C。

## 重构批次（2026-09-17 goal：零功能变化，代码更易维护、注释更清晰）

硬约束：**不改变任何功能/行为**——纯结构搬移与注释重写；每任务门禁 npm build 0 错 + i18n 平价 + 评审逐 hunk 核实"verbatim 搬移/仅注释"。CSS 148px 网格四处重复不做跨文件合并（scoped 限制+风险>收益，记录）。死 i18n 键清理（editor.removeTag/row.note/statusbar.langEn/langZh，零引用核实后删，473→470）。

- **Task A（EditorDrawer 1046 行分解+注释重写）**：批量编辑模式（模板+bulk 脚本 ~260 行）抽为 `components/editor/BulkEditForm.vue`（defineExpose save 或 emit 请求保存，抽屉底部保存按钮经薄缝调用）；ed-head 抽为 `EditorHead.vue`（~80 行）；文件头 37 行批次考古注释重写为现状行为描述 ~15 行；其余注释去批次号噪音、保留 WPF 对齐依据。
- **Task B（ServerTable 1133 行分解+注释重写）**：文件夹行模板（虚拟/非虚拟两份 verbatim 重复）抽为 `FolderRow.vue` 去重；批量条+工具簇 Teleport 块抽为 `TableToolbar.vue`；注释重写（含 :519 "Esc 关闭列菜单"失实注释）；19 处批次引用清理。
- **Task C（FormField 476 行清理）**：serial options 模块级缓存抽 `composables/useSerialOptions.js`；17 处批次引用注释重写。
- **Task D（全局注释清理+死键）**：ServerListView/SideTree/App/ServerRow/schemas/fieldTypes/composables 注释重写（fieldTypes "分组页签"过时描述等）；死 i18n 键 ×14 locales 删除；收尾门禁+全量评审。
- **Task E（风格统一，owner 2026-09-17 追加）**：引入 Prettier（.prettierrc 按主流风格：单引号/无分号/2 空格/printWidth 120——以最小化 churn 为准实测选定；trailing comma 等按现状抽样）+ `npm run format`/`format:check` 脚本；全量机械格式化一个独立提交（diff 应全部为空白/引号/换行，无逻辑变化）；owner IDE 重排版问题此后由 format 脚本终结。
- **Task F（错误修复，owner 追加"发现错误需要修复"）**：重构中发现的错误以独立 `fix(webui)` 提交（与纯重构提交区分，可回溯）。已累积待修清单：①EditorDrawer credRole 漏标字段静默不渲染（dev console.warn 兜底）②convert-locales.mjs WRAP⊆MAPPING fail-fast 断言 ③.ff-switch-text 缺 overflow-wrap:break-word ④ServerTable :519 "Esc 关闭列菜单"失实注释改写（接 Esc 属行为变化不在重构批做）。
- 基线：npm build 0；i18n 473×14（Task D 后 470×14）；dotnet 不涉及（纯前端）。顺序 A→B→C→D→E→F（F 可穿插在发现时即修）。owner WIP 禁触惯例不变。

## 后端重构批次（2026-09-17 goal：WebUi 相关 C# 零功能变化重构，不碰无关 C#）

范围：仅 `Ui/Service/WebUi/**`（后端注释考古噪音≈0，重构主战场是结构拆分）。**禁触**：SecondaryVerificationHelper（WPF 设置页共用）、MainWindowView（混合壳层）、owner 全部 WIP、webui/ 前端。验证门禁：`dotnet test Tests/Tests.csproj`（基线 161/5，5 败=owner WIP 逐条一致）+ 评审机械 diff（搬移/注释-only）。

- **Task A**：WebUiEndpoints.cs（930 行/45 路由）→ `partial static class` 按域拆文件：主文件保留 MapAll（按**原注册顺序**调用各 Map*）+ 共享助手（IsConnectable/DeriveConnectionState/BuildActiveServerIdSet…）；分域文件 Servers(+connect/search/events)、Credentials、DataSources、Settings(general/verify/launcher/runners/appearance)、Tags、Aux(version/icons/serial/ui-state)。lambda 体逐字搬移（仅包进方法+缩进）；注释顺带重写为现状描述。
- **Task B**：WebUiSettingsService.cs（619 行 30 方法）→ partial 按域拆（General/Launcher/Appearance/Tags/UiState）；EditorService/ImportExportService/DataSourceService/CredentialService 注释 pass（单域内聚不拆）。
- **Task C**：小文件（WebUiServer/DtoMapper/WebUiDto/FilterHelpers/TokenMiddleware）注释/组织梳理；Tests 注释仅修失实处（不动断言）。
- 顺序 A→B→C，每任务独立提交 + 评审；发现错误独立 fix 提交。

## 修复批次 6（owner 六次验收，13 项，2026-09-17）

- **Task A（编辑器）**：#1 资源重定向 9 开关行标题——WPF 该行有标题 `server_editor_advantage_resources`（zh"共享到远程桌面"，RdpFormView.xaml:429），schemas 增 switch-run 标题机制（首字段 `runTitleKey` → blocksOf 产出带 title 的 run 块 → 渲染于 148px 标签列）；键从 WPF 14 语言移植。#2 ed-head 协议/数据库下拉框加描述性 tooltip+aria（新 i18n 键）。#3 RdpControlAdditionalSettings **确认对应 WPF MISC 组 "Additional settings"**（RdpFormView:526-532，非 mstsc 模式显示，WPF 为带补全的 AvalonEdit）——标签从"RDP 控件附加设置"改为对齐 WPF 的"附加设置"（'Additional settings' 资源 14 语言）。
- **Task B（连接状态实时）**：#4 连接后指示器不变、手动刷新才绿——排查链路：SessionControlService_OpenConnection.cs:153/191 的 NotifyWebUiSessionChanged（时序：通知时主机是否已入 ConnectionId2Hosts）→ ReloadAll(true)→OnReloadAll→SSE reload→前端 useServers 是否监听并 reload。修根因（时序错位则调整调用点/或在主机挂载完成处补通知；前端缺监听则补）。
- **Task C（设置小项）**：#5 "添加数据源"等同款 `n-button type=primary` 按钮文字白色（themeOverrides Button primary textColor 白）。#6 外观字号选择即时生效（AppearanceGroup onChange 即调 themes 的字号应用——themes/index.js:57 已有 documentElement.style.fontSize 路径，缺的是即时触发）。#8 设置页 ESC 失效排查（escShield 疑卡死：naive 下拉经 Esc 关闭时 update:show 配对性）——可修则修，不可修则去提示；返回按钮加醒目（accent 边框/图标）。
- **Task D（关于页+更新红点）**：#7 "语言与关于"→"关于"，删语言选择（常规已有）；AboutGroup 内容对齐 WPF AboutPageView（logo+应用名+标语/版本+构建日期/Update 行（新版本链接+红点）/作者 Shawn+GitHub+邮箱/Support 使用文档链接/Make contributions 三按钮（issues/打赏/商店评价）/Included Components 10 组件链接/关闭即返回）；#12 更新检测——后端 WebUiUpdateService（自持 VersionHelper+同款 CustomCheckMethod，尊重 DoNotCheckNewVersion，缓存结果），GET /api/version 扩展 `{updateAvailable,newVersion,newVersionUrl,breaking}`；前端 ⚙ 按钮与设置导航"关于"项红点。
- **Task E（列表杂项）**：#9 最近连接列 title 精确到秒（Intl.DateTimeFormat 完整格式）。#10 编辑器打开时禁用 topbar 搜索/+下拉/设置（editorBus 增 editorOpen 状态，App.vue 消费禁用）。#11 侧栏标签区："标签"标题恒定不随滚动（sticky）；tag 超长省略（阈值常量 TAG_MAX_LEN=50，title 全名）。
- 基线：dotnet 161/5（owner WIP 固定名单）；npm build 0；i18n 473×14（预计 +若干键）。顺序 A→B→C→D→E。
