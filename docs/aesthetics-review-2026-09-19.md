# 1Remote Web UI 美学设计修正报告（多人审核）

- 日期：2026-09-19
- 分支：`Branch_web_ui`（owner 验收批次 11 · Task C）
- 性质：**审核报告，未修改任何 `webui/src` 产品代码**。全部建议供 owner 决策后另行实施。
- 配套示意图：`docs/aesthetics-review-2026-09-19.html`（双击打开，自包含，可切 暗/亮基底 × 7 强调色）
- 审核对象：`webui/src/themes/theme.css` + 24 个组件的 scoped 样式（App / ServerTable / ServerRow / FolderRow / SideTree / TableToolbar / StatusDot / ProtocolBadge / HelpLink / EditorDrawer / EditorHead / FormField / SwitchItem / MarkdownField / SubformList / BulkEditForm / ImportModal / ServerListView / SettingsView / 7 个 settings 分组 / TagManagerModal / RunnerCard 等）
- 风格基准：Linear 暗色克制风（中性灰阶 + 低饱和强调色、1px 边框、紧凑密度、12.5px 基准字号），主题系统 = CSS 变量（暗/亮 × 7 强调色），组件库 = Naive UI

---

## 1. 执行摘要

三位审核者（视觉设计师 / UI 工程师 / 设计系统审查员）共提出 **22 条发现**：视觉 8 条（V1-V8）、工程 8 条（E1-E8）、系统 6 条（S1-S6）。修正建议总表按影响面分 P1（全站级）/ P2（跨组件）/ P3（点状）三级。

**最重要的三条：**

1. **S1 · 主题令牌只覆盖颜色一个维度**。`theme.css` 定义了 16 个颜色变量，但圆角（在用 3/4/5/6/7/8/10px 七种）、字号（9-20px 共 12 档）、控件高度（22-30.5px 六档）、禁用不透明度（5 种）、阴影（4 处 3 种）、动效时长（8 种）全部散写在各组件。这是 10 轮验收迭代中"多批人分工叠加产生风格漂移"的根因——每个批次只能凭肉眼对齐，没有可引用的标尺。
2. **E6+V1 · 双圆角语言同屏并存**。Naive UI 组件走默认 3px 圆角（`themes/index.js` 的 overrides 未设 `borderRadius`），自定义控件走 5/6/7px；同一面包屑行里 `n-button`（3px）、`bb-btn`（6px）、`tt-btn`（5px）三种圆角并排，搜索框 7px 与编辑抽屉内 `n-input` 3px 各执一词。
3. **E3 · 主按钮 hover 行为分叉（复制漂移的直接证据）**。`TableToolbar` 的 `.bb-primary` 没有专属 hover 规则，叠加 `.bb-btn:hover:not(:disabled)`（特异性更高）后强调色边框在悬停时**退化为中性 `--border-strong`**；而 `EditorDrawer` 的 `.ed-primary:hover` 显式保留了 accent 边框。同一"主按钮"语义在两处实现出相反的悬停结果。

另有两处接近功能性问题：空库引导 CTA（`eg-btn`）**没有任何 hover 反馈**（V4）；搜索命中高亮 `.hl` 在部分强调色下对比度低于 AA（dark+blue 3.5:1、light+green 2.3:1，S4）。

所有建议均满足"功能不变、易用性不降低"约束；多数为视觉参数收敛，不触碰交互逻辑。

---

## 2. 设计令牌使用现状清单

### 2.1 颜色

| 类别 | 现状 | 评价 |
| --- | --- | --- |
| 主题变量 | `theme.css` 16 个：`bg / bg-panel / bg-elevated / bg-hover / border / border-strong / text-1..4 / danger / success / warning / accent / accent-hover / accent-container / accent-text`，暗亮双基底 × 7 强调色（含亮色 `accent-container` 浅底深字变体） | 结构清晰，取色纪律好——组件样式基本全部走变量 |
| 硬编码 `red` | `App.vue:357`（⚙ 红点）、`SettingsView.vue:237`（关于项红点）、`AboutGroup.vue:240`（更新链接红点）——WPF `Fill="Red"` 遗产 | 亮色下纯红过饱和，且与 `--danger`（dark `#ef6a6a` / light `#dc2626`）并存两种红 |
| 硬编码 `#fff` | `HelpLink.vue:156`（徽标 hover 反白字形）、`themes/index.js` Naive 按钮文字白（有意） | 可接受；HelpLink 若未来加浅色强调色需复核 |
| 阴影色 | `0 6px 24px rgb(0 0 0 / 25%)` ×3（ServerTable:950 / SideTree:613 / TableToolbar:199，逐字相同）、抽屉 `-10px 0 28px rgb(0 0 0 / 22%)`（EditorDrawer:558） | 无令牌；亮色基底沿用 25% 黑，偏脏（S3） |
| 数据色 | `ProtocolBadge` 9 个协议身份色、`FormField` COLOR_SWATCHES 8 色、`RunnerCard` PuTTY 主题色 | 有注释说明，属数据非样式，豁免 |
| 预览复写 | `AppearanceGroup.vue:177-203` 迷你预览硬编码 `#0f1011/#fafafa/#565a63/#a1a1aa`，与 `theme.css` 同值双源 | 漂移风险（E8） |

