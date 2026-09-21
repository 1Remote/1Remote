using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using _1RM.Model;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Utils.PuTTY;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// 数据源 CRUD/测试 与运行器读写编排逻辑（供 Web UI 端点复用，与 HTTP 层解耦）。
    /// 职责：POST/PUT/DELETE /api/datasources*（新建/更新/删除，删除带服务器数前置确认）、
    /// POST /api/datasources/{name}/test（连接测试）、GET/PUT /api/settings/runners（运行器整体往返）。
    ///
    /// 数据源持久化双集合（最大陷阱，WPF 正确模式 DataSourceViewModel.CmdAdd/CmdEdit/CmdDelete）：
    /// DataSourceService.AddOrUpdateDataSource/RemoveDataSource 只改运行时字典（AdditionalSources）；
    /// 落盘必须同步 ConfigurationService.AdditionalDataSource（独立集合，Save() 时写
    /// ProfileAdditionalDataSourceJsonPath）+ Save()。顺序 = WPF：先改 AdditionalDataSource（同一实例）
    /// → Save() → 再 AddOrUpdateDataSource（触发连接尝试 + ReloadAll）/ RemoveDataSource。
    ///
    /// 新建失败语义 = WPF：CmdAdd 无条件保存（连接失败仅弹错误提示，数据源保留），端点镜像为
    /// 保存后返回 201 + 实际 status（connected/disconnected），前端可用 /test 端点先行验证。
    ///
    /// 密码"空=保持"：Mysql/Pgsql Password setter 收 "" 会清空（EncryptPassword 置 ""），
    /// PUT 端点必须条件赋值（null/空串跳过 = 保持原密码）；POST 为新建语义，明文直传。
    ///
    /// 删除守卫（与 WPF 的有意差异）：WPF CmdDelete 不检查数据源下是否有服务器，直接移除
    /// （服务器仍留在库文件里，只是从界面消失）。Web 端为防误删增加 serverCount>0 时 409 前置确认；
    /// keepServers=true 表示用户已确认 = 按 WPF 原样删除（不迁移服务器，数据留在库文件中）。
    ///
    /// TestConnection：仅 MysqlSource/PgsqlSource 有静态 TestConnection（返回 bool）；
    /// SqliteSource 无——sqlite 走 Database_SelfCheck（返回 DatabaseStatus 含错误详情，
    /// 即 AddOrUpdateDataSource 内部同款自检）。mysql/pgsql 测试时未提供密码则沿用已存密码
    /// （EncryptPassword 解密），避免"测试连接"把密码置空。
    ///
    /// 运行器读写：整体往返 ProtocolSettings（含 SelectedRunnerName），与
    /// ProtocolConfigurationService 自身的持久化共用同一条 Newtonsoft 序列化路径
    /// （JsonKnownTypesConverter&lt;Runner&gt; 的 $type 判别 + [JsonConstructor] 参数化构造，
    /// LoadConfig 启动装载即此路径——InternalDefaultRunner/PuttyRunner 等内置运行器可完整往返，
    /// 无需回退到"仅合并外部运行器"策略，测试 Runners_Put_RoundTrip 验证）。PUT 在既有
    /// ProtocolSettings 实例上原位替换 SelectedRunnerName + Runners（保持对象身份，
    /// 编辑器 VM 等持有的引用不失效），并重放 Load 的后处理（OwnerProtocolName 回填 +
    /// ExternalRunner.MarcoNames 宏补全）后 Save()。
    /// </summary>
    public static class WebUiDataSourceService
    {
        /// <summary>
        /// POST /api/datasources：新建数据源。type: sqlite|mysql|pgsql（postgresql 同义归一）。
        /// name 缺省时 sqlite 从路径文件名推导（WPF 弹窗由用户填写，web 向导省一步）；
        /// 与现有数据源重名（CurrentCultureIgnoreCase，WPF 编辑器同款判重）→ 409。
        /// mysql/pgsql 校验 = WPF Mysql/PgsqlSettingViewModel IDataErrorInfo：host/port(1-65535)/
        /// databaseName/userName/password 均必填（password 为空 → 400，与 WPF CanSave 一致）；
        /// sqlite 仅需 path 非空。校验全过才落库（零写入）。
        /// </summary>
        public static DataSourceMutationResult Create(DataSourceSaveRequest? input)
        {
            if (input == null)
                return DataSourceMutationResult.BadRequest("body must be a JSON object");

            var type = NormalizeType(input.Type);
            if (type == null)
                return DataSourceMutationResult.BadRequest(
                    $"type: '{input.Type}' is not supported, expected one of: sqlite, mysql, pgsql");

            var cfg = input.Config ?? new DataSourceConfigInput();
            var errors = new List<string>();
            var name = input.Name?.Trim() ?? string.Empty;

            // 先校验后构造（SqliteSource.Path setter 会 new FileInfo：空路径直接抛异常，不能先建后验）
            DataSourceBase source;
            switch (type)
            {
                case "sqlite":
                {
                    var path = cfg.Path?.Trim() ?? string.Empty;
                    if (path.Length == 0)
                        errors.Add("config.path: can not be empty");
                    if (name.Length == 0)
                    {
                        // sqlite 名缺省 = 路径文件名（不含扩展名）
                        name = Path.GetFileNameWithoutExtension(path);
                    }
                    if (name.Length == 0)
                        errors.Add("name: can not be empty and could not be derived from config.path");
                    if (errors.Count > 0)
                        return DataSourceMutationResult.BadRequest(errors);
                    // SqliteSource 构造只写 readonly Name 字段（数据库名），DataSourceName 须显式赋值
                    // （Local 由 InitLocalDataSource 赋值；WPF 编辑弹窗直接改 org 的属性——Web 侧补齐）
                    source = new SqliteSource(name) { Path = path };
                    source.DataSourceName = name;
                    break;
                }
                case "mysql":
                case "pgsql":
                {
                    var host = cfg.Host?.Trim() ?? string.Empty;
                    var databaseName = cfg.DatabaseName?.Trim() ?? string.Empty;
                    var userName = cfg.UserName?.Trim() ?? string.Empty;
                    var password = cfg.Password ?? string.Empty;
                    var port = cfg.Port ?? (type == "mysql" ? 3306 : 5432);
                    if (host.Length == 0) errors.Add("config.host: can not be empty");
                    if (port < 1 || port > 65535) errors.Add("config.port: must be 1 - 65535");
                    if (databaseName.Length == 0) errors.Add("config.databaseName: can not be empty");
                    if (userName.Length == 0) errors.Add("config.userName: can not be empty");
                    if (password.Length == 0) errors.Add("config.password: can not be empty (WPF 新建弹窗同款必填)");
                    if (name.Length == 0) errors.Add("name: can not be empty");
                    if (errors.Count > 0)
                        return DataSourceMutationResult.BadRequest(errors);
                    if (type == "mysql")
                    {
                        source = new MysqlSource
                        {
                            DataSourceName = name, Host = host, Port = port,
                            DatabaseName = databaseName, UserName = userName, Password = password,
                        };
                    }
                    else
                    {
                        source = new PgsqlSource
                        {
                            DataSourceName = name, Host = host, Port = port,
                            DatabaseName = databaseName, UserName = userName, Password = password,
                        };
                    }
                    break;
                }
                default:
                    return DataSourceMutationResult.BadRequest($"type: '{input.Type}' is not supported");
            }

            var conflict = FindNameConflict(name);
            if (conflict != null)
                return DataSourceMutationResult.Conflict($"name: data source '{name}' already exists", serverCount: 0);

            // WPF CmdAdd 顺序（DataSourceViewModel.cs:105-112）：AdditionalDataSource.Add → Save → AddOrUpdate
            var cs = IoC.Get<ConfigurationService>();
            var dss = IoC.Get<DataSourceService>();
            cs.AdditionalDataSource.Add(source);
            cs.Save();
            var ret = dss.AddOrUpdateDataSource(source); // 连接尝试（超时 2s）；失败不回滚（WPF 同款）
            return DataSourceMutationResult.Ok(DtoMapper.FromDataSource(source), ret.GetErrorMessage);
        }

        /// <summary>
        /// PUT /api/datasources/{name}：更新连接参数并重连。Local → 400（SQLite 路径不暴露，
        /// plan 全局约定安全域）；未知名 → 404。字段缺失 = 保持不变；password null/空串 = 保持
        /// （Mysql/Pgsql Password setter 收 "" 会清空，必须条件赋值）。落库顺序 = WPF CmdEdit：
        /// Save() → AddOrUpdateDataSource（断开旧连接重连）。
        /// newName（body.name，batch10 Task B #7）非空且异于现名 = 改名，WPF CmdEdit 平价：
        /// 弹窗 Name 直接写 org.DataSourceName 后 Save + AddOrUpdateDataSource（后者移除同实例
        /// 旧键再入新键）。重名（CurrentCultureIgnoreCase，排除自身）→ 409；WPF sqlite 弹窗
        /// 仅 Local 禁改名（NameWritable），web 编辑模态本就不开放 Local，其余类型均可改名。
        /// </summary>
        public static DataSourceMutationResult Update(string name, DataSourceConfigInput? input, string? newName = null)
        {
            if (string.IsNullOrWhiteSpace(name) || name == DataSourceService.LOCAL_DATA_SOURCE_NAME)
                return DataSourceMutationResult.BadRequest($"data source 'Local' can not be modified via web ui");

            var source = FindAdditionalSource(name);
            if (source == null)
                return DataSourceMutationResult.NotFound();

            var cfg = input ?? new DataSourceConfigInput();
            var errors = new List<string>();
            switch (source)
            {
                case SqliteSource sqlite:
                {
                    var path = cfg.Path?.Trim();
                    if (path != null)
                    {
                        if (path.Length == 0) errors.Add("config.path: can not be empty");
                        else sqlite.Path = path;
                    }
                    break;
                }
                case MysqlSource mysql:
                {
                    ApplyServerConfig(cfg, errors,
                        (v) => mysql.Host = v, (v) => mysql.Port = v,
                        (v) => mysql.DatabaseName = v, (v) => mysql.UserName = v,
                        (v) => mysql.Password = v);
                    break;
                }
                case PgsqlSource pgsql:
                {
                    ApplyServerConfig(cfg, errors,
                        (v) => pgsql.Host = v, (v) => pgsql.Port = v,
                        (v) => pgsql.DatabaseName = v, (v) => pgsql.UserName = v,
                        (v) => pgsql.Password = v);
                    break;
                }
                default:
                    return DataSourceMutationResult.BadRequest($"data source type '{source.GetType().Name}' is not supported");
            }
            if (errors.Count > 0)
                return DataSourceMutationResult.BadRequest(errors);

            // 改名（在 config 应用后、Save 前）：排除自身的重名 → 409；同实例改名由
            // AddOrUpdateDataSource 内部移除旧键再入新键（同实例条目 TryRemove，见其实现）
            if (!string.IsNullOrWhiteSpace(newName))
            {
                var trimmed = newName.Trim();
                if (trimmed != source.DataSourceName)
                {
                    var conflict = FindNameConflict(trimmed);
                    if (conflict != null && !ReferenceEquals(conflict, source))
                        return DataSourceMutationResult.Conflict($"name: data source '{trimmed}' already exists", serverCount: 0);
                    source.DataSourceName = trimmed;
                }
            }

            var cs = IoC.Get<ConfigurationService>();
            var dss = IoC.Get<DataSourceService>();
            cs.Save(); // WPF CmdEdit：先 Save 再重连
            var ret = dss.AddOrUpdateDataSource(source);
            return DataSourceMutationResult.Ok(DtoMapper.FromDataSource(source), ret.GetErrorMessage);
        }

        /// <summary>
        /// DELETE /api/datasources/{name}?keepServers=。Local → 400；未知名 → 404。
        /// serverCount&gt;0 且未带 keepServers=true → 409 {serverCount}（Web 侧前置确认；
        /// WPF CmdDelete 无此检查直接删——keepServers=true 即用户已确认，镜像 WPF 原样删除：
        /// AdditionalDataSource.Remove → Save → RemoveDataSource，不迁移/不删服务器，
        /// 数据留在库文件中，重新添加该数据源即可找回）。
        /// </summary>
        public static DataSourceMutationResult Delete(string name, bool keepServers)
        {
            if (string.IsNullOrWhiteSpace(name) || name == DataSourceService.LOCAL_DATA_SOURCE_NAME)
                return DataSourceMutationResult.BadRequest($"data source 'Local' can not be deleted");

            var source = FindAdditionalSource(name);
            if (source == null)
                return DataSourceMutationResult.NotFound();

            var serverCount = DtoMapper.CountServers(source);
            if (serverCount > 0 && !keepServers)
            {
                return DataSourceMutationResult.Conflict(
                    $"data source '{name}' still contains {serverCount} server(s); pass keepServers=true to remove it anyway (servers stay in the database file, mirroring the WPF delete behavior)",
                    serverCount);
            }

            // WPF CmdDelete 顺序（DataSourceViewModel.cs:182-191）
            var cs = IoC.Get<ConfigurationService>();
            if (cs.AdditionalDataSource.Contains(source))
            {
                cs.AdditionalDataSource.Remove(source);
                cs.Save();
            }
            IoC.Get<DataSourceService>().RemoveDataSource(source.DataSourceName);
            return DataSourceMutationResult.Ok(null, string.Empty);
        }

        /// <summary>
        /// POST /api/datasources/{name}/test：测试连接。sqlite：Database_SelfCheck（即
        /// AddOrUpdateDataSource 内部同款自检，返回错误详情）；mysql/pgsql：静态 TestConnection，
        /// 请求 config 中未提供/空密码时沿用已存密码（解密）。带 config 时按 config 测试
        /// （字段缺省回退已存值）——支持"保存前先测"的向导流程。未知数据源 → 404。
        ///
        /// 草稿测试（batch10 Task B #8，WPF 语义对齐）：WPF Mysql/PgsqlSettingViewModel
        /// .CmdTestConnection 对**表单草稿**构造临时配置直接 TestConnection（不经保存）；
        /// 未保存的数据源名查不到已存实例，故当 name 无匹配且 body.type + config 齐备时按
        /// 草稿测试：sqlite 临时实例 SelfCheck；mysql/pgsql 校验必填后静态 TestConnection。
        /// 密码缺省可回退 name 能寻址到的已存源密码（编辑弹窗"密码留空=保持"的测试侧平价）。
        /// </summary>
        public static DataSourceTestResult Test(string name, DataSourceConfigInput? input, string? type = null)
        {
            var dss = IoC.Get<DataSourceService>();
            var resolvedName = string.IsNullOrWhiteSpace(name) ? DataSourceService.LOCAL_DATA_SOURCE_NAME : name;
            var source = dss.GetDataSource(resolvedName);
            if (source == null)
            {
                var draftType = NormalizeType(type);
                if (draftType == null || input == null)
                    return DataSourceTestResult.NotFound();
                return TestDraft(draftType, input, name, dss);
            }

            switch (source)
            {
                case SqliteSource:
                {
                    // H12：编辑弹窗的“测试连接”对表单草稿测试——config.path 非空时构造临时实例自检
                    // （TestDraft 的 sqlite 分支同款；此前无视草稿直接对已存旧路径自检，“测试成功
                    // 保存后却断线”）。草稿路径为空才回落已存实例（WPF DataSourceViewModel 绑定
                    // 原对象、测试即表单值的平价）
                    var draftPath = input?.Path?.Trim();
                    if (!string.IsNullOrEmpty(draftPath))
                    {
                        var draft = new SqliteSource(resolvedName) { Path = draftPath };
                        var draftRet = draft.Database_SelfCheck();
                        draft.Database_CloseConnection();
                        return DataSourceTestResult.Ok(draftRet.Status == EnumDatabaseStatus.OK,
                            MapStatus(draftRet.Status), draftRet.Status == EnumDatabaseStatus.OK ? string.Empty : draftRet.GetErrorMessage);
                    }
                    // sqlite 无静态 TestConnection：自检 = 连接 + 建表/校验（同 Local 初始化路径）
                    var ret = source.Database_SelfCheck();
                    return DataSourceTestResult.Ok(ret.Status == EnumDatabaseStatus.OK,
                        MapStatus(ret.Status), ret.Status == EnumDatabaseStatus.OK ? string.Empty : ret.GetErrorMessage);
                }
                case MysqlSource mysql:
                {
                    var ok = TestServerConnection(mysql, input,
                        (host, port, db, user, pwd) => MysqlSource.TestConnection(host, port, db, user, pwd));
                    return DataSourceTestResult.FromBool(ok, DtoMapper.FromDataSource(mysql).Status);
                }
                case PgsqlSource pgsql:
                {
                    var ok = TestServerConnection(pgsql, input,
                        (host, port, db, user, pwd) => PgsqlSource.TestConnection(host, port, db, user, pwd));
                    return DataSourceTestResult.FromBool(ok, DtoMapper.FromDataSource(pgsql).Status);
                }
                default:
                    return DataSourceTestResult.BadRequest($"data source type '{source.GetType().Name}' is not supported");
            }
        }

        // ------------------------------------------------------------------
        // runners
        // ------------------------------------------------------------------

        /// <summary>
        /// GET /api/settings/runners：{protocols:{SSH:{selectedRunnerName, runners:[...], macros:[...]}}, meta:{...}}。
        /// 每协议序列化 ProtocolSettings（Newtonsoft，与 ProtocolConfigurationService.Save 同路径），
        /// 顶层键改写为 camelCase（selectedRunnerName/runners/macros），runners 数组内容 PascalCase +
        /// $type 判别直通（内置/外部运行器结构完整保留；无循环引用——Save() 早已验证）。
        /// macros（fix batch7 Task E）：ProtocolSettings.MarcoNames/MarcoDescriptions（[JsonIgnore]，
        /// 序列化不可见）单独铺平为 [{name, description}]——WPF 参数宏自动补全与协议帮助 (i) 的数据源，
        /// web 添加运行器模态的宏提示用；PUT 侧 Newtonsoft 对未知键默认忽略，原样回传安全。
        /// </summary>
        public static Dictionary<string, JsonElement> ReadRunners(ProtocolConfigurationService pcs)
        {
            var protocols = new Dictionary<string, JsonElement>();
            foreach (var kv in pcs.ProtocolConfigs)
            {
                var jObj = JObject.Parse(JsonConvert.SerializeObject(kv.Value));
                var macros = new JArray();
                for (var i = 0; i < Math.Min(kv.Value.MarcoNames.Count, kv.Value.MarcoDescriptions.Count); i++)
                {
                    macros.Add(new JObject
                    {
                        ["name"] = kv.Value.MarcoNames[i],
                        ["description"] = kv.Value.MarcoDescriptions[i],
                    });
                }
                var renamed = new JObject
                {
                    ["selectedRunnerName"] = jObj["SelectedRunnerName"] ?? JValue.CreateString(""),
                    ["runners"] = jObj["Runners"] ?? new JArray(),
                    ["macros"] = macros,
                };
                using var doc = JsonDocument.Parse(renamed.ToString(Formatting.None));
                protocols[kv.Key] = doc.RootElement.Clone(); // Clone 脱离文档生命周期（STJ 内嵌安全，同 /api/servers/{id}/config）
            }
            return protocols;
        }

        /// <summary>
        /// 运行器编辑元数据（fix batch7 Task E #13）：主题/字体/字符集三个下拉的选项域，
        /// 均取 WPF 运行器设置页的同源数据，避免前端另行硬编码：
        /// - puttyThemes：PuttyRunnerSettings 主题下拉源 = PuttyThemes.Themes.Keys（静态资源
        ///   Resources/KiTTY/PuttyThemes.json）；colors 只取 WPF 预览用到的 5 个语义位
        ///   （Colour2 背景 / Colour0 前景 / Colour9 红 / Colour11 绿 / Colour15 白，与
        ///   PuttyRunner.LoadColours 的键位一致），"r,g,b" 归一为 #RRGGBB 供 web 色块预览；
        /// - fonts：WPF 字体下拉源 = Fonts.SystemFontFamilies（FamilyNames.Last().Value），
        ///   排序去重（WPF 未排序，web 下拉按名排序更可用）；无 WPF Media 环境时回退空表；
        /// - codePages：字符集清单 = PuttyRunner.CodePages（[JsonIgnore] 不随 runners 直通），
        ///   从现有配置实例取（SSH 首项恒为 PuttyRunner），不新建实例避免触发主题装载。
        /// </summary>
        public static Dictionary<string, object> ReadRunnersMeta(ProtocolConfigurationService pcs)
        {
            var themes = new List<Dictionary<string, object>>();
            foreach (var kv in PuttyThemes.Themes)
            {
                var colors = new Dictionary<string, string?>();
                // 键位与 PuttyRunner.LoadColours/SetBrush 一致（WPF 预览用到的 5 个 ColourN）
                foreach (var (semantic, colourKey) in new[]
                {
                    ("bg", "Colour2"),   // 背景色（WPF 预览外层）
                    ("fg", "Colour0"),   // 默认前景（WPF 预览 "root@putty" 行）
                    ("red", "Colour9"),  // 亮红（WPF 预览 "data.zip"）
                    ("green", "Colour11"), // 亮绿（WPF 预览 "1Remote"）
                    ("white", "Colour15"), // 亮白（WPF 预览 "cmake"/"packages"）
                })
                {
                    var option = kv.Value.FirstOrDefault(x => string.Equals(x.Key, colourKey, StringComparison.CurrentCultureIgnoreCase));
                    colors[semantic] = RgbToHex(option?.Value as string);
                }
                themes.Add(new Dictionary<string, object> { ["name"] = kv.Key, ["colors"] = colors });
            }

            var fonts = new List<string>();
            try
            {
                fonts = System.Windows.Media.Fonts.SystemFontFamilies
                    .Select(f => f.FamilyNames.Last().Value)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .OrderBy(n => n, StringComparer.InvariantCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception)
            {
                // 无 WPF Media 上下文（理论上不发生——桌面宿主必载）：空表，前端下拉退化
            }

            var codePages = pcs.ProtocolConfigs.Values
                .SelectMany(c => c.Runners.OfType<PuttyRunner>())
                .FirstOrDefault()?.CodePages ?? new List<string>();

            return new Dictionary<string, object>
            {
                ["puttyThemes"] = themes,
                ["fonts"] = fonts,
                ["codePages"] = codePages,
            };
        }

        /// <summary>"r,g,b"（PuTTY 主题 ColourN 值形态）→ "#RRGGBB"；解析失败返回 null（前端回退黑/白）。</summary>
        private static string? RgbToHex(string? rgb)
        {
            if (string.IsNullOrEmpty(rgb)) return null;
            var parts = rgb.Split(',');
            if (parts.Length != 3
                || !byte.TryParse(parts[0].Trim(), out var r)
                || !byte.TryParse(parts[1].Trim(), out var g)
                || !byte.TryParse(parts[2].Trim(), out var b))
                return null;
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>
        /// PUT /api/settings/runners：整体替换提供的协议配置（body 中缺失的协议 = 保持不变）。
        /// 入参为请求体的 protocols 元素本身（端点已解包 RunnersSaveRequest.Protocols）。
        /// 未知协议键 → 400（列出合法键，仅 6 个：SSH/Telnet/Serial/VNC/SFTP/FTP）；
        /// runners 空/缺失 → 400（InitProtocol 不变量：首项必须为内置默认运行器，空表会破坏
        /// GetRunner/协议页签）；反序列化异常（$type 未知等）→ 400。全量预校验通过才应用（零写入）。
        /// 应用 = 原位替换既有 ProtocolSettings 实例的 SelectedRunnerName + Runners（保持对象身份），
        /// 重放 Load 后处理（OwnerProtocolName 回填 + ExternalRunner.MarcoNames 宏补全）后 Save()。
        /// </summary>
        public static RunnerApplyResult ApplyRunners(ProtocolConfigurationService pcs, JsonElement? protocols)
        {
            if (protocols == null || protocols.Value.ValueKind != JsonValueKind.Object)
                return RunnerApplyResult.BadRequest("body must contain a 'protocols' object");

            var validKeys = pcs.ProtocolConfigs.Keys.ToList();
            var parsed = new List<(string key, ProtocolSettings settings)>();
            var errors = new List<string>();
            foreach (var prop in protocols.Value.EnumerateObject())
            {
                var key = validKeys.FirstOrDefault(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase));
                if (key == null)
                {
                    errors.Add($"protocols.{prop.Name}: unknown protocol, expected one of: {string.Join(", ", validKeys.OrderBy(x => x))}");
                    continue;
                }
                ProtocolSettings? settings;
                try
                {
                    // Newtonsoft 大小写不敏感匹配：selectedRunnerName → SelectedRunnerName，runners → Runners；
                    // runners 元素经 JsonKnownTypesConverter<Runner> 的 $type 判别还原为具体运行器子类
                    settings = JsonConvert.DeserializeObject<ProtocolSettings>(prop.Value.GetRawText());
                }
                catch (Exception e)
                {
                    errors.Add($"protocols.{prop.Name}: failed to deserialize ({e.Message})");
                    continue;
                }
                if (settings == null || settings.Runners == null || settings.Runners.Count == 0)
                {
                    errors.Add($"protocols.{prop.Name}: 'runners' must be a non-empty array");
                    continue;
                }
                parsed.Add((key, settings));
            }
            if (errors.Count > 0)
                return RunnerApplyResult.BadRequest(errors);
            if (parsed.Count == 0)
                return RunnerApplyResult.BadRequest("body.protocols must contain at least one protocol entry");
            foreach (var (key, settings) in parsed)
            {
                var existing = pcs.ProtocolConfigs[key];
                existing.SelectedRunnerName = settings.SelectedRunnerName ?? string.Empty;
                existing.Runners = settings.Runners;
                // Load 同款后处理（ProtocolConfigurationService.Load）：Owner 协议名回填 + 宏补全。
                // OwnerProtocolName 仅在缺失时回填：LoadConfig 落盘/装载会归一大写（TELNET），而字典键
                // 是原始大小写（Telnet）——非空即保留，保证 GET→PUT→GET 逐字节稳定
                foreach (var runner in existing.Runners)
                {
                    if (string.IsNullOrEmpty(runner.OwnerProtocolName))
                        runner.OwnerProtocolName = key;
                    if (runner is ExternalRunner er)
                        er.MarcoNames = existing.MarcoNames;
                }
            }
            pcs.Save();
            return RunnerApplyResult.Ok();
        }

        // ------------------------------------------------------------------
        // 辅助
        // ------------------------------------------------------------------

        private static string? NormalizeType(string? type)
        {
            return (type?.Trim().ToLowerInvariant()) switch
            {
                "sqlite" => "sqlite",
                "mysql" => "mysql",
                "pgsql" => "pgsql",
                "postgresql" => "pgsql", // WPF CmdAdd 的类型串（DataSourceViewModel switch 分支名）
                _ => null,
            };
        }

        /// <summary>重名检查：Local + ConfigurationService.AdditionalDataSource + 运行时字典，CurrentCultureIgnoreCase（WPF 编辑器同款）。</summary>
        private static DataSourceBase? FindNameConflict(string name)
        {
            var dss = IoC.Get<DataSourceService>();
            if (dss.LocalDataSource != null
                && string.Equals(dss.LocalDataSource.DataSourceName.Trim(), name.Trim(), StringComparison.CurrentCultureIgnoreCase))
                return dss.LocalDataSource;
            return IoC.Get<ConfigurationService>().AdditionalDataSource
                .Concat(dss.AdditionalSources.Values)
                .FirstOrDefault(x => string.Equals(x.DataSourceName.Trim(), name.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>按名查找附加数据源（ConfigurationService.AdditionalDataSource 与运行时字典同实例）。</summary>
        private static DataSourceBase? FindAdditionalSource(string name)
        {
            return IoC.Get<ConfigurationService>().AdditionalDataSource
                .FirstOrDefault(x => string.Equals(x.DataSourceName, name, StringComparison.Ordinal));
        }

        /// <summary>mysql/pgsql 更新的字段级应用：非 null 才写；password 空=保持（条件赋值）。</summary>
        private static void ApplyServerConfig(DataSourceConfigInput cfg, List<string> errors,
            Action<string> setHost, Action<int> setPort, Action<string> setDatabase,
            Action<string> setUser, Action<string> setPassword)
        {
            var host = cfg.Host?.Trim();
            if (host != null)
            {
                if (host.Length == 0) errors.Add("config.host: can not be empty");
                else setHost(host);
            }
            if (cfg.Port != null)
            {
                if (cfg.Port.Value < 1 || cfg.Port.Value > 65535) errors.Add("config.port: must be 1 - 65535");
                else setPort(cfg.Port.Value);
            }
            var database = cfg.DatabaseName?.Trim();
            if (database != null)
            {
                if (database.Length == 0) errors.Add("config.databaseName: can not be empty");
                else setDatabase(database);
            }
            var user = cfg.UserName?.Trim();
            if (user != null)
            {
                if (user.Length == 0) errors.Add("config.userName: can not be empty");
                else setUser(user);
            }
            // 空=保持：Mysql/Pgsql Password setter 收 "" 会清空 EncryptPassword，必须跳过
            if (!string.IsNullOrEmpty(cfg.Password))
                setPassword(cfg.Password);
        }

        /// <summary>mysql/pgsql 测试连接：请求 config 覆盖 + 缺省回退已存值（密码空 = 沿用已存密码）。</summary>
        private static bool TestServerConnection(DataSourceBase source, DataSourceConfigInput? input,
            Func<string, int, string, string, string, bool> test)
        {
            string host;
            int port;
            string database;
            string user;
            string password;
            switch (source)
            {
                case MysqlSource m:
                    host = input?.Host?.Trim() is { Length: > 0 } h1 ? h1 : m.Host;
                    port = input?.Port ?? m.Port;
                    database = input?.DatabaseName?.Trim() is { Length: > 0 } d1 ? d1 : m.DatabaseName;
                    user = input?.UserName?.Trim() is { Length: > 0 } u1 ? u1 : m.UserName;
                    password = !string.IsNullOrEmpty(input?.Password) ? input!.Password! : m.Password;
                    break;
                case PgsqlSource p:
                    host = input?.Host?.Trim() is { Length: > 0 } h2 ? h2 : p.Host;
                    port = input?.Port ?? p.Port;
                    database = input?.DatabaseName?.Trim() is { Length: > 0 } d2 ? d2 : p.DatabaseName;
                    user = input?.UserName?.Trim() is { Length: > 0 } u2 ? u2 : p.UserName;
                    password = !string.IsNullOrEmpty(input?.Password) ? input!.Password! : p.Password;
                    break;
                default:
                    return false;
            }
            try
            {
                return test(host, port, database, user, password);
            }
            catch (Exception)
            {
                return false; // TestConnection 内部 OpenNewConnection 异常按失败语义处理
            }
        }

        /// <summary>
        /// 草稿测试（name 无已存实例时的分支）：sqlite = 临时实例 SelfCheck（WPF sqlite 弹窗
        /// 保存前的 ValidateDbStatusAndShowMessageBox 同款自检）；mysql/pgsql = 必填校验后静态
        /// TestConnection（WPF CmdTestConnection 对表单草稿构造临时配置的同款）。
        /// 密码缺省回退：name 能寻址到已存源（改名草稿场景）时沿用其密码。
        /// </summary>
        private static DataSourceTestResult TestDraft(string type, DataSourceConfigInput cfg,
            string? name, DataSourceService dss)
        {
            switch (type)
            {
                case "sqlite":
                {
                    var path = cfg.Path?.Trim() ?? string.Empty;
                    if (path.Length == 0)
                        return DataSourceTestResult.BadRequest("config.path: can not be empty");
                    var source = new SqliteSource(string.IsNullOrWhiteSpace(name) ? "draft" : name!.Trim()) { Path = path };
                    var ret = source.Database_SelfCheck();
                    source.Database_CloseConnection();
                    return DataSourceTestResult.Ok(ret.Status == EnumDatabaseStatus.OK,
                        MapStatus(ret.Status), ret.Status == EnumDatabaseStatus.OK ? string.Empty : ret.GetErrorMessage);
                }
                case "mysql":
                case "pgsql":
                {
                    var host = cfg.Host?.Trim() ?? string.Empty;
                    var databaseName = cfg.DatabaseName?.Trim() ?? string.Empty;
                    var userName = cfg.UserName?.Trim() ?? string.Empty;
                    var port = cfg.Port ?? (type == "mysql" ? 3306 : 5432);
                    var errors = new List<string>();
                    if (host.Length == 0) errors.Add("config.host: can not be empty");
                    if (port < 1 || port > 65535) errors.Add("config.port: must be 1 - 65535");
                    if (databaseName.Length == 0) errors.Add("config.databaseName: can not be empty");
                    if (userName.Length == 0) errors.Add("config.userName: can not be empty");
                    var password = cfg.Password ?? string.Empty;
                    if (password.Length == 0 && !string.IsNullOrWhiteSpace(name))
                    {
                        // 改名草稿：旧名仍能寻址到已存源，沿用其密码（编辑弹窗"密码留空=保持"平价；
                        // Password 属性在 MysqlSource/PgsqlSource 上，getter 返回解密明文）
                        switch (dss.GetDataSource(name))
                        {
                            case MysqlSource em:
                                password = em.Password;
                                break;
                            case PgsqlSource ep:
                                password = ep.Password;
                                break;
                        }
                    }
                    if (password.Length == 0) errors.Add("config.password: can not be empty");
                    if (errors.Count > 0)
                        return DataSourceTestResult.BadRequest(string.Join("; ", errors));
                    var ok = type == "mysql"
                        ? MysqlSource.TestConnection(host, port, databaseName, userName, password)
                        : PgsqlSource.TestConnection(host, port, databaseName, userName, password);
                    return DataSourceTestResult.FromBool(ok,
                        ok ? WebUiConstants.StatusConnected : WebUiConstants.StatusDisconnected);
                }
                default:
                    return DataSourceTestResult.BadRequest($"type: '{type}' is not supported");
            }
        }

        private static string MapStatus(EnumDatabaseStatus status)
        {
            return status switch
            {
                EnumDatabaseStatus.OK => WebUiConstants.StatusConnected,
                EnumDatabaseStatus.LostConnection => WebUiConstants.StatusReconnecting,
                _ => WebUiConstants.StatusDisconnected,
            };
        }
    }

    /// <summary>数据源写操作（POST/PUT/DELETE）结果分类，由端点映射 HTTP 状态码。</summary>
    public enum DataSourceMutationStatus
    {
        Ok,
        BadRequest, // 校验失败（零写入）
        NotFound,   // 数据源不存在
        Conflict,   // 重名（POST）/ 有服务器未确认（DELETE）
    }

    public sealed class DataSourceMutationResult
    {
        public DataSourceMutationStatus Status { get; private init; }
        public List<string> Errors { get; private init; } = new();
        public DataSourceDto? Dto { get; private init; }
        /// <summary>连接尝试的错误详情（Ok 时也可能非空：保存成功但连接失败，WPF 同款语义）。</summary>
        public string ConnectError { get; private init; } = string.Empty;
        /// <summary>DELETE 409 分支携带的剩余服务器数。</summary>
        public int ServerCount { get; private init; }

        public static DataSourceMutationResult Ok(DataSourceDto? dto, string connectError = "")
            => new() { Status = DataSourceMutationStatus.Ok, Dto = dto, ConnectError = connectError };
        public static DataSourceMutationResult BadRequest(params string[] errors)
            => new() { Status = DataSourceMutationStatus.BadRequest, Errors = errors.ToList() };
        public static DataSourceMutationResult BadRequest(List<string> errors)
            => new() { Status = DataSourceMutationStatus.BadRequest, Errors = errors };
        public static DataSourceMutationResult NotFound() => new() { Status = DataSourceMutationStatus.NotFound };
        public static DataSourceMutationResult Conflict(string error, int serverCount)
            => new() { Status = DataSourceMutationStatus.Conflict, Errors = new List<string> { error }, ServerCount = serverCount };
    }

    /// <summary>POST /api/datasources/{name}/test 结果。</summary>
    public sealed class DataSourceTestResult
    {
        public DataSourceMutationStatus Status { get; private init; }
        public bool IsOk { get; private init; }
        public string StatusText { get; private init; } = string.Empty;
        public string Detail { get; private init; } = string.Empty;

        public static DataSourceTestResult Ok(bool ok, string status, string detail)
            => new() { Status = DataSourceMutationStatus.Ok, IsOk = ok, StatusText = status, Detail = detail };
        public static DataSourceTestResult FromBool(bool ok, string status)
            => Ok(ok, status, ok ? string.Empty : "connection failed (check host/port/credentials/firewall)");
        public static DataSourceTestResult NotFound() => new() { Status = DataSourceMutationStatus.NotFound };
        public static DataSourceTestResult BadRequest(string error)
            => new() { Status = DataSourceMutationStatus.BadRequest, Detail = error };
    }

    /// <summary>PUT /api/settings/runners 结果。</summary>
    public sealed class RunnerApplyResult
    {
        public bool IsOk { get; private init; }
        public List<string> Errors { get; private init; } = new();

        public static RunnerApplyResult Ok() => new() { IsOk = true };
        public static RunnerApplyResult BadRequest(params string[] errors) => new() { Errors = errors.ToList() };
        public static RunnerApplyResult BadRequest(List<string> errors) => new() { Errors = errors };
    }
}
