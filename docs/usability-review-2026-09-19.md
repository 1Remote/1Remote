# 1Remote Web UI 易用性专家评审报告（批次 11 Task B）

- 日期：2026-09-19
- 评审对象：`webui/src/`（Vue 3 + Naive UI 管理界面，运行于 WPF 壳 WebView2 / 浏览器直开）
- 评审方式：基于交互代码的多专家走查（纯评审，**未修改任何代码**）
- 结论用途：供 owner 阅读后自行决定是否修改、修改哪些

---

## 1. 执行摘要

本次以「交互设计 / 新手引导 / 键盘与效率 / 信息架构」四个专家视角，对 18 条核心用户任务流（新建、编辑、连接、搜索、批量、导入导出、文件夹管理、主题/语言切换、凭据库、运行器、标签管理等）做了基于代码的逐一走查，共提出 **32 条发现**（P0×2 / P1×6 / P2×14 / P3×10）。

总体判断：Web UI 的交互骨架质量高于平均水平——全局 Esc 逐级回退链、批量连接阈值确认、凭据 reveal 30 秒倒计时、批量编辑「保持不变/覆盖」模型、空态三态区分（离线/空库/无匹配）等都是成熟设计；与 WPF 桌面版的心智迁移面（右键菜单集、E/Del/Ctrl+D 键位、树拖拽分区判定、表单组结构）对齐充分，老用户迁移成本低。

但存在两个应优先处理的问题域：

1. **P0-1 输入法（IME）组字期间按 Esc 会误触发全局动作**。三个 window 级 Esc handler 均未过滤 `isComposing` 与输入控件焦点：中文/日文用户在搜索框取消组字 → 搜索词被整段清空；在设置页文本框取消组字 → 整页退出设置；在编辑抽屉取消组字 → 弹出「放弃未保存更改」确认。代码库中 `folderOps.js` 的回车处理已正确处理了同一问题，说明这是遗漏而非设计取舍。对 14 语言、中文用户占比高的产品，这是核心输入流的实际阻断。
2. **P0-2 编辑抽屉必填校验失败时反馈不可见**。RDP 等长表单滚动到底部点「保存」，`missingRequired` 横幅渲染在滚动区顶部（视口外），按钮无禁用、无 toast、无滚动定位——用户点保存「毫无反应」，新手完成「新建第一台服务器」这一核心任务时会被卡住。

P1 层的 6 条集中在：单击行即产生勾选与远端批量条（易误触且与桌面「选中」心智冲突）、批量操作缺删除入口、文件夹删除确认的 Enter 默认落点是最危险选项（连服务器一起删）、双击连接零提示不可发现、搜索后键盘流断裂、行操作按钮 hover 才可见（触屏/键盘不可达）。

其余 24 条为打磨项，多数修复成本低（改文案/加 title/补一条 Esc 分支），可按分级汇总表批量决策。

---

## 2. 方法

### 2.1 走读范围

| 模块 | 文件 |
|---|---|
| 全局壳/顶栏/快捷键 | `webui/src/App.vue` |
| 主列表视图/全局 Esc 链/空态 | `webui/src/views/ServerListView.vue` |
| 行列表/排序/勾选/拖拽/右键/键盘 | `webui/src/components/ServerTable.vue`、`TableToolbar.vue`、`ServerRow.vue`、`FolderRow.vue`、`composables/useRowChecks.js` |
| 侧栏树/标签区 | `webui/src/components/SideTree.vue`、`composables/folderOps.js` |
| 连接编辑器（9 协议 schema 表单） | `webui/src/components/editor/EditorDrawer.vue`、`EditorHead.vue`、`FormField.vue`、`BulkEditForm.vue` |
| 设置（7 分组） | `webui/src/views/SettingsView.vue`、`components/settings/GeneralGroup.vue`、`AppearanceGroup.vue`、`CredentialVaultGroup.vue`、`RunnerGroup.vue`、`TagManagerModal.vue` |
| 导入导出 | `webui/src/components/ImportModal.vue`、`ServerListView.vue` |
| 数据/搜索/i18n | `composables/useServers.js`、`locales/index.js`、`locales/zh-CN.json` |

### 2.2 用户任务清单（走查基线，18 条）

