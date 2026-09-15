# 1Remote Web UI 计划 2：连接编辑器 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Web UI 中实现连接编辑器——右侧滑出抽屉承载 9 种协议表单（新增/编辑/复制/删除/批量编辑），后端提供基于 JSON 直通的读写 API 与补丁式批量合并（含 MSTest）。

**Architecture:** 后端不建 40+ 字段 DTO：GET 返回「克隆+解密后的 ProtocolBase.ToJsonString() 全字段 JSON」，保存时前端回传完整 JSON → `ItemCreateHelper.CreateFromJsonString` 反序列化 → `GlobalData.AddServer/UpdateServer`（加密由 DataSourceBase 内部自动完成）。前端用 schema 驱动表单：9 协议的字段分组定义在 JS 常量中，未列出的 JSON 字段原样透传（天然兼容未来协议字段）。批量编辑采用补丁模型（patch 中缺失的字段=保持不变），后端 C# 实现 + MSTest（语义等价 WPF 哨兵机制但更干净）。

**Tech Stack:** 后端沿用 Plan 1（Minimal API + MSTest + Newtonsoft）；前端沿用（Vue 3 + Naive UI + vue-i18n）。

**上游 Spec:** `docs/superpowers/specs/2026-09-15-web-ui-redesign-design.md` §5
**计划系列:** Plan 2/4。Plan 1 已完成（主界面+主题+i18n 骨架，HEAD 133e9ec0）。

---

## 全局约定

1. **测试策略**：后端 TDD（MSTest，新测试放 `Tests/Service/WebUi/`，沿用 TestInit 夹具+每类唯一种子 id 模式）；前端以「实现 + 手动验证清单 + `npm run build` 通过 + i18n 键平价 + 非注释 CJK=0」为完成标准。
2. **基线**：`dotnet test` 44/42/2（2 个失败=所有者未跟踪 RdpConfigTests.cs，与本项目无关，never touch）；`npm run build` 0 错误。
3. **关键代码事实**（来自调研，执行时若与代码冲突以代码为准并报告）：
   - `ProtocolBase.ToJsonString()` = `JsonConvert.SerializeObject(this)`，无类型鉴别器；具体类型由 `Protocol`+`ClassVersion`（JSON 属性）选择，经 `ItemCreateHelper.CreateFromJsonString(json)`（`Ui/Utils/ItemCreateHelper.cs:57-94`）反序列化。
   - **加密**：`DataSourceBase.Database_Insert/UpdateServer` 内部克隆并 `EncryptToDatabaseLevel()`；调用方（API）传明文即可。字段：Password、AlternateCredentials[].Password/PrivateKeyPath、SSH.PrivateKey、RDP.GatewayPassword、LocalApp Secret 参数。
   - **解密**：`DecryptToConnectLevel()`（`Ui/Service/DataBaseService.cs:48-78`）。**必须先 `Clone()` 再解密**——内存缓存中的对象是加密态，WPF 原地解密缓存是既有缺陷，web 不复制。
   - `GlobalData.AddItemById?` 不存在——用 `GetItemById(dataSourceName, serverId)`（`GlobalData.cs:44-48`）；`AddServer(ProtocolBase, DataSourceBase)`（:123）；`UpdateServer(ProtocolBase)`（:140）；`DeleteServer(IEnumerable<ProtocolBase>)`（:208）；新 ULID 由插入路径回写。
   - 新建默认：`new RDP()` Port=3389/UserName=Administrator；SSH/SFTP 22、FTP 21、Telnet 23、VNC 5900、RdpApp 3389、App ""。`DisplayName` 空时自动跟随 `Address`（ProtocolBaseWithAddressPort.cs:27-36）。
   - 校验规则（IDataErrorInfo）：DisplayName 非空；Address 非空（有地址的协议）；Port 非空且可 long.Parse；RdpApp 另需 RemoteApplicationName/RemoteApplicationProgram；Serial 另需 SerialPort/BitRate；LocalApp 另需 ExePath + Secret 参数值。
   - 内置图标：`ServerIcons.Instance.IconsBase64`（无名称的 PNG base64 有序列表）；exe 提取图标：`System.Drawing.Icon.ExtractAssociatedIcon`（参照 `IconPopupDialogViewModel.cs:64-74`）。
   - 凭据名称：`ds.GetCredentials().Select(x => x.Name)`（`DataSourceBase.Source.cs:489`）。
