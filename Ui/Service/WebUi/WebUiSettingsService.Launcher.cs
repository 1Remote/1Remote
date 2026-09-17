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
    /// WebUiSettingsService 分域：启动器（launcher）设置域。
    /// ─ GET/PUT /api/settings/launcher 启动器开关/热键/凭据显示/快速连接保存信息，写后重注册热键
    ///
    /// 热键线格式 = 枚举成员名（"ControlAlt"/"M"），PUT 额外接受 "Ctrl+Alt" 显示形态
    /// （token 顺序无关）。写后经 IoC.TryGet&lt;LauncherWindowViewModel&gt;()?.SetHotKey(4 参)
    /// 重注册（与 SettingsPageViewModel.CmdSaveAndGoBack 同一线程语义：UI 线程执行；
    /// TryGet 为 null 的测试环境静默跳过）。
    ///
    /// 冲突判定 = WPF 同款 launcherEnabled != 返回值：与 WPF 的差异是 WPF 注册失败会阻断
    /// Save（return/throw），web 语义 = 配置已保存 + 409（前端提示冲突，用户可再改）——
    /// 更贴近"已保存但没注册上"的事实，且与 SettingsPageViewModel 一样把配置先行落在内存。
    /// </summary>
    public static partial class WebUiSettingsService
    {
        /// <summary>读取 launcher 设置快照（枚举序列化为成员名）。</summary>
        public static LauncherSettingsDto ReadLauncher(ConfigurationService cs)
        {
            return new LauncherSettingsDto
            {
                LauncherEnabled = cs.Launcher.LauncherEnabled,
                HotKeyModifiers = cs.Launcher.HotKeyModifiers.ToString(),
                HotKeyKey = cs.Launcher.HotKeyKey.ToString(),
                ShowCredentials = cs.Launcher.ShowCredentials,
                AllowSaveInfoInQuickConnect = cs.Launcher.AllowSaveInfoInQuickConnect,
            };
        }

        /// <summary>
        /// PUT /api/settings/launcher：部分更新 + 保存 + 热键重注册。
        /// 冲突（launcherEnabled=true 但注册失败）→ HotkeyConflict（配置已保存，端点映射 409）。
        /// </summary>
        public static LauncherSettingsResult ApplyLauncher(ConfigurationService cs, LauncherSettingsUpdateRequest? input)
        {
            if (input == null)
                return LauncherSettingsResult.BadRequest("body must be a JSON object");

            var errors = new List<string>();
            var hasModifiers = false;
            var modifiers = default(HotkeyModifierKeys);
            var hasKey = false;
            var key = default(Key);
            if (input.HotKeyModifiers != null)
            {
                if (TryParseHotKeyModifiers(input.HotKeyModifiers, out modifiers))
                    hasModifiers = true;
                else
                    errors.Add($"hotKeyModifiers: '{input.HotKeyModifiers}' is not a valid HotkeyModifierKeys member or display form (e.g. 'ControlAlt', 'Ctrl+Alt')");
            }
            if (input.HotKeyKey != null)
            {
                if (TryParseHotKeyKey(input.HotKeyKey, out key))
                    hasKey = true;
                else
                    errors.Add($"hotKeyKey: '{input.HotKeyKey}' is not a valid System.Windows.Input.Key member (e.g. 'M', 'F1', 'D2')");
            }
            if (errors.Count > 0)
                return LauncherSettingsResult.BadRequest(errors);

            if (input.LauncherEnabled != null) cs.Launcher.LauncherEnabled = input.LauncherEnabled.Value;
            if (hasModifiers) cs.Launcher.HotKeyModifiers = modifiers;
            if (hasKey) cs.Launcher.HotKeyKey = key;
            if (input.ShowCredentials != null) cs.Launcher.ShowCredentials = input.ShowCredentials.Value;
            if (input.AllowSaveInfoInQuickConnect != null) cs.Launcher.AllowSaveInfoInQuickConnect = input.AllowSaveInfoInQuickConnect.Value;

            cs.Save();

            // 重注册：与 WPF 设置页同一 4 参调用；HwndSource/Hook 有 WPF 线程亲和，与 WPF 一致在
            // UI 线程执行。IoC.TryGet 为 null（测试环境未注册）→ 静默跳过。
            var launcher = IoC.TryGet<LauncherWindowViewModel>();
            if (launcher != null)
            {
                bool registered = false;
                Execute.OnUIThreadSync(() =>
                {
                    registered = launcher.SetHotKey(cs.Launcher.LauncherEnabled, cs.Launcher.HotKeyModifiers, cs.Launcher.HotKeyKey);
                });
                // WPF 冲突判定（SettingsPageViewModel.cs:160 同款）：launcherEnabled != 注册结果。
                // launcherEnabled=false 时 SetHotKey 注销后返回 false，视为一致（无冲突）。
                if (cs.Launcher.LauncherEnabled != registered)
                    return LauncherSettingsResult.HotkeyConflict(ReadLauncher(cs));
            }
            return LauncherSettingsResult.Ok(ReadLauncher(cs));
        }

        /// <summary>
        /// 解析热键修饰键：优先枚举成员名（大小写不敏感，"ControlAlt"）；否则按显示形态解析
        /// （"Ctrl+Alt"/"win + ctrl"，token 顺序无关，ctrl/shift/alt/win|windows）。
        /// None（无修饰键）拒绝——与 WPF 启动器设置 UI 不提供 None 一致；
        /// 无对应枚举成员的组合（如 Ctrl+Shift+Alt）拒绝。
        /// </summary>
        public static bool TryParseHotKeyModifiers(string? text, out HotkeyModifierKeys modifiers)
        {
            modifiers = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (Enum.TryParse(text.Trim(), true, out modifiers))
                return modifiers != HotkeyModifierKeys.None;

            uint flags = 0;
            foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (raw.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        flags |= (uint)ModifierKeys.Control;
                        break;
                    case "shift":
                        flags |= (uint)ModifierKeys.Shift;
                        break;
                    case "alt":
                        flags |= (uint)ModifierKeys.Alt;
                        break;
                    case "win":
                    case "windows":
                        flags |= (uint)ModifierKeys.Windows;
                        break;
                    default:
                        return false;
                }
            }
            foreach (var member in Enum.GetValues<HotkeyModifierKeys>())
            {
                if ((uint)member == flags)
                {
                    if (member == HotkeyModifierKeys.None)
                        return false;
                    modifiers = member;
                    return true;
                }
            }
            return false;
        }

        /// <summary>解析热键主键：Key 枚举成员名（大小写不敏感，"M"/"F1"/"D2"）；None 拒绝。</summary>
        public static bool TryParseHotKeyKey(string? text, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            if (!Enum.TryParse(text.Trim(), true, out key))
                return false;
            return key != Key.None;
        }
    }

    public sealed class LauncherSettingsResult
    {
        public SettingsApplyStatus Status { get; private init; }
        public List<string> Errors { get; private init; } = new();
        public LauncherSettingsDto? Dto { get; private init; }

        public static LauncherSettingsResult Ok(LauncherSettingsDto dto) => new() { Status = SettingsApplyStatus.Ok, Dto = dto };
        public static LauncherSettingsResult BadRequest(params string[] errors) => new() { Status = SettingsApplyStatus.BadRequest, Errors = errors.ToList() };
        public static LauncherSettingsResult BadRequest(List<string> errors) => new() { Status = SettingsApplyStatus.BadRequest, Errors = errors };
        public static LauncherSettingsResult HotkeyConflict(LauncherSettingsDto dto) => new() { Status = SettingsApplyStatus.HotkeyConflict, Dto = dto };
    }
}
