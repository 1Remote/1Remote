using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO;

namespace _1RM.Service.DataSource.Model
{
    public class SqliteModel : DataSourceModel
    {
        private string _path = "";
        public string Path
        {
            get => _path;
            set => SetAndNotifyIfChanged(ref _path, value);
        }

        public SqliteModel(string name) : base(name)
        {
        }

        public override string GetConnectionString()
        {
            return DbExtensions.GetSqliteConnectionString(Path);
        }

        public override DatabaseType DatabaseType => DatabaseType.Sqlite;
    }
}