4. **安全边界**：编辑 API 返回/接收明文密码——与 WPF 编辑器同级暴露，受 token（仅 /api）+ 回环保护。POST/PUT 前端不缓存密码到 localStorage。
5. **提交规范**：英文 conventional commits + `Co-Authored-By: Claude <noreply@anthropic.com>`。
6. **WPF 侧零改动**（本计划不动 WPF 视图；只在 `Ui/Service/WebUi/` 与 `webui/src/` 添加）。
7. **lock(gd) 纪律**：所有新端点对 VmItemList/GetItemById 的读取沿用 Plan 1 的 `lock(gd)` 快照模式（读在锁内物化，写库在锁外；参照 WebUiEndpoints.cs 既有注释）。
8. **批量原子性措辞**：Task 3 的原子性指"预校验原子性"（任一 id 无效则整批不执行、不写库）；DB 层批量更新无事务，中途故障可能留部分写入（与 WPF 行为一致，记录即可，不加事务）。

## 文件结构

```
Ui/Service/WebUi/
├─ WebUiEndpoints.cs            # +8 端点（config/create/update/delete/batch/icons/extract/credential-names）
├─ WebUiEditorService.cs        # 新：克隆+解密+反序列化+校验+保存的编排逻辑（可单测的纯 C# 部分）
webui/src/
├─ api/index.js                 # +8 方法
├─ editor/
│  ├─ schemas.js                # 9 协议字段分组 schema（样板：RDP 完整；执行时对照 C# 类核对字段名）
│  ├─ fieldTypes.js              # 字段类型→组件映射约定（text/number/select/switch/tags/password/textarea/icon/color/credential）
│  └─ patch.js                   # 批量编辑 patch 构造（纯函数，node 可测）
├─ components/editor/
│  ├─ EditorDrawer.vue           # 抽屉骨架（头部/分组页签/保存栏）
│  ├─ FormField.vue              # 通用字段渲染器（按 schema 类型分发）
│  ├─ IconPicker.vue             # 内置网格+上传+exe 提取
│  └─ CredentialPicker.vue       # 凭据库下拉（names）
├─ views/ServerListView.vue      # 编辑器状态 + 入口接线
├─ locales/{zh-CN,en-US}.json    # +编辑器键
Tests/Service/WebUi/
├─ EditorEndpointsTests.cs       # config/create/update/delete/round-trip
└─ BatchPatchTests.cs            # 补丁合并核心单测
```

---

### Task 1: GET /api/servers/{id}/config（克隆+解密+全 JSON）

**Files:** Modify `Ui/Service/WebUi/WebUiEndpoints.cs`、`WebUiEditorService.cs`(新)、`Tests/Service/WebUi/EditorEndpointsTests.cs`(新)

- [ ] Step 1 失败测试（TestInit 夹具 + 种子 "editor-rdp-1"，`IoC.Get<GlobalData>().AddServer(new RDP{Id="editor-rdp-1",DisplayName="ed",Address="1.1.1.1",Password="pw123"}, localDs)`）：`GET /api/servers/editor-rdp-1/config?ds=Local` → 200。**键大小写约定（关键）**：端点信封字段（id/dataSourceName/protocol/json）随 ASP.NET camelCase，但**内嵌 json 对象的键保持 ToJsonString 的 PascalCase 原样直通**（System.Text.Json 不对嵌套对象键应用命名策略；`CreateFromJsonString` 的 `jObj.Protocol`/`jObj.ClassVersion` 访问是大小写敏感的，任何一侧做 camelCase 转换都会破坏直通）。断言：body 含 `"DisplayName":"ed"`、**`"Password":"pw123"`（明文）**、`"Protocol":"RDP"`（信封）、json 内 `"ClassVersion"`；未知 id → 404。断言缓存未被污染：GET 后调 `IoC.Get<GlobalData>().GetItemById(...)` 检查其 Server.Password 仍为原始（加密态）值。
- [ ] Step 2 红 → Step 3 实现 `WebUiEditorService.GetEditableConfig(dataSourceName, id)`：`GetItemById` → `(ProtocolBase)vm.Server.Clone()` → `DecryptToConnectLevel()` → `ToJsonString()`（**先克隆后解密**，理由注释）。端点包 `{ id, dataSourceName, protocol, json }`（json 为对象非字符串，前端免二次解析）。
- [ ] Step 4 绿（全套 46/44/2）→ Step 5 提交 `feat(webui): editable server config endpoint with clone-then-decrypt`

