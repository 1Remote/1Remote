using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.Model;
using _1RM.View;
using com.github.xiangyuecn.rsacsharp;
using NUlid;
using NUlid.Rng;
using Shawn.Utils;
using Stylet;

namespace _1RM.Service.DataSource
{
    public partial class MysqlDatabaseSource : DatabaseSource
    {
        private readonly IDataBase _dataBase;

        public MysqlDatabaseSource(string dataSourceId, MysqlConfig config) : base(dataSourceId, config)
        {
            _dataBase = IoC.Get<IDataBase>();
        }

        public override IDataBase GetDataBase()
        {
            return _dataBase;
        }

        public override string Database_GetPrivateKeyPath()
        {
            throw new NotImplementedException();
        }

        public static bool TestConnection(string host, int port, string dbName, string userName, string password)
        {
            var db = new DapperDataBaseFree();
            try
            {
                var str = DbExtensions.GetMysqlConnectionString(host, port, dbName, userName, password);
                db.OpenNewConnection(DatabaseType.MySql, str);
                return db.IsConnected();
            }
            finally
            {
                db.CloseConnection();
            }
        }
    }
}
