using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.DB.IDB;
using PRM.Core.Model;
using Shawn.Utils;

namespace PRM.Core.DB
{
    public static class DbExtensions
    {
        public static string GetSqliteConnectionString(string dbPath)
        {
            return $"Data Source={dbPath}; Pooling=true;Min Pool Size=1";
        }

        private static string TryGetConfig(this IDb iDb, string key)
        {
            try
            {
                var val = iDb.GetConfig(key);
                if (val == null)
                {
                    iDb.SetConfig(key, "");
                }
                return val;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return "";
            }
        }

        private static void TrySetConfig(this IDb iDb, string key, string value)
        {
            try
            {
                iDb.SetConfig(key, value ?? "");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        public static string Get_RSA_SHA1(this IDb iDb)
        {
            return iDb.TryGetConfig("RSA_SHA1");
        }

        public static void Set_RSA_SHA1(this IDb iDb, string value)
        {
            iDb.TrySetConfig("RSA_SHA1", value);
        }

        public static string Get_RSA_PublicKey(this IDb iDb)
        {
            return iDb.TryGetConfig("RSA_PublicKey");
        }

        public static void Set_RSA_PublicKey(this IDb iDb, string value)
        {
            iDb.TrySetConfig("RSA_PublicKey", value);
        }

        public static string Get_RSA_PrivateKeyPath(this IDb iDb)
        {
            return iDb.TryGetConfig("RSA_PrivateKeyPath");
        }

        public static void Set_RSA_PrivateKeyPath(this IDb iDb, string value)
        {
            iDb.TrySetConfig("RSA_PrivateKeyPath", value);
        }
    }
}