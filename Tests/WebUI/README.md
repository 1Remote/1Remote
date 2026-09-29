# 1Remote Web UI 测试指南

> **新来的同事从这里开始**：§0 五分钟跑起测试 → §2 学会读用例表 → §8 知道断言红了怎么办。
> 只想跑回归的老同事：直接看 §3 的命令。

- **基线**（2026-09-23，round16 复验）：**204 条自动化用例全绿**；14 语言词条全量平价（具体键数随轮次增删持续变化，以语言文件实测为准、由自动化门禁 `convert-locales --check` 保证，不再在文档里记具体数字——V13 定案：数字会漂移，写死必过时）；后端 `dotnet test` 207/207 全绿（round11 后记录的 3 个基线失败已随 round13/14 工作区修复消失）。
- **设计依据**：`docs/superpowers/specs/2026-09-15-web-ui-redesign-design.md`（下称 spec，随轮次增补）。
- **本目录**（`Tests/WebUI/`）存放 WebUI 的全部测试用例与文档；后端 C# 测试在上级 `Tests/` 工程。
- **用例来源**：spec + 历轮修复报告（`docs/fix-report-*.md`）+ 评审条目（G/H/E5/J/K/L/N/P/U/A/M 系列，编号含义见 §2.3）。

## 0. 五分钟上手

前提：Node.js ≥ 20.19（见 `webui/package.json` 的 engines）。**不需要**为本目录单独安装依赖——
测试只用 Node 内置模块和 webui 已装好的包。

```bash
cd webui
npm run test      # 跑本目录全部用例（约 5 秒）
```

看输出末尾三行（exit code 同步为非 0，可直接接 CI / 脚本判断）：

```
ℹ tests 204    ← 本次跑了几条用例
ℹ pass 204     ← 通过数
ℹ fail 0       ← 失败数（>0 即有回归）
```

日常三个命令（都在 `webui/` 下执行）：

| 命令 | 做什么 | 什么时候用 |
|---|---|---|
| `npm run test` | 只跑用例 | 改了前端代码后快速自检 |
| `npm run check` | 用例 + 代码风格（prettier）+ 14 语言键集平价 | **每日迭代收尾的最低门禁** |
| `npm run build` | vite 生产构建 | 验证能出产物；桌面端要拿它进 wwwroot |

## 1. 这套测试在防什么（背景）

1Remote 的主窗口管理界面正从 WPF 迁到 Vue（`webui/`）。迁移期间按轮次迭代：每轮
评审 → 修复 → 出报告（`docs/fix-report-*.md`）。历史上同类 bug 反复回潮——修好的东西
在下一次重构里又被悄悄弄坏（例：树内拖拽自上线起就是坏的，三轮修复都在改"执行体"，
真正的病因是执行体**之前**一行判定顺序）。

所以这套测试的主导思想是：**把每轮报告的"定案形态"固化成断言**。语义类修复固化成
纯函数用例（folders.test），接线类修复固化成"源码里必须存在某个模式"的锚点断言
（source-invariants.test）。此后任何改动只要破坏了定案，当天 `npm run test` 就红。

代价与纪律：**有意的设计变更必须同步改断言**（先改本文档 §6 对应行的"设计依据"，
再改断言并注明新依据）。断言不是法律，定案才是；但改定案要走 owner 拍板。

## 2. 怎么读用例表（术语表）

### 2.1 用例 ID 的结构

`TC-<域>-<编号>[M|Z]`，例：`TC-LIST-28`、`TC-ED-23M`。

| 域 | 含义 |
|---|---|
| TREE | 左侧树 / 文件夹语义（spec §3.2 §3.5） |
| LIST | 行列表 / 批量操作（spec §3.4） |
| KBD | 键盘流与浮层互斥（spec §8.2） |
| ED | 连接编辑器与设置页（spec §5 §6） |
| I18N | 14 语言国际化（spec §7） |
| BE | 后端 C# 侧锚点（走 `dotnet test`） |

后缀：**M** = 只能人工验证（视觉/手感/跨端），在 §7 冒烟清单有对应步骤；**Z** = 以源码
锚点方式断言（L3 层，见 §4）。

### 2.2 表格五列的含义

| 列 | 白话 |
|---|---|
| 用例 / 判据 | 验证什么行为（一句话） |
| 设计依据 | 这个行为是谁定的：spec 章节 / owner 决策 / 评审条目 |
| 历史缺陷 | 它防的是哪一轮的哪个 bug（"—" = 预防性用例） |
| 自动化 | 用哪层实现：L1-L5 / C# / **M**（人工） |

### 2.3 设计依据里的编号（K2、U5、N8……）是什么

历轮评审报告的**条目号**，格式 = 轮次系列 + 序号。查法：`docs/` 下按系列名搜
（如 `usability-review-*-round6.html` 里的 K 系列、round11 的 U 系列、round13 的 N 系列），
修复落地情况看 `docs/fix-report-*.md`。编号只是出处坐标，**用例本身的自足描述在"用例"列**。

### 2.4 高频黑话速查

| 词 | 白话 |
|---|---|
| 直接子级 | 进入文件夹只看到"直接放在里面"的服务器（资源管理器模型） |
| 子树 / 全部子孙 | 文件夹含子文件夹深处的全部内容 |
| 递归口径 | 数量按"全部子孙"算（徽标 / 面包屑 N 台 / 删除影响面共用） |
| tree-state 键 | 虚拟文件夹的存在凭据（`ds + SEP + 路径`），与 WPF 共用一个字典文件 |
| 源码不变量 / 锚点 | 断言"某段定案代码形态仍在"（L3 层） |
| 定案 | owner 已拍板的最终形态，见 §1 |
| 平价 | parity：与 WPF 版行为对齐 |

## 3. 每日回归流程（迭代完成后执行）

