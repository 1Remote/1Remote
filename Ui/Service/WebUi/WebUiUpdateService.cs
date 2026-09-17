using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using System.Threading.Tasks;
using Shawn.Utils;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// Web UI 侧的新版本检测服务（fix batch6 Task D #12）：WPF AboutPageViewModel 的
    /// 无 UI 平移——进程内单例语义（静态类），首次 <see cref="EnsureStarted"/> 触发一次
    /// 检查 + 每小时自动复查（System.Timers.Timer，节拍与 WPF AboutPageViewModel:39-59 一致），
    /// 检查结果缓存于静态字段（lock 保护）。与 WPF 的差异（刻意）：
    /// ─ 不做任何弹窗/BreakingChange 对话框——web 侧新版本信息只经 GET /api/version 暴露，
    ///   由前端（关于页 Update 行 + ⚙ 红点）自行呈现；
    /// ─ 缓存写入不经 OnNewVersionRelease 事件而由 <see cref="ExecuteCheck"/> 完成路径直接写：
    ///   web 侧还需要「检查是否仍在进行」信号（checking 标志）供前端决定重拉，而
    ///   VersionHelper.CheckUpdateAsync 仅在新版本发布时回调、无完成信号；改为自持 Task
    ///   调同步 CheckUpdate() 等价于订阅事件（CheckUpdateAsync 内部即 CheckUpdate + 事件转发）。
    /// 尊重 ConfigurationService.General.DoNotCheckNewVersion：禁用时 EnsureStarted 不启动
    /// （WPF StartVersionCheckTimer:28 同款早退；后续调用仍可再启动），周期 tick 途中被禁用
    /// 则停表（WPF:48-52 同款），GetStatus 恒报 available=false。
    /// </summary>
    public static class WebUiUpdateService
    {
        private static readonly object Gate = new();
        private static VersionHelper? _checker;
        private static Timer? _timer;
        private static bool _started;
        private static volatile bool _checking;
        private static VersionHelper.CheckUpdateResult _last = VersionHelper.CheckUpdateResult.False();

        /// <summary>
        /// 测试注入点（同 WebUiSettingsService.VerifyAccessAsync 的 verifier 模式）：
        /// 非空时替换真实网络检查执行体（VersionHelper.CheckUpdate 会真访问 UpdateCheckUrls，
        /// 测试桩绝不触网）。生产恒为 null。静态可变——仅测试宿主在发起请求前设置。
        /// </summary>
        public static Func<VersionHelper.CheckUpdateResult>? CheckOverrideForTest { get; set; }

        /// <summary>
        /// 幂等启动：首次调用触发一次检查 + 启动每小时复查 Timer（WPF
        /// AboutPageViewModel.StartVersionCheckTimer 同款节拍）。已启动后调用为空操作——
        /// /api/version 每次命中都会调本方法，绝不能重复触发网络检查。
        /// 禁用检查（DoNotCheckNewVersion）或无 ConfigurationService（未初始化 IoC 的
        /// 纯静态托管测试宿主）时不启动，也不置位 _started：用户后续启用后下一次调用仍可启动。
        /// </summary>
        public static void EnsureStarted()
        {
            lock (Gate)
            {
                if (_started) return;
                var cs = IoC.TryGet<ConfigurationService>();
                if (cs == null || cs.General.DoNotCheckNewVersion) return;
                _started = true;

                _checker ??= new VersionHelper(_1RM.AppVersion.VersionData,
                    _1RM.AppVersion.UpdateCheckUrls,
                    _1RM.AppVersion.UpdatePublishUrls,
                    customCheckMethod: CustomCheckMethod); // 构造参数与 WPF AboutPageViewModel:33-38 一致（含复刻的 CustomCheckMethod）
                if (_timer == null)
                {
                    _timer = new Timer
                    {
                        Interval = 1000 * 60 * 60,
                        AutoReset = true,
                    };
                    _timer.Elapsed += (sender, args) =>
                    {
                        // WPF:46-54 同款：tick 途中被禁用则停表；检查本体丢线程池，不占 Timer 回调线程
                        if (IoC.TryGet<ConfigurationService>()?.General.DoNotCheckNewVersion == true)
                        {
                            _timer.Stop();
                            return;
                        }
                        Task.Run(() => ExecuteCheck());
                    };
                }
                Task.Run(() => ExecuteCheck()); // 首检异步：端点线程立即返回，不等待网络
                _timer.Start();
            }
        }

        /// <summary>
        /// 执行一次检查并更新缓存（同步方法，供首检/周期 tick 以 Task.Run 包装与测试直调复用）。
        /// checkOverride 注入优先（VerifyAccessAsync 模式），其次静态 <see cref="CheckOverrideForTest"/>，
        /// 均空时走真实 VersionHelper.CheckUpdate（网络）。缓存只在发现新版本时覆盖（事件式语义，
        /// 与 WPF OnNewVersionRelease 一致——避免站点瞬时不可达令红点闪烁消失），但 checking
        /// 标志任何完成路径都清除。
        /// </summary>
        public static void ExecuteCheck(Func<VersionHelper.CheckUpdateResult>? checkOverride = null)
        {
            Func<VersionHelper.CheckUpdateResult> run;
            lock (Gate)
            {
                // 未经 EnsureStarted 武装（如测试直调）：按同一构造参数武装
                _checker ??= new VersionHelper(_1RM.AppVersion.VersionData,
                    _1RM.AppVersion.UpdateCheckUrls,
                    _1RM.AppVersion.UpdatePublishUrls,
                    customCheckMethod: CustomCheckMethod);
                var checker = _checker;
                run = checkOverride ?? CheckOverrideForTest ?? checker.CheckUpdate;
                _checking = true;
            }
            var result = run();
            lock (Gate)
            {
                if (result.NewerPublished)
                    _last = result;
                _checking = false;
            }
        }

        /// <summary>当前检测状态快照（/api/version 的 update 域数据源）。</summary>
        public static WebUiUpdateStatus GetStatus()
        {
            var disabled = IoC.TryGet<ConfigurationService>()?.General.DoNotCheckNewVersion == true;
            lock (Gate)
            {
                var available = !disabled && _last.NewerPublished;
                return new WebUiUpdateStatus
                {
                    Checking = !disabled && _checking,
                    Available = available,
                    NewVersion = available ? _last.NewerVersion : "",
                    NewVersionUrl = available ? _last.NewerUrl : "",
                    Breaking = available && _last.NewerHasBreakChange,
                };
            }
        }

        /// <summary>测试重置：停表并清空全部静态状态，仅供测试在用例间恢复初始态（生产绝不调用）。</summary>
        public static void ResetForTest()
        {
            lock (Gate)
            {
                _timer?.Stop();
                _timer = null;
                _checker = null;
                _started = false;
                _checking = false;
                _last = VersionHelper.CheckUpdateResult.False();
            }
        }

        /// <summary>
        /// WPF AboutPageViewModel.CustomCheckMethod:61-89 的静态复刻（正则与优先级逐行一致）：
        /// 先走 DefaultCheckMethod（"latest version:" 标记），未命中再按 Nightly 页面形态
        /// （"1remote-x.y.z-net" 文件名 / "latest version:" 宽松式）兜底；版本串首尾 '!' 标记破坏性更新。
        /// </summary>
        private static VersionHelper.CheckUpdateResult CustomCheckMethod(string html, string publishUrl, VersionHelper.Version currentVersion, VersionHelper.Version? ignoreVersion)
        {
            var ret = VersionHelper.DefaultCheckMethod(html, publishUrl, currentVersion, ignoreVersion);
            if (ret.NewerPublished)
                return ret;

            var patterns = new List<string>()
            {
                @".?1remote-([\d|\.]*.*)-net",
                @".?latest\sversion:\s*([\d|.]*)",
            };
            foreach (var pattern in patterns)
            {
                var mc = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                if (mc.Count <= 0) continue;
                var versionString = mc[0].Groups[1].Value;
                var releasedVersion = VersionHelper.Version.FromString(versionString);
                if (ignoreVersion is not null)
                {
                    if (releasedVersion <= ignoreVersion)
                    {
                        return VersionHelper.CheckUpdateResult.False();
                    }
                }
                if (releasedVersion > currentVersion)
                    return new VersionHelper.CheckUpdateResult(true, versionString, publishUrl, versionString.FirstOrDefault() == '!' || versionString.LastOrDefault() == '!');
            }
            return VersionHelper.CheckUpdateResult.False();
        }
    }

    /// <summary>/api/version 的 update 域 DTO（WebUiUpdateService.GetStatus 的返回形状）。</summary>
    public sealed class WebUiUpdateStatus
    {
        /// <summary>一次检查仍在进行（前端可稍后重拉；禁用检查时恒 false）。</summary>
        public bool Checking { get; init; }
        /// <summary>有可用新版本（DoNotCheckNewVersion 时恒 false）。</summary>
        public bool Available { get; init; }
        public string NewVersion { get; init; } = "";
        public string NewVersionUrl { get; init; } = "";
        /// <summary>新版本为破坏性更新（WPF '!' 标记语义）。</summary>
        public bool Breaking { get; init; }
    }
}
