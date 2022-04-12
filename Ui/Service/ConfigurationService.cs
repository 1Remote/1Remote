using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using PRM.Model.DAO;
using PRM.Utils;
using Shawn.Utils;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace PRM.Service
{
    public class EngagementSettings
    {
        public DateTime InstallTime = DateTime.Today.AddDays(1);
        public bool DoNotShowAgain = false;
        public string DoNotShowAgainVersionString = "";
        [JsonIgnore]
        public VersionHelper.Version DoNotShowAgainVersion => VersionHelper.Version.FromString(DoNotShowAgainVersionString);
        public DateTime LastRequestRatingsTime = DateTime.MinValue;
        public int ConnectCount = 0;
    }
    public class GeneralConfig
    {
        #region General
        public string CurrentLanguageCode = "en-us";
#if DEV
        public bool AppStartAutomatically = false;
        public bool AppStartMinimized = false;
#else
        public bool AppStartAutomatically = true;
        public bool AppStartMinimized = true;
#endif
        public bool ListPageIsCardView = false;
        public bool ConfirmBeforeClosingSession = false;
        #endregion
    }

    public class LauncherConfig
    {
        public bool LauncherEnabled = true;

#if DEV
        public HotkeyModifierKeys HotKeyModifiers = HotkeyModifierKeys.Shift;
#else
        public HotkeyModifierKeys HotKeyModifiers = HotkeyModifierKeys.Alt;
#endif

        public Key HotKeyKey = Key.M;
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

        private string _sqliteDatabasePath = "./" + ConfigurationService.AppName + ".db";
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
        private const string App = "PRemoteM";
#if DEV
#if FOR_MICROSOFT_STORE_ONLY
        public const string AppName = $"{App}(Store)_Debug";
#else
        public const string AppName = $"{App}_Debug";
#endif
#else
#if FOR_MICROSOFT_STORE_ONLY
        public const string AppName = $"{App}(Store)";
#else
        public const string AppName = $"{App}";
#endif
#endif
        public string JsonPath;

        private readonly KeywordMatchService _keywordMatchService;

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


        public ConfigurationService(bool isPortable, KeywordMatchService keywordMatchService)
        {
            _keywordMatchService = keywordMatchService;
            AvailableMatcherProviders = KeywordMatchService.GetMatchProviderInfos();
            _cfg = new Configuration();
            #region init

            // init path by `IsPortable`
            // default path of db
            // default value of json'
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            string oldIniFilePath = Path.Combine(Environment.CurrentDirectory, ConfigurationService.AppName + ".ini");
            if (File.Exists(oldIniFilePath) == false)
            {
                oldIniFilePath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".ini");
            }

            if (isPortable)
            {
                JsonPath = Path.Combine(Environment.CurrentDirectory, ConfigurationService.AppName + ".json");
                Database.SqliteDatabasePath = Path.Combine(Environment.CurrentDirectory, $"{ConfigurationService.AppName}.db");
            }
            else
            {
                if (Directory.Exists(appDateFolder) == false)
                    Directory.CreateDirectory(appDateFolder);
                JsonPath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".json");
                Database.SqliteDatabasePath = Path.Combine(appDateFolder, $"{ConfigurationService.AppName}.db");
            }
            #endregion

            if (_keywordMatchService == null)
                return;


            #region load settings
            // old user convert the 0.5 ini file to 0.6 json file
            if (File.Exists(oldIniFilePath) && File.Exists(JsonPath) == false)
            {
                try
                {
                    var cfg = LoadFromIni(oldIniFilePath, Database.SqliteDatabasePath);
                    _cfg = cfg;
                    Save();
                }
                finally
                {
                    File.Delete(oldIniFilePath);
                }
            }
            else if (File.Exists(JsonPath))
            {
                try
                {
                    var cfg = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(JsonPath));
                    if (cfg != null)
                        _cfg = cfg;
                }
                catch
                {
                    File.Delete(JsonPath);
                    Save();
                }
            }
            else
            {
                // new user
            }

            if (File.Exists(oldIniFilePath))
                File.Delete(oldIniFilePath);

            var fi = new FileInfo(Database.SqliteDatabasePath);
            if (fi.Exists == false)
                try
                {
                    if (fi.Directory.Exists == false)
                        fi.Directory.Create();
                }
                catch (Exception)
                {
                    if (isPortable)
                        Database.SqliteDatabasePath = new DatabaseConfig().SqliteDatabasePath;
                }
            #endregion






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

#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({General.AppStartAutomatically}, \"PRemoteM\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, "PRemoteM");
#else
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({General.AppStartAutomatically}, \"{AppName}\")");
            SetSelfStartingHelper.SetSelfStartByRegistryKey(General.AppStartAutomatically, AppName);
#endif
        }

        private void OnMatchProviderChangedHandler(object sender, PropertyChangedEventArgs args)
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
            if (CanSave == false)
                return;
            CanSave = false;
            var fi = new FileInfo(JsonPath);
            if (fi.Directory.Exists == false)
                fi.Directory.Create();
            lock (this)
            {
                File.WriteAllText(JsonPath, JsonConvert.SerializeObject(this._cfg, Formatting.Indented), Encoding.UTF8);
            }
#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({General.AppStartAutomatically}, \"PRemoteM\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, "PRemoteM");
#else
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({General.AppStartAutomatically}, \"{AppName}\")");
            SetSelfStartingHelper.SetSelfStartByRegistryKey(General.AppStartAutomatically, AppName);
#endif
            CanSave = true;
        }



        // TODO remove after 2023.01.01
        private static Configuration LoadFromIni(string iniPath, string dbDefaultPath)
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
