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
using PRM.I;
using PRM.Utils;
using Shawn.Utils;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace PRM.Service
{
    #region Configuration
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
        public bool ListPageIsCardView = false;
        public bool ConfirmBeforeClosingSession = false;
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
        public const DatabaseType DatabaseType = I.DatabaseType.Sqlite;

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
        public List<string> PinnedTags { get; set; } = new List<string>();
    }

    #endregion

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

            #region init
            // init path by `IsPortable`
            // default path of db
            // default value of json
            if (IsPortable)
            {
                JsonPath = Path.Combine(Environment.CurrentDirectory, ConfigurationService.AppName + ".json");
                Database.SqliteDatabasePath = Path.Combine(Environment.CurrentDirectory, $"{ConfigurationService.AppName}.db");
            }
            else
            {
                var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
                if (Directory.Exists(appDateFolder) == false)
                    Directory.CreateDirectory(appDateFolder);
                JsonPath = Path.Combine(appDateFolder, ConfigurationService.AppName + ".json");
                Database.SqliteDatabasePath = Path.Combine(appDateFolder, $"{ConfigurationService.AppName}.db");
            }
            #endregion

            if (_keywordMatchService == null)
                return;


            #region load settings
            if (File.Exists(JsonPath))
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
            var fi = new FileInfo(Database.SqliteDatabasePath);
            if (fi.Exists == false)
                try
                {
                    if (fi.Directory.Exists == false)
                        fi.Directory.Create();
                }
                catch (Exception)
                {
                    if (IsPortable)
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
            var fi = new FileInfo(JsonPath);
            if (fi.Directory.Exists == false)
                fi.Directory.Create();
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(this._cfg, Formatting.Indented), Encoding.UTF8);

#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({General.AppStartAutomatically}, \"PRemoteM\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, "PRemoteM");
#else
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({General.AppStartAutomatically}, \"{AppName}\")");
            SetSelfStartingHelper.SetSelfStartByRegistryKey(General.AppStartAutomatically, AppName);
#endif
        }
    }
}