### 2.2 字号（根字号 13px，rem 换算为 px）

| 档位(px) | rem | 使用次数 | 典型用途 |
| --- | --- | --- | --- |
| 9 | 0.6923 | 2 | 表头排序箭头（ServerTable:904）、pin 图标 |
| 10 | 0.7692 | 8 | chevron、ds-type、sc-x/f-x 关闭钮 |
| 10.5 | 0.8077 | 8 | tag/ProtocolBadge/macro-pill/ctx-hint/ed-ds |
| 11 | 0.8462 | 21 | count/tm 计数/sb-lang/ver-badge/hint |
| 11.5 | 0.8846 | 32 | thead/时间列/status-bar/r-badge/f-hint |
| 12 | 0.9231 | 51 | bb-btn/act-btn/r-tab/ed-seg/空态提示 |
| 12.5 | 0.9615 | 50 | 行文本/ctx-item/ed-btn/标签列/crumb |
| 13 | 1 | 10 | 树 label/act 字形/placeholder 标题 |
| 14 | 1.0769 | 9 | 页面标题/s-header/ed-title/expand-rail |
| 18 | 1.3846 | 1 | AboutGroup hero-name |
| 20 | 1.5385 | 1 | ImportModal dz-icon |
| 11(px 硬编码) | — | 1 | `HelpLink.vue:169`（.hl-badge-char 兜底） |

**问题**：12 档中 `11 vs 11.5`、`12 vs 12.5` 两对近邻档在视觉上不可区分，却被不同批次交替用作同级文本（按钮字号 12 与 12.5 各占一半）；基准正文 12.5px 的约定没有落实到按钮/次级控件。

### 2.3 圆角

在用值：`2 / 3 / 4 / 5 / 6 / 7 / 8 / 10 / 999(pill) / 50%(圆)`。

| 值 | 出现次数 | 代表位置 |
| --- | --- | --- |
| 2px | 2 | `.hl` 命中高亮（ServerRow:356）、mini-line |
| 3px | 11 | 行内 code、`ff-mini-btn`/`ff-color-box`/`ff-tag-sug-chip`（对齐 Naive 默认） |
| 4px | 5 | `ff-cur` 色块、r-badge、type-badge、crumb-btn、theme-dot |
| 5px | 22 | ctx-item、tag、行内图标钮（act/tt-btn/pin-btn）、sf-add、树行 |
| 6px | 20 | bb-btn、ed-btn、卡片内按钮、hk-box、ed-seg、r-tab、banner |
| 7px | 8 | **searchbox(App:287)、s-item/s-back(SettingsView:178,210)、eg-btn(ServerListView:747)、ed-tile(EditorHead:171)、md-preview(MarkdownField:86)、err-list(ImportModal:341)、AppearanceGroup .seg(:237)** |
| 8px | 11 | ctx-menu/col-menu/tree-ctx 浮层、卡片（r-card/ds-card/dropzone）、cv-table |
| 10px | 1 | AboutGroup hero-logo |

**问题**：7px 是一个完整孤立的"批次方言"（搜索框+设置页+空态按钮+抽屉头部+预览框）；5px 与 6px 在小按钮上混用（tt-btn 5 vs bb-btn 6 同一行）。

### 2.4 间距与控件高度

