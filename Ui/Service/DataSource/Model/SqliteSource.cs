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
    public sealed partial class SqliteSource : DataSourceBase
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
                IsWritable = fi.IsReadOnly == false;
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



        private readonly IDatabase _database = new DapperDatabaseFree();
        public override IDatabase GetDataBase()
        {
            return _database;
        }
    }
}
