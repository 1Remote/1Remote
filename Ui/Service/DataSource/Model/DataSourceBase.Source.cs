using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
using _1RM.Utils;
using _1RM.View;
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


        private EnumDatabaseStatus? _dataSourceDataUpdateStatus = null;
        private long _lastReadFromDataSourceMillisecondsTimestamp = 0;
        [JsonIgnore]
        protected virtual long LastReadFromDataSourceMillisecondsTimestamp
        {
            get => _lastReadFromDataSourceMillisecondsTimestamp;
            set
            {
                _lastReadFromDataSourceMillisecondsTimestamp = value;
                _dataSourceDataUpdateStatus = Status;
            }
        }

        [JsonIgnore]
        private long _dataSourceDataUpdateTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();


        public virtual bool NeedRead()
        {
            if (Status == EnumDatabaseStatus.OK)
            {
                var dataBase = GetDataBase();
                var ret = dataBase.GetDataUpdateTimestamp();
                if (!ret.IsSuccess)
                {
                    SetStatus(false);
                }
                else
                {
                    _dataSourceDataUpdateTimestamp = ret.Result;
                    return LastReadFromDataSourceMillisecondsTimestamp < _dataSourceDataUpdateTimestamp;
                }
            }



            if (_dataSourceDataUpdateStatus != null
                && _dataSourceDataUpdateStatus != Status)
            {
                // 数据库状态改变
                _dataSourceDataUpdateStatus = Status;
                return true;
            }


            if (Status != EnumDatabaseStatus.OK)
            {
                // 当连接不成功时，设置为 0，以便下次重新连接
                _lastReadFromDataSourceMillisecondsTimestamp = 0;
                if (CachedProtocols.Count > 0)
                    return true;
            }
            return false;
        }

        public void MarkAsNeedRead()
        {
            LastReadFromDataSourceMillisecondsTimestamp = 0;
        }



        /// <summary>
        /// 返回服务器信息(服务器信息已指向数据源)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProtocolBaseViewModel> GetServers(bool focus = false)
        {
            if (focus == false
                && LastReadFromDataSourceMillisecondsTimestamp >= _dataSourceDataUpdateTimestamp)
            {
                return CachedProtocols;
            }
            lock (this)
            {
                var result = Database_GetServers();
                if (result.IsSuccess)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    CachedProtocols = new List<ProtocolBaseViewModel>(result.ProtocolBases.Count);
                    foreach (var protocol in result.ProtocolBases)
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
                    SetStatus(true);
                }
                else
                {
                    CachedProtocols.Clear();
                }
                return CachedProtocols;
            }
        }

        public abstract IDatabase GetDataBase();

        private void SetStatus(bool isConnected)
        {
            if (isConnected)
            {
                Status = EnumDatabaseStatus.OK;
            }
            else
            {
                Status = Status != EnumDatabaseStatus.NotConnectedYet ? EnumDatabaseStatus.LostConnection : EnumDatabaseStatus.AccessDenied;
            }
        }

        public bool Database_OpenConnection(int connectTimeOutSeconds = 5)
        {
            var dataBase = GetDataBase();
            // open db or create db.


            var connectionString = GetConnectionString(connectTimeOutSeconds);
            if (connectionString != _lastConnectionString)
            {
                dataBase.CloseConnection();
                _lastConnectionString = connectionString;
            }

            var result = dataBase.OpenNewConnection(connectionString);
            if (!result.IsSuccess)
            {
                SimpleLogHelper.Error(result.ErrorInfo);
            }
            SetStatus(dataBase.IsConnected() == true);
            return true;
        }

        public virtual void Database_CloseConnection()
        {
            var dataBase = GetDataBase();
            if (dataBase.IsConnected())
                dataBase.CloseConnection();
        }


        private static string _lastConnectionString = "";
        public virtual EnumDatabaseStatus Database_SelfCheck(int connectTimeOutSeconds = 5)
        {
            if (Database_OpenConnection(connectTimeOutSeconds) == false)
                return Status;

            var dataBase = GetDataBase();



            // create table
            var ret = dataBase.InitTables();
            if (ret.IsSuccess == false)
            {
                SetStatus(false);
                SimpleLogHelper.Warning(ret.ErrorInfo);
                return Status;
            }

            // check writable
            IsWritable = dataBase.CheckWritable();

            // check readable
            if (!dataBase.GetConfig("EncryptionTest").IsSuccess)
            {
                SetStatus(false);
                return Status;
            }

            // check if encryption key is matched
            if (dataBase.CheckEncryptionTest() == false)
            {
                Status = EnumDatabaseStatus.EncryptKeyError;
                return Status;
            }

            SetStatus(true);
            return Status;
        }

        public Result Database_InsertServer(ProtocolBase server)
        {
            var tmp = (ProtocolBase)server.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            tmp.EncryptToDatabaseLevel();
            var result = GetDataBase().AddServer(ref tmp);
            if (result.IsSuccess)
            {
                server.Id = tmp.Id;
                server.DataSource = this;
                SetStatus(true);
            }
            return result;
        }

        public Result Database_InsertServer(List<ProtocolBase> servers)
        {
            if (_isWritable)
            {
                var cloneList = new List<ProtocolBase>();
                foreach (var server in servers)
                {
                    var tmp = (ProtocolBase)server.Clone();
                    tmp.SetNotifyPropertyChangedEnabled(false);
                    tmp.EncryptToDatabaseLevel();
                    cloneList.Add(tmp);
                }
                var ret = GetDataBase().AddServer(cloneList);
                if (ret.IsSuccess)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    for (int i = 0; i < servers.Count; i++)
                    {
                        servers[i].Id = cloneList[i].Id;
                        CachedProtocols.Add(new ProtocolBaseViewModel(servers[i]));
                    }
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Success();
        }

        public Result Database_UpdateServer(ProtocolBase org)
        {
            if (_isWritable)
            {
                Debug.Assert(org.IsTmpSession() == false);
                var tmp = (ProtocolBase)org.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                var ret = GetDataBase().UpdateServer(tmp);
                if (ret.IsSuccess)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    var old = CachedProtocols.First(x => x.Id == org.Id);
                    old.Server = org;
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Success();
        }

        public Result Database_UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            if (_isWritable)
            {
                var cloneList = new List<ProtocolBase>();
                foreach (var server in servers)
                {
                    var tmp = (ProtocolBase)server.Clone();
                    tmp.SetNotifyPropertyChangedEnabled(false);
                    tmp.EncryptToDatabaseLevel();
                    cloneList.Add(tmp);
                }

                var ret = GetDataBase().UpdateServer(cloneList);
                if (ret.IsSuccess)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    // update viewmodel
                    foreach (var protocolServer in servers)
                    {
                        var old = CachedProtocols.First(x => x.Id == protocolServer.Id);
                        // invoke main list ui change & invoke launcher ui change
                        old.Server = protocolServer;
                    }
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Success();
        }


        public Result Database_DeleteServer(IEnumerable<string> ids)
        {
            if (_isWritable)
            {
                var enumerable = ids.ToArray();
                var ret = GetDataBase().DeleteServer(enumerable);
                if (ret.IsSuccess)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    CachedProtocols.RemoveAll(x => enumerable.Contains(x.Id));
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Success();
        }

        private ResultSelects Database_GetServers()
        {
            var ret = GetDataBase().GetServers();
            if (ret.IsSuccess)
            {
                foreach (var protocolBase in ret.ProtocolBases)
                {
                    protocolBase.DataSource = this;
                }
                SetStatus(true);
            }
            return ret;
        }
    }
}