- `gap` 在用：2/3/4/5/6/7/8/9/10/12/14/16/18/24px（任意值，无 4px 基网格约定）；按钮水平内边距 9/10/12/14px 四种，垂直 5/6/7/8px 四种，另有 2.5px 半像素（tag/ProtocolBadge）。
- 控件高度（实测渲染值）：按钮 **23.5**（bulk-toggle）/ **24**（bb-btn、DataSourceGroup .act）/ **26**（en-clear、act-btn、sf-add、ed-seg）/ **28**（seg-btn、ff-mini-btn）/ **28.5**（ed-btn、ctx-item）/ **29.5**（r-tab）/ **30.5**（eg-btn）；图标钮 **18/20/22/24/26/28** 六种；输入类 searchbox 26、Naive small 28。
- 内容柱行高：树行 26、表头 32、面包屑行 34、数据行 36、设置导航项 34。

### 2.5 边框（--border vs --border-strong）

- 分隔线/卡片/行统一 `--border`；`--border-strong` 用于：表头下沿（ServerTable:827）、浮层菜单边框、搜索框边框、blockquote 竖线、dropzone 虚线。逻辑成立。
- 悬停升级模式 `border → border-strong` 在 8 处自定义按钮上一致（bb-btn/tt-btn/pill/base-card/ff-mini-btn/en-clear/hk-box/tag-chip）——这是好的一致性。
- **例外**：`RunnerCard` act-btn:469、`SubformList` sf-add:224、`FormField` ff-tag-sug-chip:708 悬停直接跳 accent 边框，形成第二种 hover 哲学（S2）。

### 2.6 阴影与动效

- 阴影：菜单类 `0 6px 24px / 25%`（复制 3 份）、抽屉 `-10px 0 28px / 22%`、StatusDot 光晕 `0 0 6px`、录制脉冲 `0 0 0 3px accent-container`。无令牌。
- 动效时长：0.12s（chevron、HelpLink）/ 0.15s（dropzone）/ 0.17s（抽屉）/ 0.4s（RunnerCard 闪光）/ 0.7s（App spinner）/ 0.8s（凭据库 spinner）/ 1.2s（快捷键脉冲）/ 1.4s（骨架屏）。
- `prefers-reduced-motion` 覆盖 4/6 处动画（EditorDrawer、骨架屏、凭据库 spinner、快捷键脉冲有；**App 搜索 spinner、RunnerCard 闪光缺**，E7）。

### 2.7 焦点与禁用

- `:focus-visible` 仅 `HelpLink.vue:113` 一处自定义实现；其余自定义按钮依赖浏览器默认焦点环，与 Naive 组件焦点环三种体系并存（S6）。
- 禁用不透明度：0.35（pin-btn）/ 0.45（ServerRow .act、CredentialVault、TagManager）/ 0.5（bb-btn、DataSourceGroup .act、f-x）/ 0.55（ff-mini-btn、ed-btn、eg-btn、act-btn）/ 0.6（ctx-item）——5 种（E4）。

---

## 3. 三位审核者发现

图例：每条含 位置 → 现状 vs 期望 → 建议 → 影响面。★★★ = 建议尽快处理，★★ = 跨组件一致性问题，★ = 点状打磨。

### 3.1 视觉设计师（令牌一致性 / 层级表达 / 对齐网格）

**V1 · 圆角无阶梯语义 ★★★**
位置：全站（见 2.3 表）。
现状：3/4/5/6/7/8/10px 七种非胶囊圆角；同一行工具栏内 3/5/6px 并存；7px 是孤立方言带。
期望：Linear 式三档语义——微元素/行内 3px、控件 6px、容器 8px（+ pill 999px / 圆 50%）。
建议：`4→3`（微型徽章/色点）、`5→6`（小按钮/菜单项）、`7→6`（控件类：searchbox、s-item、eg-btn、ed-tile、seg）或 `→8`（容器类：md-preview、err-list）、`10→8`（hero-logo 可豁免）。配套在 `themes/index.js` overrides 增加 `common.borderRadius` 统一 Naive 侧（见 E6）。
影响面：全站控件外观统一；改动为纯 CSS 值替换，零功能风险。

**V2 · 字号阶梯过密，近邻档混用 ★★★**
位置：全站（见 2.2 表）。
现状：12 档中 11 vs 11.5（21/32 次）、12 vs 12.5（51/50 次）两对不可分辨档位被跨组件随机选用；"12.5px 基准"未覆盖按钮（12px 居多）。
期望：可感知的层级阶梯，正文/按钮统一 12.5px。
建议：收敛为 5 档：**10.5（微）/ 11.5（辅）/ 12.5（正文·按钮）/ 14（标题）/ 18（展示）**；9/10→10.5，11→11.5，12→12.5，13→12.5，20→18；箭头等 9px 字形保留但归"装饰"豁免。
影响面：全站文本；一次批量替换 + 逐屏核对。

