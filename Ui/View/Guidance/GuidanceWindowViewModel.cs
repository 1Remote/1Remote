using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Stylet;

namespace _1RM.View.Guidance
{
    public class GuidanceWindowViewModel : NotifyPropertyChangedBase
    {
        private readonly LanguageService _languageService;
        private readonly ThemeService _themeService;
        private readonly Configuration _configuration;

        public GuidanceWindowViewModel(LanguageService languageService, Configuration configuration, bool profileModeIsPortable, bool profileModeIsEnabled)
        {
            _languageService = languageService;
            Debug.Assert(App.ResourceDictionary != null);
            _themeService = new ThemeService(App.ResourceDictionary!, configuration.Theme);
            _configuration = configuration;

            ProfileModeIsPortable = profileModeIsPortable;
            ProfileModeIsEnabled = profileModeIsEnabled;

            // set default language
            var ci = CultureInfo.CurrentCulture;
#if DEBUG
            Console.WriteLine("CultureInfo.CurrentCulture");
            Console.WriteLine(CultureInfo.CurrentCulture);
            Console.WriteLine("CultureInfo.CurrentUICulture");
            Console.WriteLine(CultureInfo.CurrentUICulture);
            Console.WriteLine("CultureInfo.DefaultThreadCurrentCulture");
            Console.WriteLine(CultureInfo.DefaultThreadCurrentCulture);
            Console.WriteLine("CultureInfo.DefaultThreadCurrentUICulture");
            Console.WriteLine(CultureInfo.DefaultThreadCurrentUICulture);

            Console.WriteLine("Default Language Info:");
            Console.WriteLine("* Name: {0}", ci.Name);
            Console.WriteLine("* Display Name: {0}", ci.DisplayName);
            Console.WriteLine("* English Name: {0}", ci.EnglishName);
            Console.WriteLine("* 2-letter ISO Name: {0}", ci.TwoLetterISOLanguageName);
            Console.WriteLine("* 3-letter ISO Name: {0}", ci.ThreeLetterISOLanguageName);
            Console.WriteLine("* 3-letter Win32 API Name: {0}", ci.ThreeLetterWindowsLanguageName);
#endif
            Language = CultureInfo.CurrentCulture.Name.ToLower();
            ThemeName = new ThemeConfig().ThemeName;
#if !DEBUG
            ConfigurationService.SetSelfStart(true);
#endif
        }



        public Dictionary<string, string> Languages => _languageService.LanguageCode2Name;
        public string Language
        {
            get => _configuration.General.CurrentLanguageCode;
            set
            {
                if (Languages.ContainsKey(value) &&
                    SetAndNotifyIfChanged(ref _configuration.General.CurrentLanguageCode, value))
                {
                    _languageService.SetLanguage(_configuration.General.CurrentLanguageCode);
                }
            }
        }


        public bool AppStartAutomatically
        {
            get => ConfigurationService.IsSelfStart();
            set
            {
                if (ConfigurationService.IsSelfStart() == value) return;
                var e = ConfigurationService.SetSelfStart(value);
                if (e != null)
                {
                    MessageBoxHelper.ErrorAlert("Can not set auto start dur to: " + e.Message + " May be you can try 'run as administrator' to fix it.");
                }
                RaisePropertyChanged();
            }
        }

        public bool ConfirmBeforeClosingSession
        {
            get => _configuration.General.ConfirmBeforeClosingSession;
            set => SetAndNotifyIfChanged(ref _configuration.General.ConfirmBeforeClosingSession, value);
        }

        public List<string> ThemeList => _themeService.Themes.Select(x => x.Key).ToList();
        public string ThemeName
        {
            get => _configuration.Theme.ThemeName;
            set
            {
                if (SetAndNotifyIfChanged(ref _configuration.Theme.ThemeName, value))
                {
                    SetTheme(value);
                }
            }
        }

        private bool _profileModeIsPortable;
        public bool ProfileModeIsPortable
        {
            get => _profileModeIsPortable;
            set => SetAndNotifyIfChanged(ref _profileModeIsPortable, value);
        }

        public bool ProfileModeIsEnabled { get; }


        private void SetTheme(string name)
        {
            Debug.Assert(_themeService.Themes.ContainsKey(name));
            var theme = _themeService.Themes[name];
            _configuration.Theme.PrimaryMidColor = theme.PrimaryMidColor;
            _configuration.Theme.PrimaryLightColor = theme.PrimaryLightColor;
            _configuration.Theme.PrimaryDarkColor = theme.PrimaryDarkColor;
            _configuration.Theme.PrimaryTextColor = theme.PrimaryTextColor;
            _configuration.Theme.AccentMidColor = theme.AccentMidColor;
            _configuration.Theme.AccentLightColor = theme.AccentLightColor;
            _configuration.Theme.AccentDarkColor = theme.AccentDarkColor;
            _configuration.Theme.AccentTextColor = theme.AccentTextColor;
            _configuration.Theme.BackgroundColor = theme.BackgroundColor;
            _configuration.Theme.BackgroundTextColor = theme.BackgroundTextColor;
            _themeService.ApplyTheme(_configuration.Theme);
        }
    }
}
