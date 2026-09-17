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
    /// 设置与标签管理的编排逻辑（供 Web UI 端点复用，与 HTTP 层解耦：端点只做参数解包与
    /// 结果 → HTTP 状态码映射，校验/写入/联动全部在此完成）。
    /// 本类为 partial，按业务域拆分为同目录多个文件；各域写路径共守同一纪律——
    /// 先整体校验（任一项非法即 400 且零写入）→ 写内存配置/缓存 → ConfigurationService.Save() 落盘。
    /// 本文件（主文件）只持有跨域共享成员；分域文件（各自持有该域的编排方法、域私有助手
    /// 与该域的结果类型）：
    /// ─ WebUiSettingsService.General.cs  —— GET/PUT /api/settings/general、POST /api/settings/verify
    /// ─ WebUiSettingsService.Launcher.cs —— GET/PUT /api/settings/launcher（含热键解析与写后重注册）
    /// ─ WebUiSettingsService.Tags.cs     —— /api/tags 管理域（聚合列表 / 置顶 / 重命名 / 删除）
    /// （appearance 与 ui-state 两域无编排逻辑可拆：直接在 WebUiEndpoints.Settings.cs /
    ///   WebUiEndpoints.Aux.cs 的端点内实现，不经过本服务。）
    /// </summary>
    public static partial class WebUiSettingsService
    {
        /// <summary>
        /// 支持的 14 个语言码（小写），来源与 LanguageService 静态装载一致
        /// （Ui/Resources/Languages/LanguagesResources.Files）。不依赖 IoC——测试环境不注册 LanguageService。
        /// </summary>
        public static readonly HashSet<string> SupportedLanguageCodes = new(
            LanguagesResources.Files.Select(f => f.Replace(".xaml", string.Empty)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// general/launcher 设置写结果分类，由端点映射为 HTTP 状态码
    /// （GeneralSettingsResult / LauncherSettingsResult 分别随域放在对应分域文件）。
    /// </summary>
    public enum SettingsApplyStatus
    {
        Ok,
        BadRequest,     // 校验失败（零写入）
        HotkeyConflict, // 配置已保存但热键注册失败（409，前端提示后可再改）
    }
}
