using System;
using System.Collections.Generic;
using System.IO;
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
using _1RM.Service.DataSource.Model;
using _1RM.Service.WebUi;
using _1RM.View;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/servers/import（multipart）+ /api/servers/export 集成测试。
    /// 关键约定（与 WPF ServerPageViewModelBase 的 CmdImport*/CmdExportSelectedToJson 平价）：
    /// - 导出 = 选中项 Clone + DecryptToConnectLevel → List&lt;ProtocolBase&gt; 的 Indented JSON（UTF8、
    ///   attachment），导出前必须通过二次验证（未开启时 VerifyAsyncUi 直通 true；30s 窗口内免再次验证，
    ///   窗口语义与凭据 reveal 共用 WebUiCredentialService.RevealVerifiedAtMap）；
    /// - 导入 = 各格式解析为 List&lt;ProtocolBase&gt;（JSON 防御性 DecryptToConnectLevel——导出文件是明文，
    ///   DecryptOrReturnOriginalString 对明文原样透传）→ 逐台插入（Id 置空 = IsTmpSession 新建语义）；
    ///   凭据提取走 Dapper 批量 AddServer 的按 Hash 自动提取（单台 ref 重载不提取——见服务注释）；
    /// - .rdp 导入附 TERMSRV/&lt;address&gt; Windows 凭据读取（不存在 → 密码为空，null 安全）；
    /// - .db 导入双格式探测（PRemoteM: Config+Server 表；1Remote: Configs+Servers 表）。
    /// </summary>
    [TestClass]
    public class ImportExportTests
    {
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            TestInit.Init();
            // 强制“未开启二次验证”——VerifyAsyncUi 直通 true（与 CredentialEndpointsTests 同款反射缝）
            SetSecondaryVerificationEnabled(false);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();
            WebUiEndpoints.MapAll(app);
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestClient();
        }

        private static void SetSecondaryVerificationEnabled(bool enabled)
        {
            var field = typeof(SecondaryVerificationHelper).GetField("_isEnabled",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "SecondaryVerificationHelper._isEnabled 字段必须存在");
            field.SetValue(null, (bool?)enabled);
        }

        private static string NewName(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>经 GlobalData.AddServer 种子一台 RDP 服务器（走与 WPF 相同的写库+重载路径）。</summary>
        private static (string id, string displayName) SeedServer(string displayName, string password)
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
                    UserName = "u-seed",
                    Password = password,
                    Tags = new List<string>(),
                }, local);
                Assert.IsTrue(ret.IsSuccess, "种子服务器写入失败: " + ret.ErrorInfo);
            }
            return (id, displayName);
        }

        /// <summary>multipart 上传一个内存文件到 POST /api/servers/import（folder 可选：目标文件夹路径）。</summary>
        private static async Task<HttpResponseMessage> ImportAsync(byte[] content, string fileName, string ds = "Local", string? folder = null)
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", fileName);
            var qs = $"?ds={Uri.EscapeDataString(ds)}";
            if (!string.IsNullOrEmpty(folder)) qs += $"&folder={Uri.EscapeDataString(folder)}";
            return await _client.PostAsync($"/api/servers/import{qs}", form);
        }

        private static async Task<List<(string Id, string DisplayName, string Address)>> GetServersAsync()
        {
            var resp = await _client.GetAsync("/api/servers");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.EnumerateArray().Select(x => (
                Id: x.GetProperty("id").GetString() ?? "",
                DisplayName: x.GetProperty("displayName").GetString() ?? "",
                Address: x.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() ?? "" : "")).ToList();
        }

        // ------------------------------------------------------------------
        // 嗅探纯函数
        // ------------------------------------------------------------------

        [TestMethod]
        public void DetectImportKind_MapsExtensions()
        {
            Assert.AreEqual(ImportFileKind.Json, WebUiImportExportService.DetectImportKind("a.json"));
            Assert.AreEqual(ImportFileKind.Csv, WebUiImportExportService.DetectImportKind("a.CSV"));
            Assert.AreEqual(ImportFileKind.Rdp, WebUiImportExportService.DetectImportKind("a.rdp"));
            Assert.AreEqual(ImportFileKind.Db, WebUiImportExportService.DetectImportKind("a.db"));
            Assert.AreEqual(ImportFileKind.Db, WebUiImportExportService.DetectImportKind("a.sqlite"));
            Assert.AreEqual(ImportFileKind.Unknown, WebUiImportExportService.DetectImportKind("a.txt"));
            Assert.AreEqual(ImportFileKind.Unknown, WebUiImportExportService.DetectImportKind("a"));
            Assert.AreEqual(ImportFileKind.Unknown, WebUiImportExportService.DetectImportKind(""));
            Assert.AreEqual(ImportFileKind.Unknown, WebUiImportExportService.DetectImportKind(null));
        }

        // ------------------------------------------------------------------
        // JSON 往返：export → delete → import → 密码完好
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task JsonRoundTrip_ExportDeleteImport_PasswordsIntact()
        {
            var pw1 = "pw-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var pw2 = "pw-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var (id1, name1) = SeedServer(NewName("rt-a"), pw1);
            var (id2, name2) = SeedServer(NewName("rt-b"), pw2);

            // 导出：200 + attachment + 明文（解密后）可解析
            var resp = await _client.GetAsync($"/api/servers/export?ids={id1},{id2}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());
            Assert.AreEqual("attachment", resp.Content.Headers.ContentDisposition?.DispositionType,
                "导出必须是 attachment 下载");
            StringAssert.Contains(resp.Content.Headers.ContentDisposition?.FileName ?? "", ".json");
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var json = Encoding.UTF8.GetString(bytes);
            StringAssert.Contains(json, pw1, "导出文件必须含明文密码（WPF 导出 = DecryptToConnectLevel 后落盘）");
            StringAssert.Contains(json, pw2);
            StringAssert.Contains(json, name1);

            // 删除两台
            Assert.AreEqual(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/servers/{id1}?ds=Local")).StatusCode);
            Assert.AreEqual(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/servers/{id2}?ds=Local")).StatusCode);
            var afterDelete = await GetServersAsync();
            Assert.IsFalse(afterDelete.Any(x => x.DisplayName == name1 || x.DisplayName == name2), "删除后列表不得再含两台种子");

            // 导入导出文件 → 两台回归
            resp = await ImportAsync(bytes, "export.json");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                Assert.AreEqual(2, doc.RootElement.GetProperty("added").GetInt32(), "两台都应导入成功");
                Assert.AreEqual(0, doc.RootElement.GetProperty("skipped").GetInt32());
                Assert.AreEqual(0, doc.RootElement.GetProperty("errors").GetArrayLength());
            }

            var list = await GetServersAsync();
            var back1 = list.FirstOrDefault(x => x.DisplayName == name1);
            var back2 = list.FirstOrDefault(x => x.DisplayName == name2);
            Assert.IsTrue(back1 != default, "导入后第一台应回到列表");
            Assert.IsTrue(back2 != default, "导入后第二台应回到列表");
            Assert.AreNotEqual(id1, back1.Id, "导入是新建（新 ULID），不得复用原 Id");

            // 密码完好：config 端点返回解密后的明文 json
            foreach (var (back, pw) in new[] { (back1, pw1), (back2, pw2) })
            {
                var cfg = await _client.GetAsync($"/api/servers/{back.Id}/config?ds=Local");
                Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
                StringAssert.Contains(await cfg.Content.ReadAsStringAsync(), pw, "导入后密码应往返完好（config 为解密视图）");
            }
        }

        // ------------------------------------------------------------------
        // 导出验证门
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task Export_VerificationFailed_Returns403()
        {
            var (id, name) = SeedServer(NewName("exp-403"), "pw-x");
            // 静态验证窗口与其它测试共享：先把窗口置过期，确保本测试真正走到验证调用
            WebUiCredentialService.SetRevealVerifiedAt("Local", DateTime.Now.AddMinutes(-5));
            // verifier 注入（服务层）：false/用户取消 都必须 403，绝不能下载明文
            var result = await WebUiImportExportService.ExportAsync(new List<string> { id },
                verifier: () => Task.FromResult<bool?>(false));
            Assert.AreEqual(ExportStatus.Forbidden, result.Status);
            result = await WebUiImportExportService.ExportAsync(new List<string> { id },
                verifier: () => Task.FromResult<bool?>(null));
            Assert.AreEqual(ExportStatus.Forbidden, result.Status, "用户取消同样 403");
            // 验证失败不得标记窗口（否则下一个请求会免验证通过）
            Assert.IsTrue((DateTime.Now - WebUiCredentialService.GetRevealVerifiedAt("Local")).TotalSeconds >= 30,
                "验证失败不得开启 30s 窗口");
        }

        [TestMethod]
        public async Task Export_Within30sWindow_SkipsSecondaryVerification()
        {
            var (id, name) = SeedServer(NewName("exp-win"), "pw-y");
            try
            {
                // 强制“已开启二次验证” + 预置刚验证过的时间戳 → 必须走免验证窗口；
                // 若窗口失效会弹真实 Windows 凭据对话框/挂起——即失败信号（与 reveal 窗口测试同款）
                SetSecondaryVerificationEnabled(true);
                WebUiCredentialService.SetRevealVerifiedAt("Local", DateTime.Now);
                var result = await WebUiImportExportService.ExportAsync(new List<string> { id });
                Assert.AreEqual(ExportStatus.Ok, result.Status);
                StringAssert.Contains(result.Json ?? "", name);
            }
            finally
            {
                SetSecondaryVerificationEnabled(false);
            }
        }

        [TestMethod]
        public async Task Export_UnknownId_Returns400()
        {
            var resp = await _client.GetAsync("/api/servers/export?ids=no-such-id");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [TestMethod]
        public async Task Export_EmptyIds_Returns400()
        {
            var resp = await _client.GetAsync("/api/servers/export");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
            resp = await _client.GetAsync("/api/servers/export?ids=");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        // ------------------------------------------------------------------
        // CSV
        // ------------------------------------------------------------------

        /// <summary>最小 mRemoteNG CSV：';' 分隔、首行小写列名、NodeType=Connection、Protocol=rdp。</summary>
        private static string MinimalCsv(string name, string host, string user, string password)
        {
            return "Name;Id;Parent;NodeType;Hostname;Protocol;Username;Password;Port\r\n"
                   + $"{name};csv-1;;Connection;{host};rdp;{user};{password};3389\r\n";
        }

        [TestMethod]
        public async Task CsvImport_MinimalRow_CreatesServer()
        {
            var name = NewName("csv-srv");
            var csv = MinimalCsv(name, "9.9.9.9", "csv-user", "csv-pw");
            var resp = await ImportAsync(Encoding.UTF8.GetBytes(csv), "servers.csv");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                Assert.AreEqual(1, doc.RootElement.GetProperty("added").GetInt32());
                Assert.AreEqual(0, doc.RootElement.GetProperty("errors").GetArrayLength());
            }

            var list = await GetServersAsync();
            var srv = list.FirstOrDefault(x => x.DisplayName == name);
            Assert.IsTrue(srv != default, "CSV 导入后服务器应在列表中");
            Assert.AreEqual("9.9.9.9", srv.Address);
        }

        [TestMethod]
        public async Task CsvImport_Garbage_ReturnsErrorsGracefully()
        {
            var resp = await ImportAsync(Encoding.UTF8.GetBytes("not;a;real;csv\r\n1;2;3\r\n"), "bad.csv");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "无可导入行的 CSV 应 400（WPF 静默无操作，Web 返回结构化错误）");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.GetProperty("errors").GetArrayLength() >= 1, "应返回 errors 数组");
        }

        // ------------------------------------------------------------------
        // RDP
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task RdpImport_CreatesServer_EmptyPasswordWhenNoTermsrvCredential()
        {
            var name = NewName("rdp-file");
            // TEST-NET-3 地址：测试机不可能存在 TERMSRV/203.0.113.77 凭据 → 密码为空（null 安全降级）
            var rdp = "full address:s:203.0.113.77\r\nusername:s:rdp-user\r\nscreen mode id:i:2\r\n";
            var resp = await ImportAsync(Encoding.UTF8.GetBytes(rdp), name + ".rdp");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                Assert.AreEqual(1, doc.RootElement.GetProperty("added").GetInt32());
            }

            var list = await GetServersAsync();
            var srv = list.FirstOrDefault(x => x.DisplayName == name);
            Assert.IsTrue(srv != default, "rdp 导入后服务器应在列表中（DisplayName = 上传文件名）");
            Assert.AreEqual("203.0.113.77", srv.Address);

            // TERMSRV 凭据不存在 → 密码空（config 解密视图）
            var cfg = await _client.GetAsync($"/api/servers/{srv.Id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
            var body = await cfg.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "\"UserName\":\"rdp-user\"");
            StringAssert.Contains(body, "\"Password\":\"\"");
        }

        [TestMethod]
        public async Task RdpImport_Garbage_Returns400()
        {
            // 无任何可识别键值行 → FromRdpFile 返回 null → 400
            var resp = await ImportAsync(Encoding.UTF8.GetBytes("hello world\r\n"), "garbage.rdp");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        // ------------------------------------------------------------------
        // 守卫
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task Import_UnknownExtension_Returns400()
        {
            var resp = await ImportAsync(Encoding.UTF8.GetBytes("whatever"), "file.txt");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [TestMethod]
        public async Task Import_UnknownDataSource_Returns400()
        {
            var resp = await ImportAsync(Encoding.UTF8.GetBytes("[]"), "empty.json", ds: "no-such-ds");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [TestMethod]
        public async Task Import_ReadOnlyDataSource_Returns400()
        {
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local);
            var prop = typeof(DataSourceBase).GetProperty("IsWritable");
            Assert.IsNotNull(prop, "IsWritable 属性必须存在");
            try
            {
                prop.SetValue(local, false); // protected setter，反射强制只读（夹具库恒可写，故用属性级单测）
                var resp = await ImportAsync(Encoding.UTF8.GetBytes("[]"), "empty.json");
                Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, "只读数据源必须前置 400（插入路径对只读库静默返回 Success）");
            }
            finally
            {
                prop.SetValue(local, true);
            }
        }

        [TestMethod]
        public async Task Import_EmptyJsonArray_ReturnsStructuredError()
        {
            // 空数组：WPF 亦按失败处理（空列表插入报 "Insert servers failed"）——Web 统一 400 结构化
            var resp = await ImportAsync(Encoding.UTF8.GetBytes("[]"), "empty.json");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.IsTrue(doc.RootElement.GetProperty("errors").GetArrayLength() >= 1);
        }

        // ------------------------------------------------------------------
        // 目标文件夹（batch10 Task A #1）
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task Import_WithFolderPath_AssignsImportedServersToFolder()
        {
            // folder='a/b' → 导入服务器的 TreeNodes=["a","b"]（config 端点为 PascalCase 直通域）
            var name = NewName("fld-srv");
            var csv = MinimalCsv(name, "9.9.9.8", "fld-user", "fld-pw");
            var resp = await ImportAsync(Encoding.UTF8.GetBytes(csv), "servers.csv", folder: "a/b");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, await resp.Content.ReadAsStringAsync());

            var list = await GetServersAsync();
            var srv = list.FirstOrDefault(x => x.DisplayName == name);
            Assert.IsTrue(srv != default, "导入后服务器应在列表中");
            var cfg = await _client.GetAsync($"/api/servers/{srv.Id}/config?ds=Local");
            Assert.AreEqual(HttpStatusCode.OK, cfg.StatusCode);
            var body = await cfg.Content.ReadAsStringAsync();
            StringAssert.Contains(body, "\"TreeNodes\":[\"a\",\"b\"]", "folderPath 必须落到导入服务器的 TreeNodes");
        }
    }
}
