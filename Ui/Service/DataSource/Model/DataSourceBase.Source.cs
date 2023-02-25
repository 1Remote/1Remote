using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1Remote.Security;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.View;
using com.github.xiangyuecn.rsacsharp;
using JsonKnownTypes;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
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
            if (Status == EnumDatabaseStatus.OK)
            {
                var dataBase = GetDataBase();
                DataSourceDataUpdateTimestamp = dataBase.GetDataUpdateTimestamp();
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
                var count = Database_GetServersCount();

                if (count == CachedProtocols.Count
                    && focus == false
                    && LastReadFromDataSourceMillisecondsTimestamp >= DataSourceDataUpdateTimestamp)
                {
                    return CachedProtocols;
                }

                LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (Database_OpenConnection() == false)
                {
                    Status = EnumDatabaseStatus.AccessDenied;
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

                Status = EnumDatabaseStatus.OK;
                return CachedProtocols;
            }
        }

        public abstract IDatabase GetDataBase();

        public bool Database_OpenConnection(int connectTimeOutSeconds = 5)
        {
            var dataBaseRef = GetDataBase();
            // open db or create db.
            Debug.Assert(dataBaseRef != null);


            var connectionString = GetConnectionString(connectTimeOutSeconds);
            if (connectionString != _lastConnectionString)
            {
                dataBaseRef.CloseConnection();
                _lastConnectionString = connectionString;
            }

            try
            {
                dataBaseRef.OpenNewConnection(DatabaseType, connectionString);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }

            // check connectable
            if (dataBaseRef.IsConnected() == false)
            {
                if (Status != EnumDatabaseStatus.NotConnectedYet)
                {
                    Status = EnumDatabaseStatus.LostConnection;
                }
                else
                {
                    Status = EnumDatabaseStatus.AccessDenied;
                }
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
        public virtual EnumDatabaseStatus Database_SelfCheck(int connectTimeOutSeconds = 5)
        {
            if (Database_OpenConnection(connectTimeOutSeconds) == false)
                return Status;

            var dataBaseRef = GetDataBase();



            // try create table
            try
            {
                dataBaseRef.InitTables();
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
                Status = EnumDatabaseStatus.AccessDenied;
            }

            // check readable
            IsWritable = dataBaseRef.CheckWritable();
            var isReadable = dataBaseRef.CheckReadable();
            if (isReadable == false)
            {
                Status = EnumDatabaseStatus.AccessDenied;
                return Status;
            }

            // check if encryption key is matched
            if (dataBaseRef.CheckEncryptionTest() == false)
            {
                Status = EnumDatabaseStatus.EncryptKeyError;
                return Status;
            }

            Status = EnumDatabaseStatus.OK;
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
            CachedProtocols.Add(new ProtocolBaseViewModel(server));
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
                CachedProtocols.Add(new ProtocolBaseViewModel(servers[i]));
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
                var old = CachedProtocols.First(x => x.Id == org.Id);
                old.Server = org;
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
                // update viewmodel
                foreach (var protocolServer in servers)
                {
                    var old = CachedProtocols.First(x => x.Id == protocolServer.Id);
                    // invoke main list ui change & invoke launcher ui change
                    old.Server = protocolServer;
                }
            }
            return ret;
        }

        public bool Database_DeleteServer(string id)
        {
            if (_isWritable)
            {
                var ret = GetDataBase()?.DeleteServer(id) == true;
                if (ret == true)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    CachedProtocols.RemoveAll(x => x.Id == id);
                }
                return ret;
            }
            return false;
        }

        public bool Database_DeleteServer(IEnumerable<string> ids)
        {
            if (_isWritable)
            {
                var enumerable = ids.ToArray();
                var ret = GetDataBase()?.DeleteServer(enumerable) == true;
                if (ret == true)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    CachedProtocols.RemoveAll(x => enumerable.Contains(x.Id));
                }
                return ret;
            }
            return false;
        }

        protected List<ProtocolBase> Database_GetServers()
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
            return GetDataBase()?.GetServerCount() ?? 0;
        }
    }
}
