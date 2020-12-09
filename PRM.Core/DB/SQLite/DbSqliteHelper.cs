using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Model;
using PRM.Core.Protocol;

namespace PRM.Core.DB.SQLite
{
    class DbSqliteHelper : IDb
    {
        public readonly string DbFilePath;
        public DbSqliteHelper(string dbFilePath)
        {
            DbFilePath = dbFilePath;
        }

        public void InitTables()
        {
            using var db = GetConn();
            db.Select<DbServer>();
        }

        public ProtocolServerBase GetServer(uint id)
        {
            using var db = GetConn();
            var obj = db.Select<DbServer>()
                .Where(x => x.Id == id)
                .ToOne();
            return obj?.ToServerBase();
        }

        public List<ProtocolServerBase> GetServers()
        {
            using var db = GetConn();
            var obj = db.Select<DbServer>().ToList();
            var ret = new List<ProtocolServerBase>();
            foreach (var sqLiteDbServerModel in obj)
            {
                var s = sqLiteDbServerModel.ToServerBase();
                if (s != null)
                {
                    ret.Add(s);
                }
            }
            return ret;
        }

        public uint AddServer(ProtocolServerBase server)
        {
            using var db = GetConn();
            var dbServer = server.ToDbServer();
            return (uint) db.Insert<DbServer>().AppendData(dbServer).ExecuteIdentity();
        }

        public void UpdateServer(ProtocolServerBase server)
        {
            using var db = GetConn();
            var dbServer = server.ToDbServer();
            db.Update<DbServer>().SetSource(dbServer).ExecuteAffrows();
        }

        public void DeleteServer(uint id)
        {
            using var db = GetConn();
            db.Delete<DbServer>().Where(x => x.Id == id).ExecuteAffrows();
        }

        public string GetValueByKeyFromKeyValueTable(string tableName, string key)
        {
            using var db = GetConn();
            var obj = db.Select<DbServer>().ToList();
            var ret = new List<ProtocolServerBase>();
            foreach (var sqLiteDbServerModel in obj)
            {
                var s = sqLiteDbServerModel.ToServerBase();
                if (s != null)
                {
                    ret.Add(s);
                }
            }
            return ret;
        }

        public void SetValueByKeyFromKeyValueTable(string tableName, string key, string value)
        {
            throw new NotImplementedException();
        }

        public string GetConfig(string key)
        {
            throw new NotImplementedException();
        }

        public string SetConfig(string key, string value)
        {
            throw new NotImplementedException();
        }

        public string GetProtocolTemplate(string key)
        {
            throw new NotImplementedException();
        }

        public string SetProtocolTemplate(string key, string value)
        {
            throw new NotImplementedException();
        }

        public IFreeSql GetConn()
        {
            IFreeSql fsql = new FreeSql.FreeSqlBuilder()
                .UseConnectionString(FreeSql.DataType.Sqlite, $"Data Source{DbFilePath}", typeof(FreeSql.Sqlite.SqliteProvider<>))
                .UseAutoSyncStructure(true)
                .Build();
            return fsql;
        }
    }
}