```bash
# ① 自动化用例 + 格式 + 语言键平价（约 10 秒）
cd webui && npm run check

# ② 生产构建（接线错误经构建校验；产物供桌面端托管）
npm run build

# ③ 后端（改到 C# 侧或导入导出/批量/凭据端点时）
cd .. && dotnet test Tests/Tests.csproj -p:UseAppHost=false -p:OutputPath=bin/testrun/
dotnet build Ui/Ui.csproj   # 同时把 dist/ 刷新进 wwwroot（运行 1Remote.exe 生效）
```

通过标准：①②全绿；③在后端未改动时可跳过。

> ③ 的 OutputPath 重定向不是可选项：运行中的 1Remote.exe 会锁 `Ui/bin/Debug` 下的
> exe/dll（MSB3027 重试 10 次后失败），把输出重定向到未锁目录即可绕过，**不要杀用户进程**。

## 4. 用例分层

| 层 | 载体（都在本目录） | 能抓什么 | 局限 |
|---|---|---|---|
| L1 纯逻辑 | `folders.test.mjs`、`kv-pairs.test.mjs`、`protocol-switch.test.mjs`、`ds-title.test.mjs` | 文件夹双语义、搜索/标签范围收窄、键值行解析、协议切换快照、状态点倒计时 | 不碰组件接线 |
| L2 组件模型 | `row-checks.test.mjs`、`editor-schema.test.mjs`、`draft-guard.test.mjs`、`required-guard.test.mjs` | 勾选模型、9 协议 schema、草稿守卫、必填守卫 | 不碰模板绑定 |
| L3 源码不变量 | `source-invariants.test.mjs` | 接线级回归：守卫遗漏、事件顺序、判定先后、CSS/形态锚点、后端锚点 | 断言"定案形态"，有意重构须同步更新 |
| L4 i18n | `i18n.test.mjs` | 键平价、死键引用、漏翻译、硬编码文案、术语漂移 | 无法保证译文质量 |
| L5 构建链 | `npm run build` + `format:check` + `convert-locales --check` | 编译错误、风格、14 语言键集 | 不验证行为 |
| C# 后端 | `Tests/` 工程（MSTest） | 凭据 DAO、DTO、导入导出、批量端点 | 走 `dotnet test`（见 §3 ③ 文件锁规避） |
| M 人工 | 本文档 §7 冒烟清单 | 视觉、动效、真实拖拽手感、跨端联动 | 无法全自动 |

> 一个 TC 行常对应多条断言（如 TC-LIST-28 对应 4 条 L1 断言）；**条数以测试文件为准**，
> 本表是按行为组织的索引。运行总数见文首基线。

## 5. 两个最容易踩的概念（必读）

**显示 ≠ 勾选。** 同一个文件夹：
- **显示**（进入文件夹看到哪些行）= **直接子级**；
- **勾选**（勾文件夹复选框选中哪些服务器）= **全部子孙**。
- 一切**数量**（树徽标 / 文件夹行"内含 N 台" / 面包屑 / 删除影响面）= **递归总数**。
这组语义曾反复拉锯三轮（H38 → round5-BUG3 → K2），单一来源在
`webui/src/composables/folders.js`，勿按"计数应等于列表行数"的直觉回改。

**源码不变量（L3）不是在测行为，是在测"定案还在"。** 例：断言 `ServerTable.vue` 里
`canDrop` 判定先于 `dragRow` 置空——它不模拟拖拽，只保证那行顺序没被重构弄反。
重构后断言红了 ≠ 一定是 bug：先判断设计是否有意变更（§8 流程）。

## 6. 用例明细

> 每小节开头一句话说明该域的边界；表列含义见 §2.2。

### 6.1 文件夹/树语义（spec §3.2 §3.5）

覆盖：左侧树的选中/展开/拖拽/右键，文件夹的显示与勾选语义，虚拟文件夹物化。

| ID | 用例 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-TREE-01/02 | 显示=直接子级（isDirectChildOf）与勾选=全部子孙（isInSubtreeOf）两条语义线；同名前缀不误匹配；undefined 归一化；两函数互不复用 | spec §3.2；H38 注记 | round5-BUG3、H38 一轮反复 | L1 |
| TC-TREE-03 | 「移到根」恒合法（isDescendantPath 对空串恒 false）；相等/自身子树/跨库禁入 | spec §3.5 | WPF parity | L1 |
| TC-TREE-04 | tree-state 键=「数据源+SEP+逐段路径」与 WPF 逐字符一致；键即存在（物化空文件夹） | WPF BuildView parity | round4-错误2/H1 幽灵文件夹 | L1 |
| TC-TREE-05 | 键前缀重写：重命名随键迁移展开态；合并不覆盖目标侧展开态；删除时自身键消失且子键上移；跨库键不动 | H1/H3 定案 | round4-错误2 | L1 |
| TC-TREE-06 | 服务器路径前缀重写：等值/子路径换前缀；删除上移一级 | H1 | round4-错误2 | L1 |
| TC-TREE-07 | 树模型：路径下沉、空文件夹物化、孤儿丢弃、徽标=递归口径（与列表/面包屑「N 台」一致） | E5-1/H38 口径统一 | round5-BUG3 | L1 |
| TC-TREE-08 | drop 处理器内 canDrop 判定**先于** dragRow 置空 | round5-BUG1 根因顺序 | round5-BUG1 | L3 |
| TC-TREE-09 | 树右键菜单纵向防溢出（height-130 预算，对齐 ServerTable 标准） | J2 | round2-J2 | L3 |
| TC-TREE-10 | 数据源状态点 title 走 i18n（不直出英文枚举） | H31 | round4-H31 | L3 |
| TC-TREE-11 | 无标签整区隐藏；真无数据源给引导（不死分支） | H32 owner 决策 | round4-H32 | L3 |
| TC-TREE-12M | 人工：树内拖 A→B 移入 / 上、下 25% 前后插 / 拖到根；跨库拖出 toast；拖入自身子树无响应；只读源拖不动 | spec §3.5 | round4-错误2、H3 | **M** |
| TC-TREE-13M | 人工：列表文件夹拖入树；树内跨层移动遇同名弹合并确认（取消不动/确认合并） | H3 | round4-H3 | **M** |
| TC-TREE-14M | 树/列表右键 新建/重命名/删除文件夹：重名拦截、重命名预填全选、空文件夹直删确认、含服务器两选一确认（红=连删，蓝=上移，Esc 不动作） | folderOps；第三轮 G10 | — | **M** |
| TC-TREE-15M | 人工：展开状态与自定义顺序重启后保持（与 WPF 共用 tree-state 文件） | spec §3.2 | — | **M** |
| TC-TREE-16 | 树内同级重排落盘失败：回滚本地序 + 报错（不再假成功） | K11 | round6-K11 | L3 |
| TC-TREE-17 | 文件夹长操作 busy：行内 ✎/✕ 禁用（title 解释）；拖放落区 busy 前置拒绝（无高亮） | K10 | round6-K10 | L3 |
| TC-TREE-18M | 人工：树徽标 N = 面包屑 N（递归含子文件夹）；列表行=直接子级（两个语义并存） | K2 owner 定案 | round6-K2 | **M** |
| TC-TREE-19 | 数据源状态点 title 工厂：三态词条 + 重连倒计时本地逐秒走表（eta 快照 + 本地时钟，悬停可看到倒计时刷新）；树/状态栏/设置卡三处消费同一实现 dsTitle.js | round12 用户报告 | round12 | L1 |

