using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shawn.Utils.Wpf.Image;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// WebUiEndpoints 分域：设置域（均读写桌面端既有配置服务，Web 端只做校验与映射）。
    /// ─ GET/PUT /api/settings/general    常规设置（GeneralConfig 白名单字段，部分更新）
    /// ─ POST   /api/settings/verify      验证开关翻转前的 WPF 平价安全门（Windows 凭据/Hello）
    /// ─ GET/PUT /api/settings/launcher   启动器设置（含热键，写后重注册）
    /// ─ GET/PUT /api/settings/runners    协议运行器配置（整体往返 ProtocolSettings）
    /// ─ GET/PUT /api/settings/appearance 外观设置（Web UI 专属字段，独立于 WPF ThemeConfig）
    /// 注册顺序由主文件 MapAll 统一编排（与拆分前一致）。
    /// </summary>
    public static partial class WebUiEndpoints
    {
        /// <summary>
        /// 外观取值域（spec §4）。校验大小写不敏感，存储时归一：
        /// themeMode/accent 归一小写、fontSize 归一大写，GET 返回值即规范形式。
        /// </summary>
        private static readonly HashSet<string> ValidThemeModes = new(StringComparer.OrdinalIgnoreCase)
            { "dark", "light", "system" };
        private static readonly HashSet<string> ValidAccents = new(StringComparer.OrdinalIgnoreCase)
            { "blue", "violet", "pink", "red", "orange", "green", "slate" };
        private static readonly HashSet<string> ValidFontSizes = new(StringComparer.OrdinalIgnoreCase)
            { "S", "M", "L", "XL" };

        internal static void MapSettingsRunners(WebApplication app)
        {
            // 运行器配置（Plan 3 Task 3）：整体往返 ProtocolSettings（含 SelectedRunnerName），
            // runners 数组 PascalCase + $type 直通（与 ProtocolConfigurationService 的 Newtonsoft
            // 持久化同一路径）。GET 6 协议；PUT 缺失协议 = 保持，未知协议键/runners 空 → 400 零写入。
            app.MapGet("/api/settings/runners", () =>
            {
                var pcs = IoC.Get<ProtocolConfigurationService>();
                return Results.Json(new { protocols = WebUiDataSourceService.ReadRunners(pcs) });
            });

            app.MapPut("/api/settings/runners", (RunnersSaveRequest? body) =>
            {
                var pcs = IoC.Get<ProtocolConfigurationService>();
                var result = WebUiDataSourceService.ApplyRunners(pcs, body?.Protocols);
                if (!result.IsOk)
                    return Results.BadRequest(new { errors = result.Errors });
                return Results.Json(new { protocols = WebUiDataSourceService.ReadRunners(pcs) });
            });
        }

        internal static void MapSettingsGeneral(WebApplication app)
        {
            // 常规设置（Plan 3 Task 2）：读 GeneralConfig 白名单字段 + SecondaryVerificationHelper
            // （requireSecondaryVerification 的真实状态不在 GeneralConfig，见 WebUiSettingsService 类注释）。
            // PUT 为部分更新（缺失键=保持不变），任一键非法 400 零写入；language 变更同步调
            // LanguageService.SetLanguage 让 WPF 即时生效（未注册时静默跳过）。
            app.MapGet("/api/settings/general", async () =>
            {
                var cs = IoC.Get<ConfigurationService>();
                return Results.Json(await WebUiSettingsService.ReadGeneralAsync(cs));
            });

            app.MapPut("/api/settings/general", async (GeneralSettingsUpdateRequest? body) =>
            {
                var result = await WebUiSettingsService.ApplyGeneralAsync(IoC.Get<ConfigurationService>(), body);
                return result.Status switch
                {
                    SettingsApplyStatus.Ok => Results.Json(result.Dto),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });
        }

        internal static void MapSettingsVerify(WebApplication app)
        {
            // 验证开关翻转前的 WPF 平价安全门（fix batch3 #6）：POST /api/settings/verify。
            // requireSecondaryVerification 开关前端改为点击立即生效——对齐 WPF GeneralSettingView
            // （翻转前先过一次 Windows 凭据/Hello 验证）；此处触发该验证，当前未开启验证时
            // VerifyAsyncUi 直通 true（无感知）。仅 true 放行 200；null=用户取消/false=失败 → 403
            // （前端提示后开关回弹，不提交翻转）。
            app.MapPost("/api/settings/verify", async () =>
            {
                return await WebUiSettingsService.VerifyAccessAsync()
                    ? Results.Ok(new { verified = true })
                    : Results.StatusCode(403);
            });
        }

        internal static void MapSettingsLauncher(WebApplication app)
        {
            // 启动器设置：热键为 WPF 枚举，线格式 = 成员名（"ControlAlt"/"M"），PUT 亦接受 "Ctrl+Alt"
            // 显示形态。写后重注册热键：注册失败且 launcherEnabled → 409（配置已保存，与 WPF
            // “内存先行落值 + 警告”语义对齐，见 WebUiSettingsService.ApplyLauncher 注释）。
            app.MapGet("/api/settings/launcher", () =>
            {
                var cs = IoC.Get<ConfigurationService>();
                return Results.Json(WebUiSettingsService.ReadLauncher(cs));
            });

            app.MapPut("/api/settings/launcher", (LauncherSettingsUpdateRequest? body) =>
            {
                var result = WebUiSettingsService.ApplyLauncher(IoC.Get<ConfigurationService>(), body);
                return result.Status switch
                {
                    SettingsApplyStatus.Ok => Results.Json(result.Dto),
                    SettingsApplyStatus.HotkeyConflict => Results.Json(new { error = "hotkey conflict", settings = result.Dto }, statusCode: 409),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });
        }

        internal static void MapSettingsAppearance(WebApplication app)
        {
            // 外观设置（Web UI 专属字段，独立于 WPF ThemeConfig，持久化到 1Remote.json，spec §4）。
            // GET 返回当前值（旧配置缺字段时由 Configuration 属性初始化器给默认 dark/blue/M）
            app.MapGet("/api/settings/appearance", () =>
            {
                var cs = IoC.Get<ConfigurationService>();
                return Results.Json(ReadAppearance(cs));
            });

            // PUT 校验通过后走 ConfigurationService.Save()（既有保存路径，落盘 1Remote.json），
            // 返回 200 + 归一后的存储值；任一字段非法即 400，不写入任何值。
            // font 为自由取值（字体族名不枚举校验）：空串 = 跟随系统，非空 trim 后存储
            app.MapPut("/api/settings/appearance", (AppearanceDto? dto) =>
            {
                var themeMode = dto?.ThemeMode?.Trim() ?? string.Empty;
                var accent = dto?.Accent?.Trim() ?? string.Empty;
                var fontSize = dto?.FontSize?.Trim() ?? string.Empty;
                var font = dto?.Font?.Trim() ?? string.Empty;
                if (!ValidThemeModes.Contains(themeMode))
                    return Results.BadRequest(new { error = $"invalid themeMode '{themeMode}', expected one of: dark, light, system" });
                if (!ValidAccents.Contains(accent))
                    return Results.BadRequest(new { error = $"invalid accent '{accent}', expected one of: blue, violet, pink, red, orange, green, slate" });
                if (!ValidFontSizes.Contains(fontSize))
                    return Results.BadRequest(new { error = $"invalid fontSize '{fontSize}', expected one of: S, M, L, XL" });

                var cs = IoC.Get<ConfigurationService>();
                cs.WebUiThemeMode = themeMode.ToLowerInvariant();
                cs.WebUiAccent = accent.ToLowerInvariant();
                cs.WebUiFontSize = fontSize.ToUpperInvariant();
                cs.WebUiFontFamily = font;
                cs.Save();
                return Results.Json(ReadAppearance(cs));
            });
        }

        private static AppearanceDto ReadAppearance(ConfigurationService cs)
        {
            return new AppearanceDto
            {
                ThemeMode = cs.WebUiThemeMode,
                Accent = cs.WebUiAccent,
                FontSize = cs.WebUiFontSize,
                Font = cs.WebUiFontFamily,
            };
        }
    }
}
