using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PRM.Core.Protocol;
using FreeSql.Sqlite;

namespace PRM.Core.DB.freesql
{
    public class FreeSqlDb : IDb
    {
        private IFreeSql _fsql = null;
        private FreeSql.DataType _dbType;
        private string _connectionString;

        public FreeSqlDb()
        {
        }

        public FreeSqlDb(DatabaseType dbType, string connectionString)
        {
            OpenConnection(dbType, connectionString);
        }

        public void CloseConnection()
        {
            _fsql?.Dispose();
            _fsql = null;
        }

        private static FreeSql.DataType DatabaseType2DataType(DatabaseType? type)
        {
            return type switch
            {
                DatabaseType.MySql => FreeSql.DataType.MySql,
                DatabaseType.SqlServer => FreeSql.DataType.SqlServer,
                DatabaseType.PostgreSQL => FreeSql.DataType.PostgreSQL,
                DatabaseType.Oracle => FreeSql.DataType.Oracle,
                DatabaseType.Sqlite => FreeSql.DataType.Sqlite,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public void OpenConnection(DatabaseType? type = null, string newConnectionString = "")
        {
            _fsql?.Dispose();

            if (type != null && !string.IsNullOrWhiteSpace(newConnectionString))
            {
                var dbType = DatabaseType2DataType(type);
                _dbType = dbType;
                _connectionString = newConnectionString;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
                return;

            _fsql = new FreeSql.FreeSqlBuilder()
                .UseConnectionString(_dbType, _connectionString)
                .Build();

            InitTables();
        }

        public bool IsConnected()
        {
            return _fsql != null;
        }

        public void InitTables()
        {
            _fsql?.CodeFirst.SyncStructure<DbServer>();
            _fsql?.CodeFirst.SyncStructure<DbConfig>();
        }

        public ProtocolServerBase GetServer(int id)
        {
            Debug.Assert(id > 0);
            var dbServer = _fsql?.Select<DbServer>().Where(x => x.Id == id).First();
            return dbServer?.ToProtocolServerBase();
        }

        public List<ProtocolServerBase> GetServers()
        {
            return _fsql?.Select<DbServer>().ToList().Select(x => x?.ToProtocolServerBase()).Where(x => x != null).ToList();
        }

        public int AddServer(ProtocolServerBase server)
        {
            Debug.Assert(server.Id == 0);
            var id = _fsql?.Insert<DbServer>().AppendData(server.ToDbServer()).ExecuteIdentity();
            return id == null ? 0 : (int)id;
        }

        public bool UpdateServer(ProtocolServerBase server)
        {
            Debug.Assert(server.Id > 0);
            var dbServer = server.ToDbServer();
            return _fsql?.Update<DbServer>().SetSource(dbServer).ExecuteAffrows() > 0;
        }

        public bool DeleteServer(int id)
        {
            return _fsql?.Delete<DbServer>().Where(x => x.Id == id).ExecuteAffrows() > 0;
        }

        public string GetConfig(string key)
        {
            var value = _fsql?.Select<DbConfig>().Where(x => x.Key == key).First();
            return value?.Value;
        }

        public void SetConfig(string key, string value)
        {
            if (GetConfig(key) != null)
            {
                _fsql?.Update<DbConfig>().Set(x => x.Value, value).Where(x => x.Key == key).ExecuteAffrows();
            }
            else
            {
                _fsql?.Insert(new DbConfig
                {
                    Key = key,
                    Value = value,
                }).ExecuteAffrows();
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