using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using _1RM.Model.Protocol.Base;
using System.Data.SQLite;
using MySql.Data.MySqlClient;
using NUlid;
using Shawn.Utils;
using _1RM.Utils;

// ReSharper disable InconsistentNaming

namespace _1RM.Service.DataSource.DAO.Dapper
{
    public class DapperDatabase : IDatabase
    {
        public DapperDatabase(string databaseName, DatabaseType databaseType) : base(databaseName, databaseType)
        {
        }

        protected IDbConnection? _dbConnection;
        public IDbConnection? Connection => _dbConnection;

        public override void CloseConnection()
        {
            lock (this)
            {
                _dbConnection?.Close();
                if (DatabaseType == DatabaseType.Sqlite)
                {
                    SQLiteConnection.ClearAllPools();
                }
            }
        }


        /// <summary>
        /// create a IDatabase instance and open connection
        /// </summary>
        /// <param name="newConnectionString"></param>
        public override Result OpenNewConnection(string newConnectionString)
        {
            lock (this)
            {
                if (_connectionString == newConnectionString && IsConnected())
                    return Result.Success();
                if (string.IsNullOrWhiteSpace(newConnectionString))
                    return Result.Success();
                _connectionString = newConnectionString;
                _dbConnection?.Close();
                _dbConnection?.Dispose();
                _dbConnection = null;
                return OpenConnection();
            }
        }
        

        private Exception? _lastException = null;
        protected virtual Result OpenConnection(string actionInfo = "")
        {
            if (string.IsNullOrWhiteSpace(actionInfo))
                actionInfo = IoC.Translate("We can not connect database:");

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return Result.Fail(actionInfo, DatabaseName, "The connection string is null!");
            }

            if (IsConnected()) return Result.Success();
            lock (this)
            {
                if (IsConnected()) return Result.Success();
                _dbConnection?.Close();
                _dbConnection = DatabaseType switch
                {
                    DatabaseType.MySql => new MySqlConnection(_connectionString),
                    DatabaseType.Sqlite => new SQLiteConnection(_connectionString),
                    DatabaseType.SqlServer => throw new NotImplementedException(DatabaseType.ToString() + " not supported!"),
                    DatabaseType.PostgreSQL => throw new NotImplementedException(DatabaseType.ToString() + " not supported!"),
                    DatabaseType.Oracle => throw new NotImplementedException(DatabaseType.ToString() + " not supported!"),
                    _ => throw new NotImplementedException(DatabaseType.ToString() + " not supported!")
                };

                string error = "";
                try
                {
                    _dbConnection.Open();
                    _lastException = null;
                    return Result.Success();
                }
                catch (DllNotFoundException e)
                {
                    SimpleLogHelper.Error(e);
                    MsAppCenterHelper.Error(e);
                    MessageBoxHelper.ErrorAlert(e.Message + "\r\n\r\nPlease contact the developer if you get this error to help us fix it");
                    Environment.Exit(2);
                }
                catch (MySqlException mse)
                {
                    if (_lastException?.Message != mse.Message)
                    {
                        SimpleLogHelper.Error(mse);
                        MsAppCenterHelper.Error(mse);
                    }

                    error = mse.Message;
                    _lastException = mse;
                }
                catch (TimeoutException te)
                {
                    if (_lastException?.Message != te.Message)
                        SimpleLogHelper.Error(te);
                    error = te.Message;
                    _lastException = te;
                }
                catch (Exception e)
                {
                    if (_lastException?.Message != e.Message)
                    {
                        SimpleLogHelper.Error(e);
                        MsAppCenterHelper.Error(e, new Dictionary<string, string>() { { "DatabaseType", DatabaseType.ToString() } });
                    }
                    error = e.Message;
                    _lastException = e;
                }

                return Result.Fail(actionInfo, DatabaseName, error);
            }
        }

        public override bool IsConnected()
        {
            lock (this)
            {
                return _dbConnection?.State == ConnectionState.Open;
            }
        }

        public override Result InitTables()
        {
            string info = IoC.Translate("We can not create tables on database:");
            var result = OpenConnection(info);
            if (!result.IsSuccess) return result;

            try
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
                SetEncryptionTest();
            }
            catch (Exception e)
            {
                // 创建失败时，可能是只读权限的用户，再测试一下读权限
                if (!GetConfig("EncryptionTest").IsSuccess)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
            return Result.Success();
        }

