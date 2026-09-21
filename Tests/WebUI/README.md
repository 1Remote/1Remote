# 1Remote Web UI 测试用例（每日回归）

- **日期**：2026-09-21
- **目的**：在持续迭代、新增需求、重构的过程中，把三轮修复报告（round2 / round4 /
  round5-followup）里反复出现的错误形态固化成**可自动执行的用例**，每日迭代完成后一键检查
  是否仍满足设计。设计依据：`docs/superpowers/specs/2026-09-15-web-ui-redesign-design.md`（下称 spec）。
- **用例来源**：spec §2-§12 + 三份修复报告定案形态 + 历史评审条目（G/H/E5/J 系列）。
- **配套自动化**：`Tests/WebUI/`（本目录，node:test 零新增依赖，93 条用例全绿；运行入口在 webui 侧：`cd webui && npm run test`）。

## 1. 每日回归流程（迭代完成后执行）

```bash
# ① 自动化用例 + 格式 + 语言键平价（约 10 秒）
cd webui && npm run check

# ② 生产构建（接线错误经构建校验；产物供桌面端托管）
npm run build

# ③ 后端（改到 C# 侧或导入导出/批量端点时）
cd .. && dotnet test Tests/Tests.csproj
dotnet build Ui/Ui.csproj   # 同时把 dist/ 刷新进 wwwroot（运行 1Remote.exe 生效）
```

通过标准：①②全绿；③在后端未改动时可跳过。

## 2. 用例分层

| 层 | 载体 | 能抓什么 | 局限 |
|---|---|---|---|
| L1 纯逻辑 | `Tests/WebUI/folders.test.mjs` | 文件夹显示/勾选双语义、路径前缀、tree-state 键迁移、树模型、自然 IP 排序、边栏过滤 | 不碰组件接线 |
| L2 组件模型 | `Tests/WebUI/row-checks.test.mjs` + `Tests/WebUI/editor-schema.test.mjs` | 勾选模型全语义、9 协议 schema 结构与批量补丁 | 不碰模板绑定 |
| L3 源码不变量 | `Tests/WebUI/source-invariants.test.mjs` | 接线级回归：守卫遗漏、事件顺序、判定先后、CSS 锚点、后端锚点 | 断言的是「定案形态」，有意重构须同步更新断言 |
| L4 i18n | `Tests/WebUI/i18n.test.mjs` | 键平价、死键引用、漏翻译、硬编码文案、术语漂移 | 无法保证译文质量 |
| L5 构建链 | `npm run build` + `format:check` + `convert-locales --check` | 编译错误、风格、14 语言键集 | 不验证行为 |
| M 人工 | 本文档操作步骤 | 视觉、动效、真实拖拽手感、跨端联动 | 无法全自动 |

> L3 的哲学：历史 bug 的共性是「正确语义已存在，接线把它断了」（round5-BUG1 的 canDrop
> 先置空即典型）。把定案形态固化为源码断言后，任何重构只要破坏既有接线，当天就能红。
> 若为**有意**的设计变更，同步更新断言并注明新依据（每条断言都注明来源轮次）。

## 3. 用例明细

### 3.1 文件夹/树语义（spec §3.2 §3.5）

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

### 3.2 行列表（spec §3.4）

