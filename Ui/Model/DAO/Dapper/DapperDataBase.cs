using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using MySql.Data.MySqlClient;
using NUlid;
using Shawn.Utils;
using _1RM.Utils;

namespace _1RM.Model.DAO.Dapper
{
    public class DapperDatabase : IDatabase
    {
        protected IDbConnection? _dbConnection;
        protected string _connectionString = "";
        protected DatabaseType _databaseType = DatabaseType.Sqlite;

        public override void CloseConnection()
        {
            lock (this)
            {
                _dbConnection?.Close();
                if (_databaseType == DatabaseType.Sqlite)
                {
                    System.Data.SQLite.SQLiteConnection.ClearAllPools();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        public IDbConnection? Connection => _dbConnection;

        public override void OpenConnection()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return;
            if (IsConnected()) return;
            lock (this)
            {
                if (IsConnected()) return;
                if (_dbConnection == null)
                {
                    _dbConnection?.Close();
                    switch (_databaseType)
                    {
                        case DatabaseType.MySql:
                            _dbConnection = new MySqlConnection(_connectionString);
                            break;
                        case DatabaseType.Sqlite:
                            _dbConnection = new SQLiteConnection(_connectionString);
                            break;
                        //case DatabaseType.SqlServer:
                        //    break;
                        //case DatabaseType.PostgreSQL:
                        //    break;
                        //case DatabaseType.Oracle:
                        //    break;
                        default:
                            throw new NotImplementedException(_databaseType.ToString() + " not supported!");
                    }
                }
                _dbConnection.Open();
            }
        }

        public override void OpenNewConnection(DatabaseType type, string newConnectionString)
        {
            lock (this)
            {
                if (_databaseType == type && _connectionString == newConnectionString && IsConnected())
                    return;

                if (string.IsNullOrWhiteSpace(newConnectionString))
                    return;

                _databaseType = type;
                _connectionString = newConnectionString;

                _dbConnection?.Close();
                _dbConnection?.Dispose();
                //if (_databaseType == DatabaseType.Sqlite)
                //    SQLiteConnection.ClearAllPools();
                _dbConnection = null;
                OpenConnection();
            }
        }

        public override bool IsConnected()
        {
            lock (this)
            {
                return _dbConnection?.State == ConnectionState.Open;
            }
        }

        public override void InitTables()
        {
            _dbConnection?.Execute(@$"
CREATE TABLE IF NOT EXISTS `{Config.TABLE_NAME}` (
    `{nameof(Config.Key)}` VARCHAR (64) PRIMARY KEY
                                        NOT NULL
                                        UNIQUE,
    `{nameof(Config.Value)}` TEXT NOT NULL
);
");


            _dbConnection?.Execute(@$"
CREATE TABLE IF NOT EXISTS `{Server.TABLE_NAME}` (
    `{nameof(Server.Id)}`       VARCHAR (64) PRIMARY KEY
                                             NOT NULL
                                             UNIQUE,
    `{nameof(Server.Protocol)}` VARCHAR (32) NOT NULL,
    `{nameof(Server.ClassVersion)}` VARCHAR (32) NOT NULL,
    `{nameof(Server.Json)}`     TEXT         NOT NULL
);
");
            base.SetEncryptionTest();
        }

        public override ProtocolBase? GetServer(int id)
        {
            lock (this)
            {
                Debug.Assert(id > 0);
                var dbServer =
                    _dbConnection?.QueryFirstOrDefault<Server>(
                        $"SELECT * FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)}",
                        new { Id = id });
                return dbServer?.ToProtocolServerBase();
            }
        }

        public override List<ProtocolBase>? GetServers()
        {
            lock (this)
            {
#pragma warning disable CS8619
                return _dbConnection?.Query<Server>($"SELECT * FROM `{Server.TABLE_NAME}`").Select(x => x?.ToProtocolServerBase()).Where(x => x != null).ToList();
#pragma warning restore CS8619 
            }
        }

        public override int GetServerCount()
        {
            lock (this)
            {
                return _dbConnection?.ExecuteScalar<int>($"SELECT COUNT(*) FROM `{Server.TABLE_NAME}`") ?? 0;
            }
        }


        static readonly string SqlInsert = $@"INSERT INTO `{Server.TABLE_NAME}`
(`{nameof(Server.Id)}`,`{nameof(Server.Protocol)}`, `{nameof(Server.ClassVersion)}`, `{nameof(Server.Json)}`)
VALUES
(@{nameof(Server.Id)}, @{nameof(Server.Protocol)}, @{nameof(Server.ClassVersion)}, @{nameof(Server.Json)});";

        public override string AddServer(ProtocolBase protocolBase)
        {
            lock (this)
            {
                if (protocolBase.IsTmpSession())
                    protocolBase.Id = Ulid.NewUlid().ToString();
                var server = protocolBase.ToDbServer();
                var ret = _dbConnection?.Execute(SqlInsert, server);
                if (ret > 0)
                    SetDataUpdateTimestamp();
                return ret > 0 ? server.Id : string.Empty;
            }
        }

        public override int AddServer(IEnumerable<ProtocolBase> protocolBases)
        {
            lock (this)
            {
                var rng = new NUlid.Rng.MonotonicUlidRng();
                foreach (var protocolBase in protocolBases)
                {
                    if (protocolBase.IsTmpSession())
                        protocolBase.Id = Ulid.NewUlid(rng).ToString();
                }
                var servers = protocolBases.Select(x => x.ToDbServer()).ToList();
                var ret = _dbConnection?.Execute(SqlInsert, servers) > 0 ? protocolBases.Count() : 0;
                if (ret > 0)
                    SetDataUpdateTimestamp();
                return ret;
            }
        }

        static readonly string SqlUpdate = $@"UPDATE `{Server.TABLE_NAME}` SET
`{nameof(Server.Protocol)}` = @{nameof(Server.Protocol)},
`{nameof(Server.ClassVersion)}` = @{nameof(Server.ClassVersion)},
`{nameof(Server.Json)}` = @{nameof(Server.Json)}
WHERE `{nameof(Server.Id)}`= @{nameof(Server.Id)};";
        public override bool UpdateServer(ProtocolBase server)
        {
            lock (this)
            {
                OpenConnection();
                var ret = _dbConnection?.Execute(SqlUpdate, server.ToDbServer()) > 0;
                if (ret)
                    SetDataUpdateTimestamp();
                return ret;
            }
        }

        public override bool UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            lock (this)
            {
                OpenConnection();
                var dbss = servers.Select(x => x.ToDbServer());
                var ret = _dbConnection?.Execute(SqlUpdate, dbss) > 0;
                if (ret)
                    SetDataUpdateTimestamp();
                return ret;
            }
        }

        public override bool DeleteServer(string id)
        {
            lock (this)
            {
                OpenConnection();
                var ret = _dbConnection?.Execute($@"DELETE FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)};", new { Id = id }) > 0;
                if (ret)
                    SetDataUpdateTimestamp();
                return ret;
            }
        }

        public override bool DeleteServer(IEnumerable<string> ids)
        {
            lock (this)
            {
                OpenConnection();
                var ret = _dbConnection?.Execute($@"DELETE FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` IN @{nameof(Server.Id)};", new { Id = ids }) > 0;
                if (ret)
                    SetDataUpdateTimestamp();
                return ret;
            }
        }

        public override string? GetConfig(string key)
        {
            return GetConfigPrivate(key);
        }

        protected string? GetConfigPrivate(string key)
        {
            lock (this)
            {
                OpenConnection();
                var config = _dbConnection?.QueryFirstOrDefault<Config>($"SELECT * FROM `{Config.TABLE_NAME}` WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}",
                    new { Key = key, });
                return config?.Value;
            }
        }

        private static readonly string SqlInsertConfig = $@"INSERT INTO `{Config.TABLE_NAME}` (`{nameof(Config.Key)}`, `{nameof(Config.Value)}`)  VALUES (@{nameof(Config.Key)}, @{nameof(Config.Value)})";
        private static readonly string SqlUpdateConfig = $@"UPDATE `{Config.TABLE_NAME}` SET `{nameof(Config.Value)}` = @{nameof(Config.Value)} WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}";

        public override bool SetConfig(string key, string value)
        {
            return SetConfigPrivate(key, value);
        }

        protected bool SetConfigPrivate(string key, string value)
        {
            lock (this)
            {
                OpenConnection();
                var existed = GetConfigPrivate(key) != null;
                return _dbConnection?.Execute(existed ? SqlUpdateConfig : SqlInsertConfig, new { Key = key, Value = value, }) > 0;
            }
        }

        public override bool SetConfigRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers)
        {
            lock (this)
            {
                if (_dbConnection == null)
                    return false;
                OpenConnection();
                var data = servers.Select(x => x.ToDbServer());
                using var tran = _dbConnection.BeginTransaction();
                try
                {
                    var existedPrivate = GetConfigPrivate("RSA_PrivateKeyPath") != null;
                    var existedPublic = GetConfigPrivate("RSA_PublicKey") != null;

                    _dbConnection.Execute(existedPrivate ? SqlUpdateConfig : SqlInsertConfig, new { Key = "RSA_PrivateKeyPath", Value = privateKeyPath, }, tran);
                    _dbConnection.Execute(existedPublic ? SqlUpdateConfig : SqlInsertConfig, new { Key = "RSA_PublicKey", Value = publicKey, }, tran);
                    if (data.Any())
                        _dbConnection?.Execute(SqlUpdate, data, tran);
                    tran.Commit();
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Fatal(e);
                    // Not needed any rollback, if you don't call Complete
                    // a rollback is automatic exiting from the using block
                    //tran.Rollback();
                    return false;
                }

                return true;
            }
        }

        public override void SetDataUpdateTimestamp(long time = -1)
        {
            lock (this)
            {
                var timestamp = time;
                if (time <= 0)
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                SetConfigPrivate("UpdateTimestamp", timestamp.ToString());
            }
        }

        public override long GetDataUpdateTimestamp()
        {
            lock (this)
            {
                var val = GetConfigPrivate("UpdateTimestamp");
                if (val != null
                    && long.TryParse(val, out var t)
                    && t > 0)
                    return t;

                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                SetConfigPrivate("UpdateTimestamp", now.ToString());
                return now;
            }
        }
    }
}