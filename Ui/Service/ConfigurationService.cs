using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using PRM.Model.DAO;
using PRM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using VariableKeywordMatcher.Provider.DirectMatch;
using SetSelfStartingHelper = PRM.Utils.SetSelfStartingHelper;

namespace PRM.Service
{
    public class EngagementSettings
    {
        public DateTime InstallTime = DateTime.Today;
        public bool DoNotShowAgain = false;
        public string DoNotShowAgainVersionString = "";
        [Newtonsoft.Json.JsonIgnore]
        public VersionHelper.Version DoNotShowAgainVersion => VersionHelper.Version.FromString(DoNotShowAgainVersionString);
        public DateTime LastRequestRatingsTime = DateTime.MinValue;
        public int ConnectCount = 0;
    }
    public class GeneralConfig
    {
        #region General
        public string CurrentLanguageCode = "en-us";
        public bool AppStartAutomatically = true;
        public bool AppStartMinimized = true;
        public bool ListPageIsCardView = false;
        public bool ConfirmBeforeClosingSession = false;
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ShowSessionIconInSessionWindow = true;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool TabHeaderShowCloseButton = true;
        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool TabHeaderShowReConnectButton = false;
        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ShowRecentlySessionInTray = false;
        #endregion
    }

    public class LauncherConfig
    {
        public bool LauncherEnabled = true;

#if DEBUG
        public HotkeyModifierKeys HotKeyModifiers = HotkeyModifierKeys.Shift;
#else
        public HotkeyModifierKeys HotKeyModifiers = HotkeyModifierKeys.Alt;
#endif

        public Key HotKeyKey = Key.M;

        public bool ShowNoteFieldInLauncher = true;
        public bool ShowNoteFieldInListView = true;
    }

    public class KeywordMatchConfig
    {
        /// <summary>
        /// name of the matchers
        /// </summary>
        public List<string> EnabledMatchers = new List<string>();
    }

    public class DatabaseConfig
    {
        public const DatabaseType DatabaseType = Model.DAO.DatabaseType.Sqlite;

        private string _sqliteDatabasePath = "./" + Assert.APP_NAME + ".db";
        public string SqliteDatabasePath
        {
            get
            {
                Debug.Assert(string.IsNullOrEmpty(_sqliteDatabasePath) == false);
                return _sqliteDatabasePath;
            }
            set => _sqliteDatabasePath = value.Replace(Environment.CurrentDirectory, ".");
        }
    }

    public class ThemeConfig
    {
        public string ThemeName = "Default";
        public string PrimaryMidColor = "#102b3e";
        public string PrimaryLightColor = "#445a68";
        public string PrimaryDarkColor = "#0c2230";
        public string PrimaryTextColor = "#ffffff";

        public string AccentMidColor = "#e83d61";
        public string AccentLightColor = "#ed6884";
        public string AccentDarkColor = "#b5304c";
        public string AccentTextColor = "#ffffff";

        public string BackgroundColor = "#ced8e1";
        public string BackgroundTextColor = "#000000";

        #region GetColor
        public System.Windows.Media.Color GetPrimaryMidColor => ColorAndBrushHelper.HexColorToMediaColor(PrimaryMidColor);
        public System.Windows.Media.Color GetPrimaryLightColor => ColorAndBrushHelper.HexColorToMediaColor(PrimaryLightColor);
        public System.Windows.Media.Color GetPrimaryDarkColor => ColorAndBrushHelper.HexColorToMediaColor(PrimaryDarkColor);
        public System.Windows.Media.Color GetPrimaryTextColor => ColorAndBrushHelper.HexColorToMediaColor(PrimaryTextColor);

        public System.Windows.Media.Color GetAccentMidColor => ColorAndBrushHelper.HexColorToMediaColor(AccentMidColor);
        public System.Windows.Media.Color GetAccentLightColor => ColorAndBrushHelper.HexColorToMediaColor(AccentLightColor);
        public System.Windows.Media.Color GetAccentDarkColor => ColorAndBrushHelper.HexColorToMediaColor(AccentDarkColor);
        public System.Windows.Media.Color GetAccentTextColor => ColorAndBrushHelper.HexColorToMediaColor(AccentTextColor);

