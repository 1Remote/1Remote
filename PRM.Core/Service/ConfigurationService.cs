using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
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
                if (!string.IsNullOrEmpty(_sqliteDatabasePath)) return _sqliteDatabasePath;
                var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
                if (!Directory.Exists(appDateFolder))
                    Directory.CreateDirectory(appDateFolder);
                _sqliteDatabasePath = Path.Combine(appDateFolder, $"{ConfigurationService.AppName}.db");
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

        public ConfigurationService()
        {
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            var jsonPath = Path.Combine(Environment.CurrentDirectory, ConfigurationService.AppName + ".json");
            if (IOPermissionHelper.IsFileCanWriteNow(jsonPath) == false)
            {
                jsonPath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".json");
            }
#if FOR_MICROSOFT_STORE_ONLY
            jsonPath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".json");
#endif
            JsonPath = jsonPath;


            AvailableMatcherProviders = KeywordMatchService.GetMatchProviderInfos();
            foreach (var info in AvailableMatcherProviders)
            {
                info.PropertyChanged += OnMatchProviderChangedHandler;
            }
        }

        private void OnMatchProviderChangedHandler(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(MatchProviderInfo.Enabled))
            {
                KeywordMatch.EnabledMatchers = AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToList();
                Save();
            }
        }

        public List<MatchProviderInfo> AvailableMatcherProviders { get; }
        private Configuration cfg = new Configuration();

        public GeneralConfig General => cfg.General;
        public LauncherConfig Launcher => cfg.Launcher;
        public KeywordMatchConfig KeywordMatch => cfg.KeywordMatch;
        public DatabaseConfig Database => cfg.Database;
        public ThemeConfig Theme => cfg.Theme;



        public void Load()
        {
            lock (this)
            {
                foreach (var info in AvailableMatcherProviders)
                {
                    info.PropertyChanged -= OnMatchProviderChangedHandler;
                }

                var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
                var iniPath = Path.Combine(Environment.CurrentDirectory, ConfigurationService.AppName + ".ini");
                if (IOPermissionHelper.IsFileCanWriteNow(iniPath) == false)
                {
                    iniPath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".ini");
                }
#if FOR_MICROSOFT_STORE_ONLY
            iniPath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".ini");
#endif
                if (File.Exists(iniPath))
                {
                    try
                    {
                        var cfg = LoadFromIni(iniPath);
                        this.cfg = cfg;
                        Save();
                    }
                    finally
                    {
                        File.Delete(iniPath);
                    }
                }
                else if (File.Exists(JsonPath))
                {
                    try
                    {
                        var cfg = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(JsonPath));
                        if (cfg != null)
                            this.cfg = cfg;
                    }
                    catch
                    {
                        File.Delete(JsonPath);
                        Save();
                    }
                }


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

                foreach (var info in AvailableMatcherProviders)
                {
                    info.PropertyChanged += OnMatchProviderChangedHandler;
                }
            }
        }

        public void Save()
        {
            var fi = new FileInfo(JsonPath);
            if (fi.Directory.Exists == false)
                fi.Directory.Create();
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(this.cfg, Formatting.Indented), Encoding.UTF8);
        }



        // TODO remove after 2022.01.01
        private Configuration LoadFromIni(string iniPath)
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
                    AppStartAutomatically = await SetSelfStartingHelper.IsSelfStartByStartupTask(ConfigurationService.AppName);
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

            {
                cfg.KeywordMatch.EnabledMatchers = ini.GetValue("EnableProviders".ToLower(), "KeywordMatch", "").Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            {
                cfg.Database.SqliteDatabasePath = ini.GetValue("DbPath".ToLower(), "DataSecurity", cfg.Database.SqliteDatabasePath);
            }

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
