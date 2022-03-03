using System.Timers;
using Shawn.Utils;

namespace PRM.View
{
    public class VmAboutPage : NotifyPropertyChangedBase
    {
        private readonly Timer _checkUpdateTimer;

        public VmAboutPage()
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
            _checkUpdateTimer.Start();
        }

        ~VmAboutPage()
        {
            _checkUpdateTimer?.Dispose();
        }

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

        private void OnNewVersionRelease(string version, string url)
        {
            this.NewVersion = version;
            this.NewVersionUrl = url;
        }
    }
}