### Task 2: POST /api/servers + PUT /api/servers/{id} + DELETE（保存/删除）

**Files:** Modify `WebUiEndpoints.cs`、`WebUiEditorService.cs`；Test 同上

- [ ] Step 1 失败测试（**请求体与断言中 json 内嵌键一律 PascalCase**，与 GET 直通一致；信封键 camelCase）：
  - POST `{dataSourceName:"Local", json:{"Protocol":"RDP","ClassVersion":"RDP.V1","DisplayName":"new1","Address":"2.2.2.2","Port":"3389"}}` → 200 `{id:"<新生成ULID>"}`；GET config 可回读且 AddServer 后 `/api/servers` 含 new1。
  - PUT 修改 editor-rdp-1 的 DisplayName → 200；回读已变。
  - POST 缺 DisplayName → 400；Port 非数字 → 400（校验三规则，与 WPF IDataErrorInfo 对齐）。
  - DELETE editor-rdp-1 → 204；`/api/servers` 不再含它；再 GET config → 404。
  - 往返加密断言：POST 带 Password:"pwA" → GET config 返回 "pwA"（加密发生在 DataSourceBase 内部，对称验证）。
- [ ] Step 2 红 → Step 3 实现 `WebUiEditorService.SaveFromJson(json, dataSourceName)`：`ItemCreateHelper.CreateFromJsonString(serialize(json))` → 应用校验（DisplayName/Address/Port + RdpApp/Serial/LocalApp 规则，返回错误串）→ `IsTmpSession/Id 空` → AddServer 否则 UpdateServer；DELETE 走 `GlobalData.DeleteServer`。**注意**：UpdateServer 路径要求对象的 DataSource 字段——从 GetItemById 原对象克隆后 Update 或设置 `server.DataSource`（参照 WPF 保存路径 ServerEditorPageViewModel.cs:487-500；DataSource 为 [JsonIgnore]，需服务端回填）。
- [ ] Step 4 绿（48/46/2）→ Step 5 提交 `feat(webui): server create/update/delete endpoints with WPF-parity validation`

### Task 3: POST /api/servers/batch（补丁式批量编辑）——核心逻辑 + MSTest

**Files:** Modify 同上 + `Tests/Service/WebUi/BatchPatchTests.cs`(新)

- [ ] Step 1 失败测试（先纯逻辑后端点）：
  - 种子 3 台（batch-1/2/3，不同 Port/Tag）。`POST /api/servers/batch {ids:[...], patch:{ port:"3390", tags:["x"] }}` → 200；三台 port 全变 3390、tags 全为 ["x"]。
  - patch 仅含 `{ displayName:"N" }` → 其余字段（各自不同的 address/password）**不变**（逐字段断言）。
  - patch 空 `{}` → 400。ids 空 → 400。含未知 id → 404（整批不执行——原子性）。
  - 深层补丁：`{ alternateCredentials: [...] }` 整体覆盖（显式覆盖语义，注释记录与 WPF 交集合并的偏差）。
- [ ] Step 2 红 → Step 3 实现 `WebUiEditorService.ApplyBatchPatch(ids, JObject patch)`：每台 GetItemById → Clone → 解密 → Newtonsoft `JsonObject` 逐字段覆盖（patch 中的键）→ 反序列化回具体类型 → 校验 → 收集全部成功后统一 UpdateServer（先全部构建再统一保存，保证原子性）。
- [ ] Step 4 绿（53/51/2）→ Step 5 提交 `feat(webui): batch patch endpoint with atomic multi-server merge`