        public System.Windows.Media.Color GetBackgroundColor => ColorAndBrushHelper.HexColorToMediaColor(BackgroundColor);
        public System.Windows.Media.Color GetBackgroundTextColor => ColorAndBrushHelper.HexColorToMediaColor(BackgroundTextColor);
        #endregion
    }

    public class Configuration
    {
        public GeneralConfig General { get; set; } = new GeneralConfig();
        public LauncherConfig Launcher { get; set; } = new LauncherConfig();
        public KeywordMatchConfig KeywordMatch { get; set; } = new KeywordMatchConfig();
        public DatabaseConfig Database { get; set; } = new DatabaseConfig();
        public ThemeConfig Theme { get; set; } = new ThemeConfig();
        public EngagementSettings Engagement { get; set; } = new EngagementSettings();
        public List<string> PinnedTags { get; set; } = new List<string>();
    }

    public class ConfigurationService
    {
        private readonly KeywordMatchService _keywordMatchService = new KeywordMatchService();

        public readonly List<MatchProviderInfo> AvailableMatcherProviders = new List<MatchProviderInfo>();
        private readonly Configuration _cfg;

        public GeneralConfig General => _cfg.General;
        public LauncherConfig Launcher => _cfg.Launcher;
        public KeywordMatchConfig KeywordMatch => _cfg.KeywordMatch;
        public DatabaseConfig Database => _cfg.Database;
        public ThemeConfig Theme => _cfg.Theme;
        public EngagementSettings Engagement => _cfg.Engagement;
        /// <summary>
        /// Tags that show on the tab bar of the main window
        /// </summary>
        public List<string> PinnedTags
        {
            set => _cfg.PinnedTags = value;
            get => _cfg.PinnedTags;
        }


        public ConfigurationService(Configuration cfg, KeywordMatchService keywordMatchService)
        {
            _keywordMatchService = keywordMatchService;
            _cfg = cfg;
            AvailableMatcherProviders = KeywordMatchService.GetMatchProviderInfos() ?? new List<MatchProviderInfo>();

            // init matcher
            if (KeywordMatch.EnabledMatchers.Count > 0)
            {
                foreach (var matcherProvider in AvailableMatcherProviders)
                {
                    matcherProvider.Enabled = false;
                }

                foreach (var enabledName in KeywordMatch.EnabledMatchers)
                {
                    var first = AvailableMatcherProviders.FirstOrDefault(x => x.Name == enabledName);
                    if (first != null)
                        first.Enabled = true;
                }
            }
            AvailableMatcherProviders.First(x => x.Name == DirectMatchProvider.GetName()).Enabled = true;
            AvailableMatcherProviders.First(x => x.Name == DirectMatchProvider.GetName()).IsEditable = false;
            KeywordMatch.EnabledMatchers = AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToList();
            _keywordMatchService.Init(KeywordMatch.EnabledMatchers.ToArray());
            // register matcher change event
            foreach (var info in AvailableMatcherProviders)
            {
                info.PropertyChanged += OnMatchProviderChangedHandler;
            }

            Save();
        }

        private void OnMatchProviderChangedHandler(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(MatchProviderInfo.Enabled))
            {
                KeywordMatch.EnabledMatchers = AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToList();
                Save();
                _keywordMatchService.Init(KeywordMatch.EnabledMatchers.ToArray());
            }
        }

        public bool CanSave = true;

        public void Save()
        {
            if (!CanSave) return;
            lock (this)
            {
                if (!CanSave) return;
                CanSave = false;
                for (int i = 0; i < 3; i++)
                {
                    if(i > 0)
                        Thread.Sleep(100);
                    try
                    {
                        var fi = new FileInfo(AppPathHelper.Instance.ProfileJsonPath);
                        if (fi?.Directory?.Exists == false)
                            fi.Directory.Create();
                        File.WriteAllText(AppPathHelper.Instance.ProfileJsonPath, JsonConvert.SerializeObject(this._cfg, Formatting.Indented), Encoding.UTF8);
                        break;
                    }
                    catch (Exception e)
                    {
                        MsAppCenterHelper.Error(e);
                    }
                }
                CanSave = true;
            }

            SetSelfStart();
        }


        public static Exception? SetSelfStartStatic(bool autoStart)
        {
            try
            {
#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({autoStart}, \"PRemoteM\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(autoStart, "PRemoteM");
#else
                SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({autoStart}, \"{Assert.APP_NAME}\")");
                SetSelfStartingHelper.SetSelfStartByRegistryKey(autoStart, Assert.APP_NAME);
#endif
                return null;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return e;
            }
        }


