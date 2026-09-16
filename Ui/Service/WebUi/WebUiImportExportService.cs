using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.Utils.mRemoteNG;
using _1RM.Utils.PRemoteM;
using _1RM.Utils.RdpFile;
using _1RM.View;
using WinCred = _1RM.Utils.WindowsApi.Credential.Credential;

namespace _1RM.Service.WebUi
{
    /// <summary>导入文件格式嗅探结果（按上传文件扩展名，大小写不敏感）。</summary>
    public enum ImportFileKind
    {
        Json,    // 1Remote 导出格式（CmdExportSelectedToJson 的产物）
        Csv,     // mRemoteNG CSV
        Rdp,     // Windows .rdp 连接文件
        Db,      // PRemoteM 旧库 或 1Remote sqlite 库（双格式探测）
        Unknown, // 不支持的扩展名 → 400
    }

    /// <summary>导出结果分类，由端点映射为 HTTP 状态码。</summary>
    public enum ExportStatus
    {
        Ok,
        BadRequest, // ids 空/含未知 id/含只读数据源的服务器
        Forbidden,  // 二次验证未通过（失败或用户取消）
    }

    /// <summary>导入/导出编排结果（与 HTTP 层解耦）。</summary>
    public sealed class ImportResult
    {
        public ExportStatus Status { get; private init; }
        public int Added { get; private init; }
        /// <summary>解析阶段跳过的条目数（如 JSON 数组中 CreateFromJsonString 返回 null 的项——WPF 静默 continue，Web 计数上报）。</summary>
        public int Skipped { get; private init; }
        public List<string> Errors { get; private init; } = new();
        public static ImportResult Ok(int added, int skipped) => new() { Status = ExportStatus.Ok, Added = added, Skipped = skipped };
        public static ImportResult BadRequest(List<string> errors) => new() { Status = ExportStatus.BadRequest, Errors = errors };
    }

    /// <summary>导出产物：解密克隆列表的 Indented JSON（UTF8 由端点编码为文件字节）。</summary>
    public sealed class ExportFileResult
    {
        public ExportStatus Status { get; private init; }
        public string Json { get; private init; } = string.Empty;
        /// <summary>下载文件名（WPF CmdExportSelectedToJson 的默认名：yyyyMMddhhmmss.json）。</summary>
        public string FileName { get; private init; } = string.Empty;
        public List<string> Errors { get; private init; } = new();
        public static ExportFileResult Ok(string json, string fileName) => new() { Status = ExportStatus.Ok, Json = json, FileName = fileName };
        public static ExportFileResult Forbidden() => new() { Status = ExportStatus.Forbidden };
        public static ExportFileResult BadRequest(List<string> errors) => new() { Status = ExportStatus.BadRequest, Errors = errors };
    }

    /// <summary>
    /// 服务器导入/导出编排（Plan 4 Task 2，与 HTTP 层解耦）。
    /// WPF 平价来源：ServerPageViewModelBase.cs CmdImportFromJson(:365)/CmdImportFromCsv(:505)/
    /// CmdImportFromRdp(:550)/CmdImportFromDatabase(:417) 与 CmdExportSelectedToJson(:272)。
    ///
    /// 加密语义（读码核实）：
    /// - 导出文件是明文（导出前 Clone+DecryptToConnectLevel——这就是导出必须二次验证的原因）；
    /// - 导入对 JSON 源仍调一次 DecryptToConnectLevel（WPF :386 同款）：内部是
    ///   UnSafeStringEncipher.DecryptOrReturnOriginalString——明文原样透传、密文才解密，
    ///   对明文导出文件是无害的防御性调用；1Remote 库源读出的服务器是库内密文，必须解密（WPF :462）。
    /// - 插入加密由 DataSourceBase.Database_InsertServer 在内部克隆上完成（EncryptToDatabaseLevel），
    ///   调用方持有的列表保持明文，不复用、不再落库。
    ///
    /// 凭据提取决策（读码核实 DapperDataBase.cs:274-418）：
    /// - 单台重载 AddServer(ref)（:274）不做凭据提取（只插 Server 行）；
    /// - 批量重载 AddServer(IEnumerable)（:305）对 InheritedCredentialName 非空的服务器按
    ///   Credential.GetHash() 自动提取去重（对库与批内两级判重、重名追加 (N)），凭据+服务器在同一事务；
    /// - 故导入逐台用「单元素批量重载」Database_InsertServer(List{server})：既保留按台 Result 粒度
    ///   （{added, errors} 逐台聚合），又不丢失 WPF 批量导入的凭据提取。每次调用前 AddServer 都会
    ///   GetCredentials(false) 重读库，跨调用的 Hash 判重依然成立。
    ///
    /// 已知 WPF 缺陷（Web 修正并记录）：CmdImportFromDatabase 的 1Remote 分支（:456）new
    /// SqliteSource("1Remote") 后既未设 Path 也未连接（Status 恒为 NotConnectedYet → GetServers
    /// 直接返回空缓存），该分支实为死代码；Web 端补上 Path=dbPath + Database_OpenConnection 使其生效。
    /// </summary>
    public static class WebUiImportExportService
    {
        /// <summary>导出/导入的 IconBase64 取自内置图标池；RDP.FromRdpConfig 会随机取一个（空列表会越界）。</summary>
        private static List<string> GetIcons()
        {
            var icons = ServerIcons.Instance.IconsBase64;
            var deadline = Environment.TickCount64 + 3000; // 后台装载，首访有界等待（同 /api/icons 端点）
            while (icons.Count == 0 && Environment.TickCount64 < deadline)
            {
                Thread.Sleep(50);
            }
            return icons;
        }

