using System;
using System.Collections.Concurrent;
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
        public const string LOCAL_DATA_SOURCE_NAME = "Local";

        public DataSourceService(GlobalData appData)
        {
            _appData = appData;

        }

        public SqliteDatabaseSource? LocalDataSource { get; private set; } = null;

        private readonly ConcurrentDictionary<string, IDataSource> _additionalSources = new ConcurrentDictionary<string, IDataSource>();

        public bool NeedRead()
        {
            if (LocalDataSource?.NeedRead() == true)
                return true;
            return _additionalSources.Any(x => x.Value.NeedRead() == true);
        }

        public List<ProtocolBaseViewModel> GetServers()
        {
            var ret = new List<ProtocolBaseViewModel>(100);
            if (LocalDataSource != null)
                ret.AddRange(LocalDataSource.GetServers());
            foreach (var dataSource in _additionalSources)
            {
                var pbs = dataSource.Value.GetServers();
                ret.AddRange(pbs);
            }
            return ret;
        }

        public IDataSource? GetDataSource(string sourceId = LOCAL_DATA_SOURCE_NAME)
        {
            if (string.IsNullOrEmpty(sourceId) || sourceId == LOCAL_DATA_SOURCE_NAME)
                return LocalDataSource;
            if (_additionalSources.ContainsKey(sourceId))
                return _additionalSources[sourceId];
            return null;
        }


        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        public EnumDbStatus InitLocalDataSource(SqliteConfig? sqliteConfig = null)
        {
            if (sqliteConfig == null)
            {
                sqliteConfig = IoC.Get<ConfigurationService>().DataSource.LocalDataSourceConfig;
                if (string.IsNullOrWhiteSpace(sqliteConfig.Path))
                    sqliteConfig.Path = AppPathHelper.Instance.SqliteDbDefaultPath;
                var fi = new FileInfo(sqliteConfig.Path);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
            }

            LocalDataSource?.Database_CloseConnection();
            sqliteConfig.Name = LOCAL_DATA_SOURCE_NAME;
            if (!IoPermissionHelper.HasWritePermissionOnFile(sqliteConfig.Path))
            {
                LocalDataSource = null;
                return EnumDbStatus.AccessDenied;
            }
            LocalDataSource = new SqliteDatabaseSource(sqliteConfig);
            LocalDataSource.Database_OpenConnection();
            var ret = LocalDataSource.Database_SelfCheck();
            RaisePropertyChanged(nameof(LocalDataSource));
            if (ret == EnumDbStatus.OK)
            {
                // TODO No need
                _appData.SetDbOperator(this);
            }

            return ret;
        }

        /// <summary>
        /// TODO: need ASYNC support！
        /// </summary>
        public EnumDbStatus AddOrUpdateDataSource(DataSourceConfigBase config)
        {
            {
                if (config is SqliteConfig { Name: LOCAL_DATA_SOURCE_NAME } sqliteConfig)
                {
                    return InitLocalDataSource(sqliteConfig);
                }
            }

            if (_additionalSources.ContainsKey(config.Name))
            {
                _additionalSources[config.Name].Database_CloseConnection();
            }

            switch (config)
            {
                case SqliteConfig sqliteConfig:
                    {
                        var s = new SqliteDatabaseSource(sqliteConfig);
                        var ret = s.Database_SelfCheck();
                        if (ret == EnumDbStatus.OK)
                        {
                            _additionalSources.AddOrUpdate(config.Name, s, (name, source) => s);
                        }
                        return ret;
                    }
                case MysqlConfig mysqlConfig:
                    {
                        var s = new MysqlDatabaseSource(mysqlConfig);
                        var ret = s.Database_SelfCheck();
                        if (ret == EnumDbStatus.OK)
                        {
                            _additionalSources.AddOrUpdate(config.Name, s, (name, source) => s);
                        }
                        return ret;
                    }
                default:
                    throw new NotSupportedException($"{config.GetType()} is not a supported type");
            }
        }

        public void RemoveDataSource(string name)
        {
            if (name == LOCAL_DATA_SOURCE_NAME)
                return;
            else if (_additionalSources.ContainsKey(name))
            {
                _additionalSources[name].Database_CloseConnection();
                _additionalSources.TryRemove(name, out _);
            }
        }
    }

    public static class DataSourceServiceExtend
    {
        public static IDataSource? GetDataSource(this ProtocolBase protocol)
        {
            return IoC.Get<DataSourceService>().GetDataSource(protocol.DataSourceName);
        }
        public static IDataSource? GetDataSource(this ProtocolBaseViewModel protocol)
        {
            return IoC.Get<DataSourceService>().GetDataSource(protocol.Server.DataSourceName);
        }
    }
}