| # | 任务 | 主路径 |
|---|---|---|
| T1 | 新建服务器 | 顶栏「+ ▾ 新建服务器」→ 抽屉填表 → 保存 |
| T2 | 编辑服务器 | 右键「编辑」/ 行内 ✎ / E 键 |
| T3 | 连接服务器 | 行双击 / ▸ / Enter / 右键「连接」 |
| T4 | 搜索 | Ctrl+F → 输入（200ms 防抖）→ 命中高亮 → Esc 清除 |
| T5 | 浏览文件夹 | 树点击 / 文件夹行双击 / 面包屑逐级返回 |
| T6 | 文件夹管理 | 树/列表/空白处右键 → 新建/重命名/删除 |
| T7 | 移动服务器 | 行拖到文件夹行 |
| T8 | 批量选择与操作 | 勾选/Ctrl+A/Shift 范围 → 批量条（连接/编辑/导出） |
| T9 | 批量编辑 | 勾选 >1 台 → 批量条「编辑」→ 逐字段「覆盖」 |
| T10 | 导入 | 「+ ▾ 导入」/ 空库卡 → 拖文件 → 导入 |
| T11 | 导出 | 勾选 → 导出（403 二次验证引导重试） |
| T12 | 切主题/强调色/字号 | 设置 → 外观（即时生效） |
| T13 | 切语言 | 状态栏快捷键 / 设置 → 常规 → 语言 |
| T14 | 凭据库管理 | 设置 → 凭据库 → 新建/编辑/👁 reveal/删除 |
| T15 | 运行器配置 | 设置 → 运行器 → 协议页签 → 卡片/添加/删除 |
| T16 | 标签管理 | 边栏「管理」→ 置顶/重命名/删除/连接全部 |
| T17 | 排序与列自定义 | 表头排序 / ≡ 自定义顺序+拖拽 / ▦ 列菜单/列宽拖拽 |
| T18 | 设置常规项 | 关闭行为/行为开关/日志级别/二次验证开关 |

### 2.3 评级口径

- **P0**：明显违背直觉或直接阻断核心任务，建议尽快修
- **P1**：高频路径上的显著摩擦或误操作风险
- **P2**：特定人群/场景可感知的缺陷，值得排期
- **P3**：打磨项
- 成本估计：低（文案/样式/单点分支）/ 中（组件内逻辑）/ 高（跨端契约或后端配合）

---

## 3. 交互设计专家发现

**I-1 [P1] 单击行即写入勾选集，批量条在远端（面包屑行）浮现**
- 位置：`webui/src/components/ServerTable.vue:313-316`、`webui/src/composables/useRowChecks.js:52-54`、`webui/src/components/TableToolbar.vue:41-58`
- 现状：`rowClickSelect` 的「单击=单选」分支是 `checked.value = new Set([id])`——单击任意行即产生 1 台勾选，顶部面包屑行右侧立刻出现「已选 1 台 ▶连接 ✎编辑 ⤓导出 ✕」批量条。
- 违反原则：Nielsen #1（系统状态可见性——反馈位置与操作位置分离，相隔整个表格高度）；#4（一致性与标准——桌面列表「单击=选中高亮、选中≠进入批量选择」的惯例在此被合并）。
- 影响：用户只是想点一下某行（看看它、或为键盘操作定位），批量条意外出现；若未注意，后续 Ctrl+A 前的单击残留会混入批量集。与 K-2 耦合：键盘导航的激活条件恰好是「先点一下表格」，进一步放大误选。
- 建议：单击改为纯光标/高亮（写 `cursorId`），勾选仅由复选框/Ctrl/Shift/Ctrl+A 触发；批量条浮现时给一次性动画或 toast 提示。成本：**中**（需同步调整 `keyTargetServer` 的目标行语义与 Esc 链第二级的定义）。

**I-2 [P1] 批量操作条没有「删除」**
- 位置：`webui/src/components/TableToolbar.vue:41-58`
- 现状：勾选 N 台后可用操作仅 连接/编辑/导出/清除；删除只能逐台右键（或 E/Del 单台）。
- 违反原则：Nielsen #7（灵活与高效——专业用户批量清理退役服务器是高频管理任务）。
- 影响：IT 管理员清理 50 台服务器需 50 次「右键→删除→确认」，且每删一台列表重载。
- 建议：批量条加「删除」按钮，确认框显示 N 台数（可复用 `onDelete` 确认样式，红色主按钮+台数）；与 I-1 的勾选语义调整无依赖，可独立做。成本：**中**（后端逐台 DELETE 循环已有 `folderOps.runDelete` 先例可套用）。

**I-3 [P1] 文件夹删除确认的 Enter 默认落点是「连服务器一起删」**
- 位置：`webui/src/composables/folderOps.js:229-237`
- 现状：文件夹内有服务器时，确认框 positive（红色、naive 默认聚焦）=「删除文件夹及其内全部服务器」，negative=「仅删文件夹、内容上移」。
- 违反原则：Nielsen #5（错误预防——破坏性最强的选项不应是键盘回车的默认落点）。
- 影响：键盘用户 Del→Enter 两连击即批量删除 N 台服务器（虽有确认文案，但肌肉记忆性回车危险）；恢复成本极高。
- 建议：将默认焦点移到 negative 或取消；或将「连服务器一起删」改为需要输入文件夹名/二次确认的破坏性确认。成本：**低**（naive dialog 的 `defaultFocus`/ autoFocus 属性调整）。