**V3 · 按钮高度六档漂移 ★★★**
位置：`TableToolbar.vue:105-129`（bb-btn 24px）、`EditorDrawer.vue:790-799`（ed-btn 28.5px）、`ServerListView.vue:745-754`（eg-btn 30.5px）、`DataSourceGroup.vue`（.act 24px）、`RunnerCard.vue:457-467`（act-btn 26px）、`BulkEditForm.vue:399-412`（bulk-toggle 23.5px）、`ServerListView.vue:808-818`（en-clear 26px）等。
现状：同类"次级小按钮"实测 23.5/24/26/28/28.5/29.5/30.5px 七种高度；图标钮 18-28px 六种。
期望：紧凑密度两档：**24px（行内紧凑）/ 28px（标准表单/对话框）**；图标钮同步 24/28。
建议：bb-btn/act/bulk-toggle 维持 24；ed-btn/ctx-item/eg-btn 归 28（eg-btn 30.5→28：padding 8px→7px）；en-clear/act-btn/sf-add/ed-seg 26→24 或 28 按语境归类。
影响面：跨 10+ 组件的按钮外观统一。

**V4 · 空态引导 CTA 无 hover 反馈 ★★★**
位置：`ServerListView.vue:745-762`。
现状：`.eg-btn` 只有基础态与 `:disabled`，**没有 `:hover`**；`.eg-primary` 也没有 hover 强化。空库首启动的"新建服务器/导入"是全站第一 CTA，反馈弱于列表批量条同类按钮。
期望：与 `.ed-btn`/`.bb-btn` 同款 hover（border-strong + bg-hover + text-1），主按钮保留 accent 边框（见 E3 统一规则）。
建议：补两条规则即可，纯增量。
影响面：空库/离线/无匹配三个空态的可用性观感。

**V5 · 更新红点 `background: red` 硬编码 3 处 ★★**
位置：`App.vue:357`、`SettingsView.vue:237`、`AboutGroup.vue:240`（注释已自知是 WPF 遗产）。
现状：纯红 `#f00000` 在亮色基底上过饱和，与 `--danger` 两种红并存。
期望：通知点纳入主题色系。
建议：改 `var(--danger)`，或新增 `--dot-update` 令牌（若要保持比 danger 更醒目，可在 theme.css 按基底定值）。
影响面：3 处小改；主题一致性。

**V6 · 指示器笔画宽度四种并存 ★★**
位置：StatusDot idle 空心圈 1.5px（StatusDot.vue:44）、搜索高亮 `.hl` 圆角 2px（ServerRow:356）、拖拽指示线 2px（ServerTable:811-815 / SideTree:456-459）、树叶选中条 2px（ServerRow:170）、blockquote 3px（ServerRow:327 / MarkdownField:143）、HelpLink 圆环实际 1.5px。
现状：1 / 1.5 / 2 / 3px 四种"线条重量"表达同类强调。
期望：细线统一 1.5px（控件级描边）、强调条统一 2px（状态指示），blockquote 3px 可保留为文档语义。
建议：无需大动，值域收敛到 {1, 1.5, 2, 3}→{1.5, 2, 3} 三档并写入令牌注释。
影响面：低；点状统一。

**V7 · 内容柱行高 32/34/36 近邻三档 ★**
位置：表头 32px（ServerTable:825）、面包屑行 34px（ServerListView:575）、数据行 36px（ServerRow:158）。
现状：同一内容柱内三个近邻行高，34 与 36 差异不可感知但造成节奏不齐。
期望：内容柱横向分隔带归并为 32（chrome 带）与 36（数据带）两档。
建议：面包屑行 34→36 与数据行对齐（或 32 与表头对齐，二选一，倾向 36——批量条按钮 24px 在 34px 行内偏挤）。
影响面：单行布局常量；需回归验证 Teleport 工具簇垂直居中。

**V8 · emoji 彩色图标与单色符号混排 ★**
位置：🗄（SideTree 数据源）、📁（SideTree/FolderRow）、📌（标签 pin）、👁（FormField 密码/预览）、🗂（全部数据）为彩色 emoji；▸ ✎ ⋯ ≡ ▦ ⌕ ⚙ 为单色文本符号。
现状：克制灰阶界面里彩色 emoji 亮度跳脱，且跨平台字形不一致（Win/mac 渲染差异）；WPF 遗产。
期望：图标层单色化（跟随 `currentColor`），与 HelpLink 内绘 SVG 同一路线。
建议：长期项——新增轻量 Icon 层（内联 SVG，18/16px 两档）逐个替换 emoji；短期不动。
影响面：全站图标语言；工作量大，列为路线图项。