| ID | 用例 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-LIST-01/02 | 勾选级联（限定同数据源防串库）；空文件夹复选框=勾选文件夹本身（checkedFolders，值形状 {dsName,path}） | round5-BUG2 | round5-BUG2 | L2 |
| TC-LIST-03 | 文件夹行复选框无 :disabled；悬停提示区分「级联子孙/选择空文件夹」两语义 | round5-BUG2 | round5-BUG2 | L3 |
| TC-LIST-04/05 | 表头全选=当前视图可见行（合并语义保留隐藏子孙）；数据变化即时剔除已不存在的勾选 | useRowChecks 文件头 | — | L2 |
| TC-LIST-06 | Ctrl 切换 / Shift 范围（锚点）/ 裸点击=纯光标；视图变化作废锚点 | useRowChecks 文件头 | — | L2 |
| TC-LIST-07 | 文件夹视图显示口径 isDirectChildOf 单一来源；复选框列从双击识别区排除（服务器行+文件夹行） | round4-反馈；round2 附加 | round5-BUG3 | L3 |
| TC-LIST-08 | 空文件夹批量删除链路：批量条出现条件 / 混合计数「已选 N 台 · M 个空文件夹」 / 三档确认文案 | round5-BUG2 | round5-BUG2 | L2+L3 |
| TC-LIST-09 | 搜索激活：文件夹列强制显示；列菜单该项禁用+说明；搜索 chip 带「全库范围」 | H9 | round4-H9 | L3 |
| TC-LIST-10 | 拖拽归因顺序：跨库判定先于重排模式提示 | H27 | round4-H27 | L3 |
| TC-LIST-11Z | 跨库 dragend 兜底监听恒挂 ServerTable（不随边栏卸载失效） | H18 | round4-H18 | L3 |
| TC-LIST-12M | 人工：列表行拖到文件夹行移入；「..」行上移一级；≡ 自定义排序模式下上/下半区前插/后插（非 custom 一次性提示）；文件夹行不可重排（一次性提示） | spec §3.5 | — | **M** |
| TC-LIST-13M | 人工：表头三态排序循环（名称/地址/协议/最近连接），地址列自然 IP 序（10.0.0.2 < 10.0.0.10）；刷新后保持 | spec §3.4 | — | L1+**M** |
| TC-LIST-14M | 人工：列宽拖拽、双击重置；列菜单显隐（文件夹列仅根视图可用）并持久化 | spec §3.4 | J14 | **M** |
| TC-LIST-15M | 人工：>500 行启用虚拟滚动，滚动不跳动 | spec §3.4 | — | **M** |
| TC-LIST-16M | 人工：右键菜单 全项检查（连接/新窗口连接‹占位›/其他凭据‹占位›/编辑/复制 Ctrl+D/复制地址/复制用户名/复制密码/桌面快捷方式‹占位›/删除 Del），占位项禁用+「即将推出」 | spec §3.4/H4 | round4-H4 | **M** |
| TC-LIST-17M | 人工：复制密码验证门（未开启验证直接复制；开启后桌面端弹验证，取消→403 toast；30s 窗口内免重验；Serial/Telnet 提示「无密码可复制」） | H4 | round4-H4 | **M** |
| TC-LIST-18M | 人工：行双击连接；复选框列快速连点**不**误触连接；▸/✎/⋯ 常显按钮 | round2 附加 | round2 附加 | **M** |
| TC-LIST-19M | 人工：地址列 `address:port (userName)`、用户名段弱化、长文本省略号、悬停全文；Serial 回退 COM1(9600) | round4-设计改动3/J3/J5 | round2 | **M** |
| TC-LIST-20M | 人工：备注列 Markdown 悬停弹层；时间列相对时间+悬停绝对时间；标签胶囊最多 2+溢出计数 | spec §3.4 | — | **M** |
| TC-LIST-21M | 人工：空态三态（空库引导卡/空文件夹/无匹配逐层清除）；空库引导卡协议一览 | spec §8.5 | 第三轮 G16 | **M** |
| TC-LIST-22M | 人工：批量导出下载、批量连接超阈值确认 | spec §3.4 | — | **M** |

### 3.3 键盘流与浮层（spec §8.2）

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
| TC-KBD-07M | 人工：Ctrl+K 聚焦搜索；Ctrl+F→↓→Enter 连接流（搜索框移交表格焦点） | spec §8 | 第三轮 G2 | **M** |
| TC-KBD-08M | 人工：Menu/Shift+F10 呼出行菜单；↑↓ 移动光标行（外框可见+滚入可视区） | spec §8.2 | 第三轮 G3 | **M** |
| TC-KBD-09M | 人工：Ctrl+A 全选可见/清空；E 编辑；Ctrl+D 复制预填「(副本)」不叠加后缀 | spec §8.2/H26 | round4-H26 | **M** |
| TC-KBD-09Z | 复制预填后缀剥净再补一个（源码断言 while endsWith） | H26 | round4-H26 | L3 |
| TC-KBD-10M | 人工：编辑抽屉 Ctrl+S 保存；Esc 关闭（脏则确认；确认开着时 Ctrl+S 无动作） | spec §5/H21 | round4-H21 | **M** |
| TC-KBD-11M | 人工：<900px 边栏自动收起（只收不展）；收起态入口可用；窄窗跨库拖拽有 toast 反馈 | spec §8.7/H18 | round4-H18 | **M** |
| TC-KBD-12M | 人工：主题切换（基底×强调色×经典预设×字号×字体）即时生效；服务器自定义色不受影响 | spec §4 | — | **M** |

### 3.4 编辑器与设置（spec §5 §6）

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

### 3.5 i18n（spec §7）

| ID | 判据 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-I18N-01 | 14 语言键集平价（580 键 × 14） | spec §7 | — | L4 |
| TC-I18N-02 | 源码引用的每个键在 en-US/zh-CN 中存在（死键/拼错即红）。**实测战果**：首轮运行即抓到 DataSourceGroup 引用已删键 tree.noDatasources（H32 改名漏改），已修为 tree.noDsHint | spec §7 | H16/死键类 | L4 |
| TC-I18N-03 | zh-CN 基准无三词以上英文句（漏翻译判据；其余 12 语言按 spec §7 允许 en 回退，不算缺陷） | spec §7 | H16 类 | L4 |
| TC-I18N-03A | **已知遗留**：`about.tagline` zh-CN 仍为英文「Your personal remote manager!」（allowlist；owner 裁决补译或确认为品牌语后处理） | spec §7 | H16 类 | L4 allowlist |
| TC-I18N-04 | 全部 .vue 模板（剥注释后）零硬编码 CJK | spec §7「零硬编码文案」 | H16 | L4 |
| TC-I18N-05 | 脚本字符串字面量无 CJK（console 调试串除外） | spec §7 | — | L4 |
| TC-I18N-06 | 「Runner」术语全语言统一（无 运行器/執行器） | H30 | round4-H30 | L4 |
| TC-I18N-07M | 人工：语言切换即时生效；zh/en 抽查译文质量；德语等长文案不破版（弹性容器） | spec §7 | — | **M** |