**I-4 [P0] 编辑抽屉保存失败反馈在视口外，「点保存无反应」**
- 位置：`webui/src/components/editor/EditorDrawer.vue:312-319`（save 内 `missingRequired` 赋值后直接 return）、`:415-420`（横幅渲染在 `.ed-fields` 滚动区顶部）
- 现状：RDP 表单很长，用户滚到底部脚点「保存」时：必填缺失 → `missingRequired` 横幅插到滚动区最顶端（视口外），保存按钮不禁用、无 toast、无滚动定位；服务端 400 的 `saveErrors` 同样只在顶部。
- 违反原则：Nielsen #1（状态可见性——操作后零可见反馈）；#9（错误恢复——用户不知道错在哪、在哪）。
- 影响：新手在 T1「新建第一台服务器」任务中若漏填 `DisplayName`，点保存毫无反应，任务被阻断（表现为「按钮坏了」）；这是核心路径。
- 建议（三选一或组合）：① 校验失败时 `scrollTo` 横幅并短暂高亮；② 同时 `message.warning` toast 一条「有 N 项必填未填」；③ 保存按钮在 `missingRequired.length` 时进入错误脉动样式。成本：**低**。

**I-5 [P2] 行拖拽重排在非自定义顺序模式下静默失败，但 grab 光标常显**
- 位置：`webui/src/components/ServerTable.vue:173-177`（`onRowDrop` 前置 `isCustom` 校验直接 return）、`:806-808`（`:deep(.row[draggable='true']) { cursor: grab }` 恒生效）
- 现状：行永远 `draggable=true` 且显示 grab 光标；默认排序模式下拖动行做重排，drop 被静默忽略（只有拖到文件夹行才有效果）。
- 违反原则：Nielsen #9（错误预防与恢复——操作被接受与否无任何反馈）。
- 影响：用户尝试拖拽排序失败且不知道原因（需要先点 ≡ 开启自定义顺序这一前置知识）。
- 建议：非 custom 模式下 drop 重排时 toast「请先开启自定义顺序（≡）再拖拽排序」；或非 custom 模式行 cursor 恢复 default（保留拖入文件夹能力，仅去掉误导性光标）。成本：**低**。

**I-6 [P2] 列排序无「取消排序」出口**
- 位置：`webui/src/components/ServerTable.vue:98-105`
- 现状：`toggleSort` 同键只升降切换，第三次点击不会回到默认树序；想回默认只能点 ≡ 两次（开再关，间接把 key 置空）。
- 违反原则：Nielsen #3（用户控制与自由——缺少「回到默认」出口）。
- 影响：用户按名称排序后想回到自己整理的文件夹树序，无表头可达路径。
- 建议：表头三态循环（升→降→无），↕ 图标已能表达「无排序」态。成本：**低**。

**I-7 [P2] 「清除过滤」按钮副作用超出语境**
- 位置：`webui/src/views/ServerListView.vue:147-152`
- 现状：无匹配空态的「清除过滤」会同时清空搜索词、标签过滤、树选中（回到「全部数据」）。
- 违反原则：Nielsen #3（操作后果超出用户预期的最小范围）。
- 影响：用户在某个文件夹+标签组合下搜索无结果，点「清除过滤」后连所在的文件夹位置都丢了，需重新导航。
- 建议：按钮只清除导致无匹配的维度（优先搜索词，其次标签），树选中保留；或拆成两个按钮。成本：**低**。

**I-8 [P2] 设置页全量自动保存，失败仅角落 toast、无持续失败态**
- 位置：`webui/src/components/settings/GeneralGroup.vue:76-86`、`RunnerGroup.vue:117-147`
- 现状：开关/下拉即改即存，失败 toast 一次性出现（3 秒消失）；页面无任何持久「未保存/保存失败」状态指示。
- 违反原则：Nielsen #9（错误恢复）；#1（状态可见性）。
- 影响：后端短暂断连时用户连开数个开关，一条 toast 淹没在消息流中，用户以为已生效；实际桌面侧行为未变，故障难归因。
- 建议：任一自动保存失败后在分组顶部挂一条持续横幅（「部分设置保存失败，点击重试」），成功后消失。成本：**中**（`useAutoSave` 已有 onError 钩子，加一个全局失败 ref + 重试入口）。

**I-9 [P2] 批量编辑「覆盖」初值可能落为类型默认值，存在整批误写入**
- 位置：`webui/src/components/editor/BulkEditForm.vue:146-156`（`toggleOverwrite` → `emptyValueFor`）
- 现状：对「各不相同/未回读」字段点「覆盖」时，编辑初值取 schema 默认值或选项首项（如端口 3389）；保存只校验空值，不提醒「该值将写入 N 台」。
- 违反原则：Nielsen #5（错误预防）。
- 影响：用户以为覆盖态展示的是某种「当前值」、只想微调一项，实际把默认值整批写进 N 台（虽有 title 提示但 hover 才可见）。
- 建议：覆盖态字段旁常显轻提示「将把此值写入 N 台」（复用 `overwriteTip` 文案改为行内常显）；或对数值/枚举类覆盖初值留空、强制用户显式输入。成本：**低**。

**I-10 [P3] 顶栏「+」主按钮无默认动作**
- 位置：`webui/src/App.vue:194-196`
- 现状：「+」点击只展开下拉（新建/导入），新建服务器需两步。
- 影响：新建是最高频操作，多数应用「+」直接新建。
- 建议：点主按钮区=新建，仅右侧小箭头展开菜单；或保持现状（与「导入也是一等入口」的产品意图权衡）。成本：**低**。

