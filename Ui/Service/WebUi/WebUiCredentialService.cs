using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// 凭据库编排逻辑（供 Web UI 端点复用，与 HTTP 层解耦）。
    /// 职责：GET /api/credentials 列表（含被引用数）、POST/PUT/DELETE /api/credentials*
    /// 凭据 CRUD、POST /api/credentials/{name}/reveal 明文查看（30s 窗口二次验证门）。
    ///
    /// 加密纪律：与服务器编辑器一致——调用方传明文，加密由
    /// DataSourceBase.Database_InsertCredential / Database_UpdateCredential 在内部克隆上完成
    /// （EncryptToDatabaseLevel）；GetCredentials 读回的缓存为加密态，
    /// 一切明文视图（reveal）必须建立在 CloneMe() 副本上，不得原地解密污染缓存
    /// （WPF 凭据编辑器对缓存原地解密是既有缺陷，Web 不复制）。
    /// 只读库静默成功陷阱：Database_InsertCredential/UpdateCredential 在 _isWritable=false 时
    /// 静默返回 Success——写路径必须先查 IsWritable 拦截为 400（DeleteCredential 自身会 Fail，同样前置）。
    /// 引用联动：更新/删除在 Dapper 事务内同步引用服务器（InheritedCredentialName 改名/清空、
    /// 字段同步），成功且 NeedReloadUI 时调 GlobalData.ReloadAll() 刷新 VmItemList
    /// （与 WPF CredentialVaultViewModel 保存/删除后的联动一致）。
    /// </summary>
    public static class WebUiCredentialService
    {
        /// <summary>reveal 二次验证的免验证窗口时长（秒）：同一数据源内验证一次后 30s 免再次弹窗。</summary>
        private const double RevealVerifyWindowSeconds = 30;

        private static readonly object RevealVerifiedLock = new();
        private static readonly Dictionary<string, DateTime> RevealVerifiedAtMap = new();

        /// <summary>读取某数据源最近一次 reveal 验证通过时间（无记录 = MinValue，即必须验证）。</summary>
        public static DateTime GetRevealVerifiedAt(string dataSourceName)
        {
            lock (RevealVerifiedLock)
            {
                return RevealVerifiedAtMap.TryGetValue(dataSourceName, out var t) ? t : DateTime.MinValue;
            }
        }

        /// <summary>记录某数据源 reveal 验证通过时间（窗口起点）。测试亦可预置以驱动免验证路径。</summary>
        public static void SetRevealVerifiedAt(string dataSourceName, DateTime time)
        {
            lock (RevealVerifiedLock)
            {
                RevealVerifiedAtMap[dataSourceName] = time;
            }
        }

        /// <summary>
        /// GET /api/credentials：完整列表（Name/Address/Port/UserName/被引用数）。
        /// 绝不包含 Password/PrivateKeyPath。引用计数 = 该数据源下引用此凭据名的服务器数
        /// （InheritedCredentialName + AlternateCredentials[].Name，跳过分组头与临时会话）。
        /// </summary>
        public static List<CredentialListItemDto> List(DataSourceBase dataSource)
        {
            // GetCredentials 自带缓存判定与 lock(this=ds)；物化列表在锁外使用
            var credentials = dataSource.GetCredentials().ToList();

            // 引用扫描取 VmItemList 快照（锁内物化，计数在锁外）——同 /api/servers 的快照纪律
            var gd = IoC.Get<GlobalData>();
            List<ProtocolBase> servers;
            lock (gd)
            {
                servers = gd.VmItemList
                    .Where(vm => WebUiEndpoints.IsConnectable(vm.Server)
                                 && vm.DataSourceName == dataSource.DataSourceName)
                    .Select(vm => vm.Server)
                    .ToList();
            }

            return credentials.Select(c => new CredentialListItemDto
            {
                Name = c.Name ?? string.Empty,
                Address = c.Address ?? string.Empty,
                Port = c.Port ?? string.Empty,
                UserName = c.UserName ?? string.Empty,
                RefCount = servers.Count(s => ReferencesCredential(s, c.Name)),
            }).ToList();
        }

        /// <summary>
        /// POST /api/credentials：新建（调用方传明文，Database_InsertCredential 内部克隆+加密落库）。
        /// Name 非空（trim 后）且库内唯一（忽略大小写，与 WPF 编辑器 CurrentCultureIgnoreCase 判重一致）、
        /// 长度 ≤ 100（WPF 编辑器规则）；只读数据源 → 400。
        /// Ok 载荷经 EditorSaveResult.ServerId 携带凭据名。
        /// </summary>
        public static EditorSaveResult Create(string dataSourceName, CredentialInputDto? input)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return EditorSaveResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
            if (dataSource.IsWritable != true)
                return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });

            var name = input?.Name?.Trim() ?? string.Empty;
            var errors = ValidateName(name);
            errors.AddRange(ValidateDuplicate(dataSource, name, exclude: null));
            if (errors.Count > 0)
                return EditorSaveResult.BadRequest(errors);

            var credential = new Credential
            {
                Name = name,
                Address = input?.Address?.Trim() ?? string.Empty,
                Port = input?.Port?.Trim() ?? string.Empty,
                UserName = input?.UserName?.Trim() ?? string.Empty,
                Password = input?.Password ?? string.Empty,           // 明文入，方法内加密
                PrivateKeyPath = input?.PrivateKeyPath?.Trim() ?? string.Empty,
            };
            var ret = dataSource.Database_InsertCredential(credential);
            if (!ret.IsSuccess)
                return MapInsertFailure(ret, name);
            return EditorSaveResult.Ok(credential.Name);
        }

        /// <summary>
        /// PUT /api/credentials/{name}：按名寻址整体替换（与 WPF 凭据编辑一致）。
        /// nameBefore=路由名驱动引用服务器的联动改名/字段同步（Dapper 事务内完成）。
        /// 重命名目标名做与新建相同的非空/长度/唯一校验（排除自身原名）。
        /// Password/PrivateKeyPath 三态语义（batch9 Task D ⑯，接替 batch8 Task E #17 的"空=保持"）：
        /// null（字段未提交）=保持原值；空串=显式清除；非空=新值。列表/编辑 API 均不回显
        /// 明文（安全红线），web 编辑表单以掩码占位——"未改动掩码"提交 null 沿用原值，
        /// 用户 reveal 后清空字段提交空串才能移除密钥（WPF 表单预填明文、直接清空保存的
        /// web 等价）。原缓存为加密态，先克隆再解密取明文（与 reveal 同款，不原地解密
        /// 污染缓存）；Database_UpdateCredential 内部会再次克隆+加密，明文入参与调用方
        /// 直传语义一致。Address/Port 无需同款处理：凭据库不使用这两个字段
        /// （Dapper UpdateCredential 落库前本就强制清空）。
        /// </summary>
        public static EditorSaveResult Update(string dataSourceName, string nameBefore, CredentialInputDto? input)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return EditorSaveResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
            if (dataSource.IsWritable != true)
                return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });

            var org = FindCredential(dataSource, nameBefore);
            if (org == null)
                return EditorSaveResult.NotFound();

            var name = input?.Name?.Trim() ?? string.Empty;
            var errors = ValidateName(name);
            errors.AddRange(ValidateDuplicate(dataSource, name, exclude: org));
            if (errors.Count > 0)
                return EditorSaveResult.BadRequest(errors);

            // null=保持的明文回退源（加密缓存 → 克隆解密，不污染缓存）
            var orgPlaintext = org.CloneMe();
            orgPlaintext.DecryptToConnectLevel();

            var updated = new Credential
            {
                Name = name,
                Address = input?.Address?.Trim() ?? string.Empty,
                Port = input?.Port?.Trim() ?? string.Empty,
                UserName = input?.UserName?.Trim() ?? string.Empty,
                // 三态：null=保持原值（见方法注释）；空串=显式清除；非空明文入，方法内加密落库
                Password = input?.Password == null ? orgPlaintext.Password : input!.Password!,
                PrivateKeyPath = input?.PrivateKeyPath == null ? orgPlaintext.PrivateKeyPath : input!.PrivateKeyPath!.Trim(),
            };
            // Dapper 按 Id 定位更新行（WHERE Id=@Id），须沿用原凭据的 DatabaseId
            updated.DatabaseId = org.DatabaseId;

            var ret = dataSource.Database_UpdateCredential(updated, nameBefore);
            if (!ret.IsSuccess)
                return MapInsertFailure(ret, name); // 冲突兜底同插入（并发改名撞唯一约束）
            if (ret.NeedReloadUI)
                IoC.Get<GlobalData>().ReloadAll(); // 引用服务器已被事务联动，刷新缓存（WPF 同款）
            return EditorSaveResult.Ok(updated.Name);
        }

        /// <summary>
        /// DELETE /api/credentials/{name}：删除。引用该凭据的服务器由 Dapper 事务联动清理
        /// （InheritedCredentialName 置空）——WPF 既有行为，非本端点实现。
        /// </summary>
        public static EditorSaveResult Delete(string dataSourceName, string name)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return EditorSaveResult.BadRequest(new List<string> { $"unknown dataSourceName '{dataSourceName}'" });
            if (dataSource.IsWritable != true)
                return EditorSaveResult.BadRequest(new List<string> { $"dataSource '{dataSource.DataSourceName}' is read-only" });

            if (FindCredential(dataSource, name) == null)
                return EditorSaveResult.NotFound();

            var ret = dataSource.Database_DeleteCredential(new[] { name });
            if (!ret.IsSuccess)
                return EditorSaveResult.DbError(ret.ErrorInfo);
            if (ret.NeedReloadUI)
                IoC.Get<GlobalData>().ReloadAll(); // 引用服务器的 InheritedCredentialName 已在库中被清空
            return EditorSaveResult.Ok(name);
        }

        /// <summary>
        /// POST /api/credentials/{name}/reveal：明文查看。
        /// 30s 窗口内（同数据源，服务端静态时间戳）免再次验证；否则
        /// await SecondaryVerificationHelper.VerifyAsyncUi()（async Task&lt;bool?&gt; 直接 await——
        /// 同步 dispatch 包不住 async；未开启验证时自动返回 true，与 WPF 行为一致；
        /// true=通过，null=用户取消，false=失败——仅 true 放行，其余 403）。
        /// 通过后重找凭据并 CloneMe+DecryptToConnectLevel 返回明文（缓存保持加密态）。
        /// </summary>
        public static async Task<CredentialRevealResult> Reveal(string dataSourceName, string name)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return CredentialRevealResult.BadRequest($"unknown dataSourceName '{dataSourceName}'");
            if (FindCredential(dataSource, name) == null)
                return CredentialRevealResult.NotFound();

            if ((DateTime.Now - GetRevealVerifiedAt(dataSource.DataSourceName)).TotalSeconds >= RevealVerifyWindowSeconds)
            {
                var verified = await SecondaryVerificationHelper.VerifyAsyncUi();
                if (verified != true)
                    return CredentialRevealResult.Forbidden();
                SetRevealVerifiedAt(dataSource.DataSourceName, DateTime.Now);
            }

            // 验证等待期间凭据可能已被改名/删除：重找后再克隆
            var credential = FindCredential(dataSource, name);
            if (credential == null)
                return CredentialRevealResult.NotFound();

            // 先克隆后解密：GetCredentials 缓存为加密态，原地解密会污染缓存（WPF 是既有缺陷）
            var clone = credential.CloneMe();
            clone.DecryptToConnectLevel();
            return CredentialRevealResult.Ok(clone.Password ?? string.Empty, clone.PrivateKeyPath ?? string.Empty);
        }

        /// <summary>服务器是否引用指定凭据名：InheritedCredentialName（继承）或 AlternateCredentials（备用凭据子表单）。</summary>
        private static bool ReferencesCredential(ProtocolBase server, string? credentialName)
        {
            if (string.IsNullOrEmpty(credentialName))
                return false; // 未设置继承凭据的服务器 InheritedCredentialName 为 ""，不计入
            return server switch
            {
                ProtocolBaseWithAddressPortUserPwd u when u.InheritedCredentialName == credentialName => true,
                ProtocolBaseWithAddressPort a when a.AlternateCredentials.Any(c => c.Name == credentialName) => true,
                _ => false,
            };
        }

        /// <summary>按名查找缓存凭据（ordinal 精确匹配；GetCredentials 自带缓存判定与锁）。</summary>
        private static Credential? FindCredential(DataSourceBase dataSource, string name)
        {
            return dataSource.GetCredentials().FirstOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Name 校验（WPF 编辑器 AlternativeCredentialEditViewModel 同款规则）：
        /// trim 后非空；长度 ≤ 100（模型 setter 仅对 &gt;128 静默截断，此处前置拦截）。
        /// </summary>
        private static List<string> ValidateName(string name)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Name: can not be empty");
            else if (name.Length > 100)
                errors.Add("Name: too long (max 100)");
            return errors;
        }

        /// <summary>
        /// 重名校验：与 WPF 编辑器一致按 CurrentCultureIgnoreCase 比较（exclude=更新时排除自身原名）。
        /// （DB UNIQUE 兜底由 MapInsertFailure 处理并发窗口。）
        /// </summary>
        private static List<string> ValidateDuplicate(DataSourceBase dataSource, string name, Credential? exclude)
        {
            var errors = new List<string>();
            var duplicated = dataSource.GetCredentials().Any(x =>
                !ReferenceEquals(x, exclude)
                && string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));
            if (duplicated)
                errors.Add($"Name: '{name}' is already existed");
            return errors;
        }

        /// <summary>
        /// 写库失败映射：DB UNIQUE 冲突（预校验后的并发窗口）→ 400 重名；其余 → 500。
        /// SQLite 报 "UNIQUE constraint failed"，MySQL 报 "Duplicate entry"，PG 报 "duplicate key value"。
        /// </summary>
        private static EditorSaveResult MapInsertFailure(Result ret, string name)
        {
            if (ret.ErrorInfo != null
                && (ret.ErrorInfo.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0
                    || ret.ErrorInfo.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return EditorSaveResult.BadRequest(new List<string> { $"Name: '{name}' is already existed" });
            }
            return EditorSaveResult.DbError(ret.ErrorInfo);
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

    /// <summary>reveal 结果分类，由端点映射为 HTTP 状态码。</summary>
    public enum CredentialRevealStatus
    {
        Ok,
        BadRequest, // 数据源名非法
        NotFound,   // 凭据名不存在
        Forbidden,  // 二次验证未通过（失败或用户取消）
    }

    public sealed class CredentialRevealResult
    {
        public CredentialRevealStatus Status { get; private init; }
        public string Password { get; private init; } = string.Empty;
        public string PrivateKeyPath { get; private init; } = string.Empty;
        public List<string> Errors { get; private init; } = new();

        public static CredentialRevealResult Ok(string password, string privateKeyPath)
            => new() { Status = CredentialRevealStatus.Ok, Password = password, PrivateKeyPath = privateKeyPath };
        public static CredentialRevealResult NotFound() => new() { Status = CredentialRevealStatus.NotFound };
        public static CredentialRevealResult Forbidden() => new() { Status = CredentialRevealStatus.Forbidden };
        public static CredentialRevealResult BadRequest(string error)
            => new() { Status = CredentialRevealStatus.BadRequest, Errors = new List<string> { error } };
    }
}