### 3.2 UI 工程师（实现一致性 / 复制漂移 / CSS 组织）

**E1 · 浮层阴影三元组复制 3 份 + 抽屉第 4 种 ★★★**
位置：`ServerTable.vue:950`、`SideTree.vue:613`、`TableToolbar.vue:199`（逐字相同 `0 6px 24px rgb(0 0 0 / 25%)`）、`EditorDrawer.vue:558`。
现状：无令牌，四处手写；后续调阴影要改四处。
建议：`--shadow-menu` / `--shadow-overlay` 两个令牌入 theme.css（并支撑 S3 亮色分档），四处替换。
影响面：浮层家族统一 + 单点维护。

**E2 · 样式块整段复制 ★★**
位置：`.ctx-menu/.ctx-item`（ServerTable:940-983 ≈ SideTree:603-637，约 40 行逐字重复）；`.cell`（ServerRow:177-248 与 FolderRow:89-140，注释已说明属有意兜底）；`.ed-fields/.ed-banner`（EditorDrawer 与 BulkEditForm:316-336 双份，注释已说明）。
现状：右键菜单第二份复制已经出现第一处分叉迹象（SideTree 版 min-width 130px vs ServerTable 210px——宽度差异有语义原因，但样式体重复意味着下次改 hover 只会改一处）。
建议：浮层菜单抽公共类（全局样式或共享组件）；`.cell` 双份可保留（虚拟滚动兜底语义），但在两处加"同步修改"锚注释。
影响面：维护成本；防未来漂移。

**E3 · 主按钮 hover 行为分叉（复制漂移实例）★★★**
位置：`TableToolbar.vue:116-129` vs `EditorDrawer.vue:801-821`。
现状：`.bb-btn:hover:not(:disabled)`（特异性 0,3,0）设置 `border-color: var(--border-strong)`，压过 `.bb-primary`（0,1,0）的 accent 边框——批量条「▶ 连接」按钮悬停时**强调色边框消失**、退化为中性边框；而 EditorDrawer 侧 `.ed-primary:hover:not(:disabled)`（声明在后、同特异性）显式保住 accent 边框与文字。同一"主按钮"两处悬停结果相反，且 TableToolbar 侧明显非有意（缺 guard）。
建议：为 `.bb-primary` 补 `:hover:not(:disabled)` 保留 accent 边框（对齐 ed-primary）；并在令牌落地时定义唯一"主按钮 hover"规范（accent 边框 + bg-hover + accent-text）。
影响面：批量操作条主 CTA 观感；2 行 CSS。

**E4 · 禁用不透明度 5 种 ★★**
位置：0.35（TagManagerModal:326）/ 0.45（ServerRow:407、CredentialVaultGroup、TagManagerModal:370-373）/ 0.5（TableToolbar:122、DataSourceGroup、ImportModal:333）/ 0.55（FormField、EditorDrawer:808、ServerListView:756、RunnerCard:474）/ 0.6（ServerTable:977、SideTree:636）。
建议：统一 `--opacity-disabled: 0.5`（或按"图标钮 0.45 / 文字按钮 0.55"两档），一处定义全站引用。
影响面：禁用态一致性；批量值替换。

**E5 · 字号魔法小数 190+ 处 + 1 处 px 硬编码 ★★★**
位置：`0.9231rem`×51、`0.9615rem`×50 等散写；`HelpLink.vue:169` `font-size: 11px`（唯一未随字号设置缩放的文本）。
现状：rem 小数本身正确（13px 基准的精确换算），但无可读语义、无单点调整能力；`px2rem` 工具已存在于 `themes/index.js` 却只用于 Naive overrides。
建议：V2 五档落地时同步定义 `--fs-micro/-caption/-body/-title/-display` 变量（值用 rem 保持随 S/M/L/XL 缩放），组件改引变量；HelpLink 11px→`var(--fs-caption)`。
影响面：全站；是 V2 的实现载体。

