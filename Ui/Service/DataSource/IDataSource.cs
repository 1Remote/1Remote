using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using _1RM.Model.DAO;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.Model;
using _1RM.View;
using com.github.xiangyuecn.rsacsharp;
using JsonKnownTypes;
using Newtonsoft.Json;
using Shawn.Utils;
using Stylet;

namespace _1RM.Service.DataSource
{
    public interface IDataSource : IDataService
    {
        public string GetName();

        /// <summary>
        /// 已缓存的服务器信息
        /// </summary>
        [JsonIgnore]
        public List<ProtocolBaseViewModel> CachedProtocols { get; }

        /// <summary>
        /// 返回数据源的 ID 
        /// </summary>
        /// <returns></returns>
        public string DataSourceName { get; }

        /// <summary>
        /// 返回服务器信息(服务器信息已指向数据源)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProtocolBaseViewModel> GetServers();

        public bool IsReadable { get; }
        public bool IsWritable { get; }

        public abstract long LastReadFromDataSourceTimestamp { get; }
        public abstract long DataSourceDataUpdateTimestamp { get; set; }

        /// <summary>
        /// 定期检查数据源的最后更新时间戳，大于 LastUpdateTimestamp 则返回 true
        /// </summary>
        /// <returns></returns>
        public abstract bool NeedRead();
    }

    public abstract class DatabaseSource : IDataSource
    {
        public string DataSourceName => DataSourceConfig.Name;
        public Model.DataSourceConfigBase DataSourceConfig;

        protected DatabaseSource(Model.DataSourceConfigBase dataSourceConfig)
        {
            DataSourceConfig = dataSourceConfig;
        }

        public string GetName()
        {
            return DataSourceConfig.Name;
        }

        public List<ProtocolBaseViewModel> CachedProtocols { get; protected set; } = new List<ProtocolBaseViewModel>();

        protected bool _isReadable = true;
        bool IDataSource.IsReadable => _isReadable;
        protected bool _isWritable = true;
        bool IDataSource.IsWritable => _isWritable;


        public virtual long LastReadFromDataSourceTimestamp { get; protected set; } = 0;
        public virtual long DataSourceDataUpdateTimestamp { get; set; } = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        public virtual bool NeedRead()
        {
            var dataBase = GetDataBase();
            dataBase?.OpenConnection();
            if (dataBase?.IsConnected() == true)
            {
                DataSourceDataUpdateTimestamp = dataBase.GetDataUpdateTimestamp();
                return LastReadFromDataSourceTimestamp < DataSourceDataUpdateTimestamp;
            }

            return false;
        }


        public IEnumerable<ProtocolBaseViewModel> GetServers()
        {
            lock (this)
            {
                if (LastReadFromDataSourceTimestamp >= DataSourceDataUpdateTimestamp) return CachedProtocols;

                LastReadFromDataSourceTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var protocols = Database_GetServers();
                CachedProtocols = new List<ProtocolBaseViewModel>(protocols.Count);
                foreach (var protocol in protocols)
                {
                    try
                    {
                        var serverAbstract = protocol;
                        this.DecryptToRamLevel(ref serverAbstract);
                        Execute.OnUIThreadSync(() =>
                        {
                            var vm = new ProtocolBaseViewModel(serverAbstract, this);
                            CachedProtocols.Add(vm);
                        });
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Info(e);
                    }
                }

                return CachedProtocols;
            }
        }

        public abstract IDataBase GetDataBase();

