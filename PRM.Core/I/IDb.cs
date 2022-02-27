using System;
using System.Collections.Generic;
using PRM.Core.Protocol;
using Shawn.Utils;

namespace PRM.Core.I
{
    public enum DatabaseType
    {
        MySql,
        SqlServer,
        PostgreSQL,
        Oracle,
        Sqlite,
    }

    public interface IDb
    {
        void CloseConnection();

        void OpenConnection();
        void OpenConnection(DatabaseType type, string newConnectionString);

        bool IsConnected();

        /// <summary>
        /// create tables
        /// </summary>
        void InitTables();

        ProtocolServerBase GetServer(int id);

        List<ProtocolServerBase> GetServers();

        /// <summary>
        /// insert and return id
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        int AddServer(ProtocolServerBase server);
        /// <summary>
        /// insert and return count
        /// </summary>
        /// <returns></returns>
        int AddServer(IEnumerable<ProtocolServerBase> servers);

        /// <summary>
        /// update server by id
        /// </summary>
        bool UpdateServer(ProtocolServerBase server);
        bool UpdateServer(IEnumerable<ProtocolServerBase> servers);

        /// <summary>
        /// delete server by id, if id lower than 0 delete all data.
        /// </summary>
        /// <param name="id"></param>
        bool DeleteServer(int id);
        bool DeleteServer(IEnumerable<int> ids);

        string GetConfig(string key);

        void SetConfig(string key, string value);

        string GetProtocolTemplate(string key);

        void SetProtocolTemplate(string key, string value);
    }


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

        public static string GetFromDatabase_RSA_PrivateKeyPath(this IDb iDb)
        {
            return iDb.TryGetConfig("RSA_PrivateKeyPath");
        }

        public static void Set_RSA_PrivateKeyPath(this IDb iDb, string value)
        {
            iDb.TrySetConfig("RSA_PrivateKeyPath", value);
        }
    }
}