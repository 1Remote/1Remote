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

namespace PRM.Core.Model
{
    public class PrmContext : NotifyPropertyChangedBase
    {
        public readonly ConfigurationService ConfigurationService;
        public DataService DataService;
        public readonly LanguageService LanguageService;
        public readonly LauncherService LauncherService;
        public readonly ThemeService ThemeService;
        public readonly LocalityService LocalityService;
        public readonly KeywordMatchService KeywordMatchService;

        public PrmContext(ResourceDictionary applicationResourceDictionary)
        {
            KeywordMatchService = new KeywordMatchService();
            ConfigurationService = new ConfigurationService(KeywordMatchService);
            ConfigurationService.Load();
            LanguageService = new LanguageService(applicationResourceDictionary, ConfigurationService.General.CurrentLanguageCode);
            PRM.Core.Service.LanguageService.TmpLanguageService = LanguageService;
            LauncherService = new LauncherService(LanguageService);
            ThemeService = new ThemeService(applicationResourceDictionary);
            ThemeService.ApplyTheme(ConfigurationService.Theme);
            DataService = new DataService();
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            LocalityService = new LocalityService(new Ini(Path.Combine(appDateFolder, "locality.ini")));
            AppData = new GlobalData(LocalityService);
        }

        public GlobalData AppData { get; }


        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumDbStatus InitSqliteDb(string sqlitePath)
        {
            if (!IOPermissionHelper.HasWritePermissionOnFile(sqlitePath))
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