using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/datasources CRUD+test、/api/settings/runners 读写集成测试。
    /// 关键约定：
    /// - 持久化双集合：POST/PUT/DELETE 后 ConfigurationService.AdditionalDataSource 必须反映变更
    ///   （WPF 正确模式：先改 AdditionalDataSource → Save() → AddOrUpdateDataSource/RemoveDataSource）；
    /// - 新建失败语义 = WPF CmdAdd：连接失败不回滚（201 + status != connected），可先调 /test 验证；
    /// - 密码：POST 明文必填（WPF 弹窗同款）；PUT 空/缺失 = 保持原密码（setter 收 "" 会清空）；
    /// - 删除守卫：serverCount>0 且未带 keepServers=true → 409 {serverCount}（WPF 无此检查直接删，
    ///   keepServers=true = 镜像 WPF 原样删除，服务器留在库文件）；
    /// - runners 整体往返 ProtocolSettings（含 SelectedRunnerName），runners 数组 PascalCase + $type
    ///   直通——内置运行器（PuttyRunner/InternalDefaultRunner）经 [JsonConstructor] 完整还原。
    /// 夹具只有 Local sqlite：mysql/pgsql 仅测校验分支与失败结果形状（127.0.0.1:拒绝端口 = 快速失败），
    ///   不测真实连接成功路径。
    /// </summary>
    [TestClass]
    public class DataSourceEndpointsTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        private static ConfigurationService Cs => _1RM.IoC.Get<ConfigurationService>();
        private static DataSourceService Dss => _1RM.IoC.Get<DataSourceService>();

        private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

        private static async Task<(HttpStatusCode Code, string Body)> PostAsync(string uri, string json)
        {
            using var resp = await _client.PostAsync(uri, JsonBody(json));
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }

        private static async Task<(HttpStatusCode Code, string Body)> PutAsync(string uri, string json)
        {
            using var resp = await _client.PutAsync(uri, JsonBody(json));
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }

        private static async Task<JsonElement> GetJsonAsync(string uri)
        {
            var resp = await _client.GetAsync(uri);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, uri);
            return JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();
        }

        private static string TempDbPath()
        {
            var path = Path.Combine(Path.GetTempPath(), "ds-web-test-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
            File.WriteAllText(path, string.Empty); // SqliteSource.Path setter 读 FileInfo，文件须存在（夹具同款）
            return path;
        }

        private static void DeleteTempFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { /* 被占用则留给临时目录清理 */ }
        }

        /// <summary>删除测试中创建的附加数据源（测试自身的清理路径，不依赖端点行为）。</summary>
        private static async Task CleanupDataSourceAsync(string name)
        {
            var src = Cs.AdditionalDataSource.FirstOrDefault(x => x.DataSourceName == name);
            if (src != null)
            {
                Cs.AdditionalDataSource.Remove(src);
                Cs.Save();
                Dss.RemoveDataSource(name);
            }
            await Task.CompletedTask;
        }

        // ------------------------------------------------------------------
        // GET /api/datasources（config 扩展）
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task GetDatasources_IncludesConfigWithoutPassword()
        {
            var root = await GetJsonAsync("/api/datasources");
            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);

            var local = root.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Local");
            var cfg = local.GetProperty("config");
            Assert.AreEqual(Dss.LocalDataSource!.Path, cfg.GetProperty("path").GetString(),
                "Local 的 config.path 应为夹具临时库路径（Local 的 config 也返回——前端只读展示）");
            Assert.IsFalse(cfg.EnumerateObject().Any(p => p.Name.ToLowerInvariant().Contains("password")),
                "config 不得包含密码字段");

            // mysql/pgsql 形状在创建用例中验证；此处验证所有条目都带 config 键
            Assert.IsTrue(root.EnumerateArray().All(x => x.TryGetProperty("config", out _)));
        }

        // ------------------------------------------------------------------
        // POST /api/datasources
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task PostSqlite_CreatesConnectedSourceAndPersists()
        {
            var path = TempDbPath();
            var name = Path.GetFileNameWithoutExtension(path);
            try
            {
                var (code, body) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"sqlite\",\"config\":{{\"path\":{JsonSerializer.Serialize(path)}}}}}");
                Assert.AreEqual(HttpStatusCode.Created, code, body);
                using var doc = JsonDocument.Parse(body);
                var ds = doc.RootElement.GetProperty("dataSource");
                Assert.AreEqual(name, ds.GetProperty("name").GetString(), "sqlite 名缺省 = 路径文件名（不含扩展名）");
                Assert.AreEqual("sqlite", ds.GetProperty("type").GetString());
                Assert.AreEqual("connected", ds.GetProperty("status").GetString(), "空 sqlite 文件自检建表后应为连接态");
                Assert.AreEqual(path, ds.GetProperty("config").GetProperty("path").GetString());
                Assert.IsFalse(ds.GetProperty("config").EnumerateObject().Any(p => p.Name.Contains("password")));

                // 双集合持久化：ConfigurationService.AdditionalDataSource + 运行时字典
                Assert.IsTrue(Cs.AdditionalDataSource.Any(x => x.DataSourceName == name),
                    "POST 后 ConfigurationService.AdditionalDataSource 必须包含新数据源（落盘依据）");
                Assert.IsTrue(Dss.AdditionalSources.ContainsKey(name), "运行时字典亦应包含");

                // /test 端点：sqlite 走 Database_SelfCheck
                var (tCode, tBody) = await PostAsync($"/api/datasources/{Uri.EscapeDataString(name)}/test", "{}");
                Assert.AreEqual(HttpStatusCode.OK, tCode, tBody);
                using var tDoc = JsonDocument.Parse(tBody);
                Assert.IsTrue(tDoc.RootElement.GetProperty("ok").GetBoolean());

                var list = await GetJsonAsync("/api/datasources");
                var item = list.EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
                Assert.AreEqual(path, item.GetProperty("config").GetProperty("path").GetString());
            }
            finally
            {
                await CleanupDataSourceAsync(name);
                DeleteTempFile(path);
            }
        }

        [TestMethod]
        public async Task PostMysql_BadHost_SavedButNotConnected()
        {
            var name = "ds-mysql-bad-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                var (code, body) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"mysql\",\"name\":\"{name}\",\"config\":{{\"host\":\"127.0.0.1\",\"port\":1,\"databaseName\":\"1Remote\",\"userName\":\"root\",\"password\":\"pw\"}}}}");
                Assert.AreEqual(HttpStatusCode.Created, code, body);
                using var doc = JsonDocument.Parse(body);
                var ds = doc.RootElement.GetProperty("dataSource");
                Assert.AreEqual("mysql", ds.GetProperty("type").GetString());
                Assert.AreNotEqual("connected", ds.GetProperty("status").GetString(),
                    "连接失败也保存（WPF CmdAdd 同款：不回滚），status 反映失败");
                Assert.IsFalse(string.IsNullOrEmpty(doc.RootElement.GetProperty("connectError").GetString()),
                    "连接错误详情应在响应中带回（WPF 弹错误框的 web 等价物）");

                var src = Cs.AdditionalDataSource.Single(x => x.DataSourceName == name);
                Assert.IsInstanceOfType(src, typeof(MysqlSource));

                var list = await GetJsonAsync("/api/datasources");
                var item = list.EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
                var cfg = item.GetProperty("config");
                Assert.AreEqual("127.0.0.1", cfg.GetProperty("host").GetString());
                Assert.AreEqual(1, cfg.GetProperty("port").GetInt32());
                Assert.IsFalse(cfg.EnumerateObject().Any(p => p.Name.Contains("password")), "列表 config 无密码");
            }
            finally
            {
                await CleanupDataSourceAsync(name);
            }
        }

        [TestMethod]
        public async Task PostDuplicateName_Returns409()
        {
            var path = TempDbPath();
            var name = Path.GetFileNameWithoutExtension(path);
            try
            {
                var (c1, b1) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"sqlite\",\"config\":{{\"path\":{JsonSerializer.Serialize(path)}}}}}");
                Assert.AreEqual(HttpStatusCode.Created, c1, b1);

                // 同名（大小写不敏感，WPF CurrentCultureIgnoreCase 判重同款）→ 409
                var (c2, b2) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"mysql\",\"name\":\"{name.ToUpperInvariant()}\",\"config\":{{\"host\":\"h\",\"databaseName\":\"d\",\"userName\":\"u\",\"password\":\"p\"}}}}");
                Assert.AreEqual(HttpStatusCode.Conflict, c2, b2);

                // 与 Local 重名 → 409
                var (c3, _) = await PostAsync("/api/datasources",
                    "{\"type\":\"sqlite\",\"name\":\"local\",\"config\":{\"path\":\"C:\\\\x.db\"}}");
                Assert.AreEqual(HttpStatusCode.Conflict, c3);
            }
            finally
            {
                await CleanupDataSourceAsync(name);
                DeleteTempFile(path);
            }
        }

        [TestMethod]
        public async Task PostInvalidBodies_Return400WithZeroWrite()
        {
            var before = Cs.AdditionalDataSource.Count;
            var cases = new[]
            {
                "{\"type\":\"oracle\",\"config\":{}}",
                "{\"type\":\"sqlite\",\"config\":{\"path\":\"\"}}",                      // path 空
                "{\"type\":\"sqlite\",\"name\":\"  \"}",                                  // 名不可得（无 path 可推导）
                "{\"type\":\"mysql\",\"name\":\"m1\",\"config\":{\"host\":\"\",\"port\":3306,\"databaseName\":\"d\",\"userName\":\"u\",\"password\":\"p\"}}",
                "{\"type\":\"mysql\",\"name\":\"m2\",\"config\":{\"host\":\"h\",\"port\":0,\"databaseName\":\"d\",\"userName\":\"u\",\"password\":\"p\"}}",
                "{\"type\":\"mysql\",\"name\":\"m3\",\"config\":{\"host\":\"h\",\"databaseName\":\"d\",\"userName\":\"u\",\"password\":\"\"}}", // WPF 新建必填
                "{\"type\":\"pgsql\",\"name\":\"p1\",\"config\":{\"host\":\"h\",\"databaseName\":\"\",\"userName\":\"u\",\"password\":\"p\"}}",
            };
            foreach (var json in cases)
            {
                var (code, body) = await PostAsync("/api/datasources", json);
                Assert.AreEqual(HttpStatusCode.BadRequest, code, json + " -> " + body);
            }
            Assert.AreEqual(before, Cs.AdditionalDataSource.Count, "全部 400：零写入");
        }

        [TestMethod]
        public async Task PostAcceptsPostgresqlAlias()
        {
            var name = "ds-pg-alias-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                // WPF CmdAdd 的类型串是 "postgresql"（DataSourceViewModel switch）——web 同义归一为 pgsql；
                // port 缺省 = PgsqlSource 默认 5432（未提供时走类默认）
                var (code, body) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"postgresql\",\"name\":\"{name}\",\"config\":{{\"host\":\"127.0.0.1\",\"databaseName\":\"d\",\"userName\":\"u\",\"password\":\"p\"}}}}");
                Assert.AreEqual(HttpStatusCode.Created, code, body);
                using var doc = JsonDocument.Parse(body);
                Assert.AreEqual("pgsql", doc.RootElement.GetProperty("dataSource").GetProperty("type").GetString());
                var cfg = doc.RootElement.GetProperty("dataSource").GetProperty("config");
                Assert.AreEqual(5432, cfg.GetProperty("port").GetInt32(), "port 缺省 = 5432");
                Assert.IsFalse(cfg.EnumerateObject().Any(p => p.Name.Contains("password")), "config 无密码");
            }
            finally
            {
                await CleanupDataSourceAsync(name);
            }
        }

        // ------------------------------------------------------------------
        // PUT /api/datasources/{name}
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task PutLocal_Returns400_AndPutUnknown_Returns404()
        {
            var (c1, b1) = await PutAsync("/api/datasources/Local", "{\"config\":{\"path\":\"C:\\\\other.db\"}}");
            Assert.AreEqual(HttpStatusCode.BadRequest, c1, b1);
            Assert.AreEqual(Dss.LocalDataSource!.Path, Cs.LocalDataSource.Path, "Local 路径不得被改动");

            var (c2, _) = await PutAsync("/api/datasources/no-such-ds", "{\"config\":{\"host\":\"h\"}}");
            Assert.AreEqual(HttpStatusCode.NotFound, c2);
        }

        [TestMethod]
        public async Task PutMysql_EmptyPasswordKeepsExisting_OtherFieldsUpdate()
        {
            var name = "ds-mysql-put-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                var (c0, b0) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"mysql\",\"name\":\"{name}\",\"config\":{{\"host\":\"127.0.0.1\",\"port\":1,\"databaseName\":\"db1\",\"userName\":\"u1\",\"password\":\"pw-keep\"}}}}");
                Assert.AreEqual(HttpStatusCode.Created, c0, b0);

                // password 空串 = 保持（MysqlSource.Password setter 收 "" 会清空 EncryptPassword）
                var (code, body) = await PutAsync($"/api/datasources/{Uri.EscapeDataString(name)}",
                    "{\"config\":{\"host\":\"127.0.0.2\",\"password\":\"\",\"databaseName\":\"db2\"}}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);

                var src = (MysqlSource)Cs.AdditionalDataSource.Single(x => x.DataSourceName == name);
                Assert.AreEqual("127.0.0.2", src.Host);
                Assert.AreEqual("db2", src.DatabaseName);
                Assert.AreEqual(1, src.Port, "未提供的 port = 保持");
                Assert.AreEqual("u1", src.UserName, "未提供的 userName = 保持");
                Assert.AreEqual("pw-keep", src.Password, "空密码必须保持原密码（条件赋值）");

                // null 密码同样保持；非空密码则更新
                var (c2, _) = await PutAsync($"/api/datasources/{Uri.EscapeDataString(name)}",
                    "{\"config\":{\"password\":\"pw-new\"}}");
                Assert.AreEqual(HttpStatusCode.OK, c2);
                Assert.AreEqual("pw-new", src.Password);

                var list = await GetJsonAsync("/api/datasources");
                var cfg = list.EnumerateArray().Single(x => x.GetProperty("name").GetString() == name).GetProperty("config");
                Assert.AreEqual("127.0.0.2", cfg.GetProperty("host").GetString());
            }
            finally
            {
                await CleanupDataSourceAsync(name);
            }
        }

        [TestMethod]
        public async Task PutSqlite_UpdatesPath()
        {
            var path1 = TempDbPath();
            var path2 = TempDbPath();
            var name = Path.GetFileNameWithoutExtension(path1);
            try
            {
                var (c0, _) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"sqlite\",\"config\":{{\"path\":{JsonSerializer.Serialize(path1)}}}}}");
                Assert.AreEqual(HttpStatusCode.Created, c0);

                var (code, body) = await PutAsync($"/api/datasources/{Uri.EscapeDataString(name)}",
                    $"{{\"config\":{{\"path\":{JsonSerializer.Serialize(path2)}}}}}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                using var doc = JsonDocument.Parse(body);
                Assert.AreEqual(path2, doc.RootElement.GetProperty("dataSource").GetProperty("config").GetProperty("path").GetString());

                var src = (SqliteSource)Cs.AdditionalDataSource.Single(x => x.DataSourceName == name);
                Assert.AreEqual(path2, src.Path);
            }
            finally
            {
                await CleanupDataSourceAsync(name);
                DeleteTempFile(path1);
                DeleteTempFile(path2);
            }
        }

        // ------------------------------------------------------------------
        // DELETE /api/datasources/{name}
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task DeleteLocal_Returns400_AndDeleteUnknown_Returns404()
        {
            var resp = await _client.DeleteAsync("/api/datasources/Local");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);

            var resp2 = await _client.DeleteAsync("/api/datasources/no-such-ds");
            Assert.AreEqual(HttpStatusCode.NotFound, resp2.StatusCode);
        }

        [TestMethod]
        public async Task DeleteWithServers_Returns409_ThenKeepServersRemoves()
        {
            var path = TempDbPath();
            var name = Path.GetFileNameWithoutExtension(path);
            try
            {
                var (c0, b0) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"sqlite\",\"config\":{{\"path\":{JsonSerializer.Serialize(path)}}}}}");
                Assert.AreEqual(HttpStatusCode.Created, c0, b0);

                // 种一台服务器到该附加数据源（GlobalData.AddServer 直写）
                var gd = _1RM.IoC.Get<GlobalData>();
                var ds = Dss.AdditionalSources[name];
                lock (gd)
                {
                    var ret = gd.AddServer(new RDP
                    {
                        Id = "srv-ds-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        DisplayName = "srv-ds",
                        Address = "9.9.9.9",
                    }, ds);
                    Assert.IsTrue(ret.IsSuccess, ret.ErrorInfo);
                }

                // 有服务器且未确认 → 409 {serverCount}
                var resp1 = await _client.DeleteAsync($"/api/datasources/{Uri.EscapeDataString(name)}");
                Assert.AreEqual(HttpStatusCode.Conflict, resp1.StatusCode);
                var body1 = await resp1.Content.ReadAsStringAsync();
                using var doc1 = JsonDocument.Parse(body1);
                Assert.AreEqual(1, doc1.RootElement.GetProperty("serverCount").GetInt32());
                Assert.IsTrue(Cs.AdditionalDataSource.Any(x => x.DataSourceName == name), "409 分支零删除");

                // keepServers=true = 镜像 WPF：直接移除（服务器留在库文件，不迁移不删除）
                var resp2 = await _client.DeleteAsync($"/api/datasources/{Uri.EscapeDataString(name)}?keepServers=true");
                Assert.AreEqual(HttpStatusCode.NoContent, resp2.StatusCode);
                Assert.IsFalse(Cs.AdditionalDataSource.Any(x => x.DataSourceName == name),
                    "删除后 AdditionalDataSource 必须移除");
                Assert.IsFalse(Dss.AdditionalSources.ContainsKey(name), "运行时字典亦移除");
            }
            finally
            {
                await CleanupDataSourceAsync(name);
                DeleteTempFile(path);
            }
        }

        [TestMethod]
        public async Task DeleteEmptySource_NoGuardNeeded()
        {
            var path = TempDbPath();
            var name = Path.GetFileNameWithoutExtension(path);
            try
            {
                var (c0, _) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"sqlite\",\"config\":{{\"path\":{JsonSerializer.Serialize(path)}}}}}");
                Assert.AreEqual(HttpStatusCode.Created, c0);

                // serverCount == 0：无需 keepServers 直接删
                var resp = await _client.DeleteAsync($"/api/datasources/{Uri.EscapeDataString(name)}");
                Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
                Assert.IsFalse(Cs.AdditionalDataSource.Any(x => x.DataSourceName == name));
            }
            finally
            {
                await CleanupDataSourceAsync(name);
                DeleteTempFile(path);
            }
        }

        // ------------------------------------------------------------------
        // POST /api/datasources/{name}/test
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task TestSqliteLocal_ReturnsOk()
        {
            var (code, body) = await PostAsync("/api/datasources/Local/test", "{}");
            Assert.AreEqual(HttpStatusCode.OK, code, body);
            using var doc = JsonDocument.Parse(body);
            Assert.IsTrue(doc.RootElement.GetProperty("ok").GetBoolean(), "夹具 Local sqlite 应自检通过");
            Assert.AreEqual("connected", doc.RootElement.GetProperty("status").GetString());
        }

        [TestMethod]
        public async Task TestMysql_FailsFastWithFalse_AndUnknown_Returns404()
        {
            var name = "ds-mysql-test-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                var (c0, _) = await PostAsync("/api/datasources",
                    $"{{\"type\":\"mysql\",\"name\":\"{name}\",\"config\":{{\"host\":\"127.0.0.1\",\"port\":1,\"databaseName\":\"d\",\"userName\":\"u\",\"password\":\"p\"}}}}");
                Assert.AreEqual(HttpStatusCode.Created, c0);

                // 已存源直测（拒绝端口 = 快速失败，只验结果形状）
                var (code, body) = await PostAsync($"/api/datasources/{Uri.EscapeDataString(name)}/test", "{}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                using var doc = JsonDocument.Parse(body);
                Assert.IsFalse(doc.RootElement.GetProperty("ok").GetBoolean());
                Assert.IsFalse(string.IsNullOrEmpty(doc.RootElement.GetProperty("detail").GetString()));

                // 带 config 覆盖测试（保存前验证向导路径）
                var (code2, body2) = await PostAsync($"/api/datasources/{Uri.EscapeDataString(name)}/test",
                    "{\"config\":{\"host\":\"127.0.0.1\",\"port\":2}}");
                Assert.AreEqual(HttpStatusCode.OK, code2, body2);
                using var doc2 = JsonDocument.Parse(body2);
                Assert.IsFalse(doc2.RootElement.GetProperty("ok").GetBoolean());
            }
            finally
            {
                await CleanupDataSourceAsync(name);
            }

            var (c404, _) = await PostAsync("/api/datasources/no-such-ds/test", "{}");
            Assert.AreEqual(HttpStatusCode.NotFound, c404);
        }

        // ------------------------------------------------------------------
        // GET/PUT /api/settings/runners
        // ------------------------------------------------------------------

        private static async Task<JsonElement> GetRunnersAsync()
            => await GetJsonAsync("/api/settings/runners");

        [TestMethod]
        public async Task GetRunners_ReturnsSixProtocolsWithPassthroughRunners()
        {
            var root = await GetRunnersAsync();
            var protocols = root.GetProperty("protocols");
            var keys = protocols.EnumerateObject().Select(p => p.Name).ToHashSet();
            CollectionAssert.AreEquivalent(
                new HashSet<string> { "SSH", "Telnet", "Serial", "VNC", "SFTP", "FTP" }.ToList(),
                keys.ToList(), "6 协议（RDP 不在 ProtocolConfigs——WPF 同款注释掉的内置项）");

            foreach (var prop in protocols.EnumerateObject())
            {
                Assert.AreEqual(JsonValueKind.String, prop.Value.GetProperty("selectedRunnerName").ValueKind,
                    prop.Name + ".selectedRunnerName");
                var runners = prop.Value.GetProperty("runners");
                Assert.AreEqual(JsonValueKind.Array, runners.ValueKind);
                Assert.IsTrue(runners.GetArrayLength() >= 1, prop.Name + " 至少 1 个运行器");
                foreach (var runner in runners.EnumerateArray())
                {
                    Assert.IsTrue(runner.TryGetProperty("$type", out _), "runner 直通域带 $type 判别（Newtonsoft JsonKnownTypes）");
                    Assert.IsFalse(string.IsNullOrEmpty(runner.GetProperty("Name").GetString()), "PascalCase 直通（Name）");
                }
            }

            // 内置运行器完整保留：SSH 首项 = PuttyRunner（InternalExeRunner 子类，$type 判别还原）
            var sshFirst = protocols.GetProperty("SSH").GetProperty("runners")[0];
            Assert.AreEqual("PuttyRunner", sshFirst.GetProperty("$type").GetString());
            Assert.AreEqual("Built-in PuTTY", sshFirst.GetProperty("Name").GetString());
        }

        [TestMethod]
        public async Task PutRunners_RoundTrip_PreservesSelectedRunnerAndInternalRunner()
        {
            var pcs = _1RM.IoC.Get<ProtocolConfigurationService>();
            var original = await GetRunnersAsync();
            var ssh = pcs.ProtocolConfigs["SSH"];
            var origSelected = ssh.SelectedRunnerName;
            var origCount = ssh.Runners.Count;
            try
            {
                // 原样回写：响应应与回写前等价（往返稳定），内存不漂移
                var (c1, b1) = await PutAsync("/api/settings/runners",
                    "{\"protocols\":" + original.GetProperty("protocols").GetRawText() + "}");
                Assert.AreEqual(HttpStatusCode.OK, c1, b1);
                var after = await GetRunnersAsync();
                Assert.AreEqual(original.GetRawText(), after.GetRawText(), "原样回写后 GET 应逐字节稳定");
                Assert.AreEqual(origSelected, ssh.SelectedRunnerName);
                Assert.AreEqual(origCount, ssh.Runners.Count);
                Assert.IsInstanceOfType(ssh.Runners[0], typeof(InternalDefaultRunner),
                    "整表往返后内置默认运行器应还原为具体子类（无需回退到仅合并外部运行器的策略）");

                // 变更 SelectedRunnerName：选一个不同的运行器名（runners 原样直通）
                var alt = ssh.Runners.Select(r => r.Name).FirstOrDefault(n => n != origSelected)
                          ?? ssh.Runners[0].Name;
                Assert.IsNotNull(alt);

                // 手工构造只改 selectedRunnerName 的 SSH 配置（runners 原样直通）
                var runnersRaw = original.GetProperty("protocols").GetProperty("SSH").GetProperty("runners").GetRawText();
                var payload = "{\"protocols\":{\"SSH\":{\"selectedRunnerName\":" + JsonSerializer.Serialize(alt)
                              + ",\"runners\":" + runnersRaw + "}}}";
                var (c2, b2) = await PutAsync("/api/settings/runners", payload);
                Assert.AreEqual(HttpStatusCode.OK, c2, b2);
                Assert.AreEqual(alt, ssh.SelectedRunnerName, "内存配置应更新");
                Assert.AreEqual(origCount, ssh.Runners.Count, "运行器数量保持");
                using var doc2 = JsonDocument.Parse(b2);
                Assert.AreEqual(alt, doc2.RootElement.GetProperty("protocols").GetProperty("SSH").GetProperty("selectedRunnerName").GetString());
            }
            finally
            {
                // 还原
                ssh.SelectedRunnerName = origSelected;
                pcs.Save();
            }
        }

        [TestMethod]
        public async Task PutRunners_InvalidBodies_Return400WithZeroWrite()
        {
            var pcs = _1RM.IoC.Get<ProtocolConfigurationService>();
            var snap = pcs.ProtocolConfigs.ToDictionary(kv => kv.Key, kv => (selected: kv.Value.SelectedRunnerName, count: kv.Value.Runners.Count));

            var cases = new[]
            {
                "{\"protocols\":{\"RDP\":{\"selectedRunnerName\":\"x\",\"runners\":[]}}}",   // 未知协议（RDP 不在 6 协议内）
                "{\"protocols\":{\"SSH\":{\"selectedRunnerName\":\"x\",\"runners\":[]}}}",  // runners 空
                "{\"protocols\":{\"SSH\":{}}}",                                              // runners 缺失
                "{\"protocols\":{}}",                                                        // 空对象 = 无条目
                "{}",                                                                        // 缺 protocols 键
                "{\"protocols\":\"not-an-object\"}",
            };
            foreach (var json in cases)
            {
                var (code, body) = await PutAsync("/api/settings/runners", json);
                Assert.AreEqual(HttpStatusCode.BadRequest, code, json + " -> " + body);
            }

            foreach (var kv in snap)
            {
                Assert.AreEqual(kv.Value.selected, pcs.ProtocolConfigs[kv.Key].SelectedRunnerName, kv.Key + " 零写入");
                Assert.AreEqual(kv.Value.count, pcs.ProtocolConfigs[kv.Key].Runners.Count, kv.Key + " 零写入");
            }
        }

        // ------------------------------------------------------------------
        // fix batch7 Task E #13/#14：meta（主题/字体/字符集选项域）、macros、PuTTY 配置
        // 字段与外部运行器增删的 PUT 全量往返。
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task GetRunners_IncludesMetaAndMacros()
        {
            var root = await GetRunnersAsync();
            var meta = root.GetProperty("meta");

            // puttyThemes：测试宿主预置主题占位 {"Default": []}（TestInit，绕开 pack 资源）——
            // 名称在列即证清单来自 PuttyThemes.Themes；占位无 ColourN 条目，颜色位允许 null
            Assert.AreEqual(JsonValueKind.Array, meta.GetProperty("puttyThemes").ValueKind);
            Assert.IsTrue(meta.GetProperty("puttyThemes").EnumerateArray()
                .Any(t => t.GetProperty("name").GetString() == "Default"), "主题清单应含 PuttyThemes.Themes 键");

            // fonts/codePages：选项域形状（codePages 取自 SSH 首项 PuttyRunner.CodePages）
            Assert.AreEqual(JsonValueKind.Array, meta.GetProperty("fonts").ValueKind);
            CollectionAssert.Contains(meta.GetProperty("codePages").EnumerateArray().Select(x => x.GetString()).ToList(), "UTF-8");

            // macros：每协议 [{name, description}]（WPF 参数宏自动补全的数据源）
            var ssh = root.GetProperty("protocols").GetProperty("SSH");
            Assert.AreEqual(JsonValueKind.Array, ssh.GetProperty("macros").ValueKind);
            Assert.IsTrue(ssh.GetProperty("macros").GetArrayLength() > 0, "SSH 宏清单非空（%1RM_HOSTNAME% 等）");
        }

        [TestMethod]
        public async Task PutRunners_PuttyConfigFields_UpdateAndPersist()
        {
            var pcs = _1RM.IoC.Get<ProtocolConfigurationService>();
            var ssh = pcs.ProtocolConfigs["SSH"];
            var putty = (PuttyRunner)ssh.Runners.First(r => r is PuttyRunner);
            var orig = (putty.PuttyThemeName, putty.PuttyFont, putty.PuttyFontSize, putty.LineCodePage);
            try
            {
                var original = await GetRunnersAsync();
                var sshCfg = original.GetProperty("protocols").GetProperty("SSH");

                // 直通 runners 上改 PuTTY 的主题/字体/字号/字符集（测试宿主主题占位仅 "Default"）
                var runners = JsonNode.Parse(sshCfg.GetProperty("runners").GetRawText())!.AsArray();
                var puttyNode = runners.First(n => n["$type"]!.GetValue<string>() == "PuttyRunner");
                puttyNode["PuttyThemeName"] = "Default";
                puttyNode["PuttyFont"] = "Courier New";
                puttyNode["PuttyFontSize"] = 16;
                puttyNode["LineCodePage"] = "CP437";
                var payload = "{\"protocols\":{\"SSH\":{\"selectedRunnerName\":" + JsonSerializer.Serialize(sshCfg.GetProperty("selectedRunnerName").GetString())
                              + ",\"runners\":" + runners.ToJsonString() + ",\"macros\":" + sshCfg.GetProperty("macros").GetRawText() + "}}}";

                var (code, body) = await PutAsync("/api/settings/runners", payload);
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                // ApplyRunners 原位替换 Runners 列表（新反序列化实例），断言须取替换后的当前实例
                var updated = (PuttyRunner)ssh.Runners.First(r => r is PuttyRunner);
                Assert.AreEqual("Default", updated.PuttyThemeName);
                Assert.AreEqual("Courier New", updated.PuttyFont);
                Assert.AreEqual(16, updated.PuttyFontSize);
                Assert.AreEqual("CP437", updated.LineCodePage);

                // PUT 请求体携带 macros（GET 原样回传场景）——未知键被忽略，不破坏反序列化
                var after = await GetRunnersAsync();
                var afterPutty = after.GetProperty("protocols").GetProperty("SSH").GetProperty("runners").EnumerateArray()
                    .First(n => n.GetProperty("$type").GetString() == "PuttyRunner");
                Assert.AreEqual("Courier New", afterPutty.GetProperty("PuttyFont").GetString());
                Assert.AreEqual(16, afterPutty.GetProperty("PuttyFontSize").GetInt32());
                Assert.AreEqual("CP437", afterPutty.GetProperty("LineCodePage").GetString());
            }
            finally
            {
                // 同上：恢复须作用于当前列表内的实例（PUT 已整体换过实例），否则 Save 落盘的仍是改动值
                var cur = ssh.Runners.FirstOrDefault(r => r is PuttyRunner) as PuttyRunner;
                if (cur != null)
                {
                    cur.PuttyThemeName = orig.Item1;
                    cur.PuttyFont = orig.Item2;
                    cur.PuttyFontSize = orig.Item3;
                    cur.LineCodePage = orig.Item4;
                }
                pcs.Save();
            }
        }

        [TestMethod]
        public async Task PutRunners_AddAndRemoveExternalRunner_Persists()
        {
            var pcs = _1RM.IoC.Get<ProtocolConfigurationService>();
            var sftp = pcs.ProtocolConfigs["SFTP"];
            var origRunners = sftp.Runners.ToList();
            var origSelected = sftp.SelectedRunnerName;
            const string name = "web-add-test-runner";
            try
            {
                var original = await GetRunnersAsync();
                var sftpCfg = original.GetProperty("protocols").GetProperty("SFTP");
                var runners = JsonNode.Parse(sftpCfg.GetProperty("runners").GetRawText())!.AsArray();

                // 增：SFTP 按协议族使用 ExternalRunnerForSSH（WPF CmdAddRunner 同款分支），
                // 新建对象 = 前端"添加运行器"模态构造的最小字段集（$type/Name/ExePath/Arguments/
                // ArgumentsForPrivateKey/RunWithHosting/EnvironmentVariables/SpecialCharacters）
                runners.Add(JsonNode.Parse(
                    "{\"$type\":\"ExternalRunnerForSSH\",\"Name\":\"" + name + "\",\"OwnerProtocolName\":\"SFTP\"," +
                    "\"ExePath\":\"C:\\\\tools\\\\sftp.exe\",\"Arguments\":\"sftp://%1RM_USERNAME%@%1RM_HOSTNAME%\"," +
                    "\"ArgumentsForPrivateKey\":\"/privatekey=%1RM_PRIVATE_KEY_PATH%\",\"RunWithHosting\":false," +
                    "\"EnvironmentVariables\":[{\"Key\":\"USER\",\"Value\":\"%1RM_USERNAME%\"}],\"SpecialCharacters\":[]}"));
                var addPayload = "{\"protocols\":{\"SFTP\":{\"selectedRunnerName\":" + JsonSerializer.Serialize(origSelected)
                              + ",\"runners\":" + runners.ToJsonString() + "}}}";
                var (c1, b1) = await PutAsync("/api/settings/runners", addPayload);
                Assert.AreEqual(HttpStatusCode.OK, c1, b1);

                var added = sftp.Runners.OfType<ExternalRunnerForSSH>().SingleOrDefault(r => r.Name == name);
                Assert.IsNotNull(added, "PUT 全量保存后内存配置应含新增外部运行器");
                Assert.AreEqual("C:\\tools\\sftp.exe", added.ExePath);
                Assert.AreEqual("/privatekey=%1RM_PRIVATE_KEY_PATH%", added.ArgumentsForPrivateKey);
                Assert.AreEqual(1, added.EnvironmentVariables.Count);
                Assert.IsTrue(added.MarcoNames.Count > 0, "Load 后处理应回填宏清单（ApplyRunners 重放）");

                var afterAdd = await GetRunnersAsync();
                var addedNode = afterAdd.GetProperty("protocols").GetProperty("SFTP").GetProperty("runners").EnumerateArray()
                    .FirstOrDefault(n => n.GetProperty("Name").GetString() == name);
                Assert.IsNotNull(addedNode, "GET 应返回新增运行器");
                Assert.AreEqual("ExternalRunnerForSSH", addedNode.GetProperty("$type").GetString());

                // 删：全量表回退到 GET 原始 runners（即不含新增行的状态）+
                // selectedRunnerName 指向首项（WPF CmdDeleteRunner 后回退语义）
                var origRunnersRaw = sftpCfg.GetProperty("runners").GetRawText();
                var firstName = sftpCfg.GetProperty("runners").EnumerateArray().First().GetProperty("Name").GetString();
                var delPayload = "{\"protocols\":{\"SFTP\":{\"selectedRunnerName\":" + JsonSerializer.Serialize(firstName)
                              + ",\"runners\":" + origRunnersRaw + "}}}";
                var (c2, b2) = await PutAsync("/api/settings/runners", delPayload);
                Assert.AreEqual(HttpStatusCode.OK, c2, b2);
                Assert.IsFalse(sftp.Runners.Any(r => r.Name == name), "删除后内存配置不再含该运行器");
                Assert.AreEqual(firstName, sftp.SelectedRunnerName);

                var afterDel = await GetRunnersAsync();
                Assert.IsFalse(afterDel.GetProperty("protocols").GetProperty("SFTP").GetProperty("runners").EnumerateArray()
                    .Any(n => n.GetProperty("Name").GetString() == name), "GET 不再返回已删运行器");
            }
            finally
            {
                sftp.Runners = origRunners;
                sftp.SelectedRunnerName = origSelected;
                pcs.Save();
            }
        }
    }
}
