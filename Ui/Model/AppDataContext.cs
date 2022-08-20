using System;
using System.Diagnostics;
using System.IO;
using _1RM.Model.DAO;
using _1RM.Service;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Model
{
    public class AppDataContext : NotifyPropertyChangedBase
    {
        public readonly ProtocolConfigurationService ProtocolConfigurationService;

        private IDataService? _dataService;
        public IDataService? DataService
        {
            get => _dataService;
            set => SetAndNotifyIfChanged(ref _dataService, value);
        }

        private readonly GlobalData _appData;

        public AppDataContext(ProtocolConfigurationService protocolConfigurationService, GlobalData appData)
        {
            ProtocolConfigurationService = protocolConfigurationService;
            _appData = appData;
        }



        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumDbStatus InitSqliteDb(string sqlitePath = "", IDataService? newDataService = null)
        {
            if (string.IsNullOrWhiteSpace(sqlitePath))
            {
                sqlitePath = IoC.Get<ConfigurationService>().Database.SqliteDatabasePath;
                var fi = new FileInfo(sqlitePath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
            }

            DataService?.Database_CloseConnection();

            if (!IoPermissionHelper.HasWritePermissionOnFile(sqlitePath))
            {
                DataService = null;
                return EnumDbStatus.AccessDenied;
            }

            if (newDataService != null)
                DataService = newDataService;
            else
                DataService = IoC.Get<IDataService>();
            DataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(sqlitePath));
            var ret = DataService.Database_SelfCheck();
            if (ret == EnumDbStatus.OK)
                _appData.SetDbOperator(DataService);
            return ret;
        }
    }
}