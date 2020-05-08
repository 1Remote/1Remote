using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Model;
using SQLite;

namespace PRM.Core.DB
{
    public class Config : OrmTableBase
    {
        public static void Init()
        {
            using (var db = GetDb())
            {
                db.CreateTable<Config>();
            }
            IsInit = true;
            GetRSA_SHA1();
        }

        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        [NotNull, Indexed]
        public string Key { get; set; } = "";

        [NotNull]
        public string Value { get; set; } = "";




        public static string GetRSA_SHA1()
        {
            Debug.Assert(IsInit == true);
            using (var db = GetDb())
            {
                if (db.Table<Config>().All(x => x.Key != "RSA_SHA1"))
                {
                    var c = new Config()
                    {
                        Key = "RSA_SHA1",
                        Value = "",
                    };
                    c.Insert();
                }
                {
                    var c = db.Table<Config>().First(x => x.Key == "RSA_SHA1");
                    return c.Value;
                }
            }
        }

        public static void SetRSA_SHA1(string sha1)
        {
            Debug.Assert(IsInit == true);
            using (var db = GetDb())
            {
                if (db.Table<Config>().All(x => x.Key != "RSA_SHA1"))
                {
                    var c = new Config()
                    {
                        Key = "RSA_SHA1",
                        Value = "",
                    };
                    c.Insert();
                }

                if (sha1 != null)
                {
                    var c = db.Table<Config>().First(x => x.Key == "RSA_SHA1");
                    c.Value = sha1;
                    c.Update();
                }
            }
        }
    }
}
