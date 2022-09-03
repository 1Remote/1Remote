using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.Model;
using _1RM.View;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Service.DataSource
{
    public class DataSourceService : NotifyPropertyChangedBase
    {
        private readonly GlobalData _appData;

        public const int CHECK_UPDATE_PERIOD = 5;

        public DataSourceService(GlobalData appData)
        {
            _appData = appData;
        }

        public SqliteDatabaseSource? LocalDataSource { get; private set; } = null;

        public Dictionary<string, IDataSource> AdditionalSources = new Dictionary<string, IDataSource>();

        public bool NeedRead()
        {
            if (LocalDataSource?.NeedRead() == true)
                return true;
            return AdditionalSources.Any(x => x.Value.NeedRead() == true);
        }

        public List<ProtocolBaseViewModel> GetServers()
        {
            var ret = new List<ProtocolBaseViewModel>(100);
            if (LocalDataSource != null)
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
            if (AdditionalSources.ContainsKey(sourceId))
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
                sqlitePath = IoC.Get<ConfigurationService>().DataSource.LocalDatabasePath;
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
            //Ulid.NewUlid(rng).ToString();
            LocalDataSource = new SqliteDatabaseSource("", new SqliteModel("")
            {
                Path = sqlitePath
            });
            LocalDataSource.Database_OpenConnection();
            var ret = LocalDataSource.Database_SelfCheck();
            RaisePropertyChanged(nameof(LocalDataSource));
            // TODO 
            if (ret == EnumDbStatus.OK)
                _appData.SetDbOperator(this);
            return ret;
        }
    }

    public static class DataSourceServiceExtend
    {
        public static IDataSource? GetDataSource(this ProtocolBase protocol)
        {
            return IoC.Get<DataSourceService>().GetDataSource(protocol.DataSourceId);
        }
        public static IDataSource? GetDataSource(this ProtocolBaseViewModel protocol)
        {
            return IoC.Get<DataSourceService>().GetDataSource(protocol.Server.DataSourceId);
        }
    }
}