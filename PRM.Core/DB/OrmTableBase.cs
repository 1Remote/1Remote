using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IWshRuntimeLibrary;
using PRM.Core.Model;
using SQLite;

namespace PRM.Core.DB
{
    public abstract class OrmTableBase
    {
        protected static bool IsInit = false;
        public virtual int Insert()
        {
            Debug.Assert(IsInit == true);
            using (var db = GetDb())
            {
                return db.Insert(this);
            }
        }

        public virtual bool Update()
        {
            Debug.Assert(IsInit == true);
            using (var db = GetDb())
            {
                return db.Update(this) > 0;
            }
        }

        public virtual bool Delete()
        {
            Debug.Assert(IsInit == true);
            using (var db = GetDb())
            {
                return db.Delete(this) > 0;
            }
        }

        public static List<T> ListAll<T>() where T : new()
        {
            Debug.Assert(IsInit == true);
            using (var db = GetDb())
            {
                return db.Table<T>().ToList();
            }
        }

        protected static SQLiteConnection GetDb()
        {
            return new SQLiteConnection(SystemConfig.GetInstance().DataSecurity.DbPath);
        }
    }
}