### Task 4: 辅助端点（图标/提取/凭据名）

**Files:** Modify `WebUiEndpoints.cs`；Test 同 Task 1 文件追加

- [ ] Step 1 失败测试：GET `/api/icons` → 200 数组（元素为 base64 字符串，长度>0，来自 ServerIcons.Instance.IconsBase64）；GET `/api/credentials/names?ds=Local` → 200 `{names:[...]}`（种子一台带 InheritedCredentialName 无需真凭据——先用空数组断言形状；若 TestInit 的临时库有凭据机制则加一条）；POST `/api/icons/extract-from-exe {path:"C:\\Windows\\notepad.exe"}` → 200 `{iconBase64:"<png>"}`（测试环境路径存在性——用 `Environment.SystemDirectory`+`\..\notepad.exe` 或跳过式断言：文件不存在 → 404）。
- [ ] Step 2 红 → Step 3 实现（exe 提取参照 IconPopupDialogViewModel.cs:64-74：ExtractAssociatedIcon→Bitmap→PNG base64；try/catch → 404/500）。
- [ ] Step 4 绿 → Step 5 提交 `feat(webui): icon list, exe-extract and credential-names endpoints`

### Task 5: 前端 schema——主流协议（RDP/SSH/SFTP/FTP）

**Files:** Create `webui/src/editor/schemas.js`；Create `webui/src/editor/fieldTypes.js`

- [ ] Step 1 `fieldTypes.js`：类型常量与元信息约定（`text/number/select/options[]/switch/tags/password/textarea/icon/color/credential/ref`），每个字段描述 `{key, type, label(i18n键), options?, visibleWhen?(依赖字段表达式), required?, placeholder?}`。
- [ ] Step 2 `schemas.js` 主流 4 协议。**每个 schema 必须含协议鉴别常量**（新建 POST 时前端注入 json，编辑模式回传时已存在）：`protocol:"RDP"`, `classVersion:"RDP.V1"`（对照各 C# 类构造函数核值——RDP.cs:115 等）。**RDP 为完整样板**（对照 `Ui/Model/Protocol/RDP.cs` 核对每个字段名，草案）：基本信息组（DisplayName*/Address*/Port*/Tags/Icon/Color/当前文件夹 TreeNodes 转所属文件夹下拉——用服务器 folderPath 编辑：文本输入 `folderPath`（'/'分隔，Plan 2 简化为文本+说明，Plan 4 树选择器））、凭据组（UserName/Password/AskPasswordWhenConnect/InheritedCredentialName(credential 类型)/备用凭据子表——**Plan 2 简化**：alternateCredentials 用 textarea JSON 编辑？**否——太糙**。用子表单：数组编辑 UI（增删行，每行 name/address/port/userName/password/privateKeyPath），作为 `subform` 类型） 、显示组（RdpFullScreenFlag/IsConnWithFullScreen/IsFullScreenWithConnectionBar/IsPinTheConnectionBarByDefault/RdpWindowResizeMode(select)/RdpWidth/RdpHeight(visibleWhen resizeMode=Fixed*)/IsScaleFactorFollowSystem/ScaleFactorCustomValue/DisplayPerformance）、mstsc 组（MstscModeEnabled/RdpFileAdditionalSettings(textarea)）、高级组（IsAdministrativePurposes/AudioRedirectionMode/AudioQualityMode/9 个 Enable* 开关）、网关组（GatewayMode/GatewayHostName/GatewayLogonMethod/GatewayUserName/GatewayPassword(visibleWhen)）、连接组（IsPingBeforeConnect）、杂项（RdpControlAdditionalSettings(textarea)、Note(备注字段——从 ProtocolBase 确认字段名)）。**SSH**：基本+凭据（含 PrivateKey/Password 二选一 UsePrivateKeyForConnect、PrivateKey 路径文本输入）+启动（StartupAutoCommand/OpenSftpOnConnected/ExternalKittySessionConfigPath）+Runner 说明。**SFTP/FTP**：基本+凭据+StartupPath。**执行规则**：每个字段名必须打开对应 C# 类核对拼写与大小写（Newtonsoft 默认 PascalCase 序列化——**注意**：检查 ToJsonString 是否设置 CamelCase 命名策略——默认 PascalCase！schema key 用 C# 属性原名 PascalCase，前端不做命名转换，端到端直通）。select 的 options 枚举值对照 C# 枚举的字符串值。schema 另含 `defaults`（新建初值：Port 等，对照 C# 构造函数默认）。
- [ ] Step 3 验证：node 脚本断言 schema 结构完整（每组字段 key 非空、类型合法）；对照 RDP.cs grep 字段名抽查 10 个。
- [ ] Step 4 提交 `feat(webui): editor schemas for RDP/SSH/SFTP/FTP`

