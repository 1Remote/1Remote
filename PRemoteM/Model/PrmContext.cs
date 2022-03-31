using System;
using System.IO;
using System.Windows;
using PRM.Model.DAO;
using PRM.Service;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;
using static PRM.Service.LanguageService;

namespace PRM.Model
{
    public class PrmContext : NotifyPropertyChangedBase
    {
        public readonly ConfigurationService ConfigurationService;
        public ProtocolConfigurationService ProtocolConfigurationService;

        private IDataService _dataService;
        public IDataService DataService
        {
            get => _dataService;
            set => SetAndNotifyIfChanged(ref _dataService, value);
        }

        public readonly LanguageService LanguageService;
        public readonly LauncherService LauncherService;
        public readonly ThemeService ThemeService;
        public readonly KeywordMatchService KeywordMatchService;
        public LocalityService LocalityService;
        public bool IsPortable;
        public GlobalData AppData { get; private set; }

        public PrmContext(KeywordMatchService keywordMatchService, ConfigurationService configurationService, LanguageService languageService, LauncherService launcherService, ThemeService themeService)
        {
            KeywordMatchService = keywordMatchService;
            ConfigurationService = configurationService;
            LanguageService = languageService;
            LauncherService = launcherService;
            ThemeService = themeService;
        }

        public void Init(bool isPortable)
        {
            IsPortable = isPortable;
            // init service
            ConfigurationService.Init(isPortable);
            ProtocolConfigurationService = new ProtocolConfigurationService(isPortable);
            var baseFolder = isPortable ? Environment.CurrentDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            LocalityService = new LocalityService(new Ini(Path.Combine(baseFolder, "locality.ini")));
            AppData = new GlobalData(ConfigurationService);
        }


        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumDbStatus InitSqliteDb(string sqlitePath = "")
        {
            if (string.IsNullOrWhiteSpace(sqlitePath))
            {
                sqlitePath = ConfigurationService.Database.SqliteDatabasePath;
                var fi = new FileInfo(sqlitePath);
                if (fi.Exists == false)
                    try
                    {
                        if (fi.Directory.Exists == false)
                            fi.Directory.Create();
                    }
                    catch
                    {
                        if (IsPortable)
                            sqlitePath = new DatabaseConfig().SqliteDatabasePath;
                    }
            }

            DataService?.Database_CloseConnection();

            if (!IoPermissionHelper.HasWritePermissionOnFile(sqlitePath))
            {
                DataService = null;
                return EnumDbStatus.AccessDenied;
            }

            DataService = new DataService();
            DataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(sqlitePath));
            var ret = DataService.Database_SelfCheck();
            if (ret == EnumDbStatus.OK)
                AppData.SetDbOperator(DataService);
            return ret;
        }
    }
}