**I-11 [P3] 复制服务器保存后与原名同名，列表难区分**
- 位置：`webui/src/components/editor/EditorDrawer.vue:180-191`（duplicateFrom 预填原 config，DisplayName 原样）
- 现状：复制的抽屉标题有「复制自 X」，但保存后新服务器与原服务器同名同图标，列表中两行不可区分（WPF 同为原样复制，属对齐）。
- 建议：create+duplicate 模式下 DisplayName 预填「原名 (副本)」，或在复制成功 toast 中提示改名。与 WPF 行为有偏差，需 owner 权衡。成本：**低**。

**I-12 [P3] requireSecondaryVerification 开关行无解释文案**
- 位置：`webui/src/components/settings/GeneralGroup.vue:188-199`
- 现状：开关一点即触发 Windows 凭据/Hello 验证弹窗，行内无「点击将需要验证」预告。
- 影响：好奇点击即弹系统级验证窗口，用户受惊；关闭已开启的验证项同样需先验证，无说明。
- 建议：label 下加一行 description 文案（WPF 若有对应说明文字可复用词条）。成本：**低**。

**I-13 [P2] 状态点（连接状态列/数据源状态点）无图例，title 为英文原始枚举**
- 位置：`webui/src/components/ServerRow.vue:85`、`StatusDot.vue:18`（`:title="state"` 直出后端英文枚举）
- 现状：状态列 58px 只有颜色圆点；悬停 title 是 `connected` 等英文原值，未走 i18n，且无任何图例说明绿/灰/红含义。
- 违反原则：Nielsen #2（系统与现实匹配——颜色语义无文字锚点）；#4（一致性——同页 `sb-sse` 的 title 已本地化）。
- 影响：新手无法得知状态列用途；14 语言用户看到英文 title。
- 建议：title 走 i18n（`status.connected` 等词条）；表头「状态」或设置页加一次性图例。成本：**低**。

---

## 4. 新手引导专家发现

**N-1 [P1] 「双击=连接」零提示，核心操作不可发现**
- 位置：`webui/src/components/ServerRow.vue:70`（`@dblclick` 绑定）、`ServerListView.vue:186`
- 现状：连接是最高频任务，入口有 5 个（双击/▸/Enter/右键/批量），但没有任何 UI 文字告知「双击连接」；`row.connect` 的 title 只在 ▸ 按钮上。
- 违反原则：Nielsen #1（可发现性）；新手自助完成率。
- 影响：纯 Web 背景新手（目标用户群之一）会先找「打开」按钮、尝试单击，未必发现双击；首次连接体验靠运气。
- 建议：行 title 提示「双击连接」；或首次进入列表时 toast 一条操作提示（localStorage 记忆只显一次）。成本：**低**。

**N-2 [P2] 新建/重命名/删除文件夹只藏在右键菜单**
- 位置：`webui/src/components/ServerTable.vue:240-251`（空白处右键）、`SideTree.vue:257-270`（树右键）
- 现状：文件夹全部管理操作仅右键可达（列表空白处、文件夹行、树节点三处右键）；没有任何可见按钮或菜单入口。
- 违反原则：Nielsen #7（发现的多样性——功能只有一条隐藏路径）。
- 影响：WPF 老用户无碍（心智一致），但 Web 新手组织服务器的第一步（建文件夹）可能卡住——尤其是不知道「空白处右键」这个入口。
- 建议：面包屑行或树底部加「+ 新建文件夹」可见按钮；空文件夹视图的空态文案加「在空白处右键可新建文件夹」。成本：**低**。

**N-3 [P2] 快捷键体系无帮助入口，「快捷键」菜单项是占位禁用**
- 位置：`webui/src/components/ServerTable.vue:326-336`（MENU 中 `shortcut` 项 `on` 未接线、hint=「即将推出」）
- 现状：↑↓/Enter/E/Del/Ctrl+D/Ctrl+A/Ctrl+F/Esc 链已全部实现且质量高，但除右键菜单 hint 暴露的 4 个外，其余无处可查；右键菜单里还有 3 项「即将推出」禁用项（新窗口连接/其他凭据/快捷键）。
- 违反原则：Nielsen #10（帮助文档）；对禁用项的期望管理。
- 影响：键盘效率能力被埋没；「即将推出」项在正式版中显得未完成（若短期无计划可先隐藏）。
- 建议：① 实现「快捷键」项为简单模态（静态列表即可，全部键位已有）；② 暂无计划的占位项从菜单移除，避免「功能损坏」观感。成本：**低**。

**N-4 [P2] 空库引导卡信息量不足**
- 位置：`webui/src/views/ServerListView.vue:436-443`
- 现状：空库卡只有「+ 新建第一台服务器」「⤓ 导入」两个按钮和一行桌面启动器提示；没有支持格式/迁移来源说明（格式列表在 ImportModal 里才出现）。
- 影响：从 mRemoteNG 等工具迁移来的用户需要先点导入才知道格式是否支持；对「9 协议」能力也无感知。
- 建议：卡片加一行「支持从 mRemoteNG / .rdp / .csv / .json 导入」+ 协议图标行。成本：**低**。

