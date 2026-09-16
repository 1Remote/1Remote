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
    /// 设置（general/launcher）与标签管理编排逻辑（供 Web UI 端点复用，与 HTTP 层解耦）。
    ///
    /// general 安全域：只暴露非破坏性字段（语言/关闭行为/确认开关/日志级别/tab 选项/复制选项）；
    /// 开机自启（写注册表）、便携模式、SQLite 路径不进白名单。requireSecondaryVerification 不在
    /// GeneralConfig：真实状态在 SecondaryVerificationHelper——读 await GetEnabled()，写
    /// await SetEnabledAsync(bool)（写注册表/凭据管理器机器状态；可等待版本在返回前完成写入并
    /// 使静态缓存与机器状态一致，响应值即回读 GetEnabled()，fix #13：原 async void
    /// fire-and-forget 存在“响应已返回但缓存/机器状态尚未落地”的竞态窗口）。
    ///
    /// language 变更：归一小写码后校验 14 个内置语言（LanguagesResources.Files 同源），写配置后调
    /// LanguageService.SetLanguage 让 WPF 侧即时生效（GeneralSettingViewModel.Language 同款顺序：
    /// 先 SetLanguage 再 Save）；无 WPF 环境（测试）未注册 LanguageService 时静默跳过。
    ///
    /// launcher 热键：线格式 = 枚举成员名（"ControlAlt"/"M"），PUT 额外接受 "Ctrl+Alt" 显示形态
    /// （token 顺序无关）。写后经 IoC.TryGet&lt;LauncherWindowViewModel&gt;()?.SetHotKey(4 参) 重注册
    /// （与 SettingsPageViewModel.CmdSaveAndGoBack 同一线程语义：UI 线程执行；TryGet 为 null 的
    /// 测试环境静默跳过）。冲突判定 = WPF 同款 launcherEnabled != 返回值：与 WPF 的差异是 WPF 注册
    /// 失败会阻断 Save（return/throw），web 语义 = 配置已保存 + 409（前端提示冲突，用户可再改）——
    /// 更贴近"已保存但没注册上"的事实，且与 SettingsPageViewModel 一样把配置先行落在内存。
    ///
    /// 标签 rename/delete：复刻 TagActionHelper.CmdTagRename/CmdTagDelete 的核心循环（Tags 列表
    /// ordinal 精确匹配 + 整表替换 + 批量 UpdateServer），范围限定在请求的数据源（WPF 扫全部 VmItemList）；
    /// rename 先迁移 LocalityTagService 置顶状态（GetAndRemoveTag + UpdateTag，pin 随名迁移）；
    /// delete 后仅当该标签在所有数据源都不再存在才清理置顶信息（WPF CmdTagDelete 不清理，属有意补齐——
    /// 跨数据源共享标签名时保留 pin 才正确）。
    /// </summary>
    public static class WebUiSettingsService
    {
        /// <summary>
        /// 支持的 14 个语言码（小写），来源与 LanguageService 静态装载一致
        /// （Ui/Resources/Languages/LanguagesResources.Files）。不依赖 IoC——测试环境不注册 LanguageService。
        /// </summary>
        public static readonly HashSet<string> SupportedLanguageCodes = new(
            LanguagesResources.Files.Select(f => f.Replace(".xaml", string.Empty)),
            StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------
        // general
        // ------------------------------------------------------------------

        /// <summary>读取 general 设置快照（requireSecondaryVerification 读 SecondaryVerificationHelper）。</summary>
        public static async Task<GeneralSettingsDto> ReadGeneralAsync(ConfigurationService cs)
        {
            var dto = ReadGeneralConfig(cs);
            dto.RequireSecondaryVerification = await SecondaryVerificationHelper.GetEnabled();
            return dto;
        }

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

        // ------------------------------------------------------------------
        // launcher
        // ------------------------------------------------------------------

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

        // ------------------------------------------------------------------
        // tags manage / rename / delete
        // ------------------------------------------------------------------

        /// <summary>
        /// GET /api/tags/manage?ds=：该数据源下的标签聚合（name=规范化小写 / count=服务器数 /
        /// pinned=LocalityTagService 置顶）。计数语义与 ReloadTagsFromServers 一致（标签 Trim+小写后聚合）。
        /// 排序：置顶优先，再按 locality 自定义序，最后按名。
        /// </summary>
        public static List<TagManageItemDto> ListManageTags(DataSourceBase dataSource)
        {
            var servers = SnapshotServersOfDataSource(dataSource);
            var counts = servers
                .SelectMany(s => s.Tags.Select(t => t.Trim().ToLower()))
                .GroupBy(n => n, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            LocalityTagService.Load();
            return counts
                .Select(kv => new TagManageItemDto
                {
                    Name = kv.Key,
                    Count = kv.Value,
                    Pinned = LocalityTagService.GetIsPinned(kv.Key),
                })
                .OrderByDescending(x => x.Pinned)
                .ThenBy(x => LocalityTagService.GetCustomOrder(x.Name))
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// PUT /api/tags/manage：置顶/取消置顶（幂等，目标值语义）。复刻 WPF CmdTagPin：在
        /// GlobalData.TagList 上找 Tag 对象并设置 IsPinned——其 setter 负责 locality 落盘、
        /// 置顶标签移到列表尾并重排 CustomOrder、刷新标签过滤条可见性。
        /// 置顶状态是机器本地（跨数据源共享），不在数据源内校验可写性；未知标签名 → 404。
        /// </summary>
        public static TagManageResult SetTagPinned(string? dataSourceName, string? name, bool? pinned)
        {
            if (pinned == null)
                return TagManageResult.BadRequest("body must contain a 'pinned' boolean");
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return TagManageResult.BadRequest($"unknown dataSourceName '{dataSourceName}'");

            var tagName = TagAndKeywordEncodeHelper.RectifyTagName(name);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(tagName))
                return TagManageResult.BadRequest("name: can not be empty");

            var gd = IoC.Get<GlobalData>();
            List<TagManageItemDto> listAfter;
            lock (gd)
            {
                var tag = gd.TagList.FirstOrDefault(x => string.Equals(x.Name, tagName, StringComparison.CurrentCultureIgnoreCase));
                if (tag == null)
                    return TagManageResult.NotFound();
                tag.IsPinned = pinned.Value; // Tag setter 内完成 locality 落盘与重排（WPF 同款）
                listAfter = ListManageTagsUnlocked(dataSource, gd);
            }
            // 置顶目标是全局 TagList：标签可能只存在于其它数据源（该 ds 计数为 0），此时聚合列表
            // 查不到条目——回退手工构造（name + 目标 pin 态），避免响应体为 null
            var updated = listAfter.FirstOrDefault(x => x.Name == tagName)
                ?? new TagManageItemDto { Name = tagName, Count = 0, Pinned = pinned.Value };
            return TagManageResult.Ok(updated);
        }

        /// <summary>
        /// POST /api/tags/rename：数据源范围内重命名。校验（与 WPF 输入校验同序：先空白/同名/已存在
        /// 预检再 RectifyTagName 规范化——"  " 规范化后会变成 "--" 故必须前置拦截；to 空/同名/已存在
        /// → 400；from 不在该数据源 → 404；只读数据源 → 400）全通过后按 WPF CmdTagRename 顺序执行：
        /// locality 置顶状态随名迁移 → 服务器 Tags 替换（new List：Remove 旧名 + Add 新名）→
        /// TagList 内联改名 → 批量 UpdateServer（库内生效 + ReloadTagsFromServers 重建 TagList）。
        /// 与 WPF 的差异：WPF 扫全部数据源的可编辑服务器，此处限定请求的 ds（多个数据源共享标签名时
        /// 其它数据源保持旧名，聚合视图会出现新旧两个名字——ds 范围语义，属有意设计）。
        /// </summary>
        public static TagManageResult RenameTag(string? dataSourceName, string? from, string? to)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return TagManageResult.BadRequest($"unknown dataSourceName '{dataSourceName}'");
            if (dataSource.IsWritable != true)
                return TagManageResult.BadRequest($"dataSource '{dataSource.DataSourceName}' is read-only");

            // 空白预检在规范化前（RectifyTagName 会把纯空格变成 "-"，WPF 输入校验同序）
            if (string.IsNullOrWhiteSpace(from))
                return TagManageResult.BadRequest("from: can not be empty");
            if (string.IsNullOrWhiteSpace(to))
                return TagManageResult.BadRequest("to: can not be empty");
            var fromName = TagAndKeywordEncodeHelper.RectifyTagName(from);
            var toName = TagAndKeywordEncodeHelper.RectifyTagName(to);
            if (string.IsNullOrEmpty(fromName))
                return TagManageResult.BadRequest("from: can not be empty");
            if (string.IsNullOrEmpty(toName))
                return TagManageResult.BadRequest("to: can not be empty");
            if (fromName == toName)
                return TagManageResult.BadRequest("to: must differ from 'from'");

            var gd = IoC.Get<GlobalData>();
            List<ProtocolBase> targets;
            lock (gd)
            {
                // WPF CmdTagRename 核心循环：Tags 列表 ordinal 精确匹配（标签约定小写存储）
                targets = gd.VmItemList
                    .Where(vm => vm.DataSourceName == dataSource.DataSourceName
                                 && WebUiEndpoints.IsConnectable(vm.Server)
                                 && vm.Server.Tags.Contains(fromName))
                    .Select(vm => vm.Server)
                    .ToList();
                if (targets.Count == 0)
                    return TagManageResult.NotFound();
                if (gd.VmItemList.Any(vm => vm.DataSourceName == dataSource.DataSourceName
                                            && WebUiEndpoints.IsConnectable(vm.Server)
                                            && vm.Server.Tags.Contains(toName)))
                {
                    return TagManageResult.BadRequest($"to: tag '{toName}' already exists in dataSource '{dataSource.DataSourceName}'");
                }

                // 1. locality 置顶状态随名迁移（WPF 步骤 1：GetAndRemoveTag + 改名 + UpdateTag）
                var oldTag = LocalityTagService.GetAndRemoveTag(fromName);
                if (oldTag != null)
                {
                    oldTag.Name = toName;
                    LocalityTagService.UpdateTag(oldTag);
                }

                // 2. 服务器 Tags 替换（WPF 步骤 2：整表替换——Remove 旧名 + Add 新名）
                foreach (var server in targets)
                {
                    if (server.Tags.Contains(fromName))
                    {
                        var tags = new List<string>(server.Tags);
                        tags.Remove(fromName);
                        tags.Add(toName);
                        server.Tags = tags;
                    }
                }

                // 2.5 TagList 内联改名（WPF 步骤 2.5；UpdateServer 成功后 ReloadTagsFromServers 也会重建）
                var tag = gd.TagList.FirstOrDefault(x => x.Name == fromName);
                if (tag != null) tag.Name = toName;
            }

            // 3. 落库（lock 外：UpdateServer 内部自有 StopTick/StartTick 加锁，且含 DB IO）
            var ret = gd.UpdateServer(targets);
            if (!ret.IsSuccess)
                return TagManageResult.DbError(ret.ErrorInfo, updated: 0);
            return TagManageResult.Ok(new TagRenameResultDto { From = fromName, To = toName, Updated = targets.Count });
        }

        /// <summary>
        /// DELETE /api/tags/{name}?ds=：从该数据源所有服务器移除标签（WPF CmdTagDelete 核心循环：
        /// Tags 原地 Remove + 批量 UpdateServer）。成功后仅当该标签在所有数据源都不再存在时清理
        /// locality 置顶信息（跨数据源共享名时保留 pin；WPF 自身不清理，属 plan 要求的有意补齐）。
        /// </summary>
        public static TagManageResult DeleteTag(string? dataSourceName, string? name)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return TagManageResult.BadRequest($"unknown dataSourceName '{dataSourceName}'");
            if (dataSource.IsWritable != true)
                return TagManageResult.BadRequest($"dataSource '{dataSource.DataSourceName}' is read-only");

            var tagName = TagAndKeywordEncodeHelper.RectifyTagName(name);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(tagName))
                return TagManageResult.BadRequest("name: can not be empty");

            var gd = IoC.Get<GlobalData>();
            List<ProtocolBase> targets;
            lock (gd)
            {
                targets = gd.VmItemList
                    .Where(vm => vm.DataSourceName == dataSource.DataSourceName
                                 && WebUiEndpoints.IsConnectable(vm.Server)
                                 && vm.Server.Tags.Contains(tagName))
                    .Select(vm => vm.Server)
                    .ToList();
                if (targets.Count == 0)
                    return TagManageResult.NotFound();

                foreach (var server in targets)
                {
                    if (server.Tags.Contains(tagName))
                    {
                        server.Tags.Remove(tagName); // WPF CmdTagDelete：原地移除
                    }
                }
            }

            var ret = gd.UpdateServer(targets);
            if (!ret.IsSuccess)
                return TagManageResult.DbError(ret.ErrorInfo, updated: 0);

            // 置顶信息清理：仅当所有数据源都不再有此标签（否则其它数据源仍引用，保留 pin）
            lock (gd)
            {
                var stillExists = gd.VmItemList
                    .Where(vm => WebUiEndpoints.IsConnectable(vm.Server))
                    .Any(vm => vm.Server.Tags.Contains(tagName));
                if (!stillExists)
                    LocalityTagService.GetAndRemoveTag(tagName);
            }
            return TagManageResult.Ok(updated: targets.Count);
        }

        // ------------------------------------------------------------------
        // 辅助
        // ------------------------------------------------------------------

        /// <summary>
        /// 物化某数据源的服务器快照（lock(gd) 内物化、锁外使用——与 /api/servers 快照纪律一致）。
        /// </summary>
        private static List<ProtocolBase> SnapshotServersOfDataSource(DataSourceBase dataSource)
        {
            var gd = IoC.Get<GlobalData>();
            lock (gd)
            {
                return gd.VmItemList
                    .Where(vm => vm.DataSourceName == dataSource.DataSourceName
                                 && WebUiEndpoints.IsConnectable(vm.Server))
                    .Select(vm => vm.Server)
                    .ToList();
            }
        }

        /// <summary>调用方已持 lock(gd) 的标签聚合（SetTagPinned 响应构造用：Tag.IsPinned 已原地更新）。</summary>
        private static List<TagManageItemDto> ListManageTagsUnlocked(DataSourceBase dataSource, GlobalData gd)
        {
            var counts = gd.VmItemList
                .Where(vm => vm.DataSourceName == dataSource.DataSourceName && WebUiEndpoints.IsConnectable(vm.Server))
                .Select(vm => vm.Server)
                .SelectMany(s => s.Tags.Select(t => t.Trim().ToLower()))
                .GroupBy(n => n, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            LocalityTagService.Load();
            return counts
                .Select(kv => new TagManageItemDto
                {
                    Name = kv.Key,
                    Count = kv.Value,
                    Pinned = LocalityTagService.GetIsPinned(kv.Key),
                })
                .OrderByDescending(x => x.Pinned)
                .ThenBy(x => LocalityTagService.GetCustomOrder(x.Name))
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>数据源名解析：空白视为 Local；未知返回 null（调用方转 400）。</summary>
        private static DataSourceBase? ResolveDataSource(string? dataSourceName)
        {
            var name = string.IsNullOrWhiteSpace(dataSourceName)
                ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                : dataSourceName;
            return IoC.Get<DataSourceService>().GetDataSource(name);
        }
    }

    /// <summary>general/launcher 设置写结果分类，由端点映射为 HTTP 状态码。</summary>
    public enum SettingsApplyStatus
    {
        Ok,
        BadRequest,     // 校验失败（零写入）
        HotkeyConflict, // 配置已保存但热键注册失败（409，前端提示后可再改）
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

    /// <summary>标签管理（pin/rename/delete）结果分类，由端点映射为 HTTP 状态码。</summary>
    public enum TagManageStatus
    {
        Ok,
        BadRequest, // 参数非法/未知数据源/只读数据源/重名目标
        NotFound,   // 标签在该数据源不存在
        DbError,    // 批量 UpdateServer 失败
    }

    public sealed class TagManageResult
    {
        public TagManageStatus Status { get; private init; }
        public List<string> Errors { get; private init; } = new();
        public string DbErrorInfo { get; private init; } = string.Empty;
        public TagManageItemDto? Item { get; private init; }
        public TagRenameResultDto? Rename { get; private init; }
        public int Updated { get; private init; }

        public static TagManageResult Ok(TagManageItemDto item) => new() { Status = TagManageStatus.Ok, Item = item };
        public static TagManageResult Ok(TagRenameResultDto rename) => new() { Status = TagManageStatus.Ok, Rename = rename, Updated = rename.Updated };
        public static TagManageResult Ok(int updated) => new() { Status = TagManageStatus.Ok, Updated = updated };
        public static TagManageResult NotFound() => new() { Status = TagManageStatus.NotFound };
        public static TagManageResult BadRequest(params string[] errors) => new() { Status = TagManageStatus.BadRequest, Errors = errors.ToList() };
        public static TagManageResult DbError(string errorInfo, int updated) => new() { Status = TagManageStatus.DbError, DbErrorInfo = errorInfo, Updated = updated };
    }

    /// <summary>POST /api/tags/rename 成功载荷：{from, to, updated}。</summary>
    public class TagRenameResultDto
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public int Updated { get; set; }
    }
}
