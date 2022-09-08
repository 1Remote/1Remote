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
using MySql.Data.MySqlClient;
using NUlid;
using NUlid.Rng;
using Shawn.Utils;
using Stylet;

namespace _1RM.Service.DataSource
{
    public partial class MysqlDatabaseSource : DatabaseSource
    {
        private readonly IDataBase _dataBase;

        public MysqlDatabaseSource(MysqlConfig config) : base(config)
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

        public static bool TestConnection(MysqlConfig config)
        {
            return TestConnection(config.Host, config.Port, config.DatabaseName, config.UserName, config.Password);
        }
        public static bool TestConnection(string host, int port, string dbName, string userName, string password)
        {
            var str = DbExtensions.GetMysqlConnectionString(host, port, dbName, userName, password);
            var db = IoC.Get<IDataBase>();
            try
            {
                //var dbConnection = new MySqlConnection(str);
                //dbConnection.Open();
                db.OpenNewConnection(DatabaseType.MySql, str);
                return db.IsConnected();
            }
            catch
            {
                return false;
            }
            finally
            {
                db.CloseConnection();
            }
        }
    }
}
