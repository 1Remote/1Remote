using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
    public partial class SqliteDatabaseSource : DatabaseSource
    {
        private readonly IDataBase _dataBase = IoC.Get<IDataBase>();

        public SqliteDatabaseSource(string dataSourceId, SqliteConfig config) : base(dataSourceId, config)
        {
            var fi = new FileInfo(config.Path);
            _isWritable = fi.IsReadOnly == false;
            _isReadable = true;
            Database_OpenConnection();
            Database_CloseConnection();
        }

        public override IDataBase GetDataBase()
        {
            return _dataBase;
        }


        public override string Database_GetPrivateKeyPath()
        {
            Debug.Assert(_dataBase != null);
            return _dataBase?.GetFromDatabase_RSA_PrivateKeyPath() ?? "";
        }
    }
}
