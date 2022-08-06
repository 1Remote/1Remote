using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
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
                SQLiteConnection.ClearAllPools();
            }
        }

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
                    if (_databaseType == DatabaseType.Sqlite)
                        _dbConnection = new SQLiteConnection(_connectionString);
                    else
                        throw new NotImplementedException(_databaseType.ToString() + " not supported!");
                }
                _dbConnection.Open();
            }
        }

        public virtual void OpenConnection(DatabaseType type, string newConnectionString)
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
                SQLiteConnection.ClearAllPools();
                _dbConnection = null;
                OpenConnection();
            }

            InitTables();
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
            _dbConnection?.Execute(@"
CREATE TABLE IF NOT EXISTS `Configs` (
    `Key` VARCHAR PRIMARY KEY
                  UNIQUE,
    `Value` VARCHAR NOT NULL
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
            return _dbConnection?.Execute(SqlInsert, servers) > 0 ? protocolBases.Count() : 0;
        }

        static readonly string SqlUpdate = $@"UPDATE `{Server.TABLE_NAME}` SET
`{nameof(Server.Protocol)}` = @{nameof(Server.Protocol)},
`{nameof(Server.ClassVersion)}` = @{nameof(Server.ClassVersion)},
`{nameof(Server.Json)}` = @{nameof(Server.Json)}
WHERE `{nameof(Server.Id)}`= @{nameof(Server.Id)};";
        public virtual bool UpdateServer(ProtocolBase server)
        {
            return _dbConnection?.Execute(SqlUpdate, server.ToDbServer()) > 0;
        }

        public virtual bool UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            var dbss = servers.Select(x => x.ToDbServer());
            var ret = _dbConnection?.Execute(SqlUpdate, dbss);
            return ret > 0;
        }

        public virtual bool DeleteServer(string id)
        {
            return _dbConnection?.Execute($@"DELETE FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)};", new { Id = id }) > 0;
        }

        public virtual bool DeleteServer(IEnumerable<string> ids)
        {
            return _dbConnection?.Execute($@"DELETE FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` IN @{nameof(Server.Id)};", new { Id = ids }) > 0;
        }

        public virtual string? GetConfig(string key)
        {
            var config = _dbConnection?.QueryFirstOrDefault<Config>($"SELECT * FROM `{Config.TABLE_NAME}` WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}",
                new { Key = key, });
            return config?.Value;
        }

        private static readonly string SqlInsertConfig = $@"INSERT INTO `{Config.TABLE_NAME}` (`{nameof(Config.Key)}`, `{nameof(Config.Value)}`)  VALUES (@{nameof(Config.Key)}, @{nameof(Config.Value)})";
        private static readonly string SqlUpdateConfig = $@"UPDATE `{Config.TABLE_NAME}`  SET `{nameof(Config.Value)}` = @{nameof(Config.Value)} WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}";
        public virtual void SetConfig(string key, string value)
        {
            if (GetConfig(key) != null)
            {
                _dbConnection?.Execute(SqlUpdateConfig, new { Key = key, Value = value, });
            }
            else
            {
                _dbConnection?.Execute(SqlInsertConfig, new { Key = key, Value = value, });
            }
        }

        public virtual string GetProtocolTemplate(string key)
        {
            throw new NotImplementedException();
        }

        public virtual void SetProtocolTemplate(string key, string value)
        {
            throw new NotImplementedException();
        }

        public virtual bool SetRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers)
        {
            if (_dbConnection == null)
                return false;
            var existedPrivate = GetConfig("RSA_PrivateKeyPath") != null;
            var existedPublic = GetConfig("RSA_PublicKey") != null;
            CloseConnection();
            OpenConnection();
            var data = servers.Select(x => x.ToDbServer());
            using var tran = _dbConnection.BeginTransaction();
            try
            {
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

        public virtual void SetRsaPrivateKeyPath(string privateKeyPath)
        {
            SetConfig("RSA_PrivateKeyPath", privateKeyPath);
        }
    }
}