### 3.6 后端锚点与清理

| ID | 用例 | 设计依据 | 历史缺陷 | 自动化 |
|---|---|---|---|---|
| TC-BE-01 | index.html 响应 no-cache（升级后不再读旧缓存，避免「修了还复现」假象） | round5-BUG1 附带加固 | round5-BUG1 | L3 |
| TC-BE-02 | 导入=目标文件夹前缀拼接（按 JSON 原有层级放进当前文件夹） | H15 | round4-H15 | L3 |
| TC-BE-02M | 人工：桌面端连接发起/密码询问弹窗/need_password 续答流正常（Web 侧只发 api.connect） | spec §10 | — | **M** |
| TC-BE-03M | 人工：`dotnet test` 全量（197+ 用例）；既有失败清单见 round4 报告 §一 | — | — | L5 |

## 4. 人工冒烟清单（每日 5 分钟，按序）

1. 启动应用 → 列表加载、骨架屏、状态栏统计出现（spec §8.4 SSE）。
2. 树内拖文件夹 A→B 移入；列表拖文件夹入树；同层重排。
3. 进文件夹：只列直接子级；徽标数=进入后台数；面包屑 N 台一致（E5-1 口径）。
4. 勾文件夹（级联）+ 勾空文件夹 → 批量条计数与删除确认三档文案。
5. 搜索 "web"：文件夹列出现+chip「全库范围」；清空恢复。
6. 勾 3 台 → Enter/Del 无动作；批量条连接/编辑/导出/删除按钮可用性正确。
7. 双击行连接（复选框列连点不误触）；Esc 逐级回退（菜单→勾选→搜索→光标）。
8. 右键复制密码（验证门按配置走）；E 打开抽屉 Ctrl+S 保存 toast；Esc 脏确认。
9. 设置页：数据源测试连接（草稿路径）；凭据库明文查看验证门。
10. 切语言 zh⇄en 即时生效；切主题即时生效。

## 4.5 已知遗留

| 项 | 现状 | 处理 |
|---|---|---|
| `about.tagline` zh-CN/zh-TW 为英文 | 「Your personal remote manager!」未翻译（H16 同类：手写基准漏翻） | owner 裁决：补译（走 zh/en 基准 + ROUND4 表）或确认为品牌语保留，处理后移出 allowlist |
| 语义测试双入口 | webui/scripts/`semantics-test.mjs`（12 条，报告历史引用）与 `Tests/WebUI/folders.test.mjs`（超集）并存 | 保留旧入口，语义以 folders.test.mjs 为准 |

## 4.5.1 本轮测试建设中的实测战果

- **抓到并修复**：`DataSourceGroup.vue:352` 引用已删除词条 `tree.noDatasources`（H32 改名漏改该处）——设置→数据源空态会直出键名。已改为 H32 定案键 `tree.noDsHint`；`npm run build` 已过。
- **确认遗留**：`about.tagline` zh-CN 英文残留（allowlist 记录在案）。

## 5. 用例维护规则

> 目录约定：本目录（`Tests/WebUI/`）存放全部 WebUI 测试用例与文档；npm 运行入口仍在
> webui 侧（`cd webui && npm run test`，glob 指向本目录）。`.prettierrc` 是 webui 配置的
> 同步拷贝（prettier 按文件位置找配置，包内配置覆盖不到这里；改风格两处同步）。
> 对 vue 等包依赖的解析统一走 `helpers.mjs` 的 `loadVue()`（createRequire 以
> webui/package.json 为基准），不要在本目录裸 import 包名。

1. **新增需求**：合并前在本文档对应小节补用例行，标 `**M**` 或指定测试文件；能进 L1-L4 的不进 M。
2. **修复报告中的新定案**：把定案形态同步为 L3 断言（复制现有断言改选择器），并在断言旁注明报告条目。
3. **L3 断言红但代码正确**：说明设计有意变更——更新断言前先在本文档更新「设计依据」。
4. **allowlist 膨胀即警报**：L4 的 allowlist 只减不增；新增即记入 §4.5 已知遗留。
5. 每次迭代完成后先跑 §1 流程，再写修复报告「总体回归」表格（引用本档 §1 命令）。
