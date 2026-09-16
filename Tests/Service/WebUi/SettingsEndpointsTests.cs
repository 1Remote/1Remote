using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shawn.Utils;
using Tests;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.Locality;
using _1RM.Service.WebUi;

namespace Tests.Service.WebUi
{
    /// <summary>
    /// /api/settings/general、/api/settings/launcher、/api/tags/manage|rename|{name} 集成测试。
    /// 关键约定：
    /// - general/launcher 为白名单部分更新（body 中缺失的键 = 保持不变），写后走 ConfigurationService.Save()；
    /// - language 归一小写（web 端 zh-CN → 存储值 zh-cn），校验 14 个内置语言码，非法 400 且零写入；
    /// - requireSecondaryVerification 的写入走 SecondaryVerificationHelper.SetEnabledAsync
    ///   （可等待：写注册表/凭据管理器——宿主机状态，用例 finally 还原）；fix #13 用例
    ///   经端点同款编排（ApplyGeneralAsync）断言 GetEnabled 判定源随写入同步翻转；
    /// - POST /api/settings/verify（fix batch3 #6）：开关翻转前的 WPF 平价验证门
    ///   （GeneralSettingView 翻转前先 VerifyAsyncUi）——VerifyAccessAsync 支持注入
    ///   verifier 桩，测试绝不触发真实凭据 UI；
    /// - launcher 热键为 WPF 枚举：线格式 = 枚举成员名（"ControlAlt"/"M"），PUT 额外接受 "Ctrl+Alt"
    ///   显示形态；测试环境未注册 LauncherWindowViewModel → 重注册静默跳过（PUT 仍 200）；
    /// - 标签重命名/删除复刻 TagActionHelper.CmdTagRename/CmdTagDelete 的核心循环（大小写语义：
    ///   标签规范化为小写、Tags 列表 ordinal 精确匹配），数据源范围 = 请求的 ds。
    /// </summary>
    [TestClass]
    public class SettingsEndpointsTests
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

        private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

        private static async Task<(HttpStatusCode Code, string Body)> PutAsync(string uri, string json)
        {
            using var resp = await _client.PutAsync(uri, JsonBody(json));
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }

        private static async Task<(HttpStatusCode Code, string Body)> PostAsync(string uri, string json)
        {
            using var resp = await _client.PostAsync(uri, JsonBody(json));
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
        }