### 6.2 行列表（spec §3.4）

覆盖：行渲染、勾选/批量操作条、搜索与标签过滤、拖拽落区、右键菜单、排序与列配置。

| ID | 用例 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-LIST-01/02 | 勾选级联（限定同数据源防串库）；空文件夹复选框=勾选文件夹本身（checkedFolders，值形状 {dsName,path}） | round5-BUG2 | round5-BUG2 | L2 |
| TC-LIST-03 | 文件夹行复选框无 :disabled；悬停提示区分「级联子孙/选择空文件夹」两语义 | round5-BUG2 | round5-BUG2 | L3 |
| TC-LIST-04/05 | 表头全选=当前视图可见行（合并语义保留隐藏子孙）；数据变化即时剔除已不存在的勾选 | useRowChecks 文件头 | — | L2 |
| TC-LIST-06 | Ctrl 切换 / Shift 范围（锚点）/ 裸点击=纯光标；视图变化作废锚点 | useRowChecks 文件头 | — | L2 |
| TC-LIST-07 | 文件夹视图显示口径 isDirectChildOf 单一来源；复选框列从双击识别区排除（服务器行+文件夹行） | round4-反馈；round2 附加 | round5-BUG3 | L3 |
| TC-LIST-08 | 空文件夹批量删除链路：批量条出现条件 / 混合计数「已选 N 台 · M 个空文件夹」 / 三档确认文案；**checkedFolders 时效剔除**（文件夹被填入服务器/行消失即剔、仍空且在层级则保留——round8-P4 前半） | round5-BUG2；round8-P4 | round5-BUG2、round8-P4 | L2+L3 |
| TC-LIST-09 | 搜索激活：文件夹列强制显示；列菜单该项禁用+说明；搜索 chip 带范围标注 | H9 | round4-H9 | L3 |
| TC-LIST-10 | 拖拽归因顺序：跨库判定先于重排模式提示 | H27 | round4-H27 | L3 |
| TC-LIST-11Z | 跨库 dragend 兜底监听恒挂 ServerTable（不随边栏卸载失效） | H18 | round4-H18 | L3 |
| TC-LIST-12M | 人工：列表行拖到文件夹行移入；「..」行上移一级；≡ 自定义排序模式下上/下半区前插/后插（非 custom 一次性提示）；文件夹行不可重排（一次性提示） | spec §3.5 | — | **M** |
| TC-LIST-13M | 人工：表头三态排序循环（名称/地址/协议/最近连接），地址列自然 IP 序（10.0.0.2 < 10.0.0.10）；刷新后保持 | spec §3.4 | — | L1+**M** |
| TC-LIST-14M | 人工：列宽拖拽、双击重置；列菜单显隐（文件夹列仅根视图可用）并持久化；列菜单「重置列宽」全部回默认比例 | spec §3.4；round8-P12 | J14 | **M** |
| TC-LIST-15M | 人工：>500 行启用虚拟滚动，滚动不跳动 | spec §3.4 | — | **M** |
| TC-LIST-16M | 人工：右键菜单 全项检查（连接/新窗口连接‹占位›/其他凭据‹占位›/编辑/复制 Ctrl+D/复制地址/复制用户名/复制密码/桌面快捷方式‹占位›/删除 Del），占位项禁用+「即将推出」 | spec §3.4/H4 | round4-H4 | **M** |
| TC-LIST-17M | 人工：复制密码验证门（未开启验证直接复制；开启后桌面端弹验证，取消→403 toast；30s 窗口内免重验；Serial/Telnet 提示「无密码可复制」） | H4 | round4-H4 | **M** |
| TC-LIST-18M | 人工：行双击连接；复选框列快速连点**不**误触连接；▸/✎/⋯ 常显按钮 | round2 附加 | round2 附加 | **M** |
| TC-LIST-19M | 人工：地址列 `address:port (userName)`、用户名段弱化、长文本省略号、悬停全文；Serial 回退 COM1(9600) | round4-设计改动3/J3/J5 | round2 | **M** |
| TC-LIST-20M | 人工：备注列 Markdown 悬停弹层；时间列相对时间+悬停绝对时间；标签胶囊最多 2+溢出计数 | spec §3.4 | — | **M** |
| TC-LIST-21M | 人工：空态三态（空库引导卡/空文件夹/无匹配逐层清除）；空库引导卡协议一览 | spec §8.5 | 第三轮 G16 | **M** |
| TC-LIST-22M | 人工：批量导出下载、批量连接超阈值确认 | spec §3.4 | — | **M** |
| TC-LIST-23 | 标签/搜索过滤下拖拽重排：POST 基准 = 全量库列表（allServers），不破坏其余服务器顺序 | K1（round6 P1） | round6-K1 | L3 |
| TC-LIST-24 | 面包屑计数：无过滤 = 递归口径（与树徽标同函数）；搜索/标签激活 = 命中行数 | K2 owner 定案 | round6-K2 | L3 |
| TC-LIST-25 | 标签过滤：隐藏文件夹行（K23）+ **范围与搜索同口径收窄**（文件夹=子树、数据源根=整源、全部数据=全库——U5 修复了「只算直接子级漏掉子文件夹内同标签服务器」） | K23；round11-U5 | round6-K23、round11-U5 | L1+L3 |
| TC-LIST-26 | 搜索请求失败：保留旧过滤态 + toast「搜索失败」（不再伪装零命中） | K13 | round6-K13 | L3 |
| TC-LIST-27 | 服务器行操作列双击不冒泡（双击 ▸ 不再 2×click+1×dblclick 触发 3 次连接） | K6 | round6-K6 | L3 |
| TC-LIST-28 | **搜索范围收窄（资源管理器语义）**：搜索激活时按当前选中层级收窄——文件夹=其子树、数据源根=整源（含全部子文件夹，防「根视图只命中根级」偏差）、「全部数据」=全库跨源；搜索未激活时 selection 不参与过滤；chip 范围标注随层级变化 | round11-需求①（spec §3.2 已成文） | round11-需求① | L1 |
| TC-LIST-29 | **进入/离开文件夹立即清空勾选**（树点击/文件夹双击/面包屑跳转统一归结为 selection 变化；SSE 重载等不换视图的更新仍走数据剔除）——废止「跨层级导航勾选持续存在」旧设计（round11-需求②） | round11-需求② | round11-需求② | L2+**M** |
| TC-LIST-30M | 人工：当前所在文件夹被删除/移动/重命名后，列表自动退到最近仍存在的祖先（本端操作与其它端 SSE 同步两路） | round11-需求③；round8-P1 | round8-P1（幽灵路径） | **M** |
| TC-LIST-31M | 人工：单台连接防重入——同服务器在途锁定 + 完成后 1.5s 冷却（Enter/▸/右键菜单全入口生效） | round8-P3 | round8-P3 | **M** |
| TC-LIST-32M | 人工：勾选的空文件夹被（其它端）填入服务器后，批量删除确认前按「当前确实为空」复核——不再静默搬迁内容 | round8-P4 | round8-P4 | L2+**M** |
| TC-LIST-33 | 批量连接有进度提示 + 模块级单飞锁（确认对话框阶段再点不发第二轮） | round11-U2 | round11-U2 | L3 |
| TC-LIST-34 | 批量删除三防：确认防叠框 / 空文件夹批量删除有进度 / 执行期锁（确认后进行中再点不叠第二轮） | round11-U10/U11；round13-N8 | round11、round13 | L3 |
| TC-LIST-35 | 部分成功统一 warning 档（folderOps 三处 + useBatchConnect：有删成=warning、全军覆没=error） | round13-M3 | round13-M3 | L3 |
| TC-LIST-36 | 空白右键菜单「新建服务器」与「新建文件夹」同一禁用判据（都取决于有确定数据源） | round11-U4 | round11-U4 | L3 |
| TC-LIST-37 | **Teleport 目标竞态**：设置往返（ServerListView 卸载重挂载）后切文件夹——面包屑行工具簇（#crumb-actions）仍在位、无渲染错误。根因=Teleport 目标由同一棵树渲染，首挂靠骨架屏分支先行侥幸、重挂载同帧挂载时目标查询落空 → TableToolbar 整块未挂载（el=null vnode 残留）→ 后续更新渲染崩溃+界面冻结。修复=Teleport disabled 直到挂载后拿到目标元素 | round16 用户报障（冒烟 S24） | round16 | 冒烟 S24 |
| TC-LIST-38 | **连接状态指示器按需显示**（2026-09-25 需求 + 2026-09-26 调整）：未连接（disconnected/未知）时行内指示点隐藏不可见；发起连接后显示（connecting/reconnecting=黄、connected=绿点+光晕）；断开后恢复隐藏。列位保留（行布局稳定）、**表头不显示标题（空表头保留对齐，title/aria-label 保留语义）、列宽 12px = 指示器 8px + 4px**，骨架屏该列留空同口径 | spec §3.4（2026-09-25/26 定案） | — | L3 |

