using System.Timers;
using PRM.Service;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;

namespace PRM.View
{
    public class AboutPageViewModel : NotifyPropertyChangedBase
    {
        private readonly Timer _checkUpdateTimer;

        public AboutPageViewModel()
        {
            var checker = new VersionHelper(AppVersion.VersionData, null, AppVersion.UpdateUrls);
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

        private void OnNewVersionRelease(string version, string url, bool b)
        {
            this.NewVersion = version;
            this.NewVersionUrl = url;
            this.IsBreakingNewVersion = b;
            var v = IoC.Get<ConfigurationService>().Engagement.BreakingChangeAlertVersion;
            if (this.IsBreakingNewVersion
                && VersionHelper.Version.FromString(version) > v)
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
                    IoC.Get<MainWindowViewModel>().ShowList();
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
                        IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>(), ownerViewModel: IoC.Get<MainWindowViewModel>());
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

        //public void SupportText_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    if (e.ClickCount == 3)
        //    {
        //        ConsoleManager.Toggle();
        //    }
        //}
    }
}