**E6 · Naive 组件与自定义控件圆角双轨 ★★★**
位置：`themes/index.js` overrides（~90-135 行）只覆盖颜色与字号，未设 `borderRadius`；Naive 默认 3px。
现状：表单内 `n-input`(3px) + `ff-mini-btn`(3px) 匹配良好；但工具栏 `n-button`(3px) + `bb-btn`(6px) + `tt-btn`(5px) 三种圆角同行；搜索框 7px 与抽屉内 `n-input` 3px 属两个世界。
建议：确定控件档圆角（建议 6px）后在 overrides 增加 `common: { borderRadius: '6px', borderRadiusSmall: '4px' }`，同时自定义控件 3px 档仅保留给行内 code/微元素。需整屏回归 Naive 组件（input/select/button/tag/dropdown）。
影响面：全站控件；一次性对齐双轨。

**E7 · reduced-motion 覆盖 4/6 ★★**
位置：缺 `App.vue:331-334`（搜索 spinner 0.7s 无限旋转）与 `RunnerCard.vue:369`（0.4s border 闪光，无限次触发场景为多卡新建）。
建议：补两处 `@media (prefers-reduced-motion: reduce)` 守卫（spinner 显示静态边框、flash 直接跳终态）。
影响面：可访问性；4 行 CSS。

**E8 · AppearanceGroup 迷你预览与 theme.css 双源 ★**
位置：`AppearanceGroup.vue:177-203`。
现状：迷你预览硬编码 4 个色值（`#0f1011/#fafafa/#565a63/#a1a1aa`），与 `theme.css` 的 `--bg/--text-4` 同值但无关联；主题值将来调整会静默失真。
建议：`.mini[data-theme]` 作用域内重定义变量（`.mini[data-theme='dark'] { --bg: #0f1011; --text-3: #565a63; }` 后内部用 `var()`），或至少加"与 theme.css 同步"注释。
影响面：单组件防漂移。

### 3.3 设计系统审查员（Linear 偏离 / 暗亮均衡 / 7 强调色稳健性）

**S1 · 令牌体系只有颜色一个维度 ★★★（根因）**
位置：`themes/theme.css`（127 行全部是颜色）。
现状：radius/space/height/shadow/motion/font-size/disabled/focus 全部无令牌（数据见第 2 节）——"1px 边框、紧凑密度"这些 Linear 支柱靠约定而非系统维持，10 轮多人迭代的漂移（V1-V3、E1、E4、E5）皆源于此。
建议：theme.css 扩展（草案见附录 A）：`--radius-*`、`--fs-*`、`--ctrl-h-*`、`--space-*`（可选）、`--shadow-*`、`--dur-*`、`--opacity-disabled`。分两步走：先定义+迁移 P1 项（radius/fs），其余渐进。
影响面：全站；是本报告几乎所有建议的载体。

**S2 · hover 双语言：中性升级 vs 直接 accent ★★**
位置：中性升级（8 处，见 2.5）vs accent 直跳（`RunnerCard.vue:469` act-btn、`SubformList.vue:224` sf-add、`FormField.vue:707` ff-tag-sug-chip、`ServerListView.vue:605` crumb-btn 文字变 accent-text）。
现状：同一产品两种悬停哲学。Linear 语义：hover 一律中性（边框提档/底色），accent 只表达"选中/激活/当前"。
建议：act-btn/sf-add/ff-tag-sug-chip 的 hover 边框改 `--border-strong`，文字可保留 accent-text；激活态（.active）才用 accent 边框。crumb hover 文字 accent 可保留（导航语义）或降为 text-1。
影响面：3 处小改 + 一条 hover 规范入令牌注释。

**S3 · 亮色主题阴影偏重 ★★**
位置：菜单阴影 25% 黑 ×3、抽屉 22%。
现状：Linear 亮色界面阴影透明度更低（~8-12%）并更多依赖边框；本作亮色下 25% 黑阴影显脏、层级过重。
建议：`--shadow-menu` / `--shadow-overlay` 按 `[data-theme]` 分档：dark 25%/22%，light 10%/8%（light 下可同步把浮层边框从 `--border-strong` 提一档以保证定义）。
影响面：浮层/抽屉在亮色下的克制感。

