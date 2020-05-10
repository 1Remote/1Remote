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
            var sha1 = RSA_SHA1;
            var pk = RSA_PublicKey;
            var ppkp = RSA_PrivateKeyPath;
        }

        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        [NotNull, Indexed]
        public string Key { get; set; } = "";

        [NotNull]
        public string Value { get; set; } = "";


        public static string RSA_SHA1
        {
            get
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
            set
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
                    if (value != null)
                    {
                        var c = db.Table<Config>().First(x => x.Key == "RSA_SHA1");
                        c.Value = value;
                        c.Update();
                    }
                }
            }
        }

        public static string RSA_PublicKey
        {
            get
            {
                Debug.Assert(IsInit == true);
                using (var db = GetDb())
                {
                    if (db.Table<Config>().All(x => x.Key != nameof(RSA_PublicKey)))
                    {
                        var c = new Config()
                        {
                            Key = nameof(RSA_PublicKey),
                            Value = "",
                        };
                        c.Insert();
                    }
                    {
                        var c = db.Table<Config>().First(x => x.Key == nameof(RSA_PublicKey));
                        return c.Value;
                    }
                }
            }
            set
            {
                Debug.Assert(IsInit == true);
                using (var db = GetDb())
                {
                    if (db.Table<Config>().All(x => x.Key != nameof(RSA_PublicKey)))
                    {
                        var c = new Config()
                        {
                            Key = nameof(RSA_PublicKey),
                            Value = "",
                        };
                        c.Insert();
                    }
                    if (value != null)
                    {
                        var c = db.Table<Config>().First(x => x.Key == nameof(RSA_PublicKey));
                        c.Value = value;
                        c.Update();
                    }
                }
            }
        }



        public static string RSA_PrivateKeyPath
        {
            get
            {
                Debug.Assert(IsInit == true);
                using (var db = GetDb())
                {
                    if (db.Table<Config>().All(x => x.Key != nameof(RSA_PrivateKeyPath)))
                    {
                        var c = new Config()
                        {
                            Key = nameof(RSA_PrivateKeyPath),
                            Value = "",
                        };
                        c.Insert();
                    }
                    {
                        var c = db.Table<Config>().First(x => x.Key == nameof(RSA_PrivateKeyPath));
                        return c.Value;
                    }
                }
            }
            set
            {
                Debug.Assert(IsInit == true);
                using (var db = GetDb())
                {
                    if (db.Table<Config>().All(x => x.Key != nameof(RSA_PrivateKeyPath)))
                    {
                        var c = new Config()
                        {
                            Key = nameof(RSA_PrivateKeyPath),
                            Value = "",
                        };
                        c.Insert();
                    }
                    if (value != null)
                    {
                        var c = db.Table<Config>().First(x => x.Key == nameof(RSA_PrivateKeyPath));
                        c.Value = value;
                        c.Update();
                    }
                }
            }
        }
    }
}
