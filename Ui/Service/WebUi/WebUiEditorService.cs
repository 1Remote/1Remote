using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>保存/删除结果分类，由端点映射为 HTTP 状态码。</summary>
    public enum EditorSaveStatus
    {
        Ok,
        BadRequest,      // 请求体/数据源名/鉴别字段非法、数据源只读，或 WPF 平价校验未通过（写入前拦截）
        NotFound,        // 目标 id 不存在（或不可编辑：Dummy 分组头/临时会话）
        DbError,         // 校验通过但写库失败，500 + 错误详情
    }

    public sealed class EditorSaveResult
    {
        public EditorSaveStatus Status { get; private init; }
        /// <summary>成功时的服务器 id（POST=插入路径回写的 ULID；PUT/DELETE=路由 id）。</summary>
        public string ServerId { get; private init; } = string.Empty;
        /// <summary>BadRequest 时的校验错误列表（属性名: 消息）。</summary>
        public List<string> Errors { get; private init; } = new();
        /// <summary>DbError 时的底层错误信息（Result.ErrorInfo）。</summary>
        public string DbErrorInfo { get; private init; } = string.Empty;

        public static EditorSaveResult Ok(string serverId) => new() { Status = EditorSaveStatus.Ok, ServerId = serverId };
        public static EditorSaveResult BadRequest(List<string> errors) => new() { Status = EditorSaveStatus.BadRequest, Errors = errors };
        public static EditorSaveResult NotFound() => new() { Status = EditorSaveStatus.NotFound };
        public static EditorSaveResult DbError(string errorInfo) => new() { Status = EditorSaveStatus.DbError, DbErrorInfo = errorInfo };
    }

    /// <summary>
    /// 批量回读（POST /api/servers/batch/peek）的结果分类，由端点映射为 HTTP 状态码。
    /// 与 EditorSaveResult 分离：peek 是只读操作，无 DbError/ServerId 载体。
    /// </summary>
    public sealed class BatchPeekResult
    {
        public EditorSaveStatus Status { get; private init; }
        /// <summary>
        /// Ok 时的逐台回读载荷：{id} + 非 allow-list 敏感/DTO 覆盖键的 camelCase 值表
        /// （键 = patch 键，与 BatchPatchFieldMap 同源派生，见 WebUiEditorService.PeekBatch）。
        /// </summary>
        public List<Dictionary<string, object?>> Items { get; private init; } = new();
        /// <summary>BadRequest 时的错误列表。</summary>
        public List<string> Errors { get; private init; } = new();

        public static BatchPeekResult Ok(List<Dictionary<string, object?>> items) => new() { Status = EditorSaveStatus.Ok, Items = items };
        public static BatchPeekResult BadRequest(List<string> errors) => new() { Status = EditorSaveStatus.BadRequest, Errors = errors };
        public static BatchPeekResult NotFound() => new() { Status = EditorSaveStatus.NotFound };
    }

    /// <summary>
    /// 连接编辑器的编排逻辑（供 Web UI 端点复用，与 HTTP 层解耦）。
    /// 职责：GET /api/servers/{id}/config 可编辑配置（克隆解密后的明文 JSON）、
    /// POST/PUT/DELETE /api/servers*（新建/整体替换/删除）、POST /api/servers/batch
    /// （allow-list 扁平字段补丁），以及保存前校验（复用 WPF 编辑器 IDataErrorInfo 规则）。
    ///
    /// 加密纪律：VmItemList 内存缓存中的对象是加密态（DB 读路径不落解密），
    /// 一切对外的明文视图必须建立在 Clone() 副本上——WPF 编辑器对缓存原地解密是既有缺陷，Web 不复制。
    /// 保存路径与 WPF 编辑器一致（ServerEditorPageViewModel）：调用方传明文，
    /// 加密由 DataSourceBase.Database_InsertServer/Database_UpdateServer 在内部克隆上完成。
    /// </summary>
    public static class WebUiEditorService
    {
        /// <summary>
        /// 取服务器的可编辑配置：克隆 + 解密后的全字段明文 JSON（PascalCase 直通）。
        /// 未找到或不可编辑（Dummy 分组头/临时会话）返回 null，由端点转 404。
        /// 返回的 json 可直接经 ItemCreateHelper.CreateFromJsonString 反序列化
        /// （Protocol/ClassVersion 鉴别字段在文中，访问大小写敏感，勿做命名转换）。
        /// </summary>
        public static string? GetEditableConfig(string dataSourceName, string id)
        {
            var vm = GetEditableVm(dataSourceName, id);
            if (vm == null)
                return null;

            // 必须先克隆后解密：缓存中的 Server 为加密态，原地解密会污染缓存
            // （后续读到的 Password 变明文、加密往返语义破坏），WPF 的原地解密是既有缺陷。
            var clone = (ProtocolBase)vm.Server.Clone();
            clone.DecryptToConnectLevel();
            return clone.ToJsonString();
        }

        /// <summary>
        /// H4：右键「复制密码」的服务端编排——WPF ProtocolActionHelper.cs:126-142 的 web 平价。
        /// 验证门与凭据 reveal/导出共用同一 30s 窗口（按数据源记，WebUiCredentialService；
        /// 未开启二次验证时 VerifyAsyncUi 直通 true）；通过后克隆+解密取 Password 明文回传，
        /// 剪贴板写入由前端完成（WebView2/localhost 安全上下文 = 桌面剪贴板，免去 Kestrel
        /// MTA 线程上 System.Windows.Clipboard 的 STA 处理）。
        /// 无密码协议（Serial/Telnet 等非 UserPwd 层级 / 未存密码）回传空串，由前端提示。
        /// </summary>
        public static async Task<CredentialRevealResult> RevealServerPasswordAsync(string? dataSourceName, string id)
        {
            var resolvedName = string.IsNullOrWhiteSpace(dataSourceName)
                ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                : dataSourceName;
            var vm = GetEditableVm(resolvedName, id);
            if (vm == null)
                return CredentialRevealResult.NotFound();

            if ((DateTime.Now - WebUiCredentialService.GetRevealVerifiedAt(resolvedName)).TotalSeconds >= 30)
            {
                // async Task<bool?> 必须直接 await（同步 dispatch 包不住，reveal/导出同款约束）
                var verified = await SecondaryVerificationHelper.VerifyAsyncUi();
                if (verified != true)
                    return CredentialRevealResult.Forbidden();
                WebUiCredentialService.SetRevealVerifiedAt(resolvedName, DateTime.Now);
            }

            // 验证等待期间服务器可能已被删除：重找后再克隆（reveal 同款防御）
            vm = GetEditableVm(resolvedName, id);
            if (vm == null)
                return CredentialRevealResult.NotFound();

            var clone = (ProtocolBase)vm.Server.Clone();
            clone.DecryptToConnectLevel(); // 处理 InheritedCredentialName 继承凭据的解析
            var password = clone is ProtocolBaseWithAddressPortUserPwd p ? p.Password : string.Empty;
            return CredentialRevealResult.Ok(password ?? string.Empty, string.Empty);
        }

        /// <summary>
        /// POST /api/servers：新建。json 反序列化 → 校验 → AddServer（空 Id 路由），
        /// 新 ULID 由插入路径回写到对象上并随结果返回。
        /// </summary>
        public static EditorSaveResult Create(string dataSourceName, string serverJson)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return EditorSaveResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
            if (dataSource.IsWritable != true)
                return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });

            var server = ItemCreateHelper.CreateFromJsonString(serverJson);
            if (server == null)
                return EditorSaveResult.BadRequest(new List<string>
                {
                    "json must be an object with valid 'Protocol' and 'ClassVersion' discriminators (PascalCase, case-sensitive)",
                });

            var errors = ValidateForSave(server);
            if (errors.Count > 0)
                return EditorSaveResult.BadRequest(errors);

            // AddServer 路由（与 WPF 一致）：清空 Id——IsTmpSession() 为真即走新建；
            // Database_InsertServer 在克隆上落库并把生成的 ULID 回写到本对象。
            server.Id = string.Empty;
            var ret = IoC.Get<GlobalData>().AddServer(server, dataSource);
            return ret.IsSuccess
                ? EditorSaveResult.Ok(server.Id)
                : EditorSaveResult.DbError(ret.ErrorInfo);
        }

        /// <summary>
        /// PUT /api/servers/{id}：整体替换。校验通过后走 GlobalData.UpdateServer
        /// （与 WPF 编辑器保存同一路径，含缓存/标签/UI 通知联动）。
        /// </summary>
        public static EditorSaveResult Update(string dataSourceName, string id, string serverJson)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return EditorSaveResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
            if (dataSource.IsWritable != true)
                return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });

            var server = ItemCreateHelper.CreateFromJsonString(serverJson);
            if (server == null)
                return EditorSaveResult.BadRequest(new List<string>
                {
                    "json must be an object with valid 'Protocol' and 'ClassVersion' discriminators (PascalCase, case-sensitive)",
                });

            var originalVm = GetEditableVm(dataSourceName, id);
            if (originalVm == null)
                return EditorSaveResult.NotFound();

            // DataSource 为 [JsonIgnore]（不参与序列化往返），须从缓存中的原对象回填：
            // GlobalData.UpdateServer 按 x.DataSource 分组寻库并做可写检查，缺失会被归到
            // null 组而失败。取原始缓存对象（非解密克隆）上的引用即可。
            server.Id = id;
            server.DataSource = originalVm.Server.DataSource;

            var errors = ValidateForSave(server);
            if (errors.Count > 0)
                return EditorSaveResult.BadRequest(errors);

            var ret = IoC.Get<GlobalData>().UpdateServer(server);
            return ret.IsSuccess
                ? EditorSaveResult.Ok(id)
                : EditorSaveResult.DbError(ret.ErrorInfo);
        }

        /// <summary>DELETE /api/servers/{id}：删除（GlobalData.DeleteServer，内部联动重载）。</summary>
        public static EditorSaveResult Delete(string dataSourceName, string id)
        {
            var vm = GetEditableVm(dataSourceName, id);
            if (vm == null)
                return EditorSaveResult.NotFound();
            if (vm.Server.DataSource?.IsWritable != true)
                return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSourceName}' is read-only" });

            var ret = IoC.Get<GlobalData>().DeleteServer(new[] { vm.Server });
            return ret.IsSuccess
                ? EditorSaveResult.Ok(id)
                : EditorSaveResult.DbError(ret.ErrorInfo);
        }

        #region 批量补丁（POST /api/servers/batch）

        /// <summary>
        /// 批量 patch 的字段 allow-list：camelCase patch 键（列表 DTO 域）→ C# PascalCase 属性名。
        /// 显式枚举、逐项核对过真实属性名——不盲目反射任意键（未知键 400 列出，防静默丢弃）。
        /// 键匹配大小写不敏感（camelCase 为规范形式，PascalCase 亦接受）。
        ///
        /// 覆盖范围（batch9 Task C，协议感知批量编辑）：编辑器 json 域可安全批量写的全部
        /// <b>标量</b>字段——对照 webui/src/editor/schemas.js 的字段清单逐层展开（枚举/文本/
        /// 数字/开关均可批量写，值经 ApplyPatchToServer 的反射类型门校验）。协议差异由
        /// 「属性不在该协议类型上 → 按台 400」兜底（见 ApplyBatchPatch），前端按所选协议的
        /// schema 交集渲染、不会发出不适用键。
        /// 有意不进 allow-list 的（与 WPF 批量编辑哨兵机制同类限制，子表单/深层结构无法用
        /// 单值表达）：alternateCredentials/argumentList/treeNodes（BatchPatchDeepFields
        /// 400 引导单机编辑）。
        /// </summary>
        private static readonly Dictionary<string, string> BatchPatchFieldMap = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // ---- ProtocolBase 层（全协议共有）----
            ["displayName"] = nameof(ProtocolBase.DisplayName),
            ["note"] = nameof(ProtocolBase.Note),
            ["tags"] = nameof(ProtocolBase.Tags),                       // List<string>，显式覆盖语义（非 WPF 交集合并，Plan 2 有意偏差）
            ["colorHex"] = nameof(ProtocolBase.ColorHex),
            ["iconBase64"] = nameof(ProtocolBase.IconBase64),
            ["alwaysOpenInNewTabWindow"] = nameof(ProtocolBase.AlwaysOpenInNewTabWindow), // bool?
            ["commandBeforeConnected"] = nameof(ProtocolBase.CommandBeforeConnected),
            ["hideCommandBeforeConnectedWindow"] = nameof(ProtocolBase.HideCommandBeforeConnectedWindow), // bool
            ["commandAfterDisconnected"] = nameof(ProtocolBase.CommandAfterDisconnected),
            ["selectedRunnerName"] = nameof(ProtocolBase.SelectedRunnerName), // ''=跟随全局；运行器名按协议解析
            // ---- ProtocolBaseWithAddressPort 层（Serial 只继承 ProtocolBase，不含这些键）----
            ["address"] = nameof(ProtocolBaseWithAddressPort.Address),
            ["port"] = nameof(ProtocolBaseWithAddressPort.Port),
            ["isPingBeforeConnect"] = nameof(ProtocolBaseWithAddressPort.IsPingBeforeConnect), // bool?
            ["isAutoAlternateAddressSwitching"] = nameof(ProtocolBaseWithAddressPort.IsAutoAlternateAddressSwitching), // bool?
            // ---- ProtocolBaseWithAddressPortUserPwd 层（Telnet/Serial 不含）；password/gatewayPassword/privateKey
            //      为明文，加密由 DataSourceBase 保存时完成（EncryptToDatabaseLevel：Password 全系、
            //      SSH.PrivateKey、RDP.GatewayPassword）----
            ["userName"] = nameof(ProtocolBaseWithAddressPortUserPwd.UserName),
            ["password"] = nameof(ProtocolBaseWithAddressPortUserPwd.Password),
            ["inheritedCredentialName"] = nameof(ProtocolBaseWithAddressPortUserPwd.InheritedCredentialName),
            ["askPasswordWhenConnect"] = nameof(ProtocolBaseWithAddressPortUserPwd.AskPasswordWhenConnect), // bool?
            ["usePrivateKeyForConnect"] = nameof(ProtocolBaseWithAddressPortUserPwd.UsePrivateKeyForConnect), // bool?
            ["privateKey"] = nameof(ProtocolBaseWithAddressPortUserPwd.PrivateKey),
            // ---- RDP（RdpApp 无这些键）----
            ["isAdministrativePurposes"] = nameof(RDP.IsAdministrativePurposes),       // bool?
            ["domain"] = nameof(RDP.Domain),
            ["loadBalanceInfo"] = nameof(RDP.LoadBalanceInfo),
            ["rdpFullScreenFlag"] = nameof(RDP.RdpFullScreenFlag),                     // ERdpFullScreenFlag?
            ["isConnWithFullScreen"] = nameof(RDP.IsConnWithFullScreen),               // bool?
            ["isFullScreenWithConnectionBar"] = nameof(RDP.IsFullScreenWithConnectionBar), // bool?
            ["isPinTheConnectionBarByDefault"] = nameof(RDP.IsPinTheConnectionBarByDefault), // bool?
            ["rdpWindowResizeMode"] = nameof(RDP.RdpWindowResizeMode),                 // ERdpWindowResizeMode?
            ["rdpWidth"] = nameof(RDP.RdpWidth),                                       // int?
            ["rdpHeight"] = nameof(RDP.RdpHeight),                                     // int?
            ["isScaleFactorFollowSystem"] = nameof(RDP.IsScaleFactorFollowSystem),     // bool?
            ["scaleFactorCustomValue"] = nameof(RDP.ScaleFactorCustomValue),           // uint?（setter 钳制 100-300）
            ["displayPerformance"] = nameof(RDP.DisplayPerformance),                   // EDisplayPerformance?
            ["enableClipboard"] = nameof(RDP.EnableClipboard),                         // bool?
            ["enableKeyCombinations"] = nameof(RDP.EnableKeyCombinations),             // bool?
            ["enableAudioCapture"] = nameof(RDP.EnableAudioCapture),                   // bool?
            ["enablePorts"] = nameof(RDP.EnablePorts),                                 // bool?
            ["enablePrinters"] = nameof(RDP.EnablePrinters),                           // bool?
            ["enableSmartCardsAndWinHello"] = nameof(RDP.EnableSmartCardsAndWinHello), // bool?
            ["enableDiskDrives"] = nameof(RDP.EnableDiskDrives),                       // bool?
            ["enableRedirectDrivesPlugIn"] = nameof(RDP.EnableRedirectDrivesPlugIn),   // bool?
            ["enableRedirectCameras"] = nameof(RDP.EnableRedirectCameras),             // bool?
            ["mstscModeEnabled"] = nameof(RDP.MstscModeEnabled),                       // bool
            ["rdpControlAdditionalSettings"] = nameof(RDP.RdpControlAdditionalSettings), // WPF 行式 "key:type:value" 文本
            ["gatewayMode"] = nameof(RDP.GatewayMode),                                 // EGatewayMode?
            ["gatewayHostName"] = nameof(RDP.GatewayHostName),
            ["gatewayLogonMethod"] = nameof(RDP.GatewayLogonMethod),                   // EGatewayLogonMethod?
            ["gatewayUserName"] = nameof(RDP.GatewayUserName),
            ["gatewayPassword"] = nameof(RDP.GatewayPassword),
            // ---- RDP + RdpApp 共有（音频两枚举复用 RDP.cs 的同一类型）----
            ["audioRedirectionMode"] = nameof(RDP.AudioRedirectionMode),              // EAudioRedirectionMode?
            ["audioQualityMode"] = nameof(RDP.AudioQualityMode),                      // EAudioQualityMode?
            ["rdpFileAdditionalSettings"] = nameof(RDP.RdpFileAdditionalSettings),
            // ---- SSH / Telnet / Serial ----
            ["sshVersion"] = nameof(SSH.SshVersion),                                   // int?（仅 SSH）
            ["startupAutoCommand"] = nameof(SSH.StartupAutoCommand),                  // SSH/Telnet/Serial
            ["openSftpOnConnected"] = nameof(SSH.OpenSftpOnConnected),                // bool（仅 SSH）
            ["externalKittySessionConfigPath"] = nameof(SSH.ExternalKittySessionConfigPath), // SSH/Serial
            // ---- SFTP / FTP ----
            ["startupPath"] = nameof(SFTP.StartupPath),
            // ---- VNC ----
            ["vncWindowResizeMode"] = nameof(VNC.VncWindowResizeMode),                 // EVncWindowResizeMode?
            // ---- Serial（C# string 属性，非枚举：值 = 集合字符串原文）----
            ["serialPort"] = nameof(Serial.SerialPort),
            ["bitRate"] = nameof(Serial.BitRate),
            ["dataBits"] = nameof(Serial.DataBits),
            ["stopBits"] = nameof(Serial.StopBits),
            ["parity"] = nameof(Serial.Parity),
            ["flowControl"] = nameof(Serial.FlowControl),
            // ---- RemoteApp（RdpApp）----
            ["remoteApplicationName"] = nameof(RdpApp.RemoteApplicationName),
            ["remoteApplicationProgram"] = nameof(RdpApp.RemoteApplicationProgram),
            // ---- APP（LocalApp）----
            ["exePath"] = nameof(LocalApp.ExePath),
            ["runWithHosting"] = nameof(LocalApp.RunWithHosting),                      // bool
            ["appProtocolDisplayName"] = nameof(LocalApp.AppProtocolDisplayName),
        };

        /// <summary>
        /// 有意不进 allow-list 的深层/子表单字段（与 WPF 批量编辑哨兵机制同类限制）：
        /// 批量编辑只支持扁平标量字段，子表单（备用凭据、参数表）与文件夹归属走单机编辑
        /// PUT /api/servers/{id}。命中即 400 并附引导消息。
        /// </summary>
        private static readonly HashSet<string> BatchPatchDeepFields = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "alternateCredentials",
            "argumentList",
            "treeNodes",
        };

        /// <summary>
        /// POST /api/servers/batch：补丁式批量编辑（patch 中缺失的字段 = 保持不变）。
        /// 原子性为「预校验原子性」：ids 全部查找成功 + 每台补丁应用与校验（WPF 平价）全部通过后，
        /// 才统一走 GlobalData.UpdateServer(IEnumerable)；任一环节失败 → 整批零执行。
        /// （DB 层批量更新无事务，与 WPF 行为一致；预校验失败不写库。）
        /// 返回 Ok(更新台数)；NotFound=任一 id 不存在；BadRequest=请求体/未知键/深层字段/只读/校验失败。
        /// H13：dataSourceName 为 null/空白时按每台服务器自身数据源解析（跨库批量，WPF 平价
        /// ——WPF 批量编辑无数据源限制，保存按各台归属分组落库）；显式传 ds 仍为单库语义（兼容）。
        /// </summary>
        public static EditorSaveResult ApplyBatchPatch(string? dataSourceName, List<string>? ids, string? patchJson)
        {
            if (ids == null || ids.Count == 0)
                return EditorSaveResult.BadRequest(new List<string> { "ids must be a non-empty array of server ids" });
            ids = ids.Distinct().ToList(); // 重复 id 去重：避免同台重复写库（无害但浪费）并使计数与保存一致

            if (!string.IsNullOrWhiteSpace(dataSourceName))
            {
                var dataSource = ResolveDataSource(dataSourceName);
                if (dataSource == null)
                    return EditorSaveResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
                if (dataSource.IsWritable != true)
                    return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });
            }

            JObject patch;
            try
            {
                patch = JObject.Parse(patchJson ?? string.Empty);
            }
            catch (Exception)
            {
                return EditorSaveResult.BadRequest(new List<string> { "patch must be a JSON object" });
            }
            if (patch.Count == 0)
                return EditorSaveResult.BadRequest(new List<string> { "patch must contain at least one field" });

            // 键校验：深层字段拒绝（附单机编辑引导）、未知键拒绝并列出——都不做任何查找与写入
            var keyErrors = new List<string>();
            var unknownKeys = new List<string>();
            foreach (var prop in patch.Properties())
            {
                if (BatchPatchDeepFields.Contains(prop.Name))
                    keyErrors.Add($"patch field '{prop.Name}' is not supported in batch edit (subform/array fields must be edited per-server via PUT /api/servers/{{id}})");
                else if (!BatchPatchFieldMap.ContainsKey(prop.Name))
                    unknownKeys.Add(prop.Name);
            }
            if (unknownKeys.Count > 0)
                keyErrors.Add($"unknown patch fields: {string.Join(", ", unknownKeys)}; allowed fields: {string.Join(", ", BatchPatchFieldMap.Keys)}");
            if (keyErrors.Count > 0)
                return EditorSaveResult.BadRequest(keyErrors);

            // 预校验原子性第一环：锁内查找全部 id，任一缺失/不可编辑 → 404，整批不执行。
            // H13：ds 缺省 = 跨库解析（VmItemList 全表按 id 找，与 /api/connect 同款）；显式 ds = 单库
            var gd = IoC.Get<GlobalData>();
            var singleDs = string.IsNullOrWhiteSpace(dataSourceName)
                ? null
                : ResolveDataSource(dataSourceName); // 上方已验非 null（未知名早退）
            var vms = new List<ProtocolBaseViewModel>();
            lock (gd) // 快照语义同 /api/servers：锁内只做查找，后续处理在锁外
            {
                foreach (var id in ids)
                {
                    var vm = singleDs != null
                        ? gd.GetItemById(singleDs.DataSourceName, id)
                        : gd.VmItemList.FirstOrDefault(x => x.Server.Id == id && WebUiEndpoints.IsConnectable(x.Server));
                    if (vm == null || !WebUiEndpoints.IsConnectable(vm.Server))
                        return EditorSaveResult.NotFound();
                    vms.Add(vm);
                }
            }
            // 跨库路径逐台验可写（单库的可写检查已在上方做过）
            if (string.IsNullOrWhiteSpace(dataSourceName))
            {
                foreach (var vm in vms)
                {
                    if (vm.Server.DataSource?.IsWritable != true)
                        return EditorSaveResult.BadRequest(new List<string>
                        {
                            $"dataSource '{vm.Server.DataSource?.DataSourceName ?? "?"}' is read-only",
                        });
                }
            }

            // 第二环：逐台克隆 → 应用补丁 → WPF 平价校验；全部通过才收集，任一失败 → 400（带台 id），零写入
            var updated = new List<ProtocolBase>();
            var validationErrors = new List<string>();
            foreach (var vm in vms)
            {
                // 克隆缓存中的加密态对象后直接打补丁：不动 VmItemList 原对象，未 patch 的字段保持原样。
                // 加密安全性：未 patch 的密文字段二次落库安全——EncryptToDatabaseLevel 内部用
                // UnSafeStringEncipher.EncryptOnce（解得开=已是密文→原样透传，解不开=明文→加密一次），
                // 幂等不双重加密；patch 进来的 password 为明文，恰好在保存时被加密一次（与 WPF 同路径）。
                var clone = (ProtocolBase)vm.Server.Clone();
                var errors = ApplyPatchToServer(clone, patch);
                errors.AddRange(ValidateForSave(clone));
                if (errors.Count > 0)
                {
                    validationErrors.AddRange(errors.Select(e => $"{vm.Id}: {e}"));
                    continue;
                }
                updated.Add(clone);
            }
            if (validationErrors.Count > 0)
                return EditorSaveResult.BadRequest(validationErrors);

            // 第三环：统一保存（IEnumerable 重载按 DataSource 分组落库并联动缓存/标签/UI 通知）
            var ret = gd.UpdateServer(updated);
            return ret.IsSuccess
                ? EditorSaveResult.Ok(updated.Count.ToString())
                : EditorSaveResult.DbError(ret.ErrorInfo);
        }

        /// <summary>
        /// 将 patch 的各字段应用到克隆对象：经 allow-list 取 PascalCase 属性名 → 反射定位
        /// （属性可能不在该协议类型上，如 RDP 无 StartupPath → 报错由调用方聚合）→
        /// JSON 类型门（bool←Boolean、int/枚举←Integer、string←String、List&lt;string&gt;←Array，
        /// 见 ExpectedJsonType；防字符串数字/浮点等隐式转换静默改值）→ Newtonsoft 按属性
        /// 类型转换 → 走 C# 属性 setter（与 WPF 编辑器同一语义：含 Password/PrivateKey
        /// 互斥清空、Tags 去重排序等副作用）。
        /// 返回该台的错误列表（空=全部应用成功）。
        /// </summary>
        private static List<string> ApplyPatchToServer(ProtocolBase server, JObject patch)
        {
            var errors = new List<string>();
            foreach (var prop in patch.Properties())
            {
                var propertyName = BatchPatchFieldMap[prop.Name];
                var property = server.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanWrite || property.SetMethod == null)
                {
                    errors.Add($"field '{prop.Name}' (property '{propertyName}') does not exist on protocol '{server.Protocol}'");
                    continue;
                }

                // JSON null 一律拒绝：部分属性的 C# setter 无 null 防护（如 Note 直落 null 字符串，
                // WPF 只会产出 ""），null 落库会在下游（序列化/连接路径）放大成 NRE/500。
                // 清空请显式用 ""（字符串）或 []（tags）。
                if (prop.Value == null || prop.Value.Type == JTokenType.Null)
                {
                    errors.Add($"patch field '{prop.Name}' cannot be null (use \"\" or [] to clear)");
                    continue;
                }

                // 反射类型门（batch9 Task C）：allow-list 扩到全 schema 标量后，值类型必须与
                // 属性声明的 CLR 类型严格同形——bool 字段收 Boolean、int/枚举收 Integer、
                // string 收 String。 Newtonsoft 的 ToObject 本可隐式转换（"true"→bool、12.5→int?），
                // 静默改值违背「所见即所写」，这里先行拒绝并给出可读错误。
                var expected = ExpectedJsonType(property.PropertyType);
                if (expected != null && prop.Value.Type != expected)
                {
                    errors.Add($"patch field '{prop.Name}' expects a {expected} value (property '{propertyName}' is {property.PropertyType.Name}), got {prop.Value.Type}");
                    continue;
                }

                object? converted;
                try
                {
                    var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    if (targetType.IsEnum && prop.Value?.Type == JTokenType.Integer)
                    {
                        // 枚举值域校验（评审建议）：Integer 类型门只挡类型不挡取值，
                        // 手搓 API 传未定义值（如 42）会被 Newtonsoft 接受落库——拒绝之
                        var rawEnum = prop.Value!.ToObject(targetType);
                        if (!Enum.IsDefined(targetType, rawEnum!))
                        {
                            errors.Add($"patch field '{prop.Name}': {prop.Value} is not a defined value of enum {targetType.Name}");
                            continue;
                        }
                        converted = rawEnum;
                    }
                    else if (targetType == typeof(List<string>) && prop.Value?.Type == JTokenType.Array)
                    {
                        // 数组元素类型校验（评审建议）：Newtonsoft 会把 [1,true] 强转成 ["1","True"]，
                        // 静默改写标签内容——非字符串元素直接拒绝
                        var arr = (JArray)prop.Value!;
                        if (arr.Any(el => el.Type != JTokenType.String))
                        {
                            errors.Add($"patch field '{prop.Name}': array elements must all be strings");
                            continue;
                        }
                        converted = arr.ToObject(property.PropertyType);
                    }
                    else
                    {
                        converted = prop.Value?.ToObject(property.PropertyType);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"cannot convert patch field '{prop.Name}' to {property.PropertyType.Name}: {ex.Message}");
                    continue;
                }
                property.SetValue(server, converted);
            }
            return errors;
        }

        /// <summary>
        /// 属性 CLR 类型 → patch 值应使用的 JSON 类型（类型门的映射表）。
        /// Nullable 先解包；allow-list 内的类型全覆盖（string/bool/int/uint/枚举/List&lt;string&gt;），
        /// 未来出现未收录类型返回 null（门放行、交给 ToObject 的转换异常兜底）。
        /// </summary>
        private static JTokenType? ExpectedJsonType(Type propertyType)
        {
            var t = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (t == typeof(bool)) return JTokenType.Boolean;
            if (t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return JTokenType.Integer;
            if (t == typeof(string)) return JTokenType.String;
            if (t.IsEnum) return JTokenType.Integer; // 编辑器 json 域惯例：Newtonsoft 把枚举序列化为整数
            if (t == typeof(List<string>)) return JTokenType.Array;
            return null;
        }

        /// <summary>批量回读绝不输出的加密/敏感键（patch 键域）：列表接口同样不回读。</summary>
        private static readonly HashSet<string> BatchPeekSensitiveKeys = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "privateKey",   // SSH.PrivateKey 是密文（EncryptToDatabaseLevel），且明文也属敏感
            "gatewayPassword", // RDP.GatewayPassword 同上
        };

        /// <summary>
        /// 批量回读跳过的列表 DTO 已覆盖键（patch 键域）：displayName/note/tags/colorHex/
        /// iconBase64/address/port/userName 已由 GET /api/servers 的 ServerDto 携带——前端
        /// 优先用列表 DTO 计算共享值，peek 不重复传输（iconBase64 尤其重，50 台会显著放大载荷）。
        /// </summary>
        private static readonly HashSet<string> BatchPeekListDtoCoveredKeys = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "displayName", "note", "tags", "colorHex", "iconBase64", "address", "port", "userName",
        };

        /// <summary>
        /// POST /api/servers/batch/peek：批量编辑的共享值回读（fix batch8 #8；batch9 Task C
        /// 随 allow-list 扩展为全 schema 标量键）。出参逐台 {id, ...camelCase: value}——键集
        /// 从 BatchPatchFieldMap 同源派生（扣敏感键与列表 DTO 已覆盖键），前端批量表单按
        /// 协议 schema 渲染后逐字段比对共享值；协议不适用字段为 null（如 RDP 无 StartupPath）。
        ///
        /// 安全论证（本端点绝不返回加密字段）：
        ///  - BatchPeekSensitiveKeys 三键（password/privateKey/gatewayPassword）是全部加密点
        ///    （DataService.EncryptToDatabaseLevel：Password 全系 + SSH.PrivateKey +
        ///    RDP.GatewayPassword；AlternateCredentials/ArgumentList 属子表单不在 allow-list），
        ///    派生时即剔除——键都不出现，而非值置空；
        ///  - 其余键均非机密（枚举/开关/命令文本/路径/凭据库条目名引用——列表编辑器本就展示）；
        ///  - 因此无需克隆+解密（GetEditableConfig 的明文纪律是为 password 类字段设立的），
        ///    直接读 VmItemList 缓存对象即可——缓存是加密态，但本方法不触碰任何加密属性；
        ///  - 只读端点：无任何写入路径，不克隆不落库（反射只 GetProperty+GetValue）。
        /// 错误语义与 batch 补丁对齐：ids 空/缺失 → BadRequest；未知数据源 → BadRequest；
        /// 任一 id 不存在/不可编辑 → NotFound（整批拒绝，前端回退到「未回读」占位）。
        /// </summary>
        public static BatchPeekResult PeekBatch(string? dataSourceName, List<string>? ids)
        {
            if (ids == null || ids.Count == 0)
                return BatchPeekResult.BadRequest(new List<string> { "ids must be a non-empty array of server ids" });

            var singleDs = string.IsNullOrWhiteSpace(dataSourceName)
                ? null // H13：ds 缺省 = 跨库解析（与 batch 补丁同语义）
                : ResolveDataSource(dataSourceName);
            if (!string.IsNullOrWhiteSpace(dataSourceName) && singleDs == null)
                return BatchPeekResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });

            var idList = ids.Distinct().ToList();
            var gd = IoC.Get<GlobalData>();
            var vms = new List<ProtocolBaseViewModel>();
            lock (gd) // 快照语义同 ApplyBatchPatch：锁内只做查找
            {
                foreach (var id in idList)
                {
                    var vm = singleDs != null
                        ? gd.GetItemById(singleDs.DataSourceName, id)
                        : gd.VmItemList.FirstOrDefault(x => x.Server.Id == id && WebUiEndpoints.IsConnectable(x.Server));
                    if (vm == null || !WebUiEndpoints.IsConnectable(vm.Server))
                        return BatchPeekResult.NotFound();
                    vms.Add(vm);
                }
            }

            var items = vms.Select(vm =>
            {
                var item = new Dictionary<string, object?> { ["id"] = vm.Server.Id };
                foreach (var (patchKey, propertyName) in BatchPatchFieldMap)
                {
                    if (BatchPeekSensitiveKeys.Contains(patchKey) || BatchPeekListDtoCoveredKeys.Contains(patchKey))
                        continue;
                    item[patchKey] = ReadProperty(vm.Server, propertyName);
                }
                return item;
            }).ToList();
            return BatchPeekResult.Ok(items);
        }

        /// <summary>
        /// 按声明类型读取属性：属性不在 server 的实际类型上（协议差异，如 RDP 无 StartupPath）
        /// 返回 null 而非抛错——批量回读是尽力展示共享值，协议不适用字段以 null 占位。
        /// </summary>
        private static object? ReadProperty(ProtocolBase server, string propertyName)
        {
            var property = server.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.CanRead ? property.GetValue(server) : null;
        }

        #endregion

        /// <summary>
        /// 保存前校验，与 WPF 编辑器 IDataErrorInfo 同一套规则（复用其索引器，
        /// 自动携带 ShowAddressInput/ShowPortInput 的协议差异与 LocalApp 宏推导）：
        /// DisplayName 非空；有地址协议的 Address/Port 非空且 Port 可 long.Parse；
        /// RdpApp 另需 RemoteApplicationName/RemoteApplicationProgram；
        /// Serial 另需 SerialPort/BitRate；LocalApp 另需 ExePath。
        /// 返回错误列表（"属性名: 消息"），空列表=通过。调用方须在写入前拦截（预校验原子性）。
        /// </summary>
        public static List<string> ValidateForSave(ProtocolBase server)
        {
            var errors = new List<string>();
            void Check(string propertyName)
            {
                var message = server[propertyName]; // IDataErrorInfo 索引器 = WPF 编辑器绑定的同一套规则
                if (!string.IsNullOrEmpty(message))
                    errors.Add($"{propertyName}: {message}");
            }

            Check(nameof(ProtocolBase.DisplayName));
            if (server is ProtocolBaseWithAddressPort)
            {
                Check(nameof(ProtocolBaseWithAddressPort.Address));
                Check(nameof(ProtocolBaseWithAddressPort.Port));
            }
            switch (server)
            {
                case RdpApp:
                    Check(nameof(RdpApp.RemoteApplicationName));
                    Check(nameof(RdpApp.RemoteApplicationProgram));
                    break;
                case Serial:
                    Check(nameof(Serial.SerialPort));
                    Check(nameof(Serial.BitRate));
                    break;
                case LocalApp:
                    Check(nameof(LocalApp.ExePath));
                    break;
            }
            return errors;
        }

        /// <summary>
        /// 数据源名解析：空白视为 Local（与 GET config 一致）；未知返回 null（调用方转 400）。
        /// </summary>
        private static DataSourceBase? ResolveDataSource(string? dataSourceName)
        {
            var name = string.IsNullOrWhiteSpace(dataSourceName)
                ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                : dataSourceName;
            return IoC.Get<DataSourceService>().GetDataSource(name);
        }

        /// <summary>按数据源+id 查找可编辑对象（锁内查找；不可编辑=Dummy/临时会话 视为未找到）。</summary>
        private static ProtocolBaseViewModel? GetEditableVm(string dataSourceName, string id)
        {
            var gd = IoC.Get<GlobalData>();
            ProtocolBaseViewModel? vm;
            lock (gd) // 快照语义同 /api/servers：锁内只做查找，后续处理在锁外
            {
                vm = gd.GetItemById(dataSourceName, id);
            }
            return vm != null && WebUiEndpoints.IsConnectable(vm.Server) ? vm : null;
        }
    }
}