        /// <summary>
        /// 导入格式嗅探（纯函数，按扩展名）：.json→Json、.csv→Csv、.rdp→Rdp、.db/.sqlite→Db、其余→Unknown。
        /// </summary>
        public static ImportFileKind DetectImportKind(string? fileName)
        {
            var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            return ext switch
            {
                ".json" => ImportFileKind.Json,
                ".csv" => ImportFileKind.Csv,
                ".rdp" => ImportFileKind.Rdp,
                ".db" or ".sqlite" => ImportFileKind.Db,
                _ => ImportFileKind.Unknown,
            };
        }

        /// <summary>
        /// POST /api/servers/import：解析（按 kind）→ 逐台插入目标数据源。
        /// 前置：数据源必须存在且可写（Database_InsertServer 对只读库静默返回 Success，必须前置拦截）。
        /// 返回 {added, skipped, errors}；解析整体失败（非文件、无有效行、库打不开）→ 400 {errors}。
        /// 成功插入任意台后 ReloadAll(force) 刷新 VmItemList/SSE（WPF 导入后同款）。
        /// </summary>
        public static ImportResult Import(string? dataSourceName, ImportFileKind kind, string filePath)
        {
            if (kind == ImportFileKind.Unknown)
                return ImportResult.BadRequest(new List<string> { $"unsupported file type '{Path.GetFileName(filePath)}' (expected .json/.csv/.rdp/.db)" });

            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return ImportResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
            if (dataSource.IsWritable != true)
                return ImportResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });

            List<ProtocolBase> servers;
            var skipped = 0;
            try
            {
                servers = kind switch
                {
                    ImportFileKind.Json => ParseJson(filePath, out skipped),
                    ImportFileKind.Csv => ParseCsv(filePath),
                    ImportFileKind.Rdp => ParseRdp(filePath),
                    ImportFileKind.Db => ParseDatabase(filePath),
                    _ => throw new InvalidOperationException($"unhandled kind {kind}"),
                };
            }
            catch (Exception e)
            {
                // 与 WPF import_failure_with_data_format_error 对应：格式错误 → 400 结构化上报
                return ImportResult.BadRequest(new List<string> { $"failed to parse '{Path.GetFileName(filePath)}': {e.Message}" });
            }

            if (servers == null || servers.Count == 0)
                return ImportResult.BadRequest(new List<string>
                {
                    $"no importable servers found in '{Path.GetFileName(filePath)}' ({kind})"
                });

