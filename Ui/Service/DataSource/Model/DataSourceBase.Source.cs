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
        protected bool _isReadable = true;
        [JsonIgnore]
        public bool IsReadable => _isReadable;
        protected bool _isWritable = true;
        [JsonIgnore]
        public bool IsWritable => _isWritable;

        /// <summary>
        /// 已缓存的服务器信息
        /// </summary>
        [JsonIgnore]
        public List<ProtocolBaseViewModel> CachedProtocols { get; protected set; } = new List<ProtocolBaseViewModel>();

        [JsonIgnore]
        public virtual long LastReadFromDataSourceTimestamp { get; protected set; } = 0;
        [JsonIgnore]
        public virtual long DataSourceDataUpdateTimestamp { get; set; } = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        public virtual bool NeedRead()
        {
            if (Status == EnumDbStatus.OK)
            {
                var dataBase = GetDataBase();
                DataSourceDataUpdateTimestamp = dataBase.GetDataUpdateTimestamp();
                SimpleLogHelper.Debug($"Datasource {DataSourceName} {LastReadFromDataSourceTimestamp} < {DataSourceDataUpdateTimestamp}");
                return LastReadFromDataSourceTimestamp < DataSourceDataUpdateTimestamp;
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
                    && LastReadFromDataSourceTimestamp >= DataSourceDataUpdateTimestamp)
                {
                    return CachedProtocols;
                }

                LastReadFromDataSourceTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
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
            dataBase.OpenNewConnection(DatabaseType, GetConnectionString(connectTimeOutSeconds));
            dataBase.InitTables();
            dataBase.OpenNewConnection(DatabaseType, GetConnectionString(connectTimeOutSeconds));
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

        public virtual EnumDbStatus Database_SelfCheck(int connectTimeOutSeconds = 5)
        {
            EnumDbStatus ret = EnumDbStatus.NotConnectedYet;
            var dataBase = GetDataBase();

            if (dataBase.IsConnected() == false)
            {
                try
                {
                    dataBase.OpenNewConnection(DatabaseType, GetConnectionString(connectTimeOutSeconds));
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
                }
            }

            if (dataBase.IsConnected())
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
                    ret = pks switch
                    {
                        RSA.EnumRsaStatus.CannotReadPrivateKeyFile => EnumDbStatus.RsaPrivateKeyNotFound,
                        RSA.EnumRsaStatus.PrivateKeyFormatError => EnumDbStatus.RsaPrivateKeyFormatError,
                        RSA.EnumRsaStatus.PublicKeyFormatError => EnumDbStatus.DataIsDamaged,
                        RSA.EnumRsaStatus.PrivateAndPublicMismatch => EnumDbStatus.RsaNotMatched,
                        RSA.EnumRsaStatus.NoError => EnumDbStatus.OK,
                        _ => throw new NotSupportedException()
                    };
                }
            }

            Status = ret;
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
            LastReadFromDataSourceTimestamp = 0;
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
            LastReadFromDataSourceTimestamp = 0;
        }

        public bool Database_UpdateServer(ProtocolBase org)
        {
            Debug.Assert(string.IsNullOrEmpty(org.Id) == false);
            var tmp = (ProtocolBase)org.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            EncryptToDatabaseLevel(ref tmp);
            var ret = GetDataBase()?.UpdateServer(tmp) == true;
            if (ret == true)
            {
                LastReadFromDataSourceTimestamp = 0;
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
                EncryptToDatabaseLevel(ref tmp);
                cloneList.Add(tmp);
            }

            var ret = GetDataBase()?.UpdateServer(cloneList) == true;
            if (ret == true)
            {
                LastReadFromDataSourceTimestamp = 0;
            }
            return ret;
        }

        public bool Database_DeleteServer(string id)
        {
            if (_isWritable)
            {
                var ret = GetDataBase()?.DeleteServer(id) == true;
                if (ret == true)
                    LastReadFromDataSourceTimestamp = 0;
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
                    LastReadFromDataSourceTimestamp = 0;
                return ret;
            }
            return false;
        }

        public List<ProtocolBase> Database_GetServers()
        {
            return GetDataBase()?.GetServers() ?? new List<ProtocolBase>();
        }

        public int Database_GetServersCount()
        {
            var s = GetDataBase()?.GetServers() ?? new List<ProtocolBase>();
            return s.Count;
        }
    }
}
