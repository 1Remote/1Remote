using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Service;
using _1RM.Utils;
using Google.Protobuf.WellKnownTypes;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using SetSelfStartingHelper = _1RM.Utils.SetSelfStartingHelper;

namespace _1RM.View.Settings.General
{
    public class GeneralSettingViewModel : NotifyPropertyChangedBase
    {
        private readonly ConfigurationService _configurationService;
        private readonly LanguageService _languageService;

        public GeneralSettingViewModel(ConfigurationService configurationService, LanguageService languageService)
        {
            _configurationService = configurationService;
            _languageService = languageService;
        }


        public Dictionary<string, string> Languages => _languageService.LanguageCode2Name;
        public string Language
        {
            get => _configurationService.General.CurrentLanguageCode;
            set
            {
                Debug.Assert(Languages.ContainsKey(value));
                if (SetAndNotifyIfChanged(ref _configurationService.General.CurrentLanguageCode, value))
                {
                    // reset lang service
                    _languageService.SetLanguage(value);
                    _configurationService.Save();
                }
            }
        }

        private bool _appStartAutomatically = false;
        public bool AppStartAutomatically
        {
            get => _appStartAutomatically;
            set
            {
                ConfigurationService.SetSelfStart(value);
                _appStartAutomatically = value;
                RaisePropertyChanged();
            }
        }

        private bool? _requireWindowsPasswordBeforeSensitiveOperation = null;
        public bool RequireWindowsPasswordBeforeSensitiveOperation
        {
            get => SecondaryVerificationHelper.GetEnabled();
            set
            {
                if (SetAndNotifyIfChanged(ref _requireWindowsPasswordBeforeSensitiveOperation, value))
                {
                    SecondaryVerificationHelper.SetEnabled(value);
                }
            }
        }

        public bool ConfirmBeforeClosingSession
        {
            get => _configurationService.General.ConfirmBeforeClosingSession;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.ConfirmBeforeClosingSession, value))
                {
                    _configurationService.Save();
                }
            }
        }


        public bool ShowSessionIconInSessionWindow
        {
            get => _configurationService.General.ShowSessionIconInSessionWindow;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.ShowSessionIconInSessionWindow, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public string LogPath => SimpleLogHelper.GetFileFullName();


        private RelayCommand? _cmdExploreTo = null;
        public RelayCommand CmdExploreTo
        {
            get
            {
                return _cmdExploreTo ??= new RelayCommand((o) =>
                {
                    try
                    {
                        SelectFileHelper.OpenInExplorerAndSelect(LogPath);
                    }
                    catch (Exception e)
                    {
                        MsAppCenterHelper.Error(e);
                    }
                });
            }
        }
    }
}
