using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.DB;

namespace PRM.Core.Model
{
    public class PrmContext : NotifyPropertyChangedBase
    {
        private IDb _db;
        public IDb Db
        {
            get => _db;
            set
            {
                _db = value;
                DbOperator = new DbOperator(_db);
                AppData.SetDbOperator(DbOperator);
            }
        }
        public GlobalData AppData { get; } = new GlobalData();
        public DbOperator DbOperator { get; private set; } = null;
    }
}