            // 逐台插入（单元素批量重载，见类注释的凭据提取决策）：清 Id = IsTmpSession 新建语义（WPF :385/:473 同款）
            var added = 0;
            var errors = new List<string>();
            foreach (var server in servers)
            {
                try
                {
                    server.Id = string.Empty;
                    var ret = dataSource.Database_InsertServer(new List<ProtocolBase> { server });
                    if (ret.IsSuccess)
                    {
                        added++;
                    }
                    else
                    {
                        errors.Add($"{server.DisplayName}: {ret.ErrorInfo}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"{server.DisplayName}: {e.Message}");
                }
            }

            if (added > 0)
            {
                IoC.Get<GlobalData>().ReloadAll(true); // WPF 导入成功后 ReloadAll(true) 同款（联动 SSE）
            }
            return ImportResult.Ok(added, skipped);
        }

        /// <summary>1Remote 导出 JSON → List&lt;ProtocolBase&gt;（WPF CmdImportFromJson :380-388 平价）。</summary>
        private static List<ProtocolBase> ParseJson(string path, out int skipped)
        {
            skipped = 0;
            var list = new List<ProtocolBase>();
            var entries = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(path, Encoding.UTF8))
                          ?? new List<object>();
            foreach (var entry in entries.Select(json => ItemCreateHelper.CreateFromJsonString(json.ToString() ?? "")))
            {
                if (entry == null)
                {
                    skipped++; // WPF 静默 continue；Web 计入 skipped 上报
                    continue;
                }
                entry.DecryptToConnectLevel(); // 防御性：导出文件是明文，此调用对明文透传（见类注释）
                list.Add(entry);
            }
            return list;
        }

        /// <summary>mRemoteNG CSV → List&lt;ProtocolBase&gt;（WPF CmdImportFromCsv :520 平价）。无可导入行返回 null → 400。</summary>
        private static List<ProtocolBase>? ParseCsv(string path)
        {
            return MRemoteNgImporter.FromCsv(path, GetIcons());
        }

        /// <summary>.rdp → 单台 RDP（WPF CmdImportFromRdp :562-579 平价，含 TERMSRV 凭据读取）。</summary>
        private static List<ProtocolBase> ParseRdp(string path)
        {
            var config = RdpConfig.FromRdpFile(path)
                         ?? throw new InvalidDataException("no recognizable rdp key:value lines");
            var icons = GetIcons();
            if (icons.Count == 0)
                throw new InvalidOperationException("server icons not loaded yet");
            var rdp = RDP.FromRdpConfig(config, icons);

            try
            {
                // 尝试从 Windows 凭据管理器补用户名/密码（WPF 同款 TERMSRV/<address>）；
                // 凭据不存在 → Load 返回 null → 保持 .rdp 文件自身值（通常密码为空）；非 Windows/异常 → 忽略
                using var cred = WinCred.Load("TERMSRV/" + rdp.Address);
                if (cred != null)
                {
                    rdp.UserName = cred.Username;
                    rdp.Password = cred.Password;
                }
            }
            catch (Exception)
            {
                // ignored（WPF 同款：凭据管理器不可用时静默降级）
            }
            return new List<ProtocolBase> { rdp };
        }

        /// <summary>
        /// .db 双格式探测（WPF CmdImportFromDatabase :427-474 平价 + 连接缺陷修正，见类注释）：
        /// PRemoteM 旧库（Config+Server 表）→ PRemoteMTransferHelper；1Remote 库（Configs+Servers 表）→
        /// SqliteSource 读出后 DecryptToConnectLevel（库内是密文）。两分支可共存于同一文件（WPF 顺序追加同款）。
        /// </summary>
        private static List<ProtocolBase> ParseDatabase(string path)
        {
            var list = new List<ProtocolBase>();
            var dataBase = new DapperDatabaseFree("PRemoteM", DatabaseType.Sqlite);
            var open = dataBase.OpenNewConnection(DbExtensions.GetSqliteConnectionString(path));
            if (!open.IsSuccess)
                throw new InvalidDataException("can not open sqlite database: " + open.ErrorInfo);

            // PRemoteM db
            if (dataBase.TableExists("Config").IsSuccess && dataBase.TableExists("Server").IsSuccess)
            {
                var ss = PRemoteMTransferHelper.GetServers(dataBase);
                if (ss != null)
                {
                    list.AddRange(ss);
                }
            }

            // 1Remote db
            if (dataBase.TableExists("Configs").IsSuccess && dataBase.TableExists("Servers").IsSuccess)
            {
                var ds = new SqliteSource("1Remote") { Path = path };
                ds.Database_OpenConnection(); // WPF 缺失的连接步骤（否则 Status!=OK，GetServers 恒返回空缓存）
                foreach (var s in ds.GetServers(true).Select(x => x.Server))
                {
                    s.DecryptToConnectLevel(); // 库内密文 → 明文（WPF :462 同款）
                    list.Add(s);
                }
            }

            return list;
        }