### 6.3 键盘流与浮层（spec §8.2）

覆盖：全局快捷键、Esc 逐级回退链、浮层打开时的按键让位、光标行导航。

| ID | 用例 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-KBD-01 | 七类浮层在开时按键整体让位（n-dialog/n-modal/ctx-menu/nf-menu/tree-ctx/ed-root/col-menu） | H6/E3-1 | round4-H6 | L3 |
| TC-KBD-01M | 人工：右键菜单/编辑抽屉/确认框开着时按 Enter/E/Del/Ctrl+A 无动作；关闭后恢复 | H6/E3-1 | round4-H6 | L3+**M** |
| TC-KBD-02 | 多选（>1）时 Enter/Del/Ctrl+D 禁用 | owner round4-#5 决策 | round4-#5 | L3 |
| TC-KBD-02M | 人工：勾 3 台按 Enter/Del 无动作；批量操作走批量条 | round4-#5 | round4-#5 | L3+**M** |
| TC-KBD-03 | 表格内控件聚焦时不抢按键；自动重复只对 ↑↓ 生效 | 第三轮 G2 | 第三轮 | L3 |
| TC-KBD-04 | Esc 链序：行菜单→文件夹菜单→树菜单→列菜单→勾选→搜索→标签→光标，唯一 window 级 handler 按序裁决 | 第三轮 Esc 链 | 连续三轮 | L3 |
| TC-KBD-05 | 模态在开时 Esc 整链让位（编辑抽屉/标签管理/导入/n-dialog） | 第三轮 | — | L3 |
| TC-KBD-06 | Ctrl+S 确认框让位 | H21 | round4-H21 | L3 |
| TC-KBD-13 | 设置页 Esc 在 naive 对话框/模态开着时让位（不再连设置页一起退出） | K5 | round6-K5 | L3 |
| TC-KBD-14 | 搜索框 ↓/↑ 移交前判输入法组字（isComposing，选词键不抢焦点） | K8 | round6-K8 | L3 |
| TC-KBD-15 | 虚拟滚动模式下键盘光标按行高滚动跟随（scrollTo 行号×36px，不再依赖行 DOM 存在） | round11-U3 | round11-U3 | L3 |
| TC-KBD-16 | 只勾空文件夹（无服务器勾选）时 Del/Enter 拦截——目标错位防误删 | round13-N12 | round13-N12 | L3 |
| TC-KBD-07M | 人工：Ctrl+F 聚焦搜索（再次按下全选）→↓→Enter 连接流（搜索框移交表格焦点）；Ctrl+K 已废弃、无动作（round16） | spec §8 | 第三轮 G2 | **M** |
| TC-KBD-08M | 人工：Menu/Shift+F10 呼出行菜单；↑↓ 移动光标行（外框可见+滚入可视区） | spec §8.2 | 第三轮 G3 | **M** |
| TC-KBD-09M | 人工：Ctrl+A 全选可见/清空；E 编辑；Ctrl+D 复制预填「(副本)」不叠加后缀 | spec §8.2/H26 | round4-H26 | **M** |
| TC-KBD-09Z | 复制预填后缀剥净再补一个（源码断言 while endsWith） | H26 | round4-H26 | L3 |
| TC-KBD-10M | 人工：编辑抽屉 Ctrl+S 保存；Esc 关闭（脏则确认；确认开着时 Ctrl+S 无动作） | spec §5/H21 | round4-H21 | **M** |
| TC-KBD-11M | 人工：<900px 边栏自动收起（只收不展）；收起态入口可用；窄窗跨库拖拽有 toast 反馈 | spec §8.7/H18 | round4-H18 | **M** |
| TC-KBD-12M | 人工：主题切换（基底×强调色×经典预设×字号×字体）即时生效；服务器自定义色不受影响 | spec §4 | — | **M** |