**S4 · 搜索高亮 `.hl` 在 7 强调色×双基底下对比度不稳 ★★★**
位置：`ServerRow.vue:353-358`（`background: var(--accent); color: var(--bg-panel)`）。
现状（按 WCAG 对比度实测）：
- dark + blue（默认）：**3.5:1**（12.5px 正文低于 AA 4.5）
- dark + slate：3.9:1；dark + green：8.1:1（过）
- light + green：**2.3:1**；light + orange：**2.8:1**（白字于中亮色底）
即默认主题已低于 AA，亮色绿/橙严重不足。注释宣称"均高对比"与实测不符。
建议：改用产品既有的选中语言——`background: var(--accent-container); color: var(--accent-text)`（可选 + `font-weight: 600` 增强识别）。实测该组合 dark 基底 7 色全部 ≥6.1:1；light 基底除 blue（4.47）与 orange（4.52）为 4.5 临界外其余 ≥5.1:1——临界两色可通过 `font-weight: 600` 或亮色 `accent-text` 微调一档彻底达标，且与行选中态（`.row.selected` 同款变量对）视觉同源。
影响面：搜索命中可读性 + 与选中态语言统一；1 条规则。

**S5 · text-4 弱化文字大面积低于 AA ★（记录在案）**
位置：全站时间戳/计数/提示（2.4-2.7:1）。
现状：与 Linear 同款取舍（弱化元信息），可接受；但个别"需要读清"的行也用 text-4：`ImportModal` err-item（错误详情，用户需据此改文件）、`TableToolbar` col-hint（操作提示）、`ServerListView` eo-hint（离线提示）。
建议：错误/操作提示类升 `--text-3`（5.8:1）；纯元信息维持 text-4。
影响面：约 5-6 处选择性提升。

**S6 · 焦点环三种体系并存 ★★**
位置：HelpLink 自定义 accent 环（HelpLink.vue:113-117）vs Naive 组件默认焦点（box-shadow 环）vs 自定义按钮 UA 默认黑环。
现状：键盘 Tab 穿过混合控件时焦点样式不统一；暗色下 UA 默认环观感差。
建议：全局规则（theme.css 或 App 级）：`button:focus-visible, a:focus-visible, [tabindex]:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }`，与 HelpLink 现行实现一致化。
影响面：键盘可达性 + 视觉统一；1 条全局规则。

---

## 4. 修正建议总表（按影响面排序）

| # | 建议 | 关联发现 | 级别 | 改动量估计 | 风险 |
| --- | --- | --- | --- | --- | --- |
| 1 | theme.css 扩维令牌（radius/fs/ctrl-h/shadow/dur/disabled），附录 A 草案 | S1 | P1 | 1 文件 + 渐进迁移 | 低（纯增量） |
| 2 | 圆角收敛 3/6/8 三档 + Naive overrides `borderRadius` | V1+E6 | P1 | ~60 处值替换 + overrides 2 行 | 中（需回归 Naive 组件全屏） |
| 3 | 字号收敛 5 档 + `--fs-*` 变量化 | V2+E5 | P1 | ~190 处（可脚本辅助） | 低 |
| 4 | `.hl` 高亮改 accent-container + accent-text | S4 | P1 | 1 条规则 | 低 |
| 5 | 控件高度 24/28 两档；禁用不透明度统一 0.5 | V3+E4 | P2 | ~15 处 | 低 |
| 6 | 补 `.bb-primary:hover`；固化主按钮 hover 规范 | E3 | P2 | 2 行 + 规范 | 无 |
| 7 | 补 `eg-btn`/`eg-primary` hover | V4 | P2 | 2 行 | 无 |
| 8 | 阴影令牌化 + 亮色分档 | E1+S3 | P2 | 4 处 | 低 |
| 9 | hover 语言统一（中性升级为主，accent 仅激活） | S2 | P2 | 3 处 | 低 |
| 10 | 焦点环全局统一 | S6 | P2 | 1 条全局规则 | 低（注意不与 Naive 冲突） |
| 11 | 浮层菜单样式去重（ctx-menu 公共化） | E2 | P2 | 重构 2 文件 | 中 |
| 12 | 红点 `red`→`--danger`/`--dot-update` | V5 | P3 | 3 处 | 无 |
| 13 | 补 2 处 reduced-motion | E7 | P3 | 4 行 | 无 |
| 14 | 需读清的提示行 text-4→text-3 | S5 | P3 | ~6 处 | 无 |
| 15 | 面包屑行高 34→36 | V7 | P3 | 1 处 | 低（回归 Teleport 居中） |
| 16 | 指示笔画收敛 1.5/2/3 | V6 | P3 | 点状 | 无 |
| 17 | AppearanceGroup 预览变量化 | E8 | P3 | 1 处 | 无 |
| 18 | emoji 图标单色化路线图 | V8 | P3（长期） | 大 | 中 |

实施顺序建议：#1 → #2/#3（同一批回归）→ #4-#7（独立小改可穿插）→ #8-#11 → P3 项随手清。#18 独立排期。

