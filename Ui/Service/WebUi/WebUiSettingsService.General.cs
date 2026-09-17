using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Shawn.Utils;
using Stylet;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// WebUiSettingsService 分域：常规设置（general）域。
    /// ─ GET/PUT /api/settings/general 常规设置快照与白名单部分更新
    /// ─ POST   /api/settings/verify   requireSecondaryVerification 翻转前的 WPF 平价安全门
    ///
    /// 安全域（白名单语义）：只暴露非破坏性字段（语言/关闭行为/确认开关/日志级别/tab 选项/
    /// 复制选项）；开机自启（写注册表）、便携模式、SQLite 路径不进白名单。
    ///
    /// requireSecondaryVerification 不在 GeneralConfig：真实状态在 SecondaryVerificationHelper——
    /// 读 await GetEnabled()，写 await SetEnabledAsync(bool)（写注册表/凭据管理器机器状态；
    /// 可等待版本在返回前完成写入并使静态缓存与机器状态一致，响应值即回读 GetEnabled()，
    /// fix #13：原 async void fire-and-forget 存在“响应已返回但缓存/机器状态尚未落地”的竞态窗口）。
    ///
    /// language 变更：归一小写码后校验 14 个内置语言（LanguagesResources.Files 同源，即主文件
    /// SupportedLanguageCodes），写配置后调 LanguageService.SetLanguage 让 WPF 侧即时生效
    /// （GeneralSettingViewModel.Language 同款顺序：先 SetLanguage 再 Save）；
    /// 无 WPF 环境（测试）未注册 LanguageService 时静默跳过。
    /// </summary>
    public static partial class WebUiSettingsService
    {
        /// <summary>读取 general 设置快照（requireSecondaryVerification 读 SecondaryVerificationHelper）。</summary>
        public static async Task<GeneralSettingsDto> ReadGeneralAsync(ConfigurationService cs)
        {
            var dto = ReadGeneralConfig(cs);
            dto.RequireSecondaryVerification = await SecondaryVerificationHelper.GetEnabled();
            return dto;
        }

        /// <summary>GeneralConfig → DTO 字段映射（不含 requireSecondaryVerification——它不在 GeneralConfig，由调用方单独读 SecondaryVerificationHelper）。</summary>
        private static GeneralSettingsDto ReadGeneralConfig(ConfigurationService cs)
        {
            return new GeneralSettingsDto
            {
                Language = cs.General.CurrentLanguageCode,
                CloseButtonBehavior = cs.General.CloseButtonBehavior,
                ConfirmBeforeClosingSession = cs.General.ConfirmBeforeClosingSession,
                ShowSessionIconInSessionWindow = cs.General.ShowSessionIconInSessionWindow,
                LogLevel = cs.General.LogLevel,
                TabWindowCloseButtonOnLeft = cs.General.TabWindowCloseButtonOnLeft,
                TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow = cs.General.TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow,
                CopyPortWhenCopyAddress = cs.General.CopyPortWhenCopyAddress,
                DoNotCheckNewVersion = cs.General.DoNotCheckNewVersion,
            };
        }

        /// <summary>
        /// PUT /api/settings/general：白名单部分更新。先整体校验（任一键非法 → 400 零写入），
        /// 再写内存配置 + ConfigurationService.Save()（落 1Remote.json）。
        /// </summary>
        public static async Task<GeneralSettingsResult> ApplyGeneralAsync(ConfigurationService cs, GeneralSettingsUpdateRequest? input)
        {
            if (input == null)
                return GeneralSettingsResult.BadRequest("body must be a JSON object");

            var errors = new List<string>();
            var language = input.Language?.Trim().ToLowerInvariant();
            if (language != null && !SupportedLanguageCodes.Contains(language))
            {
                errors.Add($"language: '{input.Language}' is not supported, expected one of: {string.Join(", ", SupportedLanguageCodes.OrderBy(x => x))}");
            }
            if (input.CloseButtonBehavior != null
                && !Enum.IsDefined(typeof(GeneralConfig.EnumCloseButtonBehavior), input.CloseButtonBehavior.Value))
            {
                errors.Add($"closeButtonBehavior: {input.CloseButtonBehavior} is not a valid value (0=Exit, 1=Minimize)");
            }
            if (input.LogLevel != null
                && !Enum.IsDefined(typeof(SimpleLogHelper.EnumLogLevel), input.LogLevel.Value))
            {
                errors.Add($"logLevel: {input.LogLevel} is not a valid value (0=Debug, 1=Info, 2=Warning, 3=Error, 4=Fatal, 5=Disabled)");
            }
            if (errors.Count > 0)
                return GeneralSettingsResult.BadRequest(errors);

            if (language != null && language != cs.General.CurrentLanguageCode)
            {
                cs.General.CurrentLanguageCode = language;
                // WPF 侧即时生效；未注册（测试环境）时静默跳过
                IoC.TryGet<LanguageService>()?.SetLanguage(language);
            }
            if (input.CloseButtonBehavior != null) cs.General.CloseButtonBehavior = input.CloseButtonBehavior.Value;
            if (input.ConfirmBeforeClosingSession != null) cs.General.ConfirmBeforeClosingSession = input.ConfirmBeforeClosingSession.Value;
            if (input.ShowSessionIconInSessionWindow != null) cs.General.ShowSessionIconInSessionWindow = input.ShowSessionIconInSessionWindow.Value;
            if (input.TabWindowCloseButtonOnLeft != null) cs.General.TabWindowCloseButtonOnLeft = input.TabWindowCloseButtonOnLeft.Value;
            if (input.TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow != null) cs.General.TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow = input.TabWindowSetFocusToLocalDesktopOnMouseLeaveRdpWindow.Value;
            if (input.CopyPortWhenCopyAddress != null) cs.General.CopyPortWhenCopyAddress = input.CopyPortWhenCopyAddress.Value;
            if (input.DoNotCheckNewVersion != null) cs.General.DoNotCheckNewVersion = input.DoNotCheckNewVersion.Value;
            if (input.LogLevel != null)
            {
                cs.General.LogLevel = input.LogLevel.Value;
                var level = (SimpleLogHelper.EnumLogLevel)input.LogLevel.Value;
                if (SimpleLogHelper.WriteLogLevel != level)
                {
                    // 与 WPF GeneralSettingViewModel.LogLevel 一致：写库同时立即调整运行时日志级别
                    SimpleLogHelper.WriteLogLevel = SimpleLogHelper.PrintLogLevel = level;
                }
            }
            if (input.RequireSecondaryVerification != null)
            {
                // 可等待写入（fix #13）：返回前完成机器状态写入 + 缓存一致性刷新，
                // 消除 async void fire-and-forget 的竞态窗口
                await SecondaryVerificationHelper.SetEnabledAsync(input.RequireSecondaryVerification.Value);
            }

            cs.Save();
            var dto = ReadGeneralConfig(cs);
            // 写入已 await 完成，回读即为真值（部分写入失败时 SetEnabledAsync 已按机器
            // 实际状态刷新缓存，回读与重启后的首读一致）
            dto.RequireSecondaryVerification = await SecondaryVerificationHelper.GetEnabled();
            return GeneralSettingsResult.Ok(dto);
        }

        /// <summary>
        /// POST /api/settings/verify：立即触发一次 Windows 凭据/Windows Hello 验证——
        /// requireSecondaryVerification 开关翻转前的 WPF 平价安全门（GeneralSettingView.xaml.cs:36
        /// 同款：翻转前先 VerifyAsyncUi；当前未开启验证时 VerifyAsyncUi 直通 true、无感知）。
        /// verifier 可注入（测试桩，绝不触发真实 UI）；true=通过，null=用户取消/false=失败 → 均视为未通过。
        /// </summary>
        public static async Task<bool> VerifyAccessAsync(Func<Task<bool?>>? verifier = null)
        {
            // 注入模式与 WebUiImportExportService 导出验证门一致（?? () => VerifyAsyncUi()）
            var verify = verifier ?? (() => SecondaryVerificationHelper.VerifyAsyncUi());
            return await verify() == true;
        }
    }

    public sealed class GeneralSettingsResult
    {
        public SettingsApplyStatus Status { get; private init; }
        public List<string> Errors { get; private init; } = new();
        public GeneralSettingsDto? Dto { get; private init; }

        public static GeneralSettingsResult Ok(GeneralSettingsDto dto) => new() { Status = SettingsApplyStatus.Ok, Dto = dto };
        public static GeneralSettingsResult BadRequest(params string[] errors) => new() { Status = SettingsApplyStatus.BadRequest, Errors = errors.ToList() };
        public static GeneralSettingsResult BadRequest(List<string> errors) => new() { Status = SettingsApplyStatus.BadRequest, Errors = errors };
    }
}