### 6.4 编辑器与设置（spec §5 §6）

覆盖：连接编辑抽屉（9 协议 schema / 草稿守卫 / 必填守卫 / 协议切换）、设置页各组、
数据源模态、凭据库、Runner、导入导出。

| ID | 用例 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-ED-01 | 六协议（RDP/SSH/SFTP/FTP/VNC/RemoteApp）地址/端口/检测在凭据组 pre 段前三位、地址在用户名之前 | round4-设计改动4 | round4-设计改动4 | L2 |
| TC-ED-02 | 无凭据组协议（Telnet/Serial/APP）地址不挂 credRole；Serial 无地址概念 | round4-设计改动4 | — | L2 |
| TC-ED-03 | RDP 新建默认 3389/Administrator/检测开启 | round4-设计改动4 回归口径 | — | L2 |
| TC-ED-04 | schema 完整性：字段键唯一、visibleWhen（含数组形态）引用可解析、labelKey/placeholderKey 在 zh/en 基准存在 | spec §5/§7 | H16 类 | L2 |
| TC-ED-05 | 批量补丁 diffPatch：无变化空补丁/数组整体覆盖/null 可写/缺键=保持不变 | spec 风险 2 | — | L2 |
| TC-ED-05M | 人工：批量编辑「‹N 台各不相同›」占位 + 字段级 保持不变/覆盖；恰勾 1 台转单台编辑 | spec §5 | — | **M** |
| TC-ED-06M | 人工：协议切换保留公共字段；rdp 附加设置键值行+补全；LocalApp 参数宏动态字段 | spec §5 | — | **M** |
| TC-ED-07M | 人工：脚本行 Test（回显输出/退出码）；Serial COM 口下拉 | api | — | **M** |
| TC-ED-08M | 人工：凭据卡 手动⇄凭据库切换地址/端口恒显；空凭据库指引行 | spec §5/H17 | round4-H17 | **M** |
| TC-ED-09M | 人工：凭据库设置页 明文查看验证门；只读源禁编辑；删除被引用凭据有联动提示 | spec §6 | — | **M** |
| TC-ED-10M | 人工：数据源设置页 测试连接测**草稿**（sqlite 路径改动后测新路径，不再测旧值） | H12 | round4-H12 | **M** |
| TC-ED-11M | 导入：保留 JSON 内层级（目标文件夹作前缀）；CSV/RDP 平铺入目标；三处格式说明口径一致（含 .db/.sqlite） | H15/H28 | round4-H15/H28 | **M** |
| TC-ED-11Z | 后端导入前缀拼接 + 前端 folder 参数存在 | H15 | round4-H15 | L3 |
| TC-ED-12M | 导出：跨数据源 ids 导出 JSON attachment（403 提示验证） | spec §6 | — | **M** |
| TC-ED-13 | 数据源模态必填星标（mysql/pgsql 五项 + sqlite 路径；编辑态密码留空=保持不标） | K9 | round6-K9 | **M** |
| TC-ED-14 | Web 自启开关：GET=注册表实况（IsSelfStart），PUT 走 WPF 同入口（SetSelfStart） | K27 | round6-K27 | L3 |
| TC-ED-15 | 外观保存失败 toast（connected 豁免纯浏览器预览） | K15 | round6-K15 | L3 |
| TC-ED-16 | 导出前明文密码警告确认框（引用桌面版同义警告） | K26 | round6-K26 | L3 |
| TC-ED-17 | 导入进行中 Esc/遮罩/卡片 × 三路不可关 | K14 | round6-K14 | L3 |
| TC-ED-18 | 数据源卡片状态点悬停 title 走词条（不直出英文枚举） | K16 | round6-K16 | L3 |
| TC-ED-19M | 人工：后端失联 ≥3s 全屏不可关闭警告出现、恢复自动消失；exe 冷启动不误报 | K17 | round6-K17 | **M** |
| TC-ED-20 | 主按钮统一描边（L4）+ 移除钮 hover 中性（L2）+ 脚本测试弹窗标准形态/无图标/禁回车误触（L3） | round6-L2/L3/L4 | round6 | L3 |
| TC-ED-20M | 人工：主按钮统一描边形态（设置页/模态保存=描边；对话框红色确认保留实底）；移除钮悬停无红字 | round6-L2/L4 | round6 | L3+**M** |
| TC-ED-21 | 悬停语言统一（N 系列）：脚本测试确认钮进三档映射 / ghost 主按钮悬停钉强调色+浅底 / naive 默认钮悬停归主文字色 / 设置页必填红星统一 `.req-star` | round9-N1~N4 | round9 | L3 |
| TC-ED-22M | 人工：Runner 添加弹窗——名称带红星、未触碰输入框时不显红错（touched 门控）、空值只禁保存钮 | round8-P5 | round8-P5 | **M** |
| TC-ED-23M | 人工：凭据三场景——新建保存瞬间不误报「名称已存在」（BUG-A）；无密码凭据编辑显示空输入框而非掩码（BUG-B，hasPwd/hasKeyPath 预填）；无引用凭据编辑真正落库、重命名同步进引用服务器（BUG-C） | round11-BUG-A/B/C | round11 | C#+**M** |
| TC-ED-24 | 数据源添加模态保存瞬间行为：淡出期间不闪「名称已存在」（查重带模态开态守卫）/ 保存成功即重置表单（重开是空白）/ 凭据库与 Runner 同类入口守卫不回退 | round12 | round12 | L3 |
| TC-ED-25 | 数据源卡片：测试进行中同卡片编辑/删除禁用（卡片级锁）；端口校验 1-65535（标红 + 禁用测试/保存） | round11-U16/U17 | round11-U16/U17 | L3 |
| TC-ED-26 | 数据源弹窗关闭语义：mask-closable 禁直通（遮罩点击不误关）+ 草稿守卫接线（有改动先确认） | round11-U15（round13 收口） | round11-U15 | L3 |
| TC-ED-27 | 热键录制：失焦 / 点击外部即退出录制态 | round11-U19 | round11-U19 | L3 |
| TC-ED-28 | 候选输入统一控件 SuggestInput（过滤 + Esc 取消）三处消费，不再各持拷贝 | round11-U21 | round11-U21 | L3 |
| TC-ED-29 | 保存问题总线：子表单行内必填与键值半行（残键/残值）拦截保存；批量编辑消费同一总线 + 半行纳入脏判定 | round11-U7/U25；round13-N3/N4 | round11、round13 | L3 |
| TC-ED-30 | 美学收拢（A 系列）：行内小钮 .act 全局类单一定义 / 表单行网格统一 s-form-row（标签列 100px 单值）/ 设置域单列小弹窗统一 480px 档 | round11-A1/A3/A7/A4 | round11-A 系列 | L3 |
| TC-ED-31 | 确认框体系：三档收敛 confirmDialog；丢弃确认并入 useDraftGuard 共享守卫；回调必须为语句体（值式箭头返回 false 会被 naive 判为「阻止关闭」——BUG3），全仓审计无赋值表达式值式箭头（folderOps 命名输入的 submit 短路返回写法唯一刻意豁免） | round11-A2；round13-BUG3 | round13-BUG3 | L3 |
| TC-ED-32 | 界面语言唯一持久化在后端：启动拉后端语言，前端不读写 localStorage（N6→round14-R5 定案） | round13-N6/R5 | round13 | L3 |
| TC-ED-33 | 草稿守卫（useDraftGuard）：模态无改动 → 遮罩/✕/Esc 直接关；有输入 → 点遮罩直接弹「未保存的更改」丢弃确认（与 ✕·取消·Esc 同路径）；忙碌（保存在途）一切忽略；全部添加/编辑弹窗与抽屉接线同一守卫 | round13 统一关闭语义；round14 遮罩改弹确认 | round13/round14 | L2+L3 |
| TC-ED-34 | 必填守卫（useRequiredGuard）：未填禁存（blocked）；强点保存 → 首个未填项滚动进视野并聚焦；全部添加/编辑界面接线同一守卫 | round14 需求（spec §8.1） | — | L2+L3 |
| TC-ED-35 | 键值行纯逻辑（kvPairs）：行态 ↔ [{Key,Value}] 往返无损 / 半行分类与过滤 / 外部回读合并 / 清空机制移除锁定 | round11 BUG/U6 | round11-BUG/U6 | L1 |
| TC-ED-36 | 协议切换会话快照：切走再切回，协议专属设置恢复（对齐桌面版每协议一份实例）；首次到访协议仍走交集携带 | round11-U1 | round11-U1 | L1+L2 |