---

## 5. 与示意图的对应关系

示意图 `docs/aesthetics-review-2026-09-19.html` 共 **13 个对照区块**，每块左栏为按代码真实参数复刻的现状小样、右栏为建议后小样（顶部可切换 暗/亮 基底与 7 强调色）：

| 示意图区块 | 对应发现 | 展示要点 |
| --- | --- | --- |
| B1 色彩令牌与硬编码红点 | V5、S1（颜色维度） | 16 变量色板 + `red` vs `--danger` 双基底对照 |
| B2 圆角阶梯 | V1、E6 | 7 种在用值陈列 + 工具栏行 3/5/6px 同屏实例 + 三档收敛映射 |
| B3 字号阶梯 | V2、E5 | 12 档带使用次数的阶梯条 + 11/11.5、12/12.5 并排不可辨对照 + 5 档建议 |
| B4 按钮家族 | V3、E4 | 7 种实测高度按钮逐个标注参数 + 24/28 两档建议 |
| B5 主按钮 hover 分叉 | E3 | bb-primary hover（accent 边框退化）vs ed-primary hover（保留）+ 统一后 |
| B6 空态按钮 | V4 | eg-btn 无 hover vs 补齐后 |
| B7 输入控件 | V1、E6 | searchbox 26/7px、n-input 28/3px、hk-box 28/6px、ff-color-box 28/3px 对照 + 统一 28/6 |
| B8 胶囊 chips 家族 | V3（chip 分支） | 6 种 pill 参数表（padding 1/2/2.5/3px × 字号 10.5/11/11.5px）+ 归一 |
| B9 分段控件 | V1 | ed-seg（6px/26px）vs AppearanceGroup .seg（7px/28px）+ 统一 |
| B10 图标钮与禁用态 | V3、E4 | 18-28px 六种图标钮 + 5 种禁用不透明度实测 + 归一 |
| B11 浮层阴影 | E1、S3 | 菜单阴影暗/亮两基底效果 + 分档令牌建议 |
| B12 搜索高亮 7 色 | S4 | `.hl` 在 7 强调色 × 双基底的 14 格矩阵（含对比度标注）+ accent-container 方案同矩阵全绿 |
| B13 动效时长 | E7、S1 | 8 种时长时间轴 + 3 档建议 + reduced-motion 覆盖缺口标记 |

---

## 附录 A · 建议令牌草案（仅供 owner 参考，未落入代码）

```css
/* theme.css 扩展草案（值 = 现状最大公约数，非新设计） */
:root {
  /* 字号（rem 随 S/M/L/XL 根字号缩放） */
  --fs-micro: 0.8077rem;   /* 10.5px 微：chip/箭头/计数 */
  --fs-caption: 0.8846rem; /* 11.5px 辅：时间戳/表头/hint */
  --fs-body: 0.9615rem;    /* 12.5px 正文与按钮 */
  --fs-title: 1.0769rem;   /* 14px 标题 */
  --fs-display: 1.3846rem; /* 18px hero */

  /* 圆角 */
  --radius-xs: 3px;  /* 行内 code、微元素（= Naive 微档） */
  --radius-ctrl: 6px;/* 按钮/输入/菜单项/分段 */
  --radius-box: 8px; /* 卡片/浮层/表格框 */
  --radius-pill: 999px;

  /* 控件高度 */
  --ctrl-h-s: 24px;
  --ctrl-h-m: 28px;

  /* 阴影（暗亮分档） */
  --shadow-menu: 0 6px 24px rgb(0 0 0 / 25%);   /* light: 10% */
  --shadow-overlay: -10px 0 28px rgb(0 0 0 / 22%); /* light: 8% */

  /* 动效 */
  --dur-fast: 0.12s;  /* 微交互 */
  --dur-med: 0.2s;    /* 面板/抽屉 */
  --dur-slow: 0.4s;   /* 强调闪光 */

  --opacity-disabled: 0.5;
}
```

## 附录 B · 审核约束声明

- 本报告及示意图为唯一产出；`webui/src/` 与 `Ui/` 等产品代码零改动。
- 示意图中的"现状"小样全部取自上述文件的真实 CSS 参数（圆角/内边距/字号/颜色按代码值复刻），"建议"小样仅演示本报告建议值，不代表已实施。
- 所有建议满足：功能不变（纯视觉参数/增量规则）、易用性不降低（多处提升反馈与可读性）。