        public Exception? SetSelfStart()
        {
            var e = SetSelfStartStatic(General.AppStartAutomatically);
            if (e != null)
            {
                General.AppStartAutomatically = false;
            }
            return e;
        }



        // TODO remove after 2023.01.01
        [Obsolete]
        public static Configuration LoadFromIni(string iniPath, string dbDefaultPath)
        {
            var cfg = new Configuration();
            var ini = new Ini(iniPath);

            {
                const string sectionName = "General";
                cfg.General.AppStartAutomatically = ini.GetValue("AppStartAutomatically".ToLower(), sectionName, cfg.General.AppStartAutomatically);
                cfg.General.AppStartMinimized = ini.GetValue("AppStartMinimized".ToLower(), sectionName, cfg.General.AppStartMinimized);
#if FOR_MICROSOFT_STORE_ONLY
                Task.Factory.StartNew(async () =>
                {
                    cfg.General.AppStartAutomatically = await SetSelfStartingHelper.IsSelfStartByStartupTask("PRemoteM");
                });
#endif
            }

            {
                uint modifiers = 0;
                uint key = 0;
                if (ini.GetValue("Enable", "Launcher", "") == "")
                {
                    cfg.Launcher.LauncherEnabled = ini.GetValue("Enable".ToLower(), "Launcher", cfg.Launcher.LauncherEnabled);
                    modifiers = ini.GetValue("HotKeyModifiers".ToLower(), "Launcher", modifiers);
                    key = ini.GetValue("HotKeyKey".ToLower(), "Launcher", key);
                    cfg.Launcher.HotKeyModifiers = (HotkeyModifierKeys)modifiers;
                    cfg.Launcher.HotKeyKey = (Key)key;
                    if (cfg.Launcher.HotKeyModifiers == HotkeyModifierKeys.None || cfg.Launcher.HotKeyKey == Key.None)
                    {
                        cfg.Launcher.HotKeyModifiers = HotkeyModifierKeys.Alt;
                        cfg.Launcher.HotKeyKey = Key.M;
                    }
                }
            }

            cfg.KeywordMatch.EnabledMatchers = ini.GetValue("EnableProviders".ToLower(), "KeywordMatch", "").Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            cfg.Database.SqliteDatabasePath = ini.GetValue("DbPath".ToLower(), "DataSecurity", dbDefaultPath);

            {
                const string sectionName = "Theme";
                cfg.Theme.ThemeName = ini.GetValue("(PrmColorThemeName".ToLower(), sectionName, cfg.Theme.ThemeName);
                cfg.Theme.PrimaryMidColor = ini.GetValue("(PrimaryMidColor".ToLower(), sectionName, cfg.Theme.PrimaryMidColor);
                cfg.Theme.PrimaryLightColor = ini.GetValue("(PrimaryLightColor".ToLower(), sectionName, cfg.Theme.PrimaryLightColor);
                cfg.Theme.PrimaryDarkColor = ini.GetValue("(PrimaryDarkColor".ToLower(), sectionName, cfg.Theme.PrimaryDarkColor);
                cfg.Theme.PrimaryTextColor = ini.GetValue("(PrimaryTextColor".ToLower(), sectionName, cfg.Theme.PrimaryTextColor);
                cfg.Theme.AccentMidColor = ini.GetValue("(AccentMidColor".ToLower(), sectionName, cfg.Theme.AccentMidColor);
                cfg.Theme.AccentLightColor = ini.GetValue("(AccentLightColor".ToLower(), sectionName, cfg.Theme.AccentLightColor);
                cfg.Theme.AccentDarkColor = ini.GetValue("(AccentDarkColor".ToLower(), sectionName, cfg.Theme.AccentDarkColor);
                cfg.Theme.AccentTextColor = ini.GetValue("(AccentTextColor".ToLower(), sectionName, cfg.Theme.AccentTextColor);
                cfg.Theme.BackgroundColor = ini.GetValue("(BackgroundColor".ToLower(), sectionName, cfg.Theme.BackgroundColor);
                cfg.Theme.BackgroundTextColor = ini.GetValue("(BackgroundTextColor".ToLower(), sectionName, cfg.Theme.BackgroundTextColor);
            }

            return cfg;
        }
    }
}