        private static async Task<JsonElement> GetJsonAsync(string uri)
        {
            var resp = await _client.GetAsync(uri);
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, uri);
            return JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();
        }

        // ------------------------------------------------------------------
        // /api/settings/general
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task GetGeneral_ReturnsAllTenFields()
        {
            var root = await GetJsonAsync("/api/settings/general");
            Assert.AreEqual(JsonValueKind.String, root.GetProperty("language").ValueKind);
            Assert.AreEqual(JsonValueKind.Number, root.GetProperty("closeButtonBehavior").ValueKind);
            AssertIsBool(root.GetProperty("confirmBeforeClosingSession"), "confirmBeforeClosingSession");
            AssertIsBool(root.GetProperty("showSessionIconInSessionWindow"), "showSessionIconInSessionWindow");
            Assert.AreEqual(JsonValueKind.Number, root.GetProperty("logLevel").ValueKind);
            AssertIsBool(root.GetProperty("tabWindowCloseButtonOnLeft"), "tabWindowCloseButtonOnLeft");
            AssertIsBool(root.GetProperty("tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow"), "tabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow");
            AssertIsBool(root.GetProperty("copyPortWhenCopyAddress"), "copyPortWhenCopyAddress");
            AssertIsBool(root.GetProperty("doNotCheckNewVersion"), "doNotCheckNewVersion");
            AssertIsBool(root.GetProperty("requireSecondaryVerification"), "requireSecondaryVerification（读自 SecondaryVerificationHelper）");
            // 不暴露安全域外字段（自启/便携/DB 路径不在白名单）
            Assert.IsFalse(root.EnumerateObject().Any(p => p.Name == "appStartAutomatically" || p.Name == "sqliteDatabasePath"),
                "自启动/数据库路径等破坏性字段不得出现在 general 设置");
        }

        private static void AssertIsBool(JsonElement e, string field)
        {
            Assert.IsTrue(e.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{field} 应为 bool");
        }

        [TestMethod]
        public async Task PutGeneral_PartialUpdate_OnlyProvidedKeys()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var origConfirm = cs.General.ConfirmBeforeClosingSession;
            var origCopyPort = cs.General.CopyPortWhenCopyAddress;
            var origLanguage = cs.General.CurrentLanguageCode;
            try
            {
                // 只提供两个键：其余（含 language）必须保持不变
                var (code, body) = await PutAsync("/api/settings/general",
                    "{\"confirmBeforeClosingSession\":true,\"copyPortWhenCopyAddress\":false}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);

                Assert.IsTrue(cs.General.ConfirmBeforeClosingSession, "提供的键应写入内存配置");
                Assert.IsFalse(cs.General.CopyPortWhenCopyAddress);
                Assert.AreEqual(origLanguage, cs.General.CurrentLanguageCode, "未提供的 language 不得被改动");

                var root = await GetJsonAsync("/api/settings/general");
                Assert.IsTrue(root.GetProperty("confirmBeforeClosingSession").GetBoolean());
                Assert.IsFalse(root.GetProperty("copyPortWhenCopyAddress").GetBoolean());
                Assert.AreEqual(origLanguage, root.GetProperty("language").GetString());
            }
            finally
            {
                cs.General.ConfirmBeforeClosingSession = origConfirm;
                cs.General.CopyPortWhenCopyAddress = origCopyPort;
                cs.Save();
            }
        }

        [TestMethod]
        public async Task PutGeneral_RequireSecondaryVerification_EndpointPathUpdatesFlagSource()
        {
            // fix #13：PUT requireSecondaryVerification 必须让 VerifyAsyncUi 的判定源
            // （GetEnabled → _isEnabled 缓存/机器状态）同步翻转——否则勾选后 reveal 仍直通。
            // 直接调用端点同款编排（WebUiSettingsService.ApplyGeneralAsync，PUT handler 内即此调用），
            // 断言「写入返回时缓存已生效」+「缓存失效后按机器状态重读仍一致」。
            // 注意：本用例会写宿主机状态（注册表 HKCU/凭据管理器），finally 还原为用例前取值。
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var before = await SecondaryVerificationHelper.GetEnabled();
            try
            {
                var on = await WebUiSettingsService.ApplyGeneralAsync(cs,
                    new GeneralSettingsUpdateRequest { RequireSecondaryVerification = true });
                Assert.AreEqual(SettingsApplyStatus.Ok, on.Status);
                Assert.IsTrue(on.Dto!.RequireSecondaryVerification, "写入已 await 完成，响应回读值应为 true");
                Assert.IsTrue(await SecondaryVerificationHelper.GetEnabled(), "GetEnabled（VerifyAsyncUi 判定源）应返回 true——reveal 将触发验证");

                // fix-batch2 #11：启用后同样做“进程重启”语义校验——缓存失效后按机器状态重读必须仍为
                // true（三级写入若未真正落地，重启/重读后 GetEnabled 变 false，reveal 会重新直通：
                // VerifyAsyncUi 在 GetEnabled()==false 时直接放行不弹验证）。
                typeof(SecondaryVerificationHelper).GetField("_isEnabled",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .SetValue(null, (bool?)null);
                Assert.IsTrue(await SecondaryVerificationHelper.GetEnabled(),
                    "启用后机器状态重读（重启语义）应为 true——否则进程重启后 reveal 仍直通");

                // 关闭路径同验（也为还原做铺垫）：回读 false
                var off = await WebUiSettingsService.ApplyGeneralAsync(cs,
                    new GeneralSettingsUpdateRequest { RequireSecondaryVerification = false });
                Assert.AreEqual(SettingsApplyStatus.Ok, off.Status);
                Assert.IsFalse(off.Dto!.RequireSecondaryVerification);
                Assert.IsFalse(await SecondaryVerificationHelper.GetEnabled());

                // 缓存失效后强制重读机器状态：与缓存一致（写入确实落了机器，重启后同值）
                typeof(SecondaryVerificationHelper).GetField("_isEnabled",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .SetValue(null, (bool?)null);
                Assert.IsFalse(await SecondaryVerificationHelper.GetEnabled(), "机器状态重读应与写入值一致");
            }
            finally
            {
                await SecondaryVerificationHelper.SetEnabledAsync(before); // 还原宿主机状态（尽力而为）
            }
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(null)]
        [DataRow(false)]
        public async Task VerifyAccessAsync_MapsVerifierResult(bool? verifierResult)
        {
            // fix batch3 #6：VerifyAccessAsync 是开关翻转前的验证门（WPF 平价，
            // GeneralSettingView.xaml.cs:36 同款语义）——注入 verifier 桩，绝不触发真实 UI：
            // true → true；null（用户取消）/ false（失败）→ 一律 false（未通过）
            var result = await WebUiSettingsService.VerifyAccessAsync(() => Task.FromResult(verifierResult));
            Assert.AreEqual(verifierResult == true, result,
                $"verifier 返回 {verifierResult ?? (object)"null"} 时 VerifyAccessAsync 应返回 {verifierResult == true}");
        }

        [TestMethod]
        public async Task ToggleFlow_VerifyThenPut_AppliesImmediately()
        {
            // fix batch3 #6 端到端语义：开关点击立即生效的前端时序 = 先 VerifyAccessAsync
            // （验证门）→ 通过才 PUT requireSecondaryVerification（立即落地）。
            // 验证通过路径：GetEnabled 随写入同步翻转（无需点保存按钮——owner 三次反馈的根因）；
            // 验证取消/失败路径：VerifyAccessAsync==false，前端不提交、开关回弹。
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var before = await SecondaryVerificationHelper.GetEnabled();
            try
            {
                // 验证门通过 → 立即提交翻转（WPF 平价：验证通过才翻转）
                Assert.IsTrue(await WebUiSettingsService.VerifyAccessAsync(() => Task.FromResult<bool?>(true)),
                    "verifier=true（验证通过）应放行");
                var on = await WebUiSettingsService.ApplyGeneralAsync(cs,
                    new GeneralSettingsUpdateRequest { RequireSecondaryVerification = true });
                Assert.AreEqual(SettingsApplyStatus.Ok, on.Status);
                Assert.IsTrue(on.Dto!.RequireSecondaryVerification, "PUT 后响应回读值应为 true");
                Assert.IsTrue(await SecondaryVerificationHelper.GetEnabled(), "提交返回时开关已立即生效（无需再点保存）");

                // 验证门拒绝：null=用户取消 / false=失败 → 均未通过（前端开关回弹，不提交）
                Assert.IsFalse(await WebUiSettingsService.VerifyAccessAsync(() => Task.FromResult<bool?>(null)),
                    "verifier=null（用户取消）应判未通过");
                Assert.IsFalse(await WebUiSettingsService.VerifyAccessAsync(() => Task.FromResult<bool?>(false)),
                    "verifier=false（验证失败）应判未通过");
            }
            finally
            {
                await SecondaryVerificationHelper.SetEnabledAsync(before); // 还原宿主机状态（尽力而为）
            }
        }

        [TestMethod]
        public async Task PutGeneral_Language_SwitchAndRestore()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var original = cs.General.CurrentLanguageCode;
            try
            {
                // web 端大写 locale（zh-CN）→ 归一为 GeneralConfig 小写码（zh-cn）
                var (code, body) = await PutAsync("/api/settings/general", "{\"language\":\"zh-CN\"}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                Assert.AreEqual("zh-cn", cs.General.CurrentLanguageCode);
                var root = await GetJsonAsync("/api/settings/general");
                Assert.AreEqual("zh-cn", root.GetProperty("language").GetString());

                // 换到 en-us 再验证（真实语言码间切换，往返安全）
                (code, body) = await PutAsync("/api/settings/general", "{\"language\":\"en-us\"}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                root = await GetJsonAsync("/api/settings/general");
                Assert.AreEqual("en-us", root.GetProperty("language").GetString());
            }
            finally
            {
                cs.General.CurrentLanguageCode = original;
                cs.Save();
            }
        }

        [TestMethod]
        public async Task PutGeneral_InvalidLanguage_Returns400AndKeepsValue()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var original = cs.General.CurrentLanguageCode;
            var (code, _) = await PutAsync("/api/settings/general", "{\"language\":\"xx-yy\"}");
            Assert.AreEqual(HttpStatusCode.BadRequest, code);
            Assert.AreEqual(original, cs.General.CurrentLanguageCode, "非法 language 不得写入");
        }

        [TestMethod]
        public async Task PutGeneral_InvalidEnumValues_Return400AndKeepValues()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var origClose = cs.General.CloseButtonBehavior;
            var origLog = cs.General.LogLevel;
            try
            {
                // closeButtonBehavior 超出 EnumCloseButtonBehavior {Exit=0, Minimize=1}
                var (c1, b1) = await PutAsync("/api/settings/general", "{\"closeButtonBehavior\":7}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c1, b1);

                // logLevel 超出 EnumLogLevel {Debug..Disabled = 0..5}
                var (c2, b2) = await PutAsync("/api/settings/general", "{\"logLevel\":99}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c2, b2);

                Assert.AreEqual(origClose, cs.General.CloseButtonBehavior);
                Assert.AreEqual(origLog, cs.General.LogLevel);

                // 合法值可写（0=Exit / 1=Info 均为定义值）
                var (c3, _) = await PutAsync("/api/settings/general", "{\"closeButtonBehavior\":0,\"logLevel\":1}");
                Assert.AreEqual(HttpStatusCode.OK, c3);
                Assert.AreEqual(0, cs.General.CloseButtonBehavior);
                Assert.AreEqual(1, cs.General.LogLevel);
            }
            finally
            {
                cs.General.CloseButtonBehavior = origClose;
                cs.General.LogLevel = origLog;
                SimpleLogHelper.WriteLogLevel = (SimpleLogHelper.EnumLogLevel)origLog;
                SimpleLogHelper.PrintLogLevel = (SimpleLogHelper.EnumLogLevel)origLog;
                cs.Save();
            }
        }

        [TestMethod]
        public async Task PutGeneral_EmptyBody_IsNoOp()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var before = await GetJsonAsync("/api/settings/general");
            var (code, _) = await PutAsync("/api/settings/general", "{}");
            Assert.AreEqual(HttpStatusCode.OK, code, "空对象 = 部分更新无键，应为无操作 200");
            var after = await GetJsonAsync("/api/settings/general");
            Assert.AreEqual(before.GetRawText(), after.GetRawText());
        }

        // ------------------------------------------------------------------
        // /api/settings/launcher
        // ------------------------------------------------------------------

        [TestMethod]
        public async Task GetLauncher_ReturnsFiveFields()
        {
            var root = await GetJsonAsync("/api/settings/launcher");
            AssertIsBool(root.GetProperty("launcherEnabled"), "launcherEnabled");
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
                root.GetProperty("hotKeyModifiers").GetString() ?? "",
                "^(None|Control|Shift|Alt|Windows|ShiftControl|ShiftWindows|ShiftAlt|WindowsControl|WindowsAlt|ControlAlt)$"),
                "hotKeyModifiers 线格式应为 HotkeyModifierKeys 枚举成员名");
            Assert.IsFalse(string.IsNullOrEmpty(root.GetProperty("hotKeyKey").GetString()), "hotKeyKey 应为 Key 枚举成员名（如 M）");
            AssertIsBool(root.GetProperty("showCredentials"), "showCredentials");
            AssertIsBool(root.GetProperty("allowSaveInfoInQuickConnect"), "allowSaveInfoInQuickConnect");
        }

        [TestMethod]
        public async Task PutLauncher_RoundTrip_TryGetNullPathReturns200()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var orig = SnapshotLauncher(cs);
            try
            {
                // 测试环境未注册 LauncherWindowViewModel → 重注册静默跳过，PUT 仍应 200（不崩溃）
                var (code, body) = await PutAsync("/api/settings/launcher",
                    "{\"showCredentials\":false,\"allowSaveInfoInQuickConnect\":false,\"hotKeyModifiers\":\"Ctrl+Alt\",\"hotKeyKey\":\"K\"}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);

                Assert.IsFalse(cs.Launcher.ShowCredentials);
                Assert.IsFalse(cs.Launcher.AllowSaveInfoInQuickConnect);
                Assert.AreEqual(_1RM.Service.HotkeyModifierKeys.ControlAlt, cs.Launcher.HotKeyModifiers,
                    "显示形态 Ctrl+Alt 应解析为枚举成员 ControlAlt");
                Assert.AreEqual(System.Windows.Input.Key.K, cs.Launcher.HotKeyKey);

                var root = await GetJsonAsync("/api/settings/launcher");
                Assert.AreEqual("ControlAlt", root.GetProperty("hotKeyModifiers").GetString(),
                    "GET 应返回枚举成员名（线格式）");
                Assert.AreEqual("K", root.GetProperty("hotKeyKey").GetString());
                Assert.IsFalse(root.GetProperty("showCredentials").GetBoolean());
                Assert.IsFalse(root.GetProperty("allowSaveInfoInQuickConnect").GetBoolean());
                Assert.AreEqual(orig.LauncherEnabled, root.GetProperty("launcherEnabled").GetBoolean(),
                    "未提供的 launcherEnabled 不得被改动");
            }
            finally
            {
                RestoreLauncher(cs, orig);
            }
        }

        [TestMethod]
        public async Task PutLauncher_AcceptsMemberNameAndDisplayForms()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var orig = SnapshotLauncher(cs);
            try
            {
                // 枚举成员名（大小写不敏感）
                var (c1, b1) = await PutAsync("/api/settings/launcher", "{\"hotKeyModifiers\":\"Shift\"}");
                Assert.AreEqual(HttpStatusCode.OK, c1, b1);
                Assert.AreEqual(_1RM.Service.HotkeyModifierKeys.Shift, cs.Launcher.HotKeyModifiers);

                // 显示形态 + 乱序 token + 大小写/空格宽容
                var (c2, b2) = await PutAsync("/api/settings/launcher", "{\"hotKeyModifiers\":\" win + ctrl \"}");
                Assert.AreEqual(HttpStatusCode.OK, c2, b2);
                Assert.AreEqual(_1RM.Service.HotkeyModifierKeys.WindowsControl, cs.Launcher.HotKeyModifiers);
            }
            finally
            {
                RestoreLauncher(cs, orig);
            }
        }

        [TestMethod]
        public async Task PutLauncher_InvalidValues_Return400AndKeepValues()
        {
            var cs = _1RM.IoC.Get<ConfigurationService>();
            var orig = SnapshotLauncher(cs);
            try
            {
                var (c1, b1) = await PutAsync("/api/settings/launcher", "{\"hotKeyModifiers\":\"Ctrl+Shift+Alt\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c1, "未定义的组合（无对应枚举成员）应 400: " + b1);

                var (c2, b2) = await PutAsync("/api/settings/launcher", "{\"hotKeyModifiers\":\"Meta\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c2, "未知修饰键 token 应 400: " + b2);

                var (c3, b3) = await PutAsync("/api/settings/launcher", "{\"hotKeyModifiers\":\"None\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c3, "无修饰键（None）应 400（与 WPF UI 不提供 None 一致）: " + b3);

                var (c4, b4) = await PutAsync("/api/settings/launcher", "{\"hotKeyKey\":\"NotAKey\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c4, "未知 Key 应 400: " + b4);

                var (c5, b5) = await PutAsync("/api/settings/launcher", "{\"hotKeyKey\":\"None\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c5, "Key.None 应 400: " + b5);

                // 任一键非法 → 整体零写入
                Assert.AreEqual(orig.HotKeyModifiers, cs.Launcher.HotKeyModifiers);
                Assert.AreEqual(orig.HotKeyKey, cs.Launcher.HotKeyKey);
            }
            finally
            {
                RestoreLauncher(cs, orig);
            }
        }

        private sealed record LauncherSnapshot(bool LauncherEnabled, _1RM.Service.HotkeyModifierKeys HotKeyModifiers,
            System.Windows.Input.Key HotKeyKey, bool ShowCredentials, bool AllowSaveInfoInQuickConnect);

        private static LauncherSnapshot SnapshotLauncher(ConfigurationService cs)
            => new(cs.Launcher.LauncherEnabled, cs.Launcher.HotKeyModifiers, cs.Launcher.HotKeyKey,
                cs.Launcher.ShowCredentials, cs.Launcher.AllowSaveInfoInQuickConnect);

        private static void RestoreLauncher(ConfigurationService cs, LauncherSnapshot s)
        {
            cs.Launcher.LauncherEnabled = s.LauncherEnabled;
            cs.Launcher.HotKeyModifiers = s.HotKeyModifiers;
            cs.Launcher.HotKeyKey = s.HotKeyKey;
            cs.Launcher.ShowCredentials = s.ShowCredentials;
            cs.Launcher.AllowSaveInfoInQuickConnect = s.AllowSaveInfoInQuickConnect;
            cs.Save();
        }

        // ------------------------------------------------------------------
        // /api/tags/manage（列表/置顶）、/api/tags/rename、DELETE /api/tags/{name}
        // ------------------------------------------------------------------

        private static string NewTag() => "t-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>种子 n 台带同一标签的服务器到 Local，返回 (id, tag)。</summary>
        private static List<string> SeedServersWithTag(string tag, int count)
        {
            var gd = _1RM.IoC.Get<GlobalData>();
            var local = _1RM.IoC.Get<DataSourceService>().LocalDataSource;
            Assert.IsNotNull(local);
            var ids = new List<string>();
            lock (gd)
            {
                for (var i = 0; i < count; i++)
                {
                    var id = "srv-tag-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    var ret = gd.AddServer(new RDP
                    {
                        Id = id,
                        DisplayName = "tag-seed-" + i + "-" + tag,
                        Address = "9.9.9.9",
                        Tags = new List<string> { tag },
                    }, local);
                    Assert.IsTrue(ret.IsSuccess, "种子服务器写入失败: " + ret.ErrorInfo);
                    ids.Add(id);
                }
            }
            return ids;
        }

        private static void CleanupServers(IEnumerable<string> ids)
        {
            var gd = _1RM.IoC.Get<GlobalData>();
            lock (gd)
            {
                var servers = gd.VmItemList
                    .Where(vm => ids.Contains(vm.Server.Id))
                    .Select(vm => vm.Server)
                    .ToList();
                if (servers.Count > 0)
                    gd.DeleteServer(servers);
            }
        }

        private static async Task<JsonElement> GetManageListAsync(string ds = "Local")
            => await GetJsonAsync("/api/tags/manage?ds=" + Uri.EscapeDataString(ds));

        [TestMethod]
        public async Task GetTagsManage_ReturnsNameCountPinned_PerDataSource()
        {
            var tag = NewTag();
            var ids = SeedServersWithTag(tag, 2);
            try
            {
                var root = await GetManageListAsync();
                var item = root.EnumerateArray().FirstOrDefault(x => x.GetProperty("name").GetString() == tag);
                Assert.AreNotEqual(JsonValueKind.Undefined, item.ValueKind, "manage 列表应含种子标签");
                Assert.AreEqual(2, item.GetProperty("count").GetInt32(), "计数 = 该数据源下带此标签的服务器数");
                Assert.IsFalse(item.GetProperty("pinned").GetBoolean());
            }
            finally
            {
                CleanupServers(ids);
                LocalityTagService.GetAndRemoveTag(tag);
            }
        }

        [TestMethod]
        public async Task GetTagsManage_UnknownDataSource_Returns404()
        {
            var resp = await _client.GetAsync("/api/tags/manage?ds=no-such-ds");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [TestMethod]
        public async Task RenameTag_UpdatesAllServersInDataSource()
        {
            var tag = NewTag();
            var newTag = tag + "-renamed";
            var ids = SeedServersWithTag(tag, 2);
            try
            {
                var (code, body) = await PostAsync("/api/tags/rename",
                    $"{{\"ds\":\"Local\",\"from\":\"{tag}\",\"to\":\"{newTag}\"}}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);
                using var doc = JsonDocument.Parse(body);
                Assert.AreEqual(2, doc.RootElement.GetProperty("updated").GetInt32());

                // 两台服务器的 Tags 都已替换（经配置读取验证——同 WPF 编辑器保存后的库内状态）
                foreach (var id in ids)
                {
                    var cfg = await _client.GetStringAsync($"/api/servers/{id}/config?ds=Local");
                    StringAssert.Contains(cfg, $"\"{newTag}\"");
                    Assert.IsFalse(cfg.Contains($"\"{tag}\""), "旧标签名应已从服务器移除");
                }

                // manage 列表：新名在列（count=2），旧名消失
                var list = await GetManageListAsync();
                Assert.IsTrue(list.EnumerateArray().Any(x => x.GetProperty("name").GetString() == newTag && x.GetProperty("count").GetInt32() == 2));
                Assert.IsFalse(list.EnumerateArray().Any(x => x.GetProperty("name").GetString() == tag));
            }
            finally
            {
                CleanupServers(ids);
                LocalityTagService.GetAndRemoveTag(newTag);
                LocalityTagService.GetAndRemoveTag(tag);
            }
        }

        [TestMethod]
        public async Task RenameTag_CarriesPinnedState()
        {
            var tag = NewTag();
            var newTag = tag + "-pinned";
            var ids = SeedServersWithTag(tag, 1);
            try
            {
                // 先置顶再改名：pin 状态应随 LocalityTagService 改名迁移（WPF CmdTagRename 语义）
                var (pinCode, pinBody) = await PutAsync("/api/tags/manage",
                    $"{{\"ds\":\"Local\",\"name\":\"{tag}\",\"pinned\":true}}");
                Assert.AreEqual(HttpStatusCode.OK, pinCode, pinBody);

                var (code, body) = await PostAsync("/api/tags/rename",
                    $"{{\"ds\":\"Local\",\"from\":\"{tag}\",\"to\":\"{newTag}\"}}");
                Assert.AreEqual(HttpStatusCode.OK, code, body);

                Assert.IsNull(LocalityTagService.GetTag(tag), "旧名应已从 locality 移除");
                Assert.IsTrue(LocalityTagService.GetTag(newTag)?.IsPinned == true, "置顶状态应迁移到新名");

                var list = await GetManageListAsync();
                Assert.IsTrue(list.EnumerateArray().Any(x => x.GetProperty("name").GetString() == newTag && x.GetProperty("pinned").GetBoolean()));
            }
            finally
            {
                CleanupServers(ids);
                LocalityTagService.GetAndRemoveTag(newTag);
                LocalityTagService.GetAndRemoveTag(tag);
            }
        }

        [TestMethod]
        public async Task RenameTag_InvalidTargets_Return400()
        {
            var tagA = NewTag();
            var tagB = NewTag();
            var ids = SeedServersWithTag(tagA, 1);
            ids.AddRange(SeedServersWithTag(tagB, 1));
            try
            {
                // 目标为空（RectifyTagName 后为空串）
                var (c1, b1) = await PostAsync("/api/tags/rename", $"{{\"ds\":\"Local\",\"from\":\"{tagA}\",\"to\":\"  \"}}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c1, b1);

                // 目标与源同名
                var (c2, b2) = await PostAsync("/api/tags/rename", $"{{\"ds\":\"Local\",\"from\":\"{tagA}\",\"to\":\"{tagA}\"}}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c2, b2);

                // 目标已存在（该数据源下已有 tagB）
                var (c3, b3) = await PostAsync("/api/tags/rename", $"{{\"ds\":\"Local\",\"from\":\"{tagA}\",\"to\":\"{tagB}\"}}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c3, b3);

                // 未知源标签 → 404
                var (c4, b4) = await PostAsync("/api/tags/rename", $"{{\"ds\":\"Local\",\"from\":\"no-such-tag-{Guid.NewGuid():N}\",\"to\":\"whatever\"}}");
                Assert.AreEqual(HttpStatusCode.NotFound, c4, b4);

                // 未知数据源 → 400
                var (c5, b5) = await PostAsync("/api/tags/rename", "{\"ds\":\"no-such-ds\",\"from\":\"a\",\"to\":\"b\"}");
                Assert.AreEqual(HttpStatusCode.BadRequest, c5, b5);
            }
            finally
            {
                CleanupServers(ids);
                LocalityTagService.GetAndRemoveTag(tagA);
                LocalityTagService.GetAndRemoveTag(tagB);
            }
        }

        [TestMethod]
        public async Task TagPin_TogglePersists()
        {
            var tag = NewTag();
            var ids = SeedServersWithTag(tag, 1);
            try
            {
                var (c1, b1) = await PutAsync("/api/tags/manage", $"{{\"ds\":\"Local\",\"name\":\"{tag}\",\"pinned\":true}}");
                Assert.AreEqual(HttpStatusCode.OK, c1, b1);
                using var doc1 = JsonDocument.Parse(b1);
                Assert.IsTrue(doc1.RootElement.GetProperty("pinned").GetBoolean());

                Assert.IsTrue(LocalityTagService.GetTag(tag)?.IsPinned == true, "置顶应落盘 .tags.json（locality）");
                var list = await GetManageListAsync();
                Assert.IsTrue(list.EnumerateArray().Single(x => x.GetProperty("name").GetString() == tag).GetProperty("pinned").GetBoolean());

                var (c2, b2) = await PutAsync("/api/tags/manage", $"{{\"ds\":\"Local\",\"name\":\"{tag}\",\"pinned\":false}}");
                Assert.AreEqual(HttpStatusCode.OK, c2, b2);
                Assert.IsFalse(LocalityTagService.GetTag(tag)?.IsPinned == true, "取消置顶应同步 locality");
            }
            finally
            {
                CleanupServers(ids);
                LocalityTagService.GetAndRemoveTag(tag);
            }
        }

        [TestMethod]
        public async Task TagPin_UnknownTag_Returns404()
        {
            var (code, _) = await PutAsync("/api/tags/manage",
                $"{{\"ds\":\"Local\",\"name\":\"no-such-tag-{Guid.NewGuid():N}\",\"pinned\":true}}");
            Assert.AreEqual(HttpStatusCode.NotFound, code);
        }

        [TestMethod]
        public async Task DeleteTag_RemovesFromAllServers_AndCleansPinnedState()
        {
            var tag = NewTag();
            var ids = SeedServersWithTag(tag, 2);
            var (pinCode, _) = await PutAsync("/api/tags/manage", $"{{\"ds\":\"Local\",\"name\":\"{tag}\",\"pinned\":true}}");
            Assert.AreEqual(HttpStatusCode.OK, pinCode);
            try
            {
                var resp = await _client.DeleteAsync($"/api/tags/{Uri.EscapeDataString(tag)}?ds=Local");
                Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);

                // 两台服务器均不再带此标签
                foreach (var id in ids)
                {
                    var cfg = await _client.GetStringAsync($"/api/servers/{id}/config?ds=Local");
                    Assert.IsFalse(cfg.Contains($"\"{tag}\""), "标签应已从服务器移除");
                }

                // 聚合标签列表不再含此标签
                var tags = await GetJsonAsync("/api/tags");
                Assert.IsFalse(tags.EnumerateArray().Any(x => x.GetProperty("name").GetString() == tag));

                // 置顶信息清理（plan 要求；WPF CmdTagDelete 不清理，属有意补齐）
                Assert.IsNull(LocalityTagService.GetTag(tag), "删除后置顶状态应被清理");

                var manage = await GetManageListAsync();
                Assert.IsFalse(manage.EnumerateArray().Any(x => x.GetProperty("name").GetString() == tag));
            }
            finally
            {
                CleanupServers(ids);
                LocalityTagService.GetAndRemoveTag(tag);
            }
        }

        [TestMethod]
        public async Task DeleteTag_Unknown_Returns404()
        {
            var resp = await _client.DeleteAsync($"/api/tags/no-such-tag-{Guid.NewGuid():N}?ds=Local");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        }
    }
}
