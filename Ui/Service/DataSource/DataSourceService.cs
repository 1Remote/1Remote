using System;
using System.Collections.Generic;
using System.IO;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.View;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Service.DataSource
{
    public class DataSourceService
    {
        private readonly GlobalData _appData;

        public DataSourceService(GlobalData appData)
        {
            _appData = appData;
        }

        public SqliteDataSource? LocalDataSource { get; private set; } = null;

        public Dictionary<string, IDataSource> AdditionalSources = new Dictionary<string, IDataSource>();

        public List<ProtocolBaseViewModel> GetServers()
        {
            var ret = new List<ProtocolBaseViewModel>(100);
            if(LocalDataSource != null)
                ret.AddRange(LocalDataSource.GetServers());
            foreach (var dataSource in AdditionalSources)
            {
                var pbs = dataSource.Value.GetServers();
                ret.AddRange(pbs);
            }
            return ret;
        }

        public IDataSource? GetDataSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId) && LocalDataSource != null)
                return LocalDataSource;
            else if(AdditionalSources.ContainsKey(sourceId))
                return AdditionalSources[sourceId];
            return null;
        }


        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumDbStatus InitLocalDataSource(string sqlitePath = "")
        {
            if (string.IsNullOrWhiteSpace(sqlitePath))
            {
                sqlitePath = IoC.Get<ConfigurationService>().Database.SqliteDatabasePath;
                var fi = new FileInfo(sqlitePath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
            }

            LocalDataSource?.Database_CloseConnection();

            if (!IoPermissionHelper.HasWritePermissionOnFile(sqlitePath))
            {
                LocalDataSource = null;
                return EnumDbStatus.AccessDenied;
            }

            LocalDataSource = new SqliteDataSource(sqlitePath);
            LocalDataSource.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(sqlitePath));
            var ret = LocalDataSource.Database_SelfCheck();
            // TODO 
            //if (ret == EnumDbStatus.OK)
            //    _appData.SetDbOperator(LocalDataSource);
            return ret;
        }
    }
}