using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;
using _1RM.Model.Protocol.Base;
using _1RM.View;
using com.github.xiangyuecn.rsacsharp;
using JsonKnownTypes;
using Newtonsoft.Json;
using Shawn.Utils;
using Stylet;

namespace _1RM.Service.DataSource.Model
{
    public abstract partial class DataSourceBase : NotifyPropertyChangedBase
    {
        private bool _isWritable = true;
        [JsonIgnore]
        public bool IsWritable
        {
            get => _isWritable;
            protected set => SetAndNotifyIfChanged(ref _isWritable, value);
        }

        /// <summary>
        /// 已缓存的服务器信息
        /// </summary>
        [JsonIgnore]
        public List<ProtocolBaseViewModel> CachedProtocols { get; protected set; } = new List<ProtocolBaseViewModel>();

        [JsonIgnore]
        public virtual long LastReadFromDataSourceMillisecondsTimestamp { get; protected set; } = 0;
        [JsonIgnore]
        public virtual long DataSourceDataUpdateTimestamp { get; set; } = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        public virtual bool NeedRead()
        {
            if (Status == EnumDbStatus.OK)
            {
                var dataBase = GetDataBase();
                DataSourceDataUpdateTimestamp = dataBase.GetDataUpdateTimestamp();
                //SimpleLogHelper.Debug($"Datasource {DataSourceName} {LastReadFromDataSourceMillisecondsTimestamp} < {DataSourceDataUpdateTimestamp}");
                return LastReadFromDataSourceMillisecondsTimestamp < DataSourceDataUpdateTimestamp;
            }
            return false;
        }



        /// <summary>
        /// 返回服务器信息(服务器信息已指向数据源)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProtocolBaseViewModel> GetServers(bool focus = false)
        {
            lock (this)
            {
                if (focus == false
                    && LastReadFromDataSourceMillisecondsTimestamp >= DataSourceDataUpdateTimestamp)
                {
                    return CachedProtocols;
                }

                LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (Database_OpenConnection() == false)
                {
                    Status = EnumDbStatus.AccessDenied;
                    return CachedProtocols;
                }
                var protocols = Database_GetServers();
                CachedProtocols = new List<ProtocolBaseViewModel>(protocols.Count);
                foreach (var protocol in protocols)
                {
                    try
                    {
                        Execute.OnUIThreadSync(() =>
                        {
                            var vm = new ProtocolBaseViewModel(protocol);
                            CachedProtocols.Add(vm);
                        });
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Info(e);
                    }
                }

                Status = EnumDbStatus.OK;
                return CachedProtocols;
            }
        }

        public abstract IDataBase GetDataBase();

        public bool Database_OpenConnection(int connectTimeOutSeconds = 5)
        {
            var dataBase = GetDataBase();
            // open db or create db.
            Debug.Assert(dataBase != null);

            var connectionString = GetConnectionString(connectTimeOutSeconds);
            if (connectionString != _lastConnectionString)
            {
                dataBase.CloseConnection();
                _lastConnectionString = connectionString;
            }

            dataBase.OpenNewConnection(DatabaseType, connectionString);
            try
            {
                dataBase.InitTables();
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }
            dataBase.OpenNewConnection(DatabaseType, connectionString);
            if (dataBase.IsConnected())
            {
                if (Status != EnumDbStatus.NotConnectedYet)
                {
                    Status = EnumDbStatus.LostConnection;
                }
                else
                {
                    Status = EnumDbStatus.AccessDenied;
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        public virtual void Database_CloseConnection()
        {
            var dataBase = GetDataBase();
            Debug.Assert(dataBase != null);
            if (dataBase.IsConnected())
                dataBase.CloseConnection();
        }


        private static string _lastConnectionString = "";
        public virtual EnumDbStatus Database_SelfCheck(int connectTimeOutSeconds = 5)
        {
            EnumDbStatus ret = EnumDbStatus.NotConnectedYet;
            var dataBase = GetDataBase();

            var connectionString = GetConnectionString(connectTimeOutSeconds);
            if (connectionString != _lastConnectionString)
            {
                dataBase.CloseConnection();
                _lastConnectionString = connectionString;
            }

            // check connectable
            if (dataBase.IsConnected() == false)
            {
                try
                {
                    dataBase.OpenNewConnection(DatabaseType, connectionString);
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    if (Status != EnumDbStatus.NotConnectedYet)
                    {
                        ret = EnumDbStatus.LostConnection;
                    }
                    else
                    {
                        ret = EnumDbStatus.AccessDenied;
                    }
                    Status = ret;
                    return Status;
                }
            }

            if (dataBase.IsConnected())
            {
                // try create table
                try
                {
                    dataBase.InitTables();
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Warning(e);
                }

                // check readable
                IsWritable = dataBase.CheckWritable();
                var isReadable = dataBase.CheckReadable();
                if (isReadable == false)
                {
                    Status = EnumDbStatus.AccessDenied;
                    return Status;
                }

                ret = EnumDbStatus.OK;
            }

            Status = ret;
            return Status;
        }

        public bool Database_InsertServer(ProtocolBase server)
        {
            var tmp = (ProtocolBase)server.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            tmp.EncryptToDatabaseLevel();
            GetDataBase()?.AddServer(tmp);
            server.Id = tmp.Id;
            server.DataSourceName = this.DataSourceName;
            LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return string.IsNullOrEmpty(server.Id) == false;
        }

        public bool Database_InsertServer(List<ProtocolBase> servers)
        {
            var cloneList = new List<ProtocolBase>();
            foreach (var server in servers)
            {
                var tmp = (ProtocolBase)server.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                cloneList.Add(tmp);
            }
            GetDataBase()?.AddServer(cloneList);
            for (int i = 0; i < servers.Count(); i++)
            {
                servers[i].Id = cloneList[i].Id;
            }
            LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return servers.All(x => string.IsNullOrEmpty(x.Id) == false);
        }

        public bool Database_UpdateServer(ProtocolBase org)
        {
            Debug.Assert(org.IsTmpSession() == false);

            var tmp = (ProtocolBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            tmp.EncryptToDatabaseLevel();
            var ret = GetDataBase()?.UpdateServer(tmp) == true;
            if (ret == true)
            {
                LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            return ret;
        }

        public bool Database_UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            var cloneList = new List<ProtocolBase>();
            foreach (var server in servers)
            {
                var tmp = (ProtocolBase)server.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                cloneList.Add(tmp);
            }

            var ret = GetDataBase()?.UpdateServer(cloneList) == true;
            if (ret == true)
            {
                LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            return ret;
        }

        public bool Database_DeleteServer(string id)
        {
            if (_isWritable)
            {
                var ret = GetDataBase()?.DeleteServer(id) == true;
                if (ret == true)
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return ret;
            }
            return false;
        }

        public bool Database_DeleteServer(IEnumerable<string> ids)
        {
            if (_isWritable)
            {
                var ret = GetDataBase()?.DeleteServer(ids) == true;
                if (ret == true)
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return ret;
            }
            return false;
        }

        public List<ProtocolBase> Database_GetServers()
        {
            var ps = GetDataBase()?.GetServers() ?? new List<ProtocolBase>();
            foreach (var protocolBase in ps)
            {
                protocolBase.DataSourceName = DataSourceName;
            }
            return ps;
        }

        public int Database_GetServersCount()
        {
            var s = GetDataBase()?.GetServers() ?? new List<ProtocolBase>();
            return s.Count;
        }
    }
}