        public bool Database_OpenConnection()
        {
            var dataBase = GetDataBase();
            // open db or create db.
            Debug.Assert(dataBase != null);
            dataBase.OpenNewConnection(DataSourceConfig.DatabaseType, DataSourceConfig.GetConnectionString());
            dataBase.InitTables();

            // check database rsa encrypt
            var privateKeyPath = dataBase.GetFromDatabase_RSA_PrivateKeyPath();
            if (!string.IsNullOrWhiteSpace(privateKeyPath)
                && File.Exists(privateKeyPath))
            {
                _rsa = new RSA(File.ReadAllText(Database_GetPrivateKeyPath()), true);
            }
            else
            {
                _rsa = null;
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

        public virtual EnumDbStatus Database_SelfCheck()
        {
            EnumDbStatus ret = EnumDbStatus.NotConnected;
            var dataBase = GetDataBase();
            try
            {
                dataBase?.OpenNewConnection(DataSourceConfig.DatabaseType, DataSourceConfig.GetConnectionString());
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                ret = EnumDbStatus.NotConnected;
            }

            // check connection
            if (dataBase?.IsConnected() != true)
            {
                ret = EnumDbStatus.NotConnected;
            }
            else
            {
                dataBase.InitTables();

                // validate encryption
                var privateKeyPath = dataBase.GetFromDatabase_RSA_PrivateKeyPath();
                if (string.IsNullOrWhiteSpace(privateKeyPath))
                {
                    // no encrypt
                    ret = EnumDbStatus.OK;
                }
                else
                {
                    var publicKey = dataBase.Get_RSA_PublicKey();
                    var pks = RSA.CheckPrivatePublicKeyMatch(privateKeyPath, publicKey);
                    switch (pks)
                    {
                        case RSA.EnumRsaStatus.CannotReadPrivateKeyFile:
                            return EnumDbStatus.RsaPrivateKeyNotFound;
                        case RSA.EnumRsaStatus.PrivateKeyFormatError:
                            return EnumDbStatus.RsaPrivateKeyFormatError;
                        case RSA.EnumRsaStatus.PublicKeyFormatError:
                            return EnumDbStatus.DataIsDamaged;
                        case RSA.EnumRsaStatus.PrivateAndPublicMismatch:
                            return EnumDbStatus.RsaNotMatched;
                        case RSA.EnumRsaStatus.NoError:
                            break;
                    }
                }
            }

            if (ret == EnumDbStatus.OK)
            {
                var servers = GetServers();
                DataSourceConfig.SetStatus(true, $"{servers.Count()} servers");
            }
            else
            {
                DataSourceConfig.SetStatus(false, ret.GetErrorInfo());
            }
            return ret;
        }


        protected RSA? _rsa = null;

        public virtual string Database_GetPublicKey()
        {
            return GetDataBase()?.Get_RSA_PublicKey() ?? "";
        }

        public abstract string Database_GetPrivateKeyPath();

        public virtual RSA.EnumRsaStatus Database_SetEncryptionKey(string privateKeyPath, string privateKeyContent, IEnumerable<ProtocolBase> servers)
        {
            var dataBase = GetDataBase();
            Debug.Assert(dataBase != null);

            // clear rsa key
            if (string.IsNullOrEmpty(privateKeyPath))
            {
                Debug.Assert(_rsa != null);
                Debug.Assert(string.IsNullOrWhiteSpace(Database_GetPrivateKeyPath()) == false);

                // decrypt
                var cloneList = new List<ProtocolBase>();
                foreach (var server in servers)
                {
                    var tmp = (ProtocolBase)server.Clone();
                    tmp.SetNotifyPropertyChangedEnabled(false);
                    DecryptToConnectLevel(ref tmp);
                    cloneList.Add(tmp);
                }

                // update 
                if (dataBase.SetConfigRsa("", "", cloneList))
                {
                    _rsa = null;
                }

                return RSA.EnumRsaStatus.NoError;
            }
            // set rsa key
            else
            {
                Debug.Assert(_rsa == null);
                Debug.Assert(string.IsNullOrWhiteSpace(Database_GetPrivateKeyPath()) == true);


                var pks = RSA.KeyCheck(privateKeyContent, true);
                if (pks != RSA.EnumRsaStatus.NoError)
                    return pks;
                var rsa = new RSA(privateKeyContent, true);

                // encrypt
                var cloneList = new List<ProtocolBase>();
                foreach (var server in servers)
                {
                    var tmp = (ProtocolBase)server.Clone();
                    tmp.SetNotifyPropertyChangedEnabled(false);
                    rsa?.EncryptToDatabaseLevel(ref tmp);
                    cloneList.Add(tmp);
                }

                // update 
                if (dataBase.SetConfigRsa(privateKeyPath, rsa.ToPEM_PKCS1(true), cloneList))
                {
                    _rsa = rsa;
                }

                return RSA.EnumRsaStatus.NoError;
            }
        }

        public virtual RSA.EnumRsaStatus Database_UpdatePrivateKeyPathOnly(string privateKeyPath)
        {
            Debug.Assert(_rsa != null);
            Debug.Assert(string.IsNullOrWhiteSpace(Database_GetPrivateKeyPath()) == false);
            Debug.Assert(File.Exists(privateKeyPath));

            var pks = RSA.CheckPrivatePublicKeyMatch(privateKeyPath, Database_GetPublicKey());
            if (pks == RSA.EnumRsaStatus.NoError)
            {
                GetDataBase()?.Set_RSA_PrivateKeyPath(privateKeyPath);
            }

            return pks;
        }

        public virtual string DecryptOrReturnOriginalString(string originalString)
        {
            return _rsa?.DecryptOrReturnOriginalString(originalString) ?? originalString;
        }

        public virtual void EncryptToDatabaseLevel(ref ProtocolBase server)
        {
            _rsa?.EncryptToDatabaseLevel(ref server);
        }

        public virtual void DecryptToRamLevel(ref ProtocolBase server)
        {
            _rsa?.DecryptToConnectLevel(ref server);
        }

        public virtual void DecryptToConnectLevel(ref ProtocolBase server)
        {
            _rsa?.DecryptToConnectLevel(ref server);
        }

        public void Database_InsertServer(ProtocolBase server)
        {
            var tmp = (ProtocolBase)server.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptToDatabaseLevel(ref tmp);
            GetDataBase()?.AddServer(tmp);
        }

        public void Database_InsertServer(IEnumerable<ProtocolBase> servers)
        {
            var cloneList = new List<ProtocolBase>();
            foreach (var server in servers)
            {
                var tmp = (ProtocolBase)server.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                EncryptToDatabaseLevel(ref tmp);
                cloneList.Add(tmp);
            }

            GetDataBase()?.AddServer(cloneList);
        }

        public bool Database_UpdateServer(ProtocolBase org)
        {
            Debug.Assert(string.IsNullOrEmpty(org.Id) == false);
            var tmp = (ProtocolBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptToDatabaseLevel(ref tmp);
            return GetDataBase()?.UpdateServer(tmp) == true;
        }

        public bool Database_UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            var cloneList = new List<ProtocolBase>();
            foreach (var server in servers)
            {
                var tmp = (ProtocolBase)server.Clone();
                tmp.SetNotifyPropertyChangedEnabled(false);
                EncryptToDatabaseLevel(ref tmp);
                cloneList.Add(tmp);
            }

            return GetDataBase()?.UpdateServer(cloneList) == true;
        }

        public bool Database_DeleteServer(string id)
        {
            return _isWritable && GetDataBase()?.DeleteServer(id) == true;
        }

        public bool Database_DeleteServer(IEnumerable<string> ids)
        {
            return _isWritable && GetDataBase()?.DeleteServer(ids) == true;
        }

        public List<ProtocolBase> Database_GetServers()
        {
            return GetDataBase()?.GetServers() ?? new List<ProtocolBase>();
        }
    }
}