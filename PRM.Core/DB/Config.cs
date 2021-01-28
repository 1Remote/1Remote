using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Model;
using Shawn.Utils;
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
                try
                {
                    Init();
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
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    return "";
                }
            }
            set
            {
                try
                {
                    Init();
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
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
        }

        public static string RSA_PublicKey
        {
            get
            {
                try
                {
                    Init();
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
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    return "";
                }
            }
            set
            {
                try
                {
                    Init();
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
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
        }

        public static string RSA_PrivateKeyPath
        {
            get
            {
                try
                {
                    Init();
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
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    return "";
                }
            }
            set
            {
                try
                {
                    Init();
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
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
        }
    }
}