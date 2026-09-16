# 1Remote Web UI 计划 3：设置页 / 凭据库 / 标签管理 / 完整国际化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use checkbox syntax.

**Goal:** 实现 Web 端设置中心（7 分组）、凭据库管理（含明文查看的本地二次验证联动）、标签管理、数据源与运行器管理，完成 14 语言完整迁移，移除调试钩子。

**Architecture:** 后端补齐管理端点（凭据 CRUD/明文查看走 SecondaryVerificationHelper、通用设置读写、标签管理、数据源 CRUD+测试、运行器读写），复用 Plan 1/2 的端点模式（lock(gd)/快照、token 仅守卫 /api、MapSaveResult 风格）。前端以全屏 SettingsView（左侧分组导航，对齐 spec §6）替换 PlaceholderView。i18n 用一次性 C# 脚本将 14 个 XAML ResourceDictionary 转为 JSON（键重映射表 + {0}→{name}）。

**Tech Stack:** 同 Plan 1/2。上游：spec §6/§4/§7；计划系列 3/4。

---

## 全局约定（沿用 Plan 2，增量如下）

1. **安全核心**：凭据明文查看 = `POST /api/credentials/{name}/reveal?ds=` → 服务端 `await SecondaryVerificationHelper.VerifyAsyncUi()`（**async Task<bool?>，直接 await，不要 OnUIThreadSync 包裹**——同步 dispatch 包不住 async；未开启验证时自动返回 true，与 WPF 行为一致）→ 通过后 `CloneMe()`+解密返回 {password, privateKeyPath}；30 秒内同 ds 的 reveal 免再次验证（服务端静态时间戳）。写库加密由 `DataSourceBase.Database_InsertCredential(Credential)` / `Database_UpdateCredential(Credential org, string credentialNameBeforeUpdate)` / `Database_DeleteCredential(IEnumerable<string> names)`（DataSourceBase.Source.cs:387-486）内部处理；删除/更新的事务联动清理引用服务器由 DapperDataBase_CredentialVault.cs 既有逻辑承担。**只读库静默成功陷阱**：这三个方法在 `_isWritable=false` 时静默返回 Success——端点必须先查 `IsWritable` 返回 400。凭据 Name 非空+唯一由 DB UNIQUE 约束承担（捕获冲突→400）。
2. **设置读写安全域**：Web 端只暴露**非破坏性**字段：语言、关闭行为、确认开关、日志级别、tab 选项、misc 复制选项、Launcher 热键（写后调重注册）。**不动**：开机自启（写注册表——暴露无益且危险）、便携模式切换、SQLite 路径（数据源 CRUD 覆盖）。**`requireSecondaryVerification` 不在 GeneralConfig**（那是 XAML 控件名）：真实状态在 `SecondaryVerificationHelper`（凭据管理器/注册表/文件，读 `await GetEnabled()`，写 `SetEnabled(bool)`——async void 无返回值，端点写后直接 200）。**language 变更**：GeneralConfig.CurrentLanguageCode 是**小写码**（"zh-cn"），web 端 14 语言需映射（zh-CN→zh-cn）；PUT 后调 `LanguageService.SetLanguage(code)` 同步 WPF（DEBUG 构建缺键会弹 MessageBox——测试注意）。**热键重注册**：用 4 参 `SetHotKey(bool enable, HotkeyModifierKeys, Key)` 拿 bool 判冲突（false≠冲突，需与 launcherEnabled 对比，参照 SettingsPageViewModel.cs:160）；Key/HotkeyModifierKeys 是 WPF 枚举需字符串映射表；测试环境用 `IoC.TryGet<LauncherWindowViewModel>()`（可能未注册）。
3. **数据源持久化双集合（最大陷阱）**：`DataSourceService.AddOrUpdateDataSource`/`RemoveDataSource` 只改运行时字典；落盘需同步 `ConfigurationService.AdditionalDataSource`（独立集合）+ `Save()`。WPF 正确模式（DataSourceViewModel.cs:105-112/182-191）：先改 AdditionalDataSource 内同一实例 → Save() → 再 AddOrUpdateDataSource。**TestConnection**：仅 Mysql/Pgsql 有静态 TestConnection（返回 bool）；SqliteSource 无——sqlite 的 /test 走 Database_SelfCheck（AddOrUpdateDataSource 返回的 DatabaseStatus 含错误详情）。**密码"空=保持"**：Mysql/Pgsql Password setter 收 "" 会清空——端点必须条件赋值（空串跳过）。
4. **14 语言迁移**：C# 一次性脚本 `scripts/convert-xaml-locales.cs`（dotnet run 或放入 Tests？放 scripts/ 独立 .csx 或小 csproj——**决策：写成 webui/scripts/convert-locales.mjs 用 node 正则解析 XAML**（14 个文件是纯 `<ResourceDictionary><s:String x:Key="k">v</s:String>` 结构，node 够用且不进 .csproj）；映射表硬编码在脚本里（~273 web 键 → WPF 键，多数 1:1 语义对齐，少数组合）；`{0}`→`{name}` 按每键手写映射；产出 14 个 JSON 合并进现有 zh-CN/en-US 结构（保留 web 专有键如 editor.*、statusbar.*——脚本只填充能映射的键，其余保留英文回退）。
5. 测试基线 73/71/2；提交规范同前。

