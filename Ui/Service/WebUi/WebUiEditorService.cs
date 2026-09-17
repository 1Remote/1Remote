using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        /// 注意 startupAutoCommand（SSH/Telnet/Serial）、startupPath（FTP/SFTP）、
        /// rdpFileAdditionalSettings（RDP/RdpApp）并非所有协议都有——属性不存在时按台 400（见 ApplyBatchPatch）。
        /// </summary>
        private static readonly Dictionary<string, string> BatchPatchFieldMap = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // ProtocolBase 层
            ["displayName"] = nameof(ProtocolBase.DisplayName),
            ["note"] = nameof(ProtocolBase.Note),
            ["tags"] = nameof(ProtocolBase.Tags),                       // List<string>，显式覆盖语义（非 WPF 交集合并，Plan 2 有意偏差）
            ["colorHex"] = nameof(ProtocolBase.ColorHex),
            ["iconBase64"] = nameof(ProtocolBase.IconBase64),
            // ProtocolBaseWithAddressPort 层
            ["address"] = nameof(ProtocolBaseWithAddressPort.Address),
            ["port"] = nameof(ProtocolBaseWithAddressPort.Port),
            // ProtocolBaseWithAddressPortUserPwd 层；password 为明文，加密由 DataSourceBase 保存时完成
            ["userName"] = nameof(ProtocolBaseWithAddressPortUserPwd.UserName),
            ["password"] = nameof(ProtocolBaseWithAddressPortUserPwd.Password),
            ["inheritedCredentialName"] = nameof(ProtocolBaseWithAddressPortUserPwd.InheritedCredentialName),
            ["askPasswordWhenConnect"] = nameof(ProtocolBaseWithAddressPortUserPwd.AskPasswordWhenConnect), // bool?
            // 协议专属（见上方注释：非全协议共有）
            ["startupAutoCommand"] = nameof(SSH.StartupAutoCommand),
            ["startupPath"] = nameof(SFTP.StartupPath),
            ["rdpFileAdditionalSettings"] = nameof(RDP.RdpFileAdditionalSettings),
        };

        /// <summary>
        /// 有意不进 allow-list 的深层/子表单字段（Plan 2 简化）：批量编辑只支持扁平字段，
        /// 子表单（备用凭据、参数表）走单机编辑 PUT /api/servers/{id}。命中即 400 并附引导消息。
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
        /// </summary>
        public static EditorSaveResult ApplyBatchPatch(string? dataSourceName, List<string>? ids, string? patchJson)
        {
            if (ids == null || ids.Count == 0)
                return EditorSaveResult.BadRequest(new List<string> { "ids must be a non-empty array of server ids" });
            ids = ids.Distinct().ToList(); // 重复 id 去重：避免同台重复写库（无害但浪费）并使计数与保存一致

            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return EditorSaveResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
            if (dataSource.IsWritable != true)
                return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });

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

            // 预校验原子性第一环：锁内查找全部 id，任一缺失/不可编辑 → 404，整批不执行
            var gd = IoC.Get<GlobalData>();
            var vms = new List<ProtocolBaseViewModel>();
            lock (gd) // 快照语义同 /api/servers：锁内只做查找，后续处理在锁外
            {
                foreach (var id in ids)
                {
                    var vm = gd.GetItemById(dataSource.DataSourceName, id);
                    if (vm == null || !WebUiEndpoints.IsConnectable(vm.Server))
                        return EditorSaveResult.NotFound();
                    vms.Add(vm);
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
        /// Newtonsoft 按属性类型转换 JSON 值（string/bool?/List&lt;string&gt;）→ 走 C# 属性 setter
        /// （与 WPF 编辑器同一语义：含 Password/PrivateKey 互斥清空、Tags 去重排序等副作用）。
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

                object? converted;
                try
                {
                    converted = prop.Value?.ToObject(property.PropertyType);
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
