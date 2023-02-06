﻿using System.Timers;
using System.Windows.Input;
using _1RM.Service;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View
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


            CurrentVersion = _1Remote.Security.Config.Salt;
        }

        ~AboutPageViewModel()
        {
            _checkUpdateTimer?.Stop();
            _checkUpdateTimer?.Dispose();
        }

        public string CurrentVersion { get; }


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