using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
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

        private EnumDatabaseStatus? _lastReadDataSourceStatus = null;

        /// <summary>
        /// Last time read servers from data source, in milliseconds since epoch.
        /// </summary>
        [JsonIgnore] private readonly Dictionary<string, long> _lastReadTimestamp = new Dictionary<string, long>();


        /// <summary>
        /// to return whether the table need to be read from data source.
        /// </summary>
        /// <param name="tableName"></param>
        public bool NeedRead(string tableName)
        {
            if (!_lastReadTimestamp.ContainsKey(tableName) || _lastReadTimestamp[tableName] <= 0)
            {
                SimpleLogHelper.Debug($"Check NeedRead of [{DataSourceName}] = RAM 0 < DB any = True");
                return true;
            }


            if (_lastReadDataSourceStatus != null
                && _lastReadDataSourceStatus != Status)
            {
                // 数据库状态改变，例如从 LostConnection 或 AccessDenied 等状态变为 OK。
                _lastReadDataSourceStatus = Status;
                return true;
            }


            if (Status != EnumDatabaseStatus.OK)
            {
                // 当连接不成功时，标记为需要读取以便下次重新连接
                return true;
            }

            var dataBase = GetDataBase();
            var ret = dataBase.GetTableUpdateTimestamp(tableName);
            if (!ret.IsSuccess)
            {
                SimpleLogHelper.Debug($"Check NeedRead of [{DataSourceName}] = RAM {_lastReadTimestamp[tableName]} < DB {ret.Result} = {_lastReadTimestamp[tableName] < ret.Result}");
                SetStatus(false);
            }
            else
            {
                // read data source update timestamp，and compare with last read timestamp. if 
                return _lastReadTimestamp[tableName] < ret.Result;
            }
            return true;
        }

        public void ClearReadTimestamp()
        {
            _lastReadTimestamp.Clear();
        }

        /// <summary>
        /// reset to 0, means need read.
        /// </summary>
        private void SetReadTimestampTo0(string tableName)
        {
            SetReadTimestamp(tableName, 0); // reset to 0, means need read.
        }
        /// <summary>
        /// set the table read timestamp to a specific value, default is current time in milliseconds since epoch.
        /// </summary>
        private void SetReadTimestamp(string tableName, long timestamp = -1)
        {
            if (timestamp < 0)
            {
                timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            if (_lastReadTimestamp.ContainsKey(tableName))
            {
                _lastReadTimestamp[tableName] = timestamp;
            }
            else
            {
                _lastReadTimestamp.Add(tableName, timestamp);
            }
        }



        /// <summary>
        /// 返回服务器信息(服务器信息已指向数据源)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProtocolBaseViewModel> GetServers(bool force = false)
        {
            if (Status != EnumDatabaseStatus.OK
                || force == false && !NeedRead(TableServer.TABLE_NAME))
            {
                return CachedProtocols;
            }

            lock (this)
            {
                var result = Database_GetServers();
                if (result.IsSuccess)
                {
                    SetReadTimestamp(TableServer.TABLE_NAME);
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
                    SetReadTimestampTo0(TableServer.TABLE_NAME);
                    SetReadTimestampTo0(TableCredential.TABLE_NAME); // because server may add by import one have credential, so we need to reload credentials too.
                    SetStatus(true);
                    server.Id = tmp.Id;
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
                    SetReadTimestampTo0(TableServer.TABLE_NAME);
                    SetReadTimestampTo0(TableCredential.TABLE_NAME); // because server may add by import one have credential, so we need to reload credentials too.
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Success();
        }

        public Result Database_UpdateServer(ProtocolBase org)
        {
            return Database_UpdateServer([org]);
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
                if (!ret.IsSuccess)
                {
                    return ret;
                }

                // update viewmodel
                foreach (var protocolServer in servers)
                {
                    var old = CachedProtocols.FirstOrDefault(x => x.Id == protocolServer.Id);
                    if (old != null)
                    {
                        if (old.Server != protocolServer)
                        {
                            old.Server = protocolServer;
                        }
                    }
                    else
                    {
                        SetReadTimestampTo0(TableServer.TABLE_NAME);
                        break;
                    }
                }
                SetReadTimestamp(TableServer.TABLE_NAME);
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
                var tmp = (Credential)credential.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                var result = GetDataBase().AddCredential(ref tmp);
                if (result.IsSuccess)
                {
                    credential.DatabaseId = tmp.DatabaseId; // update the DatabaseId to match the new Id
                    credential.DataSource = this;
                    SetStatus(true);
                }
                return result;
            }
            return Result.Success();
        }

        /// <summary>
        /// update credential in database by Id, and update related protocols.
        /// </summary>
        /// <param name="org">the credential to update</param>
        /// <para name="credentialNameBeforeUpdate">the credential name before update, used to find related protocols.</para>
        /// <returns></returns>
        public Result Database_UpdateCredential(Credential org, string credentialNameBeforeUpdate)
        {
            if (_isWritable)
            {
                var servers = GetServers(true);
                var relatedProtocols = servers.Select(x => x.Server)
                    .Where(x => (x as ProtocolBaseWithAddressPortUserPwd)?.InheritedCredentialName == credentialNameBeforeUpdate)
                    .Select(x => (ProtocolBaseWithAddressPortUserPwd)x).ToList();

                var tmp = (Credential)org.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                tmp.EncryptToDatabaseLevel();
                var ret = GetDataBase().UpdateCredential(tmp, relatedProtocols);
                if (ret.IsSuccess)
                {
                    org.DataSource = this;
                    SetStatus(true);
                    if (relatedProtocols.Count > 0)
                    {
                        ClearReadTimestamp(); // reload database
                        ret.NeedReloadUI = true;
                    }
                }
                return ret;
            }
            return Result.Success();
        }

        //public Result Database_UpdateCredential(IEnumerable<Credential> credentials)
        //{
        //    if (_isWritable)
        //    {
        //        var cloneList = new List<Credential>();
        //        foreach (var credential in credentials)
        //        {
        //            var tmp = (Credential)credential.Clone();
        //            tmp.SetNotifyPropertyChangedEnabled(false);
        //            tmp.EncryptToDatabaseLevel();
        //            cloneList.Add(tmp);
        //        }
        //        var ret = GetDataBase().UpdateCredential(cloneList);
        //        if (!ret.IsSuccess) return ret;
        //        ret.NeedReload = true;
        //        SetReadTimestampTo0();
        //        SetStatus(true);
        //        return ret;
        //    }
        //    return Result.Success();
        //}


        public Result Database_DeleteCredential(IEnumerable<string> names)
        {
            if (_isWritable)
            {
                var enumerable = names.ToArray();
                var servers = GetServers(true);
                var relatedProtocols = servers.Select(x => x.Server)
                    .Where(x => enumerable.Any(name => name == (x as ProtocolBaseWithAddressPortUserPwd)?.InheritedCredentialName))
                    .Select(x => (ProtocolBaseWithAddressPortUserPwd)x).ToList();
                var ret = GetDataBase().DeleteCredential(enumerable, relatedProtocols);
                if (ret.IsSuccess)
                {
                    CachedCredentials.RemoveAll(x => enumerable.Contains(x.Name));
                    if (relatedProtocols.Count > 0)
                    {
                        ClearReadTimestamp(); // reload database
                        ret.NeedReloadUI = true;
                    }
                    SetStatus(true);
                }
                return ret;
            }
            return Result.Fail(IoC.Translate("We can not delete from database:") + " readonly");
        }


        public IEnumerable<Credential> GetCredentials(bool force = false)
        {
            if (Status != EnumDatabaseStatus.OK
                || (force == false && !NeedRead(TableCredential.TABLE_NAME))
                )
            {
                return CachedCredentials;
            }
            lock (this)
            {
                var result = GetDataBase().GetCredentials();
                if (result.IsSuccess)
                {
                    SetReadTimestamp(TableCredential.TABLE_NAME);
                    foreach (var credential in result.Items)
                    {
                        credential.DataSource = this;
                    }
                    SetStatus(true);
                    CachedCredentials = result.Items;
                    SetStatus(true);
                }
                return CachedCredentials;
            }
        }
    }
}