### Task 6: 前端 schema——其余协议（VNC/Telnet/Serial/RdpApp/LocalApp）

**Files:** Modify `schemas.js`

- [ ] Step 1 五个协议 schema（对照 C# 类，同样含 protocol/classVersion 常量与 defaults）：VNC（VncWindowResizeMode）、Telnet（StartupAutoCommand）、Serial（SerialPort*/BitRate*/DataBits/StopBits/Parity/FlowControl——options 对照 Serial.cs 枚举）、RdpApp（RemoteApplicationName*/RemoteApplicationProgram*/audio 设置+RDP 公共组复用）、LocalApp（ExePath*/RunWithHosting/ArgumentList 子表单（Type/Name/Value，Secret 行密码样式）+ 宏说明 tooltip + 动态字段 visibleWhen：Address/Port/UserName/Password/PrivateKey 按 CheckMacroRequirement 语义声明）。协议间共享组用函数复用（baseFields(addressPortUserPwd) 等）。
- [ ] Step 2 结构断言 + 抽查 → Step 3 提交 `feat(webui): editor schemas for remaining protocols`

### Task 7: FormField 通用字段渲染器 + 子表单

**Files:** Create `webui/src/components/editor/FormField.vue`、`SubformList.vue`

- [ ] Step 1 FormField：按 type 分发（text→input、number→input+数字校验、select→n-select、switch→n-switch、tags→复用 Plan 1 标签输入样式、password→input type=password+眼睛切换、textarea→n-input textarea、icon→IconPicker（Task 9，先用占位）、color→7 色块+自定义、credential→CredentialPicker（Task 9 占位）、subform→SubformList）。visibleWhen 支持（字段隐藏时**不删除** json 值，仅 UI 隐藏——透传保真）。required 校验红框+i18n 消息。
- [ ] Step 2 SubformList：数组行编辑（增/删/排序可选无），行内字段递归 FormField（深度限 2）。AlternateCredentials 与 ArgumentList 共用。
- [ ] Step 3 build+transform 验证 → Step 4 提交 `feat(webui): schema-driven form field renderer`

### Task 8: EditorDrawer 抽屉骨架 + 保存流

**Files:** Create `EditorDrawer.vue`；Modify `ServerListView.vue`、`api/index.js`、locales

