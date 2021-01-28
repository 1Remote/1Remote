using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.DB;
using PRM.Core.DB.freesql;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public class PrmContext : NotifyPropertyChangedBase
    {
        public IDb Db { get; private set; } = new FreeSqlDb();

        public GlobalData AppData { get; } = new GlobalData();
        public DbOperator DbOperator { get; private set; } = null;

        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumConnectResult InitSqliteDb(string sqlitePath)
        {
            if (!IOPermissionHelper.HasWritePermissionOnFile(sqlitePath))
            {
                return EnumConnectResult.AccessDenied;
            }

            Db.CloseConnection();
            Db.OpenConnection(DatabaseType.Sqlite, FreeSqlDb.GetConnectionStringSqlite(sqlitePath));
            DbOperator = new DbOperator(Db);
            var ret = DbOperator.CheckDbRsaIsOk();
            switch (ret)
            {
                case 0:
                    AppData.SetDbOperator(DbOperator);
                    return EnumConnectResult.Success;

                case -2:
                    return EnumConnectResult.RsaPrivateKeyFormatError;

                case -3:
                    return EnumConnectResult.RsaNotMatched;

                case -1:
                default:
                    return EnumConnectResult.RsaPrivateKeyNotFound;
            }
        }

        //public void InitMysqlDb(string connectionString)
        //{
        //}
    }
}