### 6.5 i18n（spec §7）

覆盖：14 语言键集平价、源码键引用、基准翻译完整性、硬编码文案、术语统一。

| ID | 判据 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-I18N-01 | 14 语言键集平价（全量平价由自动化门禁保证，不记具体键数——V13） | spec §7 | — | L4 |
| TC-I18N-02 | 源码引用的每个键在 en-US/zh-CN 中存在（死键/拼错即红）。**实测战果**：首轮运行即抓到 DataSourceGroup 引用已删键 tree.noDatasources（H32 改名漏改），已修为 tree.noDsHint | spec §7 | H16/死键类 | L4 |
| TC-I18N-03 | zh-CN 基准无三词以上英文句（漏翻译判据；其余 12 语言按 spec §7 允许 en 回退，不算缺陷） | spec §7 | H16 类 | L4 |
| TC-I18N-06 | 组件库语言映射集中 locales/naive.js（14 语言全配 + enUS 回落）；自绘词条 K18-K21 改写后由键平价/漏翻门覆盖 | K24/K18-K21 | round6 | L3+L4 |
| TC-I18N-07 | 「Runner」术语全语言统一（无 运行器/執行器） | H30 | round4-H30 | L4 |
| TC-I18N-08 | 词条清理防线：死词条删除后由键平价门钉住（不得复活）；「帮助」12 生成语言补译由漏翻门覆盖；中文占位符 e.g.→如： 归翻译质量抽查 | round8-P13 | round8-P13 | L4+**M** |
| TC-I18N-03A | **已知遗留**：`about.tagline` zh-CN 仍为英文「Your personal remote manager!」（allowlist；owner 裁决补译或确认为品牌语后处理） | spec §7 | H16 类 | L4 allowlist |
| TC-I18N-04 | 全部 .vue 模板（剥注释后）零硬编码 CJK | spec §7「零硬编码文案」 | H16 | L4 |
| TC-I18N-05 | 脚本字符串字面量无 CJK（console 调试串除外） | spec §7 | — | L4 |
| TC-I18N-07M | 人工：语言切换即时生效；zh/en 抽查译文质量；德语等长文案不破版（弹性容器） | spec §7 | — | **M** |

