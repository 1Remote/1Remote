﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using _1RM.Model.DAO;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using VariableKeywordMatcher.Provider.DirectMatch;
using SetSelfStartingHelper = _1RM.Utils.SetSelfStartingHelper;

namespace _1RM.Service
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
        public bool ShowNoteFieldInListView = true;
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
        public bool ShowCredentials = true;
    }

    public class KeywordMatchConfig
    {
        /// <summary>
        /// name of the matchers
        /// </summary>
        public List<string> EnabledMatchers = new List<string>();
    }

    //public class DataSourcesConfig
    //{
    //    public SqliteSource LocalDataSource { get; set; } = new SqliteSource()
    //    {
    //        DataSourceName = DataSourceService.LOCAL_DATA_SOURCE_NAME,
    //        Path = "./" + Assert.APP_NAME + ".db"
    //    };
    //    public List<DataSourceBase> AdditionalDataSource { get; set; } = new List<DataSourceBase>();
    //}

    public class ThemeConfig
    {
        public string ThemeName = "Dark";

        public string PrimaryMidColor = "#323233";
        public string PrimaryLightColor = "#474748";
        public string PrimaryDarkColor = "#2d2d2d";
        public string PrimaryTextColor = "#cccccc";

        public string AccentMidColor = "#FF007ACC";
        public string AccentLightColor = "#FF32A7F4";
        public string AccentDarkColor = "#FF0061A3";
        public string AccentTextColor = "#FFFFFFFF";

        public string BackgroundColor = "#1e1e1e";
        public string BackgroundTextColor = "#cccccc";

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
        public int DatabaseCheckPeriod { get; set; } = 10;

        private string _sqliteDatabasePath = "./" + Assert.APP_NAME + ".db";
        public string SqliteDatabasePath
        {
            get => _sqliteDatabasePath;
            set => _sqliteDatabasePath = value.Replace(Environment.CurrentDirectory, ".");
        }

        public ThemeConfig Theme { get; set; } = new ThemeConfig();
        public EngagementSettings Engagement { get; set; } = new EngagementSettings();
        public List<string> PinnedTags { get; set; } = new List<string>();

        public static Configuration Load(string path)
        {
            var tmp = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(path));
            return tmp ?? new Configuration();
        }
    }

    public class ConfigurationService
    {
        private readonly KeywordMatchService _keywordMatchService;
        public readonly List<MatchProviderInfo> AvailableMatcherProviders;
        private readonly Configuration _cfg = new Configuration();

        public GeneralConfig General => _cfg.General;
        public LauncherConfig Launcher => _cfg.Launcher;
        public KeywordMatchConfig KeywordMatch => _cfg.KeywordMatch;
        public SqliteSource LocalDataSource { get; } = new SqliteSource();

        public int DatabaseCheckPeriod
        {
            get => _cfg.DatabaseCheckPeriod >= 0 ? (_cfg.DatabaseCheckPeriod > 99 ? 99 : _cfg.DatabaseCheckPeriod) : 0;
            set => _cfg.DatabaseCheckPeriod = value >= 0 ? (value > 99 ? 99 : value) : 0;
        }


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


        public List<DataSourceBase> AdditionalDataSource { get; set; } = new List<DataSourceBase>();



        public ConfigurationService(KeywordMatchService keywordMatchService, Configuration? cfg = null, List<DataSourceBase>? additionalDataSource = null)
        {
            if (cfg != null)
                _cfg = cfg;
            if (additionalDataSource != null)
                AdditionalDataSource = additionalDataSource;
            _keywordMatchService = keywordMatchService;
            AvailableMatcherProviders = KeywordMatchService.GetMatchProviderInfos() ?? new List<MatchProviderInfo>();

            LocalDataSource.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SqliteSource.Path))
                {
                    _cfg.SqliteDatabasePath = LocalDataSource.Path;
                }
            };

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


            AdditionalDataSource = DataSourceService.AdditionalSourcesLoadFromProfile(AppPathHelper.Instance.ProfileAdditionalDataSourceJsonPath);
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
            AdditionalDataSource = AdditionalDataSource.Distinct().ToList();
            if (!CanSave) return;
            lock (this)
            {
                if (!CanSave) return;
                CanSave = false;
                {
                    var fi = new FileInfo(AppPathHelper.Instance.ProfileJsonPath);
                    if (fi?.Directory?.Exists == false)
                        fi.Directory.Create();
                    File.WriteAllText(AppPathHelper.Instance.ProfileJsonPath, JsonConvert.SerializeObject(this._cfg, Formatting.Indented), Encoding.UTF8);
                }

                DataSourceService.AdditionalSourcesSaveToProfile(AppPathHelper.Instance.ProfileAdditionalDataSourceJsonPath, AdditionalDataSource);

                CanSave = true;
            }

            SetSelfStart();
        }

        public static Exception? CheckSetSelfStart()
        {
            try
            {
#if FOR_MICROSOFT_STORE_ONLY
                SetSelfStartingHelper.SetSelfStartByStartupTask(true, Assert.APP_NAME);
                SetSelfStartingHelper.SetSelfStartByStartupTask(false, Assert.APP_NAME);
#else
                SetSelfStartingHelper.SetSelfStartByRegistryKey(true, Assert.APP_NAME);
                SetSelfStartingHelper.SetSelfStartByRegistryKey(false, Assert.APP_NAME);
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
            try
            {
#if FOR_MICROSOFT_STORE_ONLY
                SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, Assert.APP_NAME);
#else
                SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({General.AppStartAutomatically}, \"{Assert.APP_NAME}\")");
                SetSelfStartingHelper.SetSelfStartByRegistryKey(General.AppStartAutomatically, Assert.APP_NAME);
#endif
                return null;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                General.AppStartAutomatically = false;
                return e;
            }
        }


        public static ConfigurationService LoadFromAppPath(KeywordMatchService keywordMatchService)
        {
            var cfg = new Configuration();

            if (File.Exists(AppPathHelper.Instance.ProfileJsonPath))
            {
                var tmp = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(AppPathHelper.Instance.ProfileJsonPath));
                if (tmp != null)
                    cfg = tmp;
            }

            var ads = DataSourceService.AdditionalSourcesLoadFromProfile(AppPathHelper.Instance.ProfileAdditionalDataSourceJsonPath);

            return new ConfigurationService(keywordMatchService, cfg, ads);
        }
    }
}
