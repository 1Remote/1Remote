using System;
using System.Diagnostics;
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
        public readonly ProtocolConfigurationService ProtocolConfigurationService;

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
        public readonly LocalityService LocalityService;
        public bool IsPortable { get; private set; }
        public readonly GlobalData AppData;

        public PrmContext(KeywordMatchService keywordMatchService, ConfigurationService configurationService, LanguageService languageService, LauncherService launcherService, ThemeService themeService, LocalityService localityService, ProtocolConfigurationService protocolConfigurationService, GlobalData appData)
        {
            KeywordMatchService = keywordMatchService;
            ConfigurationService = configurationService;
            LanguageService = languageService;
            LauncherService = launcherService;
            ThemeService = themeService;
            LocalityService = localityService;
            ProtocolConfigurationService = protocolConfigurationService;
            AppData = appData;
        }

        public void Init(bool isPortable)
        {
            IsPortable = isPortable;
            // init service
        }


        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumDbStatus InitSqliteDb(string sqlitePath = "")
        {
            if (string.IsNullOrWhiteSpace(sqlitePath))
            {
                sqlitePath = IoC.Get<ConfigurationService>().Database.SqliteDatabasePath;
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

            
            DataService = IoC.Get<IDataService>();
            DataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(sqlitePath));
            var ret = DataService.Database_SelfCheck();
            if (ret == EnumDbStatus.OK)
                AppData.SetDbOperator(DataService);
            return ret;
        }



        public static EnumDbStatus SetupSqliteDbConnection(DataService dataService, string sqlitePath)
        {
            Debug.Assert(dataService != null);
            Debug.Assert(string.IsNullOrEmpty(sqlitePath));
            dataService.Database_CloseConnection();
            if (!IoPermissionHelper.HasWritePermissionOnFile(sqlitePath))
            {
                return EnumDbStatus.AccessDenied;
            }
            dataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(sqlitePath));
            var ret = dataService.Database_SelfCheck();
            return ret;
        }
    }
}