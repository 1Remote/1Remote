using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.DB;
using PRM.Core.DB.Dapper;
using PRM.Core.I;
using PRM.Core.Service;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.Core.Model
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
        public readonly LocalityService LocalityService;
        public readonly KeywordMatchService KeywordMatchService;
        public readonly bool IsPortable;

        public PrmContext(bool isPortable, ResourceDictionary applicationResourceDictionary)
        {
            IsPortable = isPortable;
            // init service
            KeywordMatchService = new KeywordMatchService();
            ConfigurationService = new ConfigurationService(isPortable, KeywordMatchService);
            ProtocolConfigurationService = new ProtocolConfigurationService(isPortable);
            if (applicationResourceDictionary != null)
            {
                LanguageService = new LanguageService(applicationResourceDictionary, ConfigurationService.General.CurrentLanguageCode);
                LanguageService.TmpLanguageService = LanguageService;
                ThemeService = new ThemeService(applicationResourceDictionary, ConfigurationService.Theme);
            }
            LauncherService = new LauncherService(LanguageService);

            var baseFolder = isPortable? Environment.CurrentDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            LocalityService = new LocalityService(new Ini(Path.Combine(baseFolder, "locality.ini")));
            AppData = new GlobalData(ConfigurationService);
        }

        public GlobalData AppData { get; }

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
                    catch (Exception e)
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