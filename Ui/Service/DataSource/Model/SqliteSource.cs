using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Service.DataSource.Model
{
    public partial class SqliteSource : DataSourceBase
    {
        private string _path = "";
        public string Path
        {
            get => _path;
            set
            {
                var newValue = value.Replace(Environment.CurrentDirectory, ".");
                SetAndNotifyIfChanged(ref _path, newValue);
                var fi = new FileInfo(Path);
                _isWritable = fi.IsReadOnly == false;
                _isReadable = true;
            }
        }

        public SqliteSource() : base()
        {
            SimpleLogHelper.Debug(nameof(SqliteSource) + " Construct: " + this.GetHashCode());
        }

        ~SqliteSource()
        {
            SimpleLogHelper.Debug(nameof(SqliteSource) + " Release: " + this.GetHashCode());
        }

        public override string GetConnectionString(int connectTimeOutSeconds = 5)
        {
            return DbExtensions.GetSqliteConnectionString(Path);
        }

        [JsonIgnore]
        public override DatabaseType DatabaseType => DatabaseType.Sqlite;
        [JsonIgnore]
        public override string Description => Path;



        private readonly IDataBase _dataBase = new DapperDataBaseFree();
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
