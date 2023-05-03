using System;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Timers;
using _1RM.Service;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;

namespace _1RM.View
{
    public class AboutPageViewModel : NotifyPropertyChangedBase
    {
        private readonly Timer _checkUpdateTimer;

        public AboutPageViewModel()
        {
            var checker = new VersionHelper(AppVersion.VersionData,
                AppVersion.UpdateCheckUrls,
                AppVersion.UpdatePublishUrls,
                customCheckMethod: CustomCheckMethod);
            checker.OnNewVersionRelease += OnNewVersionRelease;
            _checkUpdateTimer = new Timer()
            {
                Interval = 1000 * 10, // first time check,  eta 10s
                AutoReset = true,
            };
            _checkUpdateTimer.Elapsed += (sender, args) =>
            {
                SimpleLogHelper.Debug("Check update.");
                _checkUpdateTimer.Interval = 1000 * 3600; // next time check,  eta 3600s
                checker.CheckUpdateAsync();
            };
            checker.CheckUpdateAsync();
        }

        private static VersionHelper.CheckUpdateResult CustomCheckMethod(string html, string publishUrl, VersionHelper.Version currentVersion, VersionHelper.Version? ignoreVersion)
        {
            var ret = VersionHelper.DefaultCheckMethod(html, publishUrl, currentVersion, ignoreVersion);
            if (ret.NewerPublished)
                return ret;

            var mc = Regex.Matches(html, @".?1remote-([\d|\.]*.*)-net6", RegexOptions.IgnoreCase);
            if (mc.Count > 0)
            {
                var versionString = mc[mc.Count - 1].Groups[1].Value;
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

        ~AboutPageViewModel()
        {
            _checkUpdateTimer?.Stop();
            _checkUpdateTimer?.Dispose();
        }

        public string CurrentVersion => AppVersion.Version;


        private string _newVersion = "";
        public string NewVersion
        {
            get => _newVersion;
            set => SetAndNotifyIfChanged(ref _newVersion, value);
        }

        private string _newVersionUrl = "";

        public string NewVersionUrl
        {
            get => _newVersionUrl;
            set => SetAndNotifyIfChanged(ref _newVersionUrl, value);
        }

        private bool _isBreakingNewVersion;
        public bool IsBreakingNewVersion
        {
            get => _isBreakingNewVersion;
            set => SetAndNotifyIfChanged(ref _isBreakingNewVersion, value);
        }

        private void OnNewVersionRelease(VersionHelper.CheckUpdateResult result)
        {
            this.NewVersion = result.NewerVersion;
            this.NewVersionUrl = result.NewerUrl;
            this.IsBreakingNewVersion = result.NewerHasBreakChange;
            var v = IoC.Get<ConfigurationService>().Engagement.BreakingChangeAlertVersion;
            if (this.IsBreakingNewVersion
                && VersionHelper.Version.FromString(result.NewerVersion) > v)
            {
                Execute.OnUIThreadSync(() =>
                {
                    IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>());
                });
            }
        }


        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand((o) =>
                {
                    IoC.Get<MainWindowViewModel>().ShowList(false);
                });
            }
        }

        private RelayCommand? _cmdUpdate;
        public RelayCommand CmdUpdate
        {
            get
            {
                return _cmdUpdate ??= new RelayCommand((o) =>
                {
                    if (IsBreakingNewVersion)
                    {
                        MaskLayerController.ShowProcessingRing();
                        IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>(), ownerViewModel: IoC.Get<MainWindowViewModel>());
                        MaskLayerController.HideMask();
                    }
                    else
                    {
#if FOR_MICROSOFT_STORE_ONLY
                        HyperlinkHelper.OpenUriBySystem("ms-windows-store://review/?productid=9PNMNF92JNFP");
#else
                        HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
#endif
                    }
                });
            }
        }

        private RelayCommand? _cmdConsoleToggle;
        public RelayCommand CmdConsoleToggle
        {
            get
            {
                return _cmdConsoleToggle ??= new RelayCommand((o) =>
                {
                    ConsoleManager.Toggle();
                });
            }
        }
    }
}