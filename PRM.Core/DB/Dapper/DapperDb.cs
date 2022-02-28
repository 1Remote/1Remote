using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windows.Foundation.Collections;
using Dapper;
using PRM.Core.I;
using PRM.Core.Protocol;

namespace PRM.Core.DB.Dapper
{
    public class DapperDb : IDb
    {
        protected IDbConnection _dbConnection;
        protected string _connectionString;
        protected readonly object _locker = new object();
        protected DatabaseType _databaseType = DatabaseType.Sqlite;

        public virtual void CloseConnection()
        {
            if (_dbConnection == null)
                return;
            lock (_locker)
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
            lock (_locker)
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
            lock (_locker)
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
            lock (_locker)
            {
                return _dbConnection?.State == ConnectionState.Open;
            }
        }

        public virtual void InitTables()
        {
            _dbConnection?.Execute(@"
CREATE TABLE IF NOT EXISTS `Server` (
  `Id` INTEGER PRIMARY KEY AUTOINCREMENT,
  `Protocol` VARCHAR,
  `ClassVersion` VARCHAR,
  `JsonConfigString` VARCHAR
);");
            _dbConnection?.Execute(@"
BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS `Config` (
  `Id` INTEGER PRIMARY KEY AUTOINCREMENT,
  `Key` VARCHAR,
  `Value` VARCHAR
);
CREATE UNIQUE INDEX IF NOT EXISTS `uk_key` ON `Config`(`Key`);
COMMIT TRANSACTION;
");

            try
            {
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        public virtual ProtocolServerBase GetServer(int id)
        {
            Debug.Assert(id > 0);
            var dbServer =
                _dbConnection?.QueryFirstOrDefault<Server>(
                    $"SELECT * FROM `{nameof(Server)}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)}",
                    new { Id = id });
            return dbServer?.ToProtocolServerBase();
        }

        public virtual List<ProtocolServerBase> GetServers()
        {
            return _dbConnection?.Query<Server>($"SELECT * FROM `{nameof(Server)}`")
                ?.Select(x => x?.ToProtocolServerBase()).Where(x => x != null).ToList();
        }


        static readonly string SqlInsert = $@"INSERT INTO `{nameof(Server)}`
(`{nameof(Server.Protocol)}`, `{nameof(Server.ClassVersion)}`, `{nameof(Server.JsonConfigString)}`)
VALUES
(@{nameof(Server.Protocol)}, @{nameof(Server.ClassVersion)}, @{nameof(Server.JsonConfigString)});";
        public virtual int AddServer(ProtocolServerBase server)
        {
            Debug.Assert(server.Id == 0);
            var ret = _dbConnection?.Execute(SqlInsert, server.ToDbServer());
            return ret > 0 ? (_dbConnection?.QuerySingle<int>("SELECT LAST_INSERT_ROWID();") ?? 0) : 0;
        }

        public virtual int AddServer(IEnumerable<ProtocolServerBase> servers)
        {
            var dbss = servers.Select(x => x.ToDbServer());
            return _dbConnection?.Execute(SqlInsert, dbss) > 0 ? servers.Count() : 0;
        }

        static readonly string SqlUpdate = $@"UPDATE Server SET
`{nameof(Server.Protocol)}` = @{nameof(Server.Protocol)},
`{nameof(Server.ClassVersion)}` = @{nameof(Server.ClassVersion)},
`{nameof(Server.JsonConfigString)}` = @{nameof(Server.JsonConfigString)}
WHERE `{nameof(Server.Id)}`= @{nameof(Server.Id)};";
        public virtual bool UpdateServer(ProtocolServerBase server)
        {
            Debug.Assert(server.Id > 0);
            return _dbConnection?.Execute(SqlUpdate, server.ToDbServer()) > 0;
        }

        public virtual bool UpdateServer(IEnumerable<ProtocolServerBase> servers)
        {
            var dbss = servers.Select(x => x.ToDbServer());
            var ret = _dbConnection?.Execute(SqlUpdate, dbss);
            return ret > 0;
        }

        public virtual bool DeleteServer(int id)
        {
            return _dbConnection?.Execute($@"DELETE FROM `{nameof(Server)}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)};", new { Id = id }) > 0;
        }

        public virtual bool DeleteServer(IEnumerable<int> ids)
        {
            return _dbConnection?.Execute($@"DELETE FROM `{nameof(Server)}` WHERE `{nameof(Server.Id)}` IN @{nameof(Server.Id)};", new { Id = ids }) > 0;
        }

        public virtual string GetConfig(string key)
        {
            var config = _dbConnection?.QueryFirstOrDefault<Config>($"SELECT * FROM `{nameof(Config)}` WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}",
                new { Key = key, });
            return config?.Value;
        }

        private static readonly string SqlInsertConfig = $@"INSERT INTO `{nameof(Config)}` (`{nameof(Config.Key)}`, `{nameof(Config.Value)}`)  VALUES (@{nameof(Config.Key)}, @{nameof(Config.Value)})";
        private static readonly string SqlUpdateConfig = $@"UPDATE `{nameof(Config)}`  SET `{nameof(Config.Value)}` = @{nameof(Config.Value)} WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}";
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

        public virtual bool SetRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolServerBase> servers)
        {
            var existedPrivate = GetConfig("RSA_PrivateKeyPath") != null;
            var existedPublic = GetConfig("RSA_PublicKey") != null;
            CloseConnection();
            OpenConnection();
            var data = servers.Select(x => x.ToDbServer());
            using (var tran = _dbConnection.BeginTransaction())
            {
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
            }
            return true;
        }

        public virtual void SetRsaPrivateKeyPath(string privateKeyPath)
        {
            SetConfig("RSA_PrivateKeyPath", privateKeyPath);
        }
    }
}