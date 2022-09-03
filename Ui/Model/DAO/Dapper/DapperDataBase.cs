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

namespace _1RM.Model.DAO.Dapper
{
    public class DapperDataBase : IDataBase
    {
        protected IDbConnection? _dbConnection;
        protected string _connectionString = "";
        protected DatabaseType _databaseType = DatabaseType.Sqlite;

        public virtual void CloseConnection()
        {
            if (_dbConnection == null)
                return;
            lock (this)
            {
                _dbConnection.Close();
                if (_databaseType == DatabaseType.Sqlite)
                    SQLiteConnection.ClearAllPools();
            }
        }

        public IDbConnection? Connection => _dbConnection;

        public virtual void OpenConnection()
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

        public virtual void OpenNewConnection(DatabaseType type, string newConnectionString)
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
                if (_databaseType == DatabaseType.Sqlite)
                    SQLiteConnection.ClearAllPools();
                _dbConnection = null;
                OpenConnection();
            }
        }

        public virtual bool IsConnected()
        {
            lock (this)
            {
                return _dbConnection?.State == ConnectionState.Open;
            }
        }

        public virtual void InitTables()
        {
            _dbConnection?.Execute(@$"
CREATE TABLE IF NOT EXISTS `{Config.TABLE_NAME}` (
    `{nameof(Config.Key)}` VARCHAR PRIMARY KEY
                  UNIQUE,
    `{nameof(Config.Value)}` VARCHAR NOT NULL
);
");


            _dbConnection?.Execute(@$"
CREATE TABLE IF NOT EXISTS `{Server.TABLE_NAME}` (
    `{nameof(Server.Id)}`       VARCHAR (64) PRIMARY KEY
                                                NOT NULL
                                                UNIQUE,
    `{nameof(Server.Protocol)}` VARCHAR (32) NOT NULL,
    `{nameof(Server.ClassVersion)}` VARCHAR NOT NULL,
    `{nameof(Server.Json)}`     TEXT         NOT NULL
);
");
        }

        public virtual ProtocolBase? GetServer(int id)
        {
            Debug.Assert(id > 0);
            var dbServer =
                _dbConnection?.QueryFirstOrDefault<Server>(
                    $"SELECT * FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)}",
                    new { Id = id });
            return dbServer?.ToProtocolServerBase();
        }

        public virtual List<ProtocolBase>? GetServers()
        {
#pragma warning disable CS8619
            return _dbConnection?.Query<Server>($"SELECT * FROM `{Server.TABLE_NAME}`").Select(x => x?.ToProtocolServerBase()).Where(x => x != null).ToList();
#pragma warning restore CS8619
        }


        static readonly string SqlInsert = $@"INSERT INTO `{Server.TABLE_NAME}`
(`{nameof(Server.Id)}`,`{nameof(Server.Protocol)}`, `{nameof(Server.ClassVersion)}`, `{nameof(Server.Json)}`)
VALUES
(@{nameof(Server.Id)}, @{nameof(Server.Protocol)}, @{nameof(Server.ClassVersion)}, @{nameof(Server.Json)});";

        public virtual string AddServer(ProtocolBase protocolBase)
        {
            var server = protocolBase.ToDbServer();
            server.Id = Ulid.NewUlid().ToString();
            var ret = _dbConnection?.Execute(SqlInsert, server);
            if (ret > 0)
                SetDataUpdateTimestamp();
            return ret > 0 ? server.Id : string.Empty;
        }

        public virtual int AddServer(IEnumerable<ProtocolBase> protocolBases)
        {
            var rng = new NUlid.Rng.MonotonicUlidRng();
            var servers = protocolBases.Select(x => x.ToDbServer()).ToList();
            foreach (var s in servers)
            {
                s.Id = Ulid.NewUlid(rng).ToString();
            }
            var ret = _dbConnection?.Execute(SqlInsert, servers) > 0 ? protocolBases.Count() : 0;
            if (ret > 0)
                SetDataUpdateTimestamp();
            return ret;
        }

        static readonly string SqlUpdate = $@"UPDATE `{Server.TABLE_NAME}` SET
`{nameof(Server.Protocol)}` = @{nameof(Server.Protocol)},
`{nameof(Server.ClassVersion)}` = @{nameof(Server.ClassVersion)},
`{nameof(Server.Json)}` = @{nameof(Server.Json)}
WHERE `{nameof(Server.Id)}`= @{nameof(Server.Id)};";
        public virtual bool UpdateServer(ProtocolBase server)
        {
            var ret = _dbConnection?.Execute(SqlUpdate, server.ToDbServer()) > 0;
            if (ret)
                SetDataUpdateTimestamp();
            return ret;
        }

        public virtual bool UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            var dbss = servers.Select(x => x.ToDbServer());
            var ret = _dbConnection?.Execute(SqlUpdate, dbss) > 0;
            if (ret)
                SetDataUpdateTimestamp();
            return ret;
        }

        public virtual bool DeleteServer(string id)
        {
            if (_dbConnection == null)
                return false;
            var ret = _dbConnection?.Execute($@"DELETE FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)};", new { Id = id }) > 0;
            if (ret)
                SetDataUpdateTimestamp();
            return ret;
        }

        public virtual bool DeleteServer(IEnumerable<string> ids)
        {
            if (_dbConnection == null)
                return false;
            var ret = _dbConnection?.Execute($@"DELETE FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` IN @{nameof(Server.Id)};", new { Id = ids }) > 0;
            if (ret)
                SetDataUpdateTimestamp();
            return ret;
        }

        public virtual string? GetConfig(string key)
        {
            var config = _dbConnection?.QueryFirstOrDefault<Config>($"SELECT * FROM `{Config.TABLE_NAME}` WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}",
                new { Key = key, });
            return config?.Value;
        }

        private static readonly string SqlInsertConfig = $@"INSERT INTO `{Config.TABLE_NAME}` (`{nameof(Config.Key)}`, `{nameof(Config.Value)}`)  VALUES (@{nameof(Config.Key)}, @{nameof(Config.Value)})";
        private static readonly string SqlUpdateConfig = $@"UPDATE `{Config.TABLE_NAME}` SET `{nameof(Config.Value)}` = @{nameof(Config.Value)} WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}";

        public virtual bool SetConfig(string key, string value)
        {
            var existed = GetConfig("UpdateTimestamp") != null;
            return _dbConnection?.Execute(existed ? SqlUpdateConfig : SqlInsertConfig, new { Key = key, Value = value, }) > 0;
        }

        public virtual bool SetConfigRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers)
        {
            if (_dbConnection == null)
                return false;
            var data = servers.Select(x => x.ToDbServer());
            using var tran = _dbConnection.BeginTransaction();
            try
            {

                var existedPrivate = GetConfig("RSA_PrivateKeyPath") != null;
                var existedPublic = GetConfig("RSA_PublicKey") != null;

                _dbConnection.Execute(existedPrivate ? SqlUpdateConfig : SqlInsertConfig, new { Key = "RSA_PrivateKeyPath", Value = privateKeyPath, }, tran);
                _dbConnection.Execute(existedPublic ? SqlUpdateConfig : SqlInsertConfig, new { Key = "RSA_PublicKey", Value = publicKey, }, tran);
                if (data.Any())
                    _dbConnection?.Execute(SqlUpdate, data, tran);
                tran.Commit();
            }
            catch (Exception)
            {
                tran.Rollback();
                return false;
            }

            return true;
        }

        private void SetDataUpdateTimestamp()
        {
            DataUpdateTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        public virtual long DataUpdateTimestamp
        {
            get
            {
                var val = GetConfig("UpdateTimestamp");
                if (val != null
                    && long.TryParse(val, out var t))
                    return t;
                SetConfig("UpdateTimestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
                return 0;
            }
            set
            {
                var timestamp = value;
                if (value <= 0)
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                SetConfig("UpdateTimestamp", timestamp.ToString());
            }
        }
    }
}