using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.DB;
using PRM.Core.DB.Dapper;
using PRM.Core.DB.IDB;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public class PrmContext : NotifyPropertyChangedBase
    {
        protected IDb Db { get; private set; } = new DapperDb();

        public GlobalData AppData { get; } = new GlobalData();
        public DbOperator DbOperator { get; private set; } = null;

        public KeywordMatchService KeywordMatchService { get; } = new KeywordMatchService();

        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumDbStatus InitSqliteDb(string sqlitePath)
        {
            if (!IOPermissionHelper.HasWritePermissionOnFile(sqlitePath))
            {
                DbOperator = null;
                return EnumDbStatus.AccessDenied;
            }

            Db.OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(sqlitePath));
            DbOperator = new DbOperator(Db);
            var ret = DbOperator.CheckDbRsaStatus();
            if (ret == EnumDbStatus.OK)
                AppData.SetDbOperator(DbOperator);
            return ret;
        }

        //public void InitMysqlDb(string connectionString)
        //{
        //}
    }
}