- [ ] Step 1 Drawer：右滑 68% 宽（拖边缘 560-900）、头部（图标+标题=新建 RDP/编辑：{名}+归属+最后修改时间（编辑模式从列表 DTO lastConnectTime 之外显示服务端无修改时间——**用所属数据源+协议代替**，修改时间字段后端不存储，跳过并注释 spec 偏差）+协议切换 n-select——切换时保留公共字段：从前端视角=保留 base 类字段丢弃专属字段，schema 切换+json 中保留两边共同分组的字段+**注入新协议的 Protocol/ClassVersion 常量**）、分组页签（schema 组名，未保存小圆点）、底部（取消/Ctrl+S 保存/Esc 关闭+未保存确认 n-modal）。
- [ ] Step 2 保存流：加载（config→json 合入本地响应式对象）；编辑模式 PUT（整个 json 对象：schema 字段+透传未列字段——**实现**：加载时深拷贝原 json，schema 绑定修改该拷贝，保存时整体回传）；新建模式 POST（**前端组装完整 json：schema.defaults 起底 + 用户输入 + 注入 `Protocol`/`ClassVersion` 鉴别字段**，无鉴别字段后端无法选型）。保存成功 → toast + 关抽屉 + SSE 自动刷新列表。
- [ ] Step 3 入口接线（ServerListView）：＋新建（启用 Plan 1 的禁用按钮）、✎ 编辑（启用）、右键 编辑/复制（复制=config→清 id→POST）/删除（确认 n-modal→DELETE→toast）、批量条 批量编辑（选中 N>1 → Task 10 批量模式）。
- [ ] Step 4 验证（build、i18n 平价、CJK 审计）→ Step 5 提交 `feat(webui): editor drawer with schema tabs and save flow`

### Task 9: IconPicker + CredentialPicker

**Files:** Create `IconPicker.vue`、`CredentialPicker.vue`；Modify `FormField.vue` 接线、api

- [ ] IconPicker：弹窗网格（/api/icons 缩略图、点击选择）+ 本地上传（`<input type=file>` → FileReader → base64，**不经过服务器文件系统**）+ exe 路径提取（文本框+按钮 → POST extract-from-exe）+ 无图标（协议默认）。当前值预览。
- [ ] CredentialPicker：n-select 远程选项（/api/credentials/names，按当前 dataSourceName）+ "手动输入"项（清空 inheritedCredentialName）。
- [ ] 验证+提交 `feat(webui): icon and credential pickers`

### Task 10: 批量编辑模式（前端）

**Files:** Create `webui/src/editor/patch.js`；Modify `EditorDrawer.vue`、`ServerListView.vue`

- [ ] patch.js 纯函数：`diffPatch(initialShared, current)` → 仅含用户改动字段的 patch；node 断言（改动 1 字段→patch 1 键；未动→{}）。
- [ ] Drawer 批量模式：加载 N 台的公共值（前端从 servers ref 取：逐字段全相同→显示值，不同→空+「‹N 台各不相同›」占位+「保持不变/覆盖」切换）；保存→POST batch；成功 toast「已更新 N 台」。
- [ ] 接线批量条「批量编辑」按钮（启用）→ 验证+提交 `feat(webui): bulk edit mode with patch construction`

### Task 11: i18n + 收尾验收

**Files:** locales、全部编辑器组件

- [ ] 编辑器全部文案入 JSON（zh/en 平价；RDP 40+ 字段 label——可接受英文 label 缺失时回退显示字段名？**否**——两语言都全量提供，字段 label 用简洁中文/英文）。CJK 审计=0 非注释。
- [ ] 端到端手验（自动化部分）：POST/PUT/DELETE/batch 循环 curl（带种子）+ 前端 build + 后端全套测试（53+/51+/2）。
- [ ] **Owner 手动清单**：新建 RDP 全字段保存→连接成功；编辑现有服务器密码→连接成功（加密往返）；批量改端口；复制；删除；9 协议各开一次表单无错；图标三来源；深浅主题下表单可读。
- [ ] 提交 `feat(webui): editor i18n and e2e polish`

## 完成定义

- 9 协议表单全部可渲染、可保存（字段名与 C# 一致，往返无丢失——透传字段保真）
- 新建/编辑/复制/删除/批量编辑全链路可用；密码加密往返正确（DB 密文、表单明文）
- 批量补丁合并有 MSTest（原子性+缺省保持不变）
- 校验与 WPF 对齐（DisplayName/Address/Port/RdpApp/Serial/LocalApp 规则）
- i18n 双语平价、零硬编码；Plan 1 功能零回归（全套测试+build）
- 有意简化（记录在案）：文件路径类输入为文本框（原生 picker 归 Plan 3/4）；alternateCredentials/argumentList 为子表单（非 WPF 全等价 UI）；批量 Tags=显式覆盖（WPF 交集合并的偏差已注释）；文件夹选择为文本输入（树选择器归 Plan 4）
