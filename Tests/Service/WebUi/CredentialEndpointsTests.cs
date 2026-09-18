using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.WebUi;
using _1RM.View;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/credentials CRUD + reveal 集成测试。
    /// 关键约定：
    /// - 写入路径传明文，加密由 DataSourceBase.Database_Insert/UpdateCredential 在内部克隆上完成
    ///   （EncryptToDatabaseLevel）——GetCredentials 读回的缓存/库内必须是密文；
    /// - reveal = SecondaryVerificationHelper.VerifyAsyncUi（未开启验证时直通 true）→
    ///   CloneMe + DecryptToConnectLevel 返回明文，缓存保持密文（不得原地解密）；
    /// - 重名判定与 WPF 编辑器一致：CurrentCultureIgnoreCase；
    /// - 删除/更新联动引用服务器（Dapper 事务）→ NeedReloadUI → 端点须 ReloadAll 刷新 VmItemList。
    /// </summary>
    [TestClass]
    public class CredentialEndpointsTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();

            // SecondaryVerificationHelper 的启用状态取自宿主机器（凭据管理器/注册表/文件），
            // 测试不依赖机器状态：直接写静态缓存强制“未开启”——与 WPF 启动路径
            // （Init()->GetEnabled==null->SetEnabled(false)）的默认一致，VerifyAsyncUi 直通返回 true。
            SetSecondaryVerificationEnabled(false);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult(); // 显式启动，避免 GetTestClient 竞态
            _client = app.GetTestClient();
        }

        /// <summary>强制二次验证开关（反射写静态缓存，不落注册表/凭据管理器）。</summary>
        private static void SetSecondaryVerificationEnabled(bool enabled)
        {
            var field = typeof(SecondaryVerificationHelper).GetField("_isEnabled",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "SecondaryVerificationHelper._isEnabled 字段必须存在");
            field.SetValue(null, (bool?)enabled);
        }

        private static string NewName(string prefix = "cred-a")
        {
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

        /// <summary>POST /api/credentials 新建凭据（credential 域为 PascalCase，与 WPF 模型一致）。</summary>
        private static async Task<HttpResponseMessage> PostCredentialAsync(string name,
            string userName = "user-0", string password = "", string privateKeyPath = "", string ds = "Local")
        {
            var body = $"{{\"ds\":\"{ds}\",\"credential\":{{\"Name\":\"{name}\",\"Address\":\"\",\"Port\":\"\","
                       + $"\"UserName\":\"{userName}\",\"Password\":\"{password}\",\"PrivateKeyPath\":\"{privateKeyPath}\"}}}}";
            return await _client.PostAsync("/api/credentials", JsonBody(body));
        }

        private static async Task<HttpResponseMessage> PutCredentialAsync(string routeName, string newName,
            string userName, string password = "", string privateKeyPath = "")
        {
            var body = $"{{\"credential\":{{\"Name\":\"{newName}\",\"Address\":\"\",\"Port\":\"\","
                       + $"\"UserName\":\"{userName}\",\"Password\":\"{password}\",\"PrivateKeyPath\":\"{privateKeyPath}\"}}}}";
            return await _client.PutAsync($"/api/credentials/{Uri.EscapeDataString(routeName)}?ds=Local", JsonBody(body));
        }

        /// <summary>GET /api/credentials?ds=Local 并解析为凭据列表快照（元组化——JsonElement 随文档释放失效）。</summary>
        private static async Task<List<(string Name, string UserName, int RefCount)>> GetListAsync()
        {
            var resp = await _client.GetAsync("/api/credentials?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("credentials").EnumerateArray()
                .Select(x => (
                    Name: x.GetProperty("name").GetString() ?? "",
                    UserName: x.GetProperty("userName").GetString() ?? "",
                    RefCount: x.GetProperty("refCount").GetInt32()))
                .ToList();
        }

        private static async Task<(string Name, string UserName, int RefCount)> GetListItemAsync(string name)
        {
            var list = await GetListAsync();
            var item = list.FirstOrDefault(x => x.Name == name);
            Assert.IsTrue(item != default, $"凭据列表中应含 '{name}'");
            return item;
        }

        // ------------------------------------------------------------------
        // CRUD 往返
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task CrudRoundTrip_CreateListUpdateDelete()
        {
            var name = NewName();
            var resp = await PostCredentialAsync(name, userName: "user-1");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                Assert.AreEqual(name, doc.RootElement.GetProperty("name").GetString(), "POST 成功应回显 name");
            }

            // 列表可见 + 字段正确 + 不含敏感字段
            var list = await GetListAsync();
            var item = list.FirstOrDefault(x => x.Name == name);
            Assert.IsTrue(item != default, "POST 后列表应含新凭据");
            Assert.AreEqual("user-1", item.UserName);
            Assert.AreEqual(0, item.RefCount, "无引用服务器时 refCount 应为 0");
            var raw = await (await _client.GetAsync("/api/credentials?ds=Local")).Content.ReadAsStringAsync();
            Assert.IsFalse(raw.ToLower().Contains("password"), "列表不得含 password 字段");
            Assert.IsFalse(raw.ToLower().Contains("privatekey"), "列表不得含私钥字段");

            // 更新 userName（同名整体替换）
            resp = await PutCredentialAsync(name, name, userName: "user-2");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());
            item = await GetListItemAsync(name);
            Assert.AreEqual("user-2", item.UserName);

            // 删除 → 列表移除
            var del = await _client.DeleteAsync($"/api/credentials/{Uri.EscapeDataString(name)}?ds=Local");
            Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);
            list = await GetListAsync();
            Assert.IsFalse(list.Any(x => x.Name == name), "删除后列表不得再含该凭据");
        }

        [TestMethod]
        public async Task Create_DuplicateName_Returns400_CaseInsensitive()
        {
            var name = NewName("cred-dup");
            var resp = await PostCredentialAsync(name);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

            // 完全同名 → 400（WPF 编辑器语义：CurrentCultureIgnoreCase）
            resp = await PostCredentialAsync(name);
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                Assert.IsTrue(doc.RootElement.GetProperty("errors").GetArrayLength() >= 1, "重名应返回 errors 数组");
            }

            // 大小写变体同样视为重名
            resp = await PostCredentialAsync(name.ToUpperInvariant());
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "重名判定应与 WPF 一致（忽略大小写）");
        }

        [TestMethod]
        public async Task Create_EmptyOrWhitespaceName_Returns400()
        {
            var resp = await PostCredentialAsync("");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
            resp = await PostCredentialAsync("   ");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [TestMethod]
        public async Task Create_NameTooLong_Returns400()
        {
            // WPF 编辑器规则：Name 长度 > 100 报错（Credential.Name setter 对 >128 才静默截断，此处前置拦截）
            var resp = await PostCredentialAsync(new string('n', 101));
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [TestMethod]
        public async Task Create_UnknownDataSource_Returns400()
        {
            var resp = await PostCredentialAsync(NewName("cred-x"), ds: "no-such-ds");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [TestMethod]
        public async Task List_UnknownDataSource_Returns404()
        {
            var resp = await _client.GetAsync("/api/credentials?ds=no-such-ds");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task Create_Password_EncryptedAtRest_InCredentialCache()
        {
            var name = NewName("cred-enc");
            var password = "pw-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var resp = await PostCredentialAsync(name, password: password);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

            // GetCredentials 读回（插入后表时间戳已更新 → 重读库）：缓存中必须是密文
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local);
            var cached = local.GetCredentials().FirstOrDefault(x => x.Name == name);
            Assert.IsNotNull(cached, "插入后 GetCredentials 应能读到新凭据");
            Assert.AreNotEqual(password, cached.Password, "凭据缓存必须保持加密态（加密由 Database_InsertCredential 内部完成）");
        }

        // ------------------------------------------------------------------
        // PUT
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task Update_Rename_MovesListEntry_AndKeepsReferentialLink()
        {
            var nameA = NewName("cred-ren");
            var nameB = nameA + "-renamed";
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(nameA)).StatusCode);

            // 引用服务器：InheritedCredentialName = nameA
            var (serverId, serverName) = await SeedServerAsync(NewName("srv-ren"), inheritedCredentialName: nameA);
            Assert.AreEqual(1, (await GetListItemAsync(nameA)).RefCount);

            // 重命名 A → B（nameBefore=nameA 驱动引用联动）
            var resp = await PutCredentialAsync(nameA, nameB, userName: "user-r");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                Assert.AreEqual(nameB, doc.RootElement.GetProperty("name").GetString(), "PUT 成功应回显新 name");
            }

            var list = await GetListAsync();
            Assert.IsFalse(list.Any(x => x.Name == nameA), "重命名后旧名应消失");
            Assert.IsTrue(list.Any(x => x.Name == nameB), "重命名后新名应在列");

            // Dapper 事务联动：引用服务器的 InheritedCredentialName 同步为新名
            var cfg = await _client.GetAsync($"/api/servers/{serverId}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
            StringAssert.Contains(await cfg.Content.ReadAsStringAsync(), $"\"InheritedCredentialName\":\"{nameB}\"");
        }

        [TestMethod]
        public async Task Update_RenameToExistingName_Returns400()
        {
            var nameA = NewName("cred-rn1");
            var nameB = NewName("cred-rn2");
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(nameA)).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(nameB)).StatusCode);

            var resp = await PutCredentialAsync(nameA, nameB, userName: "user-x");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
            // 目标自身原名应允许（改名回自身 = 无操作整体替换，不视为重名）
            resp = await PutCredentialAsync(nameA, nameA, userName: "user-x");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        [TestMethod]
        public async Task Update_UnknownName_Returns404()
        {
            var resp = await PutCredentialAsync("no-such-credential", "whatever", userName: "user-x");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task Update_SyncsReferencingServer_UserName()
        {
            var name = NewName("cred-sync");
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(name, userName: "u-cred")).StatusCode);

            var (serverId, _) = await SeedServerAsync(NewName("srv-sync"), inheritedCredentialName: name, userName: "u-old");

            // 更新凭据 userName → 引用服务器同步（WPF 编辑保存同一路径）
            var resp = await PutCredentialAsync(name, name, userName: "u-new");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());

            var cfg = await _client.GetAsync($"/api/servers/{serverId}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
            StringAssert.Contains(await cfg.Content.ReadAsStringAsync(), "\"UserName\":\"u-new\"",
                "凭据更新后引用服务器的 UserName 应被事务联动刷新");
        }

        /// <summary>
        /// fix batch8 Task E #17：空密码=保持原值。列表/编辑 API 不回显 Password/PrivateKeyPath
        /// 明文（安全红线），web 编辑表单无从预填——空提交必须沿用原值，否则任何一次不改密的
        /// 编辑都会静默清空密钥。对照：非空密码 PUT 正常更新。校验走 GetCredentials(true)
        /// 强制重读库（密文态）后克隆解密比对明文（与 reveal 同款），缓存不落明文。
        /// </summary>
        [TestMethod]
        public async Task Update_EmptyPasswordAndKey_KeepOriginals_NonEmptyUpdates()
        {
            var name = NewName("cred-keep");
            var pw1 = "pw-keep-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var key1 = "C:/keys/keep-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            Assert.AreEqual(HttpStatusCode.OK,
                (await PostCredentialAsync(name, password: pw1, privateKeyPath: key1)).StatusCode);

            // 空密码/空私钥 PUT：非加密字段正常更新，加密字段保持原值
            var resp = await PutCredentialAsync(name, name, userName: "user-keep", password: "", privateKeyPath: "");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());

            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local);
            var cached = local.GetCredentials(true).First(x => x.Name == name);
            Assert.AreNotEqual(pw1, cached.Password, "库内/缓存必须保持加密态");
            var clone = cached.CloneMe();
            clone.DecryptToConnectLevel();
            Assert.AreEqual(pw1, clone.Password, "空密码 PUT 必须保持原密码");
            Assert.AreEqual(key1, clone.PrivateKeyPath, "空私钥路径 PUT 必须保持原路径（表单同样不回显）");
            Assert.AreEqual("user-keep", cached.UserName, "非加密字段应正常更新");

            // 对照：非空密码 PUT 更新（私钥留空仍保持）
            var pw2 = "pw-new-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            resp = await PutCredentialAsync(name, name, userName: "user-keep2", password: pw2);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());

            cached = local.GetCredentials(true).First(x => x.Name == name);
            Assert.AreNotEqual(pw2, cached.Password, "更新后库内仍须为密文");
            var clone2 = cached.CloneMe();
            clone2.DecryptToConnectLevel();
            Assert.AreEqual(pw2, clone2.Password, "非空密码 PUT 应更新密码");
            Assert.AreEqual(key1, clone2.PrivateKeyPath, "未提及的私钥路径保持原值");
            Assert.AreEqual("user-keep2", cached.UserName);
        }

        // ------------------------------------------------------------------
        // DELETE 联动
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task Delete_ClearsInheritedCredentialName_OnReferencingServer()
        {
            var name = NewName("cred-del");
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(name)).StatusCode);

            var (serverId, serverName) = await SeedServerAsync(NewName("srv-del"), inheritedCredentialName: name);
            Assert.AreEqual(1, (await GetListItemAsync(name)).RefCount,
                "被一台服务器继承引用时 refCount 应为 1");

            var del = await _client.DeleteAsync($"/api/credentials/{Uri.EscapeDataString(name)}?ds=Local");
            Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);

            // 服务器保留、InheritedCredentialName 被事务联动清空；端点须 ReloadAll 使缓存可见
            var cfg = await _client.GetAsync($"/api/servers/{serverId}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode, "删除凭据不得删除引用服务器");
            var body = await cfg.Content.ReadAsStringAsync();
            StringAssert.Contains(body, $"\"DisplayName\":\"{serverName}\"");
            StringAssert.Contains(body, "\"InheritedCredentialName\":\"\"");

            // 缓存一致：VmItemList 中的对象也已被刷新
            var gd = _1RM.IoC.Get<GlobalData>();
            ProtocolBaseViewModel? vm;
            lock (gd)
            {
                vm = gd.GetItemById("Local", serverId);
            }
            Assert.IsNotNull(vm);
            Assert.AreEqual("", ((ProtocolBaseWithAddressPortUserPwd)vm.Server).InheritedCredentialName,
                "删除联动后缓存中的服务器 InheritedCredentialName 应被清空");
        }

        [TestMethod]
        public async Task Delete_UnknownName_Returns404()
        {
            var del = await _client.DeleteAsync("/api/credentials/no-such-credential?ds=Local");
            Assert.AreEqual(HttpStatusCode.NotFound, del.StatusCode);
        }

        [TestMethod]
        public async Task RefCount_CountsReferencingServers_InheritedAndAlternate()
        {
            var name = NewName("cred-ref");
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(name)).StatusCode);

            // 三台引用服务器：A=继承引用，B=备用凭据引用，C=继承+备用（同一台只计 1 次，语义=被引用台数）
            var gd = _1RM.IoC.Get<GlobalData>();
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local);
            lock (gd)
            {
                var common = new RDP
                {
                    Id = "srv-ref-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    DisplayName = "ref-common",
                    Address = "8.8.8.8",
                    Tags = new List<string>(),
                    InheritedCredentialName = name,
                };
                Assert.IsTrue(gd.AddServer(common, local).IsSuccess);

                Assert.IsTrue(gd.AddServer(new RDP
                {
                    Id = "srv-ref-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    DisplayName = "ref-alt",
                    Address = "8.8.4.4",
                    Tags = new List<string>(),
                    AlternateCredentials = new ObservableCollection<Credential>
                    {
                        new() { Name = name, Address = "8.8.4.4", Port = "3390", UserName = "alt", Password = "alt-pw" },
                    },
                }, local).IsSuccess);

                var both = (RDP)common.Clone();
                both.Id = "srv-ref-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                both.DisplayName = "ref-both";
                both.AlternateCredentials = new ObservableCollection<Credential>
                {
                    new() { Name = name, Address = "8.8.4.4", Port = "3390", UserName = "alt", Password = "alt-pw" },
                };
                Assert.IsTrue(gd.AddServer(both, local).IsSuccess);
            }

            var item = await GetListItemAsync(name);
            Assert.AreEqual(3, item.RefCount,
                "引用计数 = 被引用服务器台数（继承/备用均计，同一台重复引用只计一次）");
        }

        // ------------------------------------------------------------------
        // reveal
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task Reveal_ReturnsPlaintext_AndCacheStaysEncrypted()
        {
            var name = NewName("cred-rvl");
            var password = "pw-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(name, password: password)).StatusCode);

            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local);
            var cached = local.GetCredentials().First(x => x.Name == name);
            Assert.AreNotEqual(password, cached.Password, "前置：缓存为密文");

            // 未开启二次验证 → VerifyAsyncUi 直通 true → 返回明文
            var resp = await _client.PostAsync($"/api/credentials/{Uri.EscapeDataString(name)}/reveal?ds=Local", null);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                Assert.AreEqual(password, doc.RootElement.GetProperty("password").GetString(), "reveal 应返回明文密码");
                Assert.AreEqual("", doc.RootElement.GetProperty("privateKeyPath").GetString());
            }

            // reveal 后缓存仍为密文（CloneMe 后解密，不得原地解密）
            cached = local.GetCredentials().First(x => x.Name == name);
            Assert.AreNotEqual(password, cached.Password, "reveal 不得污染缓存（须克隆后解密）");

            // 30s 窗口内的第二次 reveal（免验证路径）同样返回明文
            var resp2 = await _client.PostAsync($"/api/credentials/{Uri.EscapeDataString(name)}/reveal?ds=Local", null);
            Assert.AreEqual(HttpStatusCode.OK, resp2.StatusCode);
            StringAssert.Contains(await resp2.Content.ReadAsStringAsync(), password);

            // 成功 reveal 应刷新该 ds 的验证时间戳（窗口起点）
            Assert.IsTrue((DateTime.Now - WebUiCredentialService.GetRevealVerifiedAt("Local")).TotalSeconds < 30,
                "reveal 成功后应记录验证时间戳");
        }

        [TestMethod]
        public async Task Reveal_PrivateKeyPath_RoundTrip()
        {
            var name = NewName("cred-key");
            Assert.AreEqual(HttpStatusCode.OK,
                (await PostCredentialAsync(name, password: "", privateKeyPath: "C:/keys/id_rsa")).StatusCode);

            var resp = await _client.PostAsync($"/api/credentials/{Uri.EscapeDataString(name)}/reveal?ds=Local", null);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.AreEqual("C:/keys/id_rsa", doc.RootElement.GetProperty("privateKeyPath").GetString());
        }

        [TestMethod]
        public async Task Reveal_UnknownName_Returns404()
        {
            var resp = await _client.PostAsync("/api/credentials/no-such-credential/reveal?ds=Local", null);
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task Reveal_UnknownDataSource_Returns400()
        {
            var resp = await _client.PostAsync("/api/credentials/whatever/reveal?ds=no-such-ds", null);
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        /// <summary>
        /// 30s 验证窗口：窗口内有效时间戳 → 跳过 VerifyAsyncUi（本测试强制“验证已开启”，
        /// 若窗口失效会弹真实 Windows 凭据对话框/挂起——即失败信号）。
        /// </summary>
        [TestMethod]
        public async Task Reveal_Within30sWindow_SkipsSecondaryVerification()
        {
            var name = NewName("cred-win");
            var password = "pw-win-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            Assert.AreEqual(HttpStatusCode.OK, (await PostCredentialAsync(name, password: password)).StatusCode);

            try
            {
                // 强制“已开启二次验证”+ 预置刚验证过的时间戳 → reveal 必须走免验证窗口
                SetSecondaryVerificationEnabled(true);
                WebUiCredentialService.SetRevealVerifiedAt("Local", DateTime.Now);

                var resp = await _client.PostAsync($"/api/credentials/{Uri.EscapeDataString(name)}/reveal?ds=Local", null);
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, "30s 窗口内应跳过二次验证直接返回");
                StringAssert.Contains(await resp.Content.ReadAsStringAsync(), password);
            }
            finally
            {
                SetSecondaryVerificationEnabled(false);
            }
        }

        // ------------------------------------------------------------------
        // 辅助
        // ------------------------------------------------------------------

        /// <summary>经 GlobalData.AddServer 种子一台引用凭据的 RDP 服务器（走与 WPF 相同的写库+重载路径）。</summary>
        private static async Task<(string id, string displayName)> SeedServerAsync(string displayName,
            string inheritedCredentialName, string userName = "u-seed")
        {
            var id = "srv-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var gd = _1RM.IoC.Get<GlobalData>();
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local, "TestInit 后本地数据源必须可用");
            lock (gd)
            {
                var ret = gd.AddServer(new RDP
                {
                    Id = id,
                    DisplayName = displayName,
                    Address = "7.7.7.7",
                    UserName = userName,
                    Tags = new List<string>(),
                    InheritedCredentialName = inheritedCredentialName,
                }, local);
                Assert.IsTrue(ret.IsSuccess, "种子服务器写入失败: " + ret.ErrorInfo);
            }

            // 前置确认：服务器已可通过 API 读到（落库+重载完成）
            var cfg = await _client.GetAsync($"/api/servers/{id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode, "种子服务器应可经 /api/servers/{id}/config 读取");
            return (id, displayName);
        }
    }
}