## 任务

### Task 1: 凭据端点（CRUD + reveal 二次验证）+ MSTest
Files: WebUiEndpoints.cs/WebUiEditorService.cs(或新 WebUiCredentialService.cs)/WebUiDto.cs + Tests/Service/WebUi/CredentialEndpointsTests.cs
- GET /api/credentials?ds= → 完整列表（Name/Address/Port/UserName/被引用数——引用数由 servers tags 扫描 InheritedCredentialName+AlternateCredentials 统计；**不含密码/私钥路径**）。GET /api/credentials/names 已存在（Task 4）保留。
- POST /api/credentials {ds, credential:{Name*,Address,Port,UserName,Password,PrivateKeyPath}} → 走 `ds.Database_InsertCredential`（验证 Name 非空+库内唯一——复用 WPF 规则 Credential.cs:50-62）。
- PUT /api/credentials/{name}?ds=（按名寻址，与 WPF 一致）→ UpdateCredential。DELETE /api/credentials/{name}?ds= → DeleteCredential（联动服务器更新由既有事务处理）。
- POST /api/credentials/{name}/reveal?ds= → SecondaryVerificationHelper 验证（UI 线程弹窗）→ 通过则 CloneMe+DecryptToConnectLevel 返回 {password, privateKeyPath}；30s 窗口内免验证（静态时间戳字典 per ds）。未开启二次验证的配置（开关本身在 WPF GeneralConfig.CbRequireWindowsPasswordBeforeSensitiveOperation）→ 直接返回（与 WPF 行为一致）。测试：reveal 在无验证配置下直通；Name 冲突 400；CRUD 往返；删除联动（种一台引用服务器，删凭据后服务器 InheritedCredentialName 被清——验证 WPF 事务行为）。
- 前端 api +5 方法。

### Task 2: 设置/标签端点 + MSTest
Files: 同上 + Tests/Service/WebUi/SettingsEndpointsTests.cs
- GET/PUT /api/settings/general：{language, closeButtonBehavior, confirmBeforeClosingSession, showSessionIconInSessionWindow, logLevel, tabWindowCloseButtonOnLeft, tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow, copyPortWhenCopyAddress, doNotCheckNewVersion, requireSecondaryVerification}（读 GeneralConfig；PUT 白名单写 + ConfigurationService.Save()；**language 变更同时调用 LanguageService.SetLanguage** 让 WPF 侧即时生效）。开机自启/便携模式/DB 路径不进白名单（返回体也不含）。
- GET/PUT /api/settings/launcher：{launcherEnabled, hotKeyModifiers, hotKeyKey, showCredentials, allowSaveInfoInQuickConnect}；PUT 后调 LauncherWindowViewModel 重注册热键（IoC.Get<LauncherWindowViewModel>().SetHotKey()——UI 线程）。
- GET/PUT /api/tags/manage {name,pinned}（pin/unpin→LocalityTagService）；POST /api/tags/rename {ds,from,to}（扫 servers 改 Tags+保存）；DELETE /api/tags/{name}?ds=（从所有 servers 移除该 tag；置顶信息清理）。
- 前端 api +8。

### Task 3: 数据源 + 运行器端点 + MSTest
Files: 同上 + Tests/Service/WebUi/DataSourceEndpointsTests.cs
- GET /api/datasources 已存在——扩展返回 {name,type,status,writable,reconnectInfo,serverCount,config}（config=连接参数：sqlite path / mysql host+port+db+user（**不含密码**）/ pgsql 同）。POST /api/datasources {type,config} → DataSourceService.AddDataSource + TestConnection；PUT /api/datasources/{name} {config} → 更新+重连；DELETE /api/datasources/{name}（Local 不可删 400）；POST /api/datasources/{name}/test → 测试连接结果。密码字段：PUT 时空串=保持原密码（与 WPF 编辑语义一致）。
- GET/PUT /api/settings/runers：按协议 {protocol: [runner 配置数组]}（ProtocolConfigurationService；外部运行器含 ExePath/参数宏/环境变量/Hosting；密码无）。结构直通 JSON（与编辑器相同的 PascalCase 直通策略——runner 类用 Newtonsoft 序列化，确认无循环引用）。
- 前端 api +6。

