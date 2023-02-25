using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.Model;
using _1RM.View;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using Newtonsoft.Json;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Service.DataSource
{
    public class DataSourceService : NotifyPropertyChangedBase
    {
        public const int CHECK_UPDATE_PERIOD = 5;
        public const string LOCAL_DATA_SOURCE_NAME = "Local";

        public DataSourceService()
        {
        }

        public SqliteSource? LocalDataSource { get; private set; } = null;

        public readonly ConcurrentDictionary<string, DataSourceBase> AdditionalSources = new ConcurrentDictionary<string, DataSourceBase>();

        public List<ProtocolBaseViewModel> GetServers(bool focus)
        {
            lock (this)
            {
                var ret = new List<ProtocolBaseViewModel>(100);
                if (LocalDataSource != null)
                    ret.AddRange(LocalDataSource.GetServers(focus));
                foreach (var dataSource in AdditionalSources)
                {
                    try
                    {
                        var pbs = dataSource.Value.GetServers(focus);
                        ret.AddRange(pbs);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Warning(e);
                    }
                }
                return ret;
            }
        }

        public DataSourceBase? GetDataSource(string sourceId = LOCAL_DATA_SOURCE_NAME)
        {
            if (string.IsNullOrEmpty(sourceId) || sourceId == LOCAL_DATA_SOURCE_NAME)
                return LocalDataSource;
            if (AdditionalSources.ContainsKey(sourceId))
                return AdditionalSources[sourceId];
            return null;
        }


        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        public EnumDatabaseStatus InitLocalDataSource(SqliteSource? sqliteConfig = null)
        {
            if (sqliteConfig == null)
            {
                // sqliteConfig == null means we need init a new local data source.
                // so read from configs and find where db is.
                sqliteConfig = IoC.Get<ConfigurationService>().LocalDataSource;
                if (string.IsNullOrWhiteSpace(sqliteConfig.Path))
                    sqliteConfig.Path = AppPathHelper.Instance.SqliteDbDefaultPath;
                var fi = new FileInfo(sqliteConfig.Path);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
            }

            LocalDataSource?.Database_CloseConnection();
            sqliteConfig.DataSourceName = LOCAL_DATA_SOURCE_NAME;
            if (!IoPermissionHelper.HasWritePermissionOnFile(sqliteConfig.Path))
            {
                LocalDataSource = null;
                return EnumDatabaseStatus.AccessDenied;
            }
            LocalDataSource = sqliteConfig;
            var ret = LocalDataSource.Database_SelfCheck();
            RaisePropertyChanged(nameof(LocalDataSource));
            return ret;
        }


        public EnumDatabaseStatus AddOrUpdateDataSource(DataSourceBase config, int connectTimeOutSeconds = 5, bool doReload = true)
        {
            try
            {
                if (config is SqliteSource { DataSourceName: LOCAL_DATA_SOURCE_NAME } localConfig)
                {
                    return InitLocalDataSource(localConfig);
                }

                // remove the old one
                var olds = AdditionalSources.Where(x => x.Value == config);
                foreach (var pair in olds)
                {
                    AdditionalSources.TryRemove(pair.Key, out var _);
                }
                AdditionalSources.TryRemove(config.DataSourceName, out var _);


                config.Database_CloseConnection();
                var ret = config.Database_SelfCheck();
                AdditionalSources.AddOrUpdate(config.DataSourceName, config, (name, source) => config);
                return ret;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
                return EnumDatabaseStatus.AccessDenied;
            }
            finally
            {
                if (doReload)
                    IoC.Get<GlobalData>().ReloadServerList();
            }
        }

        public void RemoveDataSource(string name)
        {
            if (name == LOCAL_DATA_SOURCE_NAME)
                return;
            else if (AdditionalSources.ContainsKey(name))
            {
                AdditionalSources[name].Database_CloseConnection();
                if (AdditionalSources.TryRemove(name, out _))
                {
                    IoC.Get<GlobalData>().ReloadServerList(true);
                }
            }
        }

        public static List<DataSourceBase> AdditionalSourcesLoadFromProfile(string path)
        {
            var ads = new List<DataSourceBase>();
            if (File.Exists(path))
            {
                var tmp = JsonConvert.DeserializeObject<List<DataSourceBase>>(File.ReadAllText(AppPathHelper.Instance.ProfileAdditionalDataSourceJsonPath));
                if (tmp != null)
                    ads = tmp;
            }
            return ads;
        }


        public static void AdditionalSourcesSaveToProfile(string path, List<DataSourceBase> sources)
        {
            if (sources.Count == 0)
            {
                var fi = new FileInfo(path);
                if (fi.Exists)
                    fi.Delete();
            }
            else
            {
                var fi = new FileInfo(path);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                File.WriteAllText(path, JsonConvert.SerializeObject(sources, Formatting.Indented), Encoding.UTF8);
            }
        }
    }

    public static class DataSourceServiceExtend
    {
        public static DataSourceBase? GetDataSource(this ProtocolBase protocol)
        {
            return IoC.Get<DataSourceService>().GetDataSource(protocol.DataSourceName);
        }
        public static DataSourceBase? GetDataSource(this ProtocolBaseViewModel protocol)
        {
            return IoC.Get<DataSourceService>().GetDataSource(protocol.Server.DataSourceName);
        }
    }
}