**N-5 [P2] 「数据库」与「数据源」术语混用**
- 位置：`webui/src/locales/zh-CN.json:432`（`editor.headDsLabel`=「数据库」/ en「Database」）vs `settings.nav.data`=「数据源」、树/状态栏全部用「数据源」
- 现状：同一概念（data source）在编辑器头部标签叫「数据库」，在设置导航、树、导入模态叫「数据源」。
- 违反原则：Nielsen #4（一致性）；新手术语学习成本翻倍。
- 影响：新手在编辑器看到「数据库： Local」会以为还有别的东西；文档检索也分裂。
- 建议：`headDsLabel` 词条改为「数据源」/「Data source」（12 个转换生成的 locale 需同步重跑 `scripts/convert-locales.mjs` 或手工改这一个键）。成本：**低**。

**N-6 [P3] ≡「自定义顺序」模式语义弱**
- 位置：`webui/src/components/TableToolbar.vue:62-69`
- 现状：开关只有 24px 图标 + title；开启意味着「行可拖拽重排、顺序持久化、与桌面共用」这整套语义全靠猜。
- 影响：配合 I-5（拖拽静默失败），用户很难自己建立「先点 ≡ 再拖」的心智。
- 建议：激活态在表格首行上方显示一次性提示「拖拽行以调整顺序，再次点击 ≡ 恢复」；或 title 文案扩写。成本：**低**。

**N-7 [P3] 首次使用无任何 onboarding**
- 位置：全局
- 现状：首次打开只有空库卡，无功能巡礼/提示条。
- 影响：对「IT 管理员 + 部分新手」的混合用户群，新手自助依赖探索。
- 建议：可选的首次提示条（3 步：双击连接 / Ctrl+F 搜索 / ⚙ 进设置），localStorage 记忆；优先级低，因为核心任务（T1/T3）修好 N-1 与 I-4 后已可自助。成本：**中**。

**正面观察（新手引导）**：空态三态区分（离线/空库/无匹配）语义准确且防误导（`ServerListView.vue:133-146` 注释明确）；批量编辑「保持不变/覆盖」模型远优于常见「表单即全量覆写」实现；导入模态的目标行「导入到：ds/文件夹」让落点透明；语言选择即时生效无刷新。

---

## 5. 键盘与效率专家发现

**K-1 [P0] Esc 链未过滤 IME 组字与输入控件——中文用户组字取消即误触发全局动作**
- 位置（三处 window 级 handler 均无 `e.isComposing` 检查、无 `e.target` 输入控件过滤）：
  - `webui/src/views/ServerListView.vue:258-270`（`onGlobalEsc`：焦点在搜索框输入中按 Esc → 链走到第三级 `searchQuery.value = ''`，**正在输入的搜索词被整段清空**）
  - `webui/src/views/SettingsView.vue:77-87`（`onKey`：设置页任何输入框内按 Esc → `leaveSettings()` **整页退出**；即使非 IME，文本域内按 Esc 退整页也违反直觉）
  - `webui/src/components/editor/EditorDrawer.vue:279-292`（`onKey`：表单输入中按 Esc → 弹「放弃未保存更改」确认）
- 对照：`webui/src/composables/folderOps.js:78-87` 的回车处理已完整处理 `isComposing`，证明团队知晓该问题，Esc 链属遗漏。
- 违反原则：Nielsen #5（错误预防）；对 CJK 输入法用户是核心输入流的实际破坏。
- 影响：14 语言中 zh-CN/zh-TW/ja-JP 用户每次取消组字候选都可能触发；最痛的是搜索词丢失（重打整段）与设置页退出（自动保存缓解了数据丢失，但上下文全丢）。
- 建议：三个 handler 统一加两道守卫：`if (e.isComposing) return` 与 `if (e.target.closest('input, textarea, [contenteditable]')) return`（设置页需要「文本域内 Esc 不退页」语义；搜索框内 Esc 清搜索可保留为显式行为——在搜索框自身的元素级 handler 处理并 stopPropagation，与文件头注释里已论证过的方案一致）。成本：**低**（每处 2 行，重点是回归测试 Esc 链）。

**K-2 [P1] 搜索后键盘流断裂：↑↓/Enter 需先点击表格，且点击即误选行**
- 位置：`webui/src/components/ServerTable.vue:392-399`（`tableFocused` 初始 false，靠 mousedown/focusin 置真）、`:412-421`
- 现状：Ctrl+F 搜索 → 结果已显示 → 按 ↓ 想选择第一条 → 无反应（tableFocused=false）；必须先点击表格，而点击行又触发 I-1 的误勾选。
- 违反原则：Nielsen #7（灵活高效）；键盘用户「搜索→选择→连接」标准流断裂。
- 影响：高频键盘流（搜索→回车连接）在 Web 版不可用；WPF 桌面版该流是通的，迁移用户会感到退化。
- 建议：搜索框内按 ↓ 时把焦点移交给表格（置 tableFocused=true 并光标落首行）；配合 I-1 的单击语义调整后，「点击激活」的误选副作用也一并消除。成本：**中**。

