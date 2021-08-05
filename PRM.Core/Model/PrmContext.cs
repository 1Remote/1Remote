using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.DB;
using PRM.Core.DB.Dapper;
using PRM.Core.I;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public class PrmContext : NotifyPropertyChangedBase
    {
        public GlobalData AppData { get; } = new GlobalData();
        public DataService DataService { get; private set; } = null;

        public KeywordMatchService KeywordMatchService { get; } = new KeywordMatchService();

        /// <summary>
        /// init db connection to a sqlite db. Do make sure sqlitePath is writable!.
        /// </summary>
        /// <param name="sqlitePath"></param>
        public EnumDbStatus InitSqliteDb(string sqlitePath)
        {
            if (!IOPermissionHelper.HasWritePermissionOnFile(sqlitePath))
            {
                DataService = null;
                return EnumDbStatus.AccessDenied;
            }
            
            DataService = new DataService();
            DataService.OpenDatabaseConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(sqlitePath));
            var ret = DataService.CheckDbRsaStatus();
            if (ret == EnumDbStatus.OK)
                AppData.SetDbOperator(DataService);
            return ret;
        }
    }
}