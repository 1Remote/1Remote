using System.Collections.Generic;
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
        BadRequest,      // 请求体/数据源名/鉴别字段非法，或 WPF 平价校验未通过（写入前拦截）
        NotFound,        // 目标 id 不存在（或不可编辑：Dummy 分组头/临时会话）
        DbError,         // 校验通过但写库失败（含只读数据源），500 + 错误详情
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
    /// 连接编辑器的编排逻辑（供 Web UI 端点复用，与 HTTP 层解耦；Task: create/update/delete/batch 同此后继）。
    /// 加密纪律：VmItemList 内存缓存中的对象是加密态（DB 读路径不落解密），
    /// 一切对外的明文视图必须建立在 Clone() 副本上——WPF 编辑器对缓存原地解密是既有缺陷，Web 不复制。
    /// 保存路径与 WPF 编辑器一致（ServerEditorPageViewModel）：调用方传明文，
    /// 加密由 DataSourceBase.Database_Insert/UpdateServer 在内部克隆上完成。
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

            var ret = IoC.Get<GlobalData>().DeleteServer(new[] { vm.Server });
            return ret.IsSuccess
                ? EditorSaveResult.Ok(id)
                : EditorSaveResult.DbError(ret.ErrorInfo);
        }

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