**K-3 [P1] 行操作按钮（▸/✎/⋯）hover 才浮现，触屏与键盘不可见**
- 位置：`webui/src/components/ServerRow.vue:243-248`（`.cell-act { opacity: 0 }`，仅 `:hover`/`.selected` 显示）
- 现状：操作列三个按钮透明隐藏，触屏设备无 hover、键盘聚焦时也无 `:focus-within` 样式（可聚焦但不可见）。
- 违反原则：Nielsen #1（可见性）；WCAG 2.4.7（焦点可见）。
- 影响：触屏笔记本/平板 WebView2 场景行操作不可发现；键盘 Tab 到按钮上看不见焦点在哪。
- 建议：加 `.row:focus-within .cell-act { opacity: 1 }` 与 `@media (hover: none) { .cell-act { opacity: 1 } }`。成本：**低**（两行 CSS）。

**K-4 [P2] 列菜单（▦）不在全局 Esc 链中**
- 位置：`webui/src/components/TableToolbar.vue:29-35`（注释自述「Esc 未接线——全局 Esc 链归 ServerListView，未覆盖列菜单」）
- 现状：▦ 列菜单展开时按 Esc：不关菜单，而是去清搜索/清光标（若链上有其他状态），或什么都不做。
- 违反原则：Nielsen #4（一致性——同类浮层 Esc 行为不一致：右键菜单在链首、列菜单不在）。
- 建议：把列菜单开关并入 `closeMenuIfOpen` 同级（TableToolbar 的关闭归 ServerTable 转发即可），或菜单自身注册一次性 Esc。成本：**低**。

**K-5 [P2] 编辑抽屉声明 `aria-modal` 但无焦点圈闭（focus trap）**
- 位置：`webui/src/components/editor/EditorDrawer.vue:371`（`role="dialog" aria-modal="true"`）
- 现状：Tab 可以从抽屉字段跑到蒙层之下的底层页面（侧栏树按钮、状态栏语言按钮、面包屑仍可聚焦）。
- 违反原则：WCAG 2.4.3（焦点顺序）；Nielsen #4。对屏幕阅读器用户，`aria-modal` 声明与实际行为不符比不声明更糟。
- 影响：浏览器直开场景（无 WPF 壳的模态感）键盘/读屏用户会「掉到」被遮挡的界面里迷失；WebView2 内影响较小（但仍可 Tab 出去）。
- 建议：抽屉打开时循环 Tab（监听 Tab 键在首尾元素间环绕），或首字段自动聚焦 + Tab 圈闭；n-modal（导入/标签管理）naive 已自带 trap，仅需处理自绘抽屉。成本：**中**。

**K-6 [P2] 右键菜单与侧栏树完全无键盘导航**
- 位置：`webui/src/components/ServerTable.vue:779-791`（ctx-menu 为 button 列表但打开后焦点不移入、无方向键）、`SideTree.vue:316-331`（树行 div 不可聚焦、无 ↑↓/左右展开折叠）
- 现状：右键菜单只能鼠标打开（除行内 ⋯ 按钮）；打开后方向键不能在菜单项间移动；整棵树鼠标专用。
- 违反原则：Nielsen #7；WCAG 2.1.1（键盘可达）。
- 影响：键盘用户无法完成右键菜单与树操作（重命名文件夹、切换数据源等）；对「IT 管理员」中偏好键盘的群体是效率与可达性双重缺口。
- 建议：分两步——① 菜单打开即聚焦首项，↑↓ 移动、Enter 激活、Esc 关（Esc 链已有）；② 树行加 roving tabindex + ↑↓/Enter/左右。成本：**中**（①低、②中）。

**K-7 [P3] 空格勾选光标行、Ctrl+C 复制地址未接**
- 位置：`webui/src/components/ServerTable.vue:412-445`（`onGlobalKey` 已接 ↑↓/Enter/Ctrl+A/Ctrl+D/Del/E）
- 现状：键盘无法勾选单行（只能 Ctrl+A 全选）；复制地址/用户名只能右键菜单。
- 建议：Space=勾选光标行；Ctrl+C=复制光标行地址（与右键菜单 copy-address 同链路）。成本：**低**。

**K-8 [P3] 新建服务器无 Ctrl+N**
- 位置：全局（`App.vue:36-44` 仅 Ctrl+F 一条）
- 现状：最高频创建操作无键盘入口。
- 建议：Ctrl+N 触发 `requestNewServer`（editorBus 通路现成）。成本：**低**。

**正面观察（键盘）**：全局 Esc 逐级回退链（菜单→勾选→搜索→光标）是罕见的精细设计，`ServerListView.vue:252-270` 注释完整；Ctrl+F 聚焦+全选、重复按键防抖（`e.repeat` 处理 Enter 连发）、`tableFocused` 防抢输入的意图、右键菜单 hint 与键位双轨暴露、编辑抽屉 Ctrl+S/Esc、设置页 escShield 对 naive 下拉 Esc 链序的 capture 快照方案，都是高质量实现——K-1 的修复应在这些既有机制上补守卫，而不是重构。

---

## 6. 信息架构专家发现

