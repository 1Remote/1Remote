using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using Dapper;
using PRM.Core.DB.IDB;
using PRM.Core.Protocol;

namespace PRM.Core.DB.Dapper
{
    public class DapperDb : IDb
    {
        private IDbConnection _dbConnection;
        private string _connectionString;
        private readonly object _locker = new object();

        public void CloseConnection()
        {
            lock (_locker)
            {
                _dbConnection?.Dispose();
                _dbConnection = null;
            }
        }

        public void OpenConnection(DatabaseType? type = null, string newConnectionString = "")
        {
            lock (_locker)
            {
                if (_connectionString == newConnectionString
                    && IsConnected())
                    return;

                if (string.IsNullOrWhiteSpace(newConnectionString))
                    return;

                if (_connectionString != newConnectionString)
                {
                    _connectionString = newConnectionString;
                    _dbConnection?.Dispose();
                    _dbConnection = null;

                    if (type == DatabaseType.Sqlite)
                        _dbConnection = new SQLiteConnection(_connectionString);
                    else
                        throw new NotImplementedException(type.ToString() + " not supported!");

                    _dbConnection.Open();
                }
            }

            InitTables();
        }

        public bool IsConnected()
        {
            lock (_locker)
            {
                return _dbConnection?.State == ConnectionState.Open;
            }
        }

        public void InitTables()
        {
            _dbConnection?.Execute(@"
CREATE TABLE IF NOT EXISTS `Server` (
  `Id` INTEGER PRIMARY KEY AUTOINCREMENT,
  `Protocol` VARCHAR,
  `ClassVersion` VARCHAR,
  `JsonConfigString` VARCHAR,
  `UpdatedToken` VARCHAR
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
        }

        public ProtocolServerBase GetServer(int id)
        {
            Debug.Assert(id > 0);
            var dbServer =
                _dbConnection?.QueryFirstOrDefault<Server>(
                    $"SELECT * FROM `{nameof(Server)}` WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)}",
                    new { Id = id });
            return dbServer?.ToProtocolServerBase();
        }

        public List<ProtocolServerBase> GetServers()
        {
            return _dbConnection?.Query<Server>($"SELECT * FROM `{nameof(Server)}`")
                ?.Select(x => x?.ToProtocolServerBase()).Where(x => x != null).ToList();
        }

        public int AddServer(ProtocolServerBase server)
        {
            Debug.Assert(server.Id == 0);
            return _dbConnection?.Execute(
                $@"
INSERT INTO `{nameof(Server)}`
(`{nameof(Server.Protocol)}`, `{nameof(Server.ClassVersion)}`, `{nameof(Server.JsonConfigString)}`, `{nameof(Server.UpdatedToken)}`)
VALUES
(@{nameof(Server.Protocol)}, @{nameof(Server.ClassVersion)}, @{nameof(Server.JsonConfigString)}, @{nameof(Server.UpdatedToken)});",
                server.ToDbServer()) > 0
                ? _dbConnection?.QuerySingle<int>("SELECT LAST_INSERT_ROWID();") ?? 0
                : 0;
        }

        public bool UpdateServer(ProtocolServerBase server)
        {
            Debug.Assert(server.Id > 0);
            return _dbConnection?.Execute(
                $@"
UPDATE Server SET
`{nameof(Server.Protocol)}` = @{nameof(Server.Protocol)},
`{nameof(Server.ClassVersion)}` = @{nameof(Server.ClassVersion)},
`{nameof(Server.JsonConfigString)}` = @{nameof(Server.JsonConfigString)},
`{nameof(Server.UpdatedToken)}` = @{nameof(Server.UpdatedToken)}
WHERE `{nameof(Server.Id)}`= @{nameof(Server.Id)};",
                server.ToDbServer()) > 0;
        }

        public bool DeleteServer(int id)
        {
            return _dbConnection?.Execute($@"
DELETE FROM `{nameof(Server)}`
WHERE `{nameof(Server.Id)}` = @{nameof(Server.Id)};", new { Id = id }) > 0;
        }

        public string GetConfig(string key)
        {
            var config = _dbConnection?.QueryFirstOrDefault<Config>($"SELECT * FROM `{nameof(Config)}` WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}",
                new { Key = key, });
            return config?.Value;
        }

        public void SetConfig(string key, string value)
        {
            if (GetConfig(key) != null)
            {
                _dbConnection?.Execute(
                    $@"UPDATE `{nameof(Config)}`  SET `{nameof(Config.Value)}` = @{nameof(Config.Value)} WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}",
                    new
                    {
                        Key = key,
                        Value = value,
                    });
            }
            else
            {
                _dbConnection?.Execute(
                    $@"INSERT INTO `{nameof(Config)}` (`{nameof(Config.Key)}`, `{nameof(Config.Value)}`)  VALUES (@{nameof(Config.Key)}, @{nameof(Config.Value)})",
                    new
                    {
                        Key = key,
                        Value = value,
                    });
            }
        }

        public string GetProtocolTemplate(string key)
        {
            throw new NotImplementedException();
        }

        public void SetProtocolTemplate(string key, string value)
        {
            throw new NotImplementedException();
        }
    }
}