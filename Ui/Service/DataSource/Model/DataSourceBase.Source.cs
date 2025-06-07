using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
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
        [JsonIgnore]
        public List<Credential> CachedCredentials { get; protected set; } = new List<Credential>();

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
                    SimpleLogHelper.Debug($"{DataSourceName}：NeedRead = {LastReadFromDataSourceMillisecondsTimestamp} < {_dataSourceDataUpdateTimestamp} = {LastReadFromDataSourceMillisecondsTimestamp < _dataSourceDataUpdateTimestamp}");
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
                MarkAsNeedRead();
            }

            return true;
        }

        public void MarkAsNeedRead()
        {
            LastReadFromDataSourceMillisecondsTimestamp = 0;
        }



        /// <summary>
        /// 返回服务器信息(服务器信息已指向数据源)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProtocolBaseViewModel> GetServers(bool force = false)
        {
            if (Status != EnumDatabaseStatus.OK
                || force == false && !NeedRead())
            {
                return CachedProtocols;
            }

            lock (this)
            {
                var result = Database_GetServers();
                if (result.IsSuccess)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    CachedProtocols = new List<ProtocolBaseViewModel>(result.Items.Count);
                    foreach (var protocol in result.Items)
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
                            SimpleLogHelper.DebugInfo(e);
                        }
                    }
                    SetStatus(true);
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


        public Tuple<bool, string> Database_OpenConnection(int connectTimeOutSeconds = 5)
        {
            try
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
                SetStatus(dataBase.IsConnected() == true);
                if (!result.IsSuccess)
                {
                    SimpleLogHelper.DebugError(result.ErrorInfo);
                    return new Tuple<bool, string>(false, result.ErrorInfo);
                }
            }
            catch (Exception e)
            {
                SetStatus(false);
                return new Tuple<bool, string>(false, e.Message);
            }

            return new Tuple<bool, string>(true, "");
        }

        public virtual void Database_CloseConnection()
        {
            var dataBase = GetDataBase();
            if (dataBase.IsConnected())
                dataBase.CloseConnection();
        }


        private static string _lastConnectionString = "";
        public virtual DatabaseStatus Database_SelfCheck(int connectTimeOutSeconds = 5)
        {
            var tro = Database_OpenConnection(connectTimeOutSeconds);
            if (tro.Item1 == false)
            {
                SetStatus(false);
                return DatabaseStatus.New(Status, tro.Item2);
            }

            var dataBase = GetDataBase();



            // create table
            {
                var initTablesResult = dataBase.InitTables();
                if (initTablesResult.IsSuccess == false)
                {
                    SetStatus(false);
                    SimpleLogHelper.DebugWarning(initTablesResult.ErrorInfo);
                    return DatabaseStatus.New(Status, initTablesResult.ErrorInfo);
                }
            }

            // check writable
            IsWritable = dataBase.CheckWritable();

            // check readable
            if (!dataBase.GetConfig("EncryptionTest").IsSuccess)
            {
                // can not read, return access denied
                SetStatus(false);
                return DatabaseStatus.New(Status, "read database test failed");
            }

            // check if encryption key is matched
            if (dataBase.CheckEncryptionTest() == false)
            {
                Status = EnumDatabaseStatus.EncryptKeyError;
                return DatabaseStatus.New(Status);
            }

            if (Status != EnumDatabaseStatus.OK)
            {
                MarkAsNeedRead();
            }
            SetStatus(true);
            dataBase.CloseConnection();
            return DatabaseStatus.New(Status);
        }

        public Result Database_InsertServer(ProtocolBase server)
        {
            if (_isWritable)
            {
                var tmp = (ProtocolBase)server.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                var result = GetDataBase().AddServer(ref tmp);
                if (result.IsSuccess)
                {
                    server.Id = tmp.Id;
                    server.DataSource = this;
                    result.NeedReload = true;
                    MarkAsNeedRead();
                    SetStatus(true);
                }
                return result;
            }
            return Result.Success();
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
                    MarkAsNeedRead();
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
                    var old = CachedProtocols.FirstOrDefault(x => x.Id == org.Id);
                    if (old != null)
                    {
                        old.Server = org;
                        LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }
                    else
                    {
                        // cached data not equal to db data, refresh caches.
                        ret.NeedReload = true;
                        MarkAsNeedRead();
                    }
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
                if (!ret.IsSuccess) return ret;


                // update viewmodel
                foreach (var protocolServer in servers)
                {
                    var old = CachedProtocols.FirstOrDefault(x => x.Id == protocolServer.Id);
                    if (old != null)
                    {
                        old.Server = protocolServer;
                    }
                    else
                    {
                        ret.NeedReload = true;
                        MarkAsNeedRead();
                        break;
                    }
                }

                if (!ret.NeedReload)
                {
                    LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                }
                SetStatus(true);
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
                    CachedProtocols.RemoveAll(x => enumerable.Contains(x.Id));
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Fail(IoC.Translate("We can not delete from database:") + " readonly");
        }

        private ResultSelects<ProtocolBase> Database_GetServers()
        {
            var ret = GetDataBase().GetServers();
            if (ret.IsSuccess)
            {
                foreach (var protocolBase in ret.Items)
                {
                    protocolBase.DataSource = this;
                }
                SetStatus(true);
            }
            return ret;
        }



        public Result Database_InsertCredential(Credential credential)
        {
            if (_isWritable)
            {
                var tmp = (Credential) credential.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                var result = GetDataBase().AddPassword(tmp);
                if (result.IsSuccess)
                {
                    credential.DataSource = this;
                    result.NeedReload = true;
                    MarkAsNeedRead();
                    SetStatus(true);
                }
                return result;
            }
            return Result.Success();
        }


        public Result Database_UpdateCredential(Credential org)
        {
            if (_isWritable)
            {
                var tmp = (Credential)org.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                var ret = GetDataBase().UpdatePassword(tmp);
                if (ret.IsSuccess)
                {
                    MarkAsNeedRead();
                    ret.NeedReload = true;
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Success();
        }

        public Result Database_UpdateCredential(IEnumerable<Credential> credentials)
        {
            if (_isWritable)
            {
                var cloneList = new List<Credential>();
                foreach (var credential in credentials)
                {
                    var tmp = (Credential)credential.Clone();
                    tmp.SetNotifyPropertyChangedEnabled(false);
                    tmp.EncryptToDatabaseLevel();
                    cloneList.Add(tmp);
                }

                var ret = GetDataBase().UpdatePassword(cloneList);
                if (!ret.IsSuccess) return ret;

                ret.NeedReload = true;
                MarkAsNeedRead();
                SetStatus(true);
                return ret;
            }
            return Result.Success();
        }


        public Result Database_DeleteCredential(IEnumerable<string> names)
        {
            if (_isWritable)
            {
                var enumerable = names.ToArray();
                var ret = GetDataBase().DeletePassword(enumerable);
                if (ret.IsSuccess)
                {
                    CachedCredentials.RemoveAll(x => enumerable.Contains(x.Name));
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Fail(IoC.Translate("We can not delete from database:") + " readonly");
        }

        private ResultSelects<Credential> Database_GetCredentials()
        {
            var ret = GetDataBase().GetPasswords();
            if (ret.IsSuccess)
            {
                foreach (var credential in ret.Items)
                {
                    credential.DataSource = this;
                }
                SetStatus(true);
            }
            return ret;
        }


        public IEnumerable<Credential> GetCredentials(bool force = false)
        {
            var result = Database_GetCredentials();
            if (result.IsSuccess)
            {
                return result.Items;
            }
            return Array.Empty<Credential>();

            //if (Status != EnumDatabaseStatus.OK
            //    || force == false && !NeedRead())
            //{
            //    return CachedCredentials;
            //}
            //lock (this)
            //{
            //    var result = Database_GetCredentials();
            //    if (result.IsSuccess)
            //    {
            //        LastReadFromDataSourceMillisecondsTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            //        CachedCredentials = new List<ProtocolBaseViewModel>(result.Items.Count);
            //        foreach (var protocol in result.Items)
            //        {
            //            try
            //            {
            //                Execute.OnUIThreadSync(() =>
            //                {
            //                    var vm = new ProtocolBaseViewModel(protocol);
            //                    CachedCredentials.Add(vm);
            //                });
            //            }
            //            catch (Exception e)
            //            {
            //                SimpleLogHelper.DebugInfo(e);
            //            }
            //        }
            //        SetStatus(true);
            //    }
            //    return CachedCredentials;
            //}
        }
    }
}