**A-1 [P2] 排序/列显隐/列宽仅存 localStorage（本地），与桌面端不共享——持久化策略分叉**
- 位置：`webui/src/components/ServerTable.vue:87-105`（`1r-sort`）、`composables/useColumns.js`（`1r-cols`）
- 现状：树展开态、自定义顺序、主题、语言均走后端/共享存储与 WPF 一致；但列表排序键、列显隐、列宽是 Web 本地私有。
- 违反原则：Nielsen #4（一致性——同一「视图偏好」类状态一半跨端一半本地）。
- 影响：双端并用的用户（本产品主场景）在桌面调整的列在 Web 不生效，反之亦然；且换浏览器/清缓存即丢。
- 建议：短期在文档/关于页注明「排序与列为 Web 本地设置」；长期接入 `/api/ui-state`（后端已有该端点家族）。成本：**高**（需后端键扩展），建议只做短期披露。

**A-2 [P2] 编辑器内凭据组无法跳转到凭据库管理**
- 位置：`webui/src/components/editor/EditorDrawer.vue:440-462`（cred-mode 切换与 picker）、`CredentialPicker.vue`
- 现状：服务器表单选「凭据库」模式时只能从既有凭据中选；想新建/改凭据必须退出抽屉 → ⚙ → 凭据库，编辑上下文全丢。
- 违反原则：Nielsen #7（高效）；任务连续性。
- 影响：配置新服务器的任务流被打断（T1 与 T14 交错是真实场景：先建凭据再建服务器）。
- 建议：picker 旁加「管理凭据…」链接，确认后关闭抽屉并深链 `#/settings?g=g-credentials`（深链机制 `SettingsView.vue:58-63` 已支持，只差跳转与 ds 透传）。成本：**中**。

**A-3 [P3] 长表单无分组锚点导航**
- 位置：`webui/src/components/editor/EditorDrawer.vue:413-497`
- 现状：9 协议全部组垂直铺开单页滚动（有意决策、sticky 组标题）；RDP 含高级组时字段量很大。
- 影响：与 WPF 页签式编辑器的心智不同，老用户找「高级」组靠滚动+记忆；超过两屏后定位成本上升。
- 建议：抽屉头部或侧边加分组锚点条（点击滚动到组，sticky 标题已具备滚动目标）；非必须。成本：**中**。

**A-4 [P3] 标签管理入口仅在边栏，设置页无对应项**
- 位置：`webui/src/components/SideTree.vue:387-389`（「+ 管理」chip）
- 现状：标签是「服务器数据的管理操作」，放在列表页边栏（`TagManagerModal.vue` 头注释论证过），但设置页 7 分组中没有标签入口。
- 影响：用户在设置里找标签管理找不到（IA 预期分裂）；属有意取舍，成本低的对冲是在设置相关分组加一条说明链接。
- 建议：设置「数据源」或「常规」组底部加一行「标签管理在主界面边栏」的说明链接（打开主界面+模态需总线支持，或仅文字指引）。成本：**低**。

**A-5 [P3] 搜索激活时树选中态与列表口径分离**
- 位置：`webui/src/components/ServerTable.vue:63-68`（搜索激活时树过滤整体让位，全库递归命中）
- 现状：搜索期间树仍高亮原选中节点，列表却显示跨库命中；`search-chip` 提示了过滤词但未说明「已忽略文件夹位置」。
- 影响：用户在文件夹内搜索，看到别家数据源的命中会疑惑「我没在这个库里」；已有的 chip 与 folder 列（显示「数据源 / 路径」）是有效缓解。
- 建议：search-chip 的 title 扩写「搜索覆盖全部数据源与子文件夹」。成本：**低**。

**正面观察（信息架构）**：设置 7 分组扁平 + `?g=` 深链 + 「关于」红点/⚙ 红点双提示；面包屑/树/文件夹行三处共用同一 selection 模型，语义严格一致（计数口径 `countDirectChildServers` 三处同源）；WPF 心智迁移面覆盖广（右键菜单集、E/Del/Ctrl+D、树拖拽 25% 分区、表单组结构、宏帮助链接），`folders.js`/`folderOps.js` 大量注释标明 WPF 源码行号，迁移质量可审计。

---

## 7. 分级汇总表