        /// <summary>
        /// GET /api/servers/export?ids=a,b,c：跨数据源按每台自身数据源取（无需 ds 参数）。
        /// 验证门 = WPF 导出前 SecondaryVerificationHelper.VerifyAsyncUi（:281）平价：
        /// 任一涉及数据源在 30s 窗口外 → await 验证（未开启时直通 true）；非 true → 403；
        /// 通过后为全部涉及数据源记窗口起点（与凭据 reveal 共用 RevealVerifiedAtMap——
        /// 「该数据源的操作者刚通过二次验证」是同一语义）。verifier 参数仅供测试注入
        /// （_isEnabled 反射缝会弹真实 CREDUI 对话框，无法覆盖失败分支）。
        /// 构建 = 每台 Clone + DecryptToConnectLevel → SerializeObject(List, Indented)（WPF :295-304 同款）。
        /// </summary>
        public static async Task<ExportFileResult> ExportAsync(List<string>? ids, Func<Task<bool?>>? verifier = null)
        {
            if (ids == null || ids.Count == 0)
                return ExportFileResult.BadRequest(new List<string> { "ids must be a non-empty comma-separated list of server ids" });
            ids = ids.Distinct().ToList();

            // 快照语义同 /api/servers：锁内只做查找
            var gd = IoC.Get<GlobalData>();
            List<ProtocolBase> servers;
            lock (gd)
            {
                servers = gd.VmItemList
                    .Where(vm => ids.Contains(vm.Server.Id) && WebUiEndpoints.IsConnectable(vm.Server))
                    .Select(vm => vm.Server)
                    .ToList();
            }

            var errors = new List<string>();
            var found = new HashSet<string>();
            foreach (var server in servers)
            {
                found.Add(server.Id);
                // WPF 平价：CmdExportSelectedToJson 只导 IsEditable（数据源可写）的选中项且 CanExecute 要求全部可编辑
                if (server.DataSource?.IsWritable != true)
                    errors.Add($"{server.DisplayName} ({server.Id}): its dataSource is read-only, WPF export requires editable servers");
            }
            foreach (var id in ids.Where(id => !found.Contains(id)))
            {
                errors.Add($"unknown server id '{id}'");
            }
            if (errors.Count > 0)
                return ExportFileResult.BadRequest(errors);

            // 验证门：30s 窗口（按涉及数据源，与 reveal 共用）；任一在窗口外则验证一次
            var involvedDataSources = servers.Select(x => x.DataSource?.DataSourceName)
                .Where(n => !string.IsNullOrEmpty(n)).Cast<string>().Distinct().ToList();
            var now = DateTime.Now;
            if (involvedDataSources.Any(name =>
                    (now - WebUiCredentialService.GetRevealVerifiedAt(name)).TotalSeconds >= 30))
            {
                var verify = verifier ?? (() => SecondaryVerificationHelper.VerifyAsyncUi());
                var verified = await verify();
                if (verified != true)
                    return ExportFileResult.Forbidden();
                var stamp = DateTime.Now;
                foreach (var name in involvedDataSources)
                {
                    WebUiCredentialService.SetRevealVerifiedAt(name, stamp);
                }
            }

            // 构建：克隆 + 解密（缓存/库内对象保持加密态，绝不动原对象）
            var list = new List<ProtocolBase>();
            foreach (var server in servers)
            {
                var clone = (ProtocolBase)server.Clone();
                clone.DecryptToConnectLevel();
                list.Add(clone);
            }
            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            // 文件名格式与 WPF SelectFileHelper 默认名一致（yyyyMMddhhmmss.json）
            return ExportFileResult.Ok(json, DateTime.Now.ToString("yyyyMMddhhmmss") + ".json");
        }

        /// <summary>数据源名解析：空白视为 Local（与其它端点一致）；未知返回 null（调用方转 400）。</summary>
        private static DataSourceBase? ResolveDataSource(string? dataSourceName)
        {
            var name = string.IsNullOrWhiteSpace(dataSourceName)
                ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                : dataSourceName;
            return IoC.Get<DataSourceService>().GetDataSource(name);
        }
    }
}