### 6.6 后端锚点与清理

覆盖：WebUiServer / WebUiEditorService 等后端定案的源码锚点，以及 C# 测试基线。

| ID | 用例 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-BE-01 | index.html 响应 no-cache（升级后不再读旧缓存，避免「修了还复现」假象） | round5-BUG1 附带加固 | round5-BUG1 | L3 |
| TC-BE-02 | 导入=目标文件夹前缀拼接（按 JSON 原有层级放进当前文件夹） | H15 | round4-H15 | L3 |
| TC-BE-02M | 人工：桌面端连接发起/密码询问弹窗/need_password 续答流正常（Web 侧只发 api.connect） | spec §10 | — | **M** |
| TC-BE-03M | 人工：`dotnet test` 全量；基线 2026-09-23（round16 实测）：207/207 全绿（round11 后曾有的 3 个失败 GetGeneral_ReturnsAllTenFields、RdpConfigTests ×2 已修复消失） | — | — | L5 |
| TC-BE-04 | 自启开关 DTO 双向字段 + 写走 ConfigurationService.SetSelfStart（WPF 同入口） | K27 | round6-K27 | L3 |
| TC-BE-05 | 凭据 DAO 修复：无引用凭据更新不再静默丢弃（Commit/Rollback 统一收口）+ 引用服务器联动写**变更后**状态；既有 4 个失败测试转绿（凭据域 21/21） | round11-BUG-C | round11-BUG-C | C# |
| TC-BE-06 | 列表 DTO 存在性布尔 hasPwd/hasKeyPath：密文非空 ⇔ 明文非空（不解密判定）；字段名避开 password/privatekey 子串不破敏感值红线；`List_HasPwdHasKeyPath_FlagsReflectSecretPresence` 钉住 | round11-BUG-B | round11-BUG-B | C# |
| TC-BE-07 | 导出 ids 走 POST body（GET 查询串 8KB 上限不再踩；大量勾选导出不截断） | round13-N7 | round13-N7 | C# |

## 7. 人工冒烟清单（每日 5 分钟，按序）

> 自动化覆盖不到的部分：视觉、动效、真实拖拽手感、跨端联动。改完代码跑完
> `npm run check` 后，按序过一遍；带括号编号的步骤对应 §6 表中带 **M** 的用例。

1. 启动应用 → 列表加载、骨架屏、状态栏统计出现（spec §8.4 SSE）。
2. 树内拖文件夹 A→B 移入；列表拖文件夹入树；同层重排。
3. 进文件夹：只列直接子级；徽标数=进入后台数；面包屑 N 台一致（E5-1 口径）。
4. 勾文件夹（级联）+ 勾空文件夹 → 批量条计数与删除确认三档文案。
5. 搜索 "web"：文件夹列出现+chip 范围标注；清空恢复。
6. 勾 3 台 → Enter/Del 无动作；批量条连接/编辑/导出/删除按钮可用性正确。
7. 双击行连接（复选框列连点不误触）；Esc 逐级回退（菜单→勾选→搜索→光标）。
8. 右键复制密码（验证门按配置走）；E 打开抽屉 Ctrl+S 保存 toast；Esc 脏确认。
9. 设置页：数据源测试连接（草稿路径）；凭据库明文查看验证门。
10. 切语言 zh⇄en 即时生效；切主题即时生效（主按钮描边/悬停形态随主题正常）。
11. 设置→常规→开机自启开关翻转后，任务管理器「启动」页出现/消失 1Remote（K27）。
12. 关闭后端进程 → 3 秒后全屏失联警告；重启后端 → 自动消失（K17）。
13. 在文件夹 A 内搜索：结果只含 A 及其子文件夹（chip 标注「当前文件夹及子文件夹」）；
    退到数据源根再搜同一词 → 整源命中（标注「本数据源」）（round11-需求①）。
14. 勾几台后点进另一文件夹 → 勾选清空、批量条消失；回到原文件夹不恢复（round11-需求②）。
15. 在文件夹 A 内（另一端或本端）删除/改名 A → 列表自动退到上级且无幽灵空列表（round11-需求③）。
16. 凭据库：新建保存瞬间无「已存在」误报；无密码凭据编辑框为空（非掩码）；无引用凭据改密码保存后 reveal 取到新值（round11 三 BUG）。
17. 批量连接：进度提示出现；确认对话框开着时再点「连接」不重复发起（round11-U2）。
18. 批量删除：确认后执行中再点删除不叠第二轮（round13-N8）；只勾空文件夹按 Del/Enter 无动作（round13-N12）。
19. 数据源添加：保存瞬间不闪「已存在」、重开是空白表单（round12）；测试进行中编辑/删除禁用（U16）；端口填 70000 → 标红 + 保存/测试禁用（U17）；遮罩点击不直接关弹窗（U15）。
20. 切语言后重启应用仍保持所选语言（唯一持久化在后端，round13-N6/R5）；热键录制中点击窗口其它位置即退出录制（U19）。
21. 草稿守卫：任意添加/编辑弹窗输入内容后点遮罩 → 弹「未保存的更改」确认；清空输入后点遮罩 → 直接关（round13/14）。
22. 必填引导：必填项未填点保存 → 保存被拦且首个未填项滚动到视野并聚焦（round14）。
23. 设置往返后切文件夹：界面正常刷新、面包屑行右侧工具簇仍在（Teleport 目标竞态回归，
    round16 用户报障「设置返回后切文件夹界面冻结」；自动化=S24）。