| 级别 | 编号 | 标题 | 主位置 | 成本 |
|---|---|---|---|---|
| P0 | I-4 | 保存失败反馈在视口外，点保存无反应 | EditorDrawer.vue:312-319,415 | 低 |
| P0 | K-1 | Esc 链未过滤 IME 组字/输入框（三处） | ServerListView.vue:258 / SettingsView.vue:77 / EditorDrawer.vue:279 | 低 |
| P1 | I-1 | 单击行即勾选+远端批量条浮现 | ServerTable.vue:313 / useRowChecks.js:52 | 中 |
| P1 | I-2 | 批量操作缺「删除」 | TableToolbar.vue:41-58 | 中 |
| P1 | I-3 | 文件夹删除确认 Enter 默认=连服务器删 | folderOps.js:229-237 | 低 |
| P1 | N-1 | 双击连接零提示 | ServerRow.vue:70 | 低 |
| P1 | K-2 | 搜索后键盘流断裂（需先点表格） | ServerTable.vue:392-421 | 中 |
| P1 | K-3 | 行操作按钮 hover 才可见（触屏/键盘不可达） | ServerRow.vue:243-248 | 低 |
| P2 | I-5 | 非 custom 模式拖拽静默失败+grab 光标误导 | ServerTable.vue:173,806 | 低 |
| P2 | I-6 | 排序无取消出口 | ServerTable.vue:98-105 | 低 |
| P2 | I-7 | 「清除过滤」过度清除 | ServerListView.vue:147-152 | 低 |
| P2 | I-8 | 自动保存失败无持续状态 | GeneralGroup.vue:76 / RunnerGroup.vue:117 | 中 |
| P2 | I-9 | 批量覆盖初值为默认值，误写入风险 | BulkEditForm.vue:146-156 | 低 |
| P2 | I-13 | 状态点无图例、title 英文原值 | StatusDot.vue:18 | 低 |
| P2 | N-2 | 文件夹管理仅右键可达 | ServerTable.vue:240 / SideTree.vue:257 | 低 |
| P2 | N-3 | 快捷键帮助缺失+「即将推出」占位项 | ServerTable.vue:326-336 | 低 |
| P2 | N-4 | 空库引导卡信息量不足 | ServerListView.vue:436-443 | 低 |
| P2 | N-5 | 「数据库」vs「数据源」术语不一致 | zh-CN.json:432 | 低 |
| P2 | K-4 | 列菜单不在 Esc 链 | TableToolbar.vue:29-35 | 低 |
| P2 | K-5 | 编辑抽屉无 focus trap | EditorDrawer.vue:371 | 中 |
| P2 | K-6 | 右键菜单/树无键盘导航 | ServerTable.vue:779 / SideTree.vue:316 | 中 |
| P2 | A-1 | 排序/列设置为 Web 本地，双端分叉 | ServerTable.vue:87 / useColumns.js | 高 |
| P2 | A-2 | 编辑器内无法跳转凭据库管理 | EditorDrawer.vue:440 / CredentialPicker.vue | 中 |
| P3 | I-10 | 「+」主按钮无默认动作 | App.vue:194-196 | 低 |
| P3 | I-11 | 复制后同名难区分 | EditorDrawer.vue:180-191 | 低 |
| P3 | I-12 | 二次验证开关无解释文案 | GeneralGroup.vue:188-199 | 低 |
| P3 | N-6 | ≡ 自定义顺序语义弱 | TableToolbar.vue:62-69 | 低 |
| P3 | N-7 | 首次使用无 onboarding | 全局 | 中 |
| P3 | K-7 | Space 勾选/Ctrl+C 复制未接 | ServerTable.vue:412-445 | 低 |
| P3 | K-8 | Ctrl+N 未接 | App.vue:36-44 | 低 |
| P3 | A-3 | 长表单无分组锚点 | EditorDrawer.vue:413-497 | 中 |
| P3 | A-4 | 标签管理设置页无入口说明 | SideTree.vue:387 | 低 |
| P3 | A-5 | 搜索期间树/列表口径分离无说明 | ServerTable.vue:63-68 | 低 |

**分布**：P0×2、P1×6、P2×14、P3×10，共 32 条。成本：低×22、中×9、高×1（A-1 建议仅做披露则降为低）。

---

## 8. 建议采纳优先级

**第一批（建议立即，均为低成本、防阻断/防数据破坏）**
1. K-1：三处 Esc handler 加 `isComposing` + 输入控件守卫（低）
2. I-4：保存校验失败滚动到横幅 + toast（低）
3. I-3：删除确认默认焦点改安全侧（低）
4. K-3：`.cell-act` 加 focus-within 与 hover:none 常显（两行 CSS）
5. K-4：列菜单并入 Esc 链（低）

**第二批（排期，改善高频路径手感）**
6. N-1 + N-3 + I-13 + N-5：可发现性文案包（双击提示/快捷键模态/状态点 i18n/术语统一）（低）
7. I-1 + K-2：勾选语义与键盘流联动改造（中，需回归 Esc 链与 keyTargetServer）
8. I-2：批量删除（中，后端循环有先例）
9. I-5 + I-6 + N-6：排序/拖拽可预期性三件套（低）
10. N-2 + N-4：新建文件夹可见入口 + 空库卡信息（低）

**第三批（择机，结构性投入）**
11. I-8 + I-9：自动保存失败横幅 + 批量覆盖确认强化（中）
12. K-5 + K-6：focus trap + 菜单/树键盘导航（中，可按 ①菜单 ②树 拆分）
13. A-2：编辑器→凭据库深链（中）
14. I-7、A-5、I-10、I-11、I-12、K-7、K-8、A-4：零散打磨（各低）

**暂不建议动**：A-1（跨端共享列设置，成本高收益窄，先在文档披露）；A-3（长表单锚点，等真实反馈）；N-7（onboarding，等第一批上线后评估）。

> 评审局限说明：本报告基于代码推演实际交互，未做真人测试；行号基于 Branch_web_ui 分支 2026-09-19 快照（b231f528）。所有建议均未实施，采纳与否由 owner 决策。