### Task 4: SettingsView 骨架 + 常规/外观/语言组
Files: webui/src/views/SettingsView.vue（替换 PlaceholderView 引用）、components/settings/*（GeneralGroup/AppearanceGroup/LanguageAboutGroup）
- 左侧 7 组导航（常规/启动器/数据源/凭据库/外观/运行器/语言与关于）+ 右侧表单卡；Esc/←返回。
- 外观组：迁移 themes 的 setTheme UI（基底三选 + 8 强调色圆点 + 字号 + 经典预设行——对齐 spec §6 样张）。
- 常规组：Task 2 的 general 字段表单（switch 为主）+ 保存按钮（PUT）+ 未保存提示。
- 语言与关于：语言下拉（14 locale 列表静态）+ setLocale 联动（写 /api/settings/general.language 同步 WPF！）+ 版本（api.version()）+ 链接占位。
- **移除 themes/index.js 的 window.__theme 调试钩子**（外观组 UI 已成为正式入口）+ 状态栏语言按钮改跳设置页或保留（保留——快捷入口）。

### Task 5: 凭据库组 + 标签管理
Files: components/settings/CredentialVaultGroup.vue、components/settings/TagManagerModal.vue
- 凭据库组：表格（名称/用户名/地址/被引用/操作）+ 数据源过滤 + 新建/编辑（模态表单，Name 必填唯一提示）+ 删除确认（显示被引用 N 台警告）+ 👁 reveal（调 reveal API→30s 内显示明文→切行/超时隐藏；验证窗口是本地弹的，UI 显示"请在桌面端完成验证…"的等待态）。
- 标签管理：SideTree"+ 管理"chip 打开模态：列表（pin 置顶/计数/重命名 inline/删除）+ 连接全部按钮。接线。

### Task 6: 启动器/数据源/运行器组
Files: components/settings/{LauncherGroup,DataSourceGroup,RunnerGroup}.vue
- 启动器：启用开关 + 热键录制框（keydown 捕获显示 "Ctrl+Alt+M" 形态，PUT 后 WPF 即时重注册——热键冲突时后端返回错误显示）。
- 数据源：卡片列表（状态点/类型/计数/测试按钮）+ 添加向导模态（类型三选 + 参数表单 + 测试连接通过才能保存）+ 编辑（密码留空=不变）+ 删除确认。
- 运行器：协议页签 + 每协议 runner 列表（默认内置置灰说明 + 外部运行器卡片编辑：exe 路径文本框/宏参数 textarea/Hosting 开关）——**简化渲染**（schema 不做全描述符，直接字段化已知属性，PascalCase 直通；**整体往返 ProtocolSettings（含 SelectedRunnerName）**，不只 runner 数组）。
- 占位说明：文件类路径输入为文本框（原生 picker 归 Plan 4）。

### Task 7: 14 语言转换 + 收尾验收
Files: webui/scripts/convert-locales.mjs、webui/src/locales/*.json（12 新文件）、main.js（动态加载或全量打包——全量打包：12×~8KB 可接受）
- 脚本：解析 Ui/Resources/Languages/*.xaml（s:String x:Key；跳过元数据键 language_name/language_code-*/key）→ 映射表（脚本内硬编码 ~273 键映射，语义对齐 WPF 术语）→ 合并进 web 键结构（web 专有键保留 en 值作为该语言回退——比缺键回退 zh 更合理：**回退链改为 该语言→en-US**）→ 写 12 个新 JSON。{0}/{1} 占位符按键映射为 {name}。产出 diff 报告（每语言填充率）。
- locales/index.js：语言列表扩到 14；回退链 language→en-US（vue-i18n fallbackLocale 改 'en-US'，zh-CN 键仍全）。设置页语言下拉显示原生名。
- 验收：三端门禁 + i18n 平价（14 语言与 en 键集一致——脚本保证）+ CJK 0 + 设置往返 curl 探针 + owner 手动清单（含 Windows Hello reveal 流程、热键重注册、数据源测试连接、14 语言切换）。

## 完成定义
- 设置 7 组全部可用（安全白名单语义；WPF 侧同步生效：语言/热键/外观）
- 凭据库 CRUD + reveal（本地二次验证联动）+ 删除联动引用清理
- 标签管理（置顶/重命名/删除/连接全部）；数据源 CRUD+测试；运行器读写
- 14 语言全量（转换脚本入库可重跑；回退 en-US）；__theme 钩子移除
- Plan 1/2 零回归（73+/71+/2 基线、build×2、i18n 平价、CJK 0）
- 有意简化记录：文件路径文本框（Plan 4 原生 picker）、便携模式/自启不暴露、runner 高级编辑简化