## 8. 断言红了怎么办（排障流程）

1. **读失败输出**：node:test 会打印失败用例标题、断言位置（文件:行）与 expected/actual 差异。
   L3 的失败消息里通常直接写明它防的是哪一轮的什么 bug。
2. **先判断红的性质**，二选一：
   - **真回归**——最近的改动把定案弄坏了 → 修代码，不要迁就着改断言；
   - **有意的设计变更**——owner 已拍板改语义 → 先更新本文档 §6 对应行的「用例/设计依据」，
     再更新断言，并在断言旁注明新依据（引用报告条目或 spec 章节）。
3. **拿不准就查出处**：用例标题/消息里的编号（K2、U5……）按 §2.3 的方法去 `docs/` 搜
   评审原文与修复报告。
4. **改完自证**：`npm run check` 全绿 + `npm run build` 通过；涉及 C# 锚点的跑 §3 ③。
5. **PR/提交说明里写明**：红了哪条、属于回归还是设计变更、依据是什么。

> i18n 门（L4）的 allowlist 是**只减不增**的白名单：新增条目必须同步记入 §9 已知遗留，
> 否则视为把漏翻合法化。

## 9. 已知遗留

| 项 | 现状 | 处理 |
|---|---|---|
| `about.tagline` zh-CN/zh-TW 为英文 | 「Your personal remote manager!」未翻译（H16 同类：手写基准漏翻） | owner 裁决：补译（走 zh/en 基准 + ROUND4 表）或确认为品牌语保留，处理后移出 allowlist |
| 语义测试双入口 | webui/scripts/`semantics-test.mjs`（12 条，报告历史引用）与 `Tests/WebUI/folders.test.mjs`（超集）并存 | 保留旧入口，语义以 folders.test.mjs 为准 |

> 2026-09-23 round16 复验：原第三行「后端 3 个基线失败」（GetGeneral_ReturnsAllTenFields、
> RdpConfigTests ×2）已随 round13/14 工作区修复消失（实测 207/207），从本表移除。

### 9.1 本套件的实测战果（为什么值得维护）

- **抓到并修复**：`DataSourceGroup.vue:352` 引用已删除词条 `tree.noDatasources`（H32 改名漏改
  该处）——设置→数据源空态会直出键名。建套件首跑即抓到，已改为 H32 定案键 `tree.noDsHint`。
- **确认遗留**：`about.tagline` zh-CN 英文残留（allowlist 记录在案）。

## 10. 如何新增一条用例（step-by-step）

1. **判断能不能自动化**。能写成纯函数/纯数据断言的（L1/L2），不要放进人工（**M**）。
   判断参考 §4 各层的"能抓什么"。
2. **放对文件**。按 §4 的载体表选；新主题（如本次的 draft-guard）可以新建
   `<主题>.test.mjs`，文件头按现有文件的风格写一段"本文件测什么 + 语义单一来源在哪"。
3. **写断言**。模板：

   ```js
   import { describe, it } from 'node:test'
   import assert from 'node:assert/strict'
   // 被测模块从 webui 源码相对导入；包依赖（vue 等）走 helpers 的 loadVue()
   import { isInSubtreeOf } from '../../webui/src/composables/folders.js'

   describe('某功能（来源：roundN-XX）', () => {
     it('白话描述预期行为（含防的是哪个 bug）', () => {
       assert.equal(isInSubtreeOf('a/b', 'a'), true)
     })
   })
   ```

   - 标题写给**半年后的自己**：行为 + 出处编号，不写实现细节。
   - L3 锚点断言的失败消息要写清"防的是哪一轮的什么"。
4. **登记进本文档**：在 §6 对应小节加一行（ID 顺延，别与既有冲突；M 后缀留给人工），
   五列填法见 §2.2。若新开了测试文件，同步 §4 载体表。
5. **验证**：`npm run check` 全绿；`npm run build` 通过。
6. **有 L4 allowlist 变化**（新增/删除豁免）必须同步 §9。

## 11. 目录约定与文件速查

> 本目录（`Tests/WebUI/`）存放全部 WebUI 测试用例与文档；npm 运行入口仍在 webui 侧
> （`cd webui && npm run test`，glob 指向本目录）。

| 文件 | 层 | 一句话 |
|---|---|---|
| `folders.test.mjs` | L1 | 文件夹/路径/搜索范围纯函数语义 |
| `kv-pairs.test.mjs` | L1 | 键值行编辑器纯逻辑（往返/半行/合并） |
| `protocol-switch.test.mjs` | L1/L2 | 编辑器协议切换会话快照 |
| `ds-title.test.mjs` | L1 | 数据源状态点 title 工厂（含重连倒计时） |
| `row-checks.test.mjs` | L2 | 行勾选模型（级联/空文件夹/三态/剔除） |
| `editor-schema.test.mjs` | L2 | 9 协议 schema 结构 + 批量补丁 diffPatch |
| `draft-guard.test.mjs` | L2 | 模态草稿守卫（关闭语义/丢弃确认） |
| `required-guard.test.mjs` | L2 | 必填项守卫（禁存/滚动引导） |
| `source-invariants.test.mjs` | L3 | 源码接线锚点（守卫/顺序/形态，按轮次分组） |
| `i18n.test.mjs` | L4 | i18n 五道门 |
| `helpers.mjs` | — | 共享工具（读源码/剥注释/SFC 分段/loadVue） |

其他约定：

- `.prettierrc` 是 webui 配置的同步拷贝（prettier 按文件位置找配置，包内配置覆盖不到这里；
  改风格两处同步）。
- 对 vue 等包依赖**不要裸 `import 'vue'`**——本目录在 webui 包外，解析不到 node_modules。
  统一走 `helpers.mjs` 的 `loadVue()`（createRequire 以 webui/package.json 为基准）；
  需要与源码模块共享同一 vue 实例时，也可像 required-guard 那样直连
  `../../webui/node_modules/vue/index.mjs`。
- 每次迭代完成后先跑 §3 流程，再写修复报告的「总体回归」表格（引用本档 §3 命令）。
