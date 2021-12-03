using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using Newtonsoft.Json;
using PRM.Core.I;
using PRM.Core.Model;
using Shawn.Utils;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace PRM.Core.Service
{

    public class GeneralConfig
    {
        #region General
        public string CurrentLanguageCode = "en-us";
        public bool AppStartAutomatically = true;
#if DEV
        public bool AppStartMinimized = false;
#else
        public bool AppStartMinimized  = true;
#endif
        #endregion
    }

    public class LauncherConfig
    {
        public bool LauncherEnabled = true;

#if DEV
        public HotkeyModifierKeys HotKeyModifiers = HotkeyModifierKeys.ShiftAlt;
#else
        public HotkeyModifierKeys HotKeyModifiers  = HotkeyModifierKeys.Alt;
#endif

        public Key HotKeyKey = Key.M;

        public bool AllowTagSearch = true;
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
        private string _sqliteDatabasePath = "";
        public const DatabaseType DatabaseType = I.DatabaseType.Sqlite;

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

        public List<string> PinnedTags { get; set; } = new List<string>();
    }

    public class ConfigurationService
    {
#if DEV
        public const string AppName = "PRemoteM_Debug";
        public const string AppFullName = "PersonalRemoteManager_Debug";
#else
        public const string AppName = "PRemoteM";
        public const string AppFullName = "PersonalRemoteManager";
#endif
        public readonly string JsonPath;

        private readonly KeywordMatchService _keywordMatchService;

        /// <summary>
        /// true -> Portable mode(for exe) setting files saved in Environment.CurrentDirectory;
        /// false -> ApplicationData mode(for Microsoft store.)  setting files saved in Environment.CurrentDirectory 
        /// </summary>
        public readonly bool IsPortable;

        public List<MatchProviderInfo> AvailableMatcherProviders { get; }
        private readonly Configuration _cfg = new Configuration();

        public GeneralConfig General => _cfg.General;
        public LauncherConfig Launcher => _cfg.Launcher;
        public KeywordMatchConfig KeywordMatch => _cfg.KeywordMatch;
        public DatabaseConfig Database => _cfg.Database;
        public ThemeConfig Theme => _cfg.Theme;

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
            IsPortable = isPortable;

            // init path by `IsPortable`
            // default path of db
            // default value of ini
            // default value of json
            string oldIniFilePath;
            if (IsPortable)
            {
                JsonPath = Path.Combine(Environment.CurrentDirectory, ConfigurationService.AppName + ".json");
                Database.SqliteDatabasePath = Path.Combine(Environment.CurrentDirectory, $"{ConfigurationService.AppName}.db");
                oldIniFilePath = Path.Combine(Environment.CurrentDirectory, ConfigurationService.AppName + ".ini");
                if (File.Exists(oldIniFilePath) == false && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName, ConfigurationService.AppName + ".ini")))
                {
                    oldIniFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName, ConfigurationService.AppName + ".ini");
                }
            }
            else
            {
                var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
                if (Directory.Exists(appDateFolder) == false)
                    Directory.CreateDirectory(appDateFolder);
                JsonPath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".json");
                oldIniFilePath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".ini");
                Database.SqliteDatabasePath = Path.Combine(appDateFolder, $"{ConfigurationService.AppName}.db");
            }

            // old user convert the 0.5 ini file to 0.6 json file
            if (File.Exists(oldIniFilePath))
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
            // load form json file
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
            if (KeywordMatch.EnabledMatchers.Count == 0)
            {
                KeywordMatch.EnabledMatchers = AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToList();
            }
            // register matcher change event
            foreach (var info in AvailableMatcherProviders)
            {
                info.PropertyChanged += OnMatchProviderChangedHandler;
            }

#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({General.AppStartAutomatically}, \"{AppName}\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, AppName);
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
        
        public void Save()
        {
            var fi = new FileInfo(JsonPath);
            if (fi.Directory.Exists == false)
                fi.Directory.Create();
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(this._cfg, Formatting.Indented), Encoding.UTF8);

#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({General.AppStartAutomatically}, \"{AppName}\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, AppName);
#else
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({General.AppStartAutomatically}, \"{AppName}\")");
            SetSelfStartingHelper.SetSelfStartByRegistryKey(General.AppStartAutomatically, AppName);
#endif
        }
        


        // TODO remove after 2022.03.01
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
                    cfg.General.AppStartAutomatically = await SetSelfStartingHelper.IsSelfStartByStartupTask(ConfigurationService.AppName);
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
                    cfg.Launcher.AllowTagSearch = ini.GetValue("AllowGroupNameSearch".ToLower(), "Launcher", cfg.Launcher.AllowTagSearch);
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