        public override Result TableExists(string tableName)
        {
            string info = "We can not check table exists on database: ";
            var result = OpenConnection(info);
            if (!result.IsSuccess) return result;
            try
            {
                var ret = _dbConnection.QueryFirstOrDefault<int>($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName", new { tableName, });
                if (ret > 0)
                    return Result.Success();
                return Result.Fail(info, DatabaseName, $"Table {tableName} not exists!");
            }
            catch (Exception e)
            {
                return Result.Fail(info, DatabaseName, e.Message);
            }
        }

        public override ResultSelects GetServers()
        {
            string info = IoC.Translate("We can not select from database:");

            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return ResultSelects.Fail(result.ErrorInfo);
                try
                {
                    var ps = _dbConnection.Query<Server>($"SELECT * FROM `{Server.TABLE_NAME}`")
                                                            .Select(x => x?.ToProtocolServerBase())
                                                            .Where(x => x != null).ToList();
                    return ResultSelects.Success((ps as List<ProtocolBase>)!);
                }
                catch (Exception e)
                {
                    return ResultSelects.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        private const string SqlInsert = $@"INSERT INTO `{Server.TABLE_NAME}`
(`{nameof(Server.Id)}`,`{nameof(Server.Protocol)}`, `{nameof(Server.ClassVersion)}`, `{nameof(Server.Json)}`)
VALUES
(@{nameof(Server.Id)}, @{nameof(Server.Protocol)}, @{nameof(Server.ClassVersion)}, @{nameof(Server.Json)});";
        /// <summary>
        /// 插入成功后会更新 protocolBase.Id
        /// </summary>
        public override Result AddServer(ref ProtocolBase protocolBase)
        {
            string info = IoC.Translate("We can not insert into database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;

                try
                {
                    if (protocolBase.IsTmpSession())
                        protocolBase.Id = Ulid.NewUlid().ToString();
                    var server = protocolBase.ToDbServer();
                    int affCount = _dbConnection?.Execute(SqlInsert, server) ?? 0;
                    if (affCount > 0)
                        SetDataUpdateTimestamp();
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        /// <summary>
        /// 插入成功后会更新 protocolBase.Id
        /// </summary>
        public override Result AddServer(IEnumerable<ProtocolBase> protocolBases)
        {
            string info = IoC.Translate("We can not insert into database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var rng = new NUlid.Rng.MonotonicUlidRng();
                    foreach (var protocolBase in protocolBases)
                    {
                        if (protocolBase.IsTmpSession())
                            protocolBase.Id = Ulid.NewUlid(rng).ToString();
                    }
                    var servers = protocolBases.Select(x => x.ToDbServer()).ToList();
                    var affCount = _dbConnection?.Execute(SqlInsert, servers) > 0 ? protocolBases.Count() : 0;
                    if (affCount > 0)
                        SetDataUpdateTimestamp();
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        static readonly string SqlUpdate = $@"UPDATE `{Server.TABLE_NAME}` SET
`{nameof(Server.Protocol)}` = @{nameof(Server.Protocol)},
`{nameof(Server.ClassVersion)}` = @{nameof(Server.ClassVersion)},
`{nameof(Server.Json)}` = @{nameof(Server.Json)}
WHERE `{nameof(Server.Id)}`= @{nameof(Server.Id)};";
        public override Result UpdateServer(ProtocolBase server)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var ret = _dbConnection?.Execute(SqlUpdate, server.ToDbServer()) > 0;
                    if (ret)
                    {
                        SetDataUpdateTimestamp();
                    }
                    else
                    {
                        // TODO 如果`{nameof(Server.Id)}`= @{nameof(Server.Id)}的项目不存在时怎么办？
                    }
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override Result UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var items = servers.Select(x => x.ToDbServer());
                    var ret = _dbConnection?.Execute(SqlUpdate, items) > 0;
                    if (ret)
                    {
                        SetDataUpdateTimestamp();
                    }
                    else
                    {
                        // TODO 如果`{nameof(Server.Id)}`= @{nameof(Server.Id)}的项目不存在时怎么办？
                    }
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override Result DeleteServer(IEnumerable<string> ids)
        {
            var info = IoC.Translate("We can not delete from database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var ret = _dbConnection?.Execute($@"DELETE FROM `{Server.TABLE_NAME}` WHERE `{nameof(Server.Id)}` IN @{nameof(Server.Id)};", new { Id = ids }) > 0;
                    if (ret)
                        SetDataUpdateTimestamp();
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override ResultString GetConfig(string key)
        {
            return GetConfigPrivate(key);
        }

        protected ResultString GetConfigPrivate(string key)
        {
            string info = IoC.Translate("We can not read from database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return ResultString.Fail(result.ErrorInfo);
                try
                {
                    var config = _dbConnection?.QueryFirstOrDefault<Config>($"SELECT * FROM `{Config.TABLE_NAME}` WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}",
                        new { Key = key, });
                    return ResultString.Success(config?.Value);
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        private static readonly string SqlInsertConfig = $@"INSERT INTO `{Config.TABLE_NAME}` (`{nameof(Config.Key)}`, `{nameof(Config.Value)}`)  VALUES (@{nameof(Config.Key)}, @{nameof(Config.Value)})";
        private static readonly string SqlUpdateConfig = $@"UPDATE `{Config.TABLE_NAME}` SET `{nameof(Config.Value)}` = @{nameof(Config.Value)} WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}";
        private static readonly string SqlDeleteConfig = $@"Delete FROM `{Config.TABLE_NAME}` WHERE `{nameof(Config.Key)}` = @{nameof(Config.Key)}";

        public override Result SetConfig(string key, string? value)
        {
            return SetConfigPrivate(key, value);
        }

        protected Result SetConfigPrivate(string key, string? value)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;

                var get = GetConfigPrivate(key);
                if (!get.IsSuccess) return Result.Fail(get.ErrorInfo);
                try
                {
                    bool ret = true;
                    if (value == null)
                    {
                        _dbConnection?.Execute(SqlDeleteConfig, new { Key = key, });
                    }
                    else
                    {
                        bool existed = !string.IsNullOrEmpty(get.Result);
                        ret = _dbConnection?.Execute(existed ? SqlUpdateConfig : SqlInsertConfig, new { Key = key, Value = value, }) > 0;
                    }
                    return ret ? Result.Success() : Result.Fail("Failed");
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override Result SetDataUpdateTimestamp(long time = -1)
        {
            lock (this)
            {
                var timestamp = time;
                if (time <= 0)
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return SetConfigPrivate("UpdateTimestamp", timestamp.ToString());
            }
        }

        public override ResultLong GetDataUpdateTimestamp()
        {
            lock (this)
            {
                var val = GetConfigPrivate("UpdateTimestamp");
                if (!val.IsSuccess) return ResultLong.Fail(val.ErrorInfo);
                if (long.TryParse(val.Result, out var t)
                    && t > 0)
                    return ResultLong.Success(t);
                return ResultLong.Success(0);
            }
        }
    }
}