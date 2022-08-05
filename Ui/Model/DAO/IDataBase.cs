using System;
using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Shawn.Utils;

namespace _1RM.Model.DAO
{
    public enum DatabaseType
    {
        MySql,
        SqlServer,
        // ReSharper disable once IdentifierTypo
        // ReSharper disable once InconsistentNaming
        PostgreSQL,
        Oracle,
        // ReSharper disable once IdentifierTypo
        Sqlite,
    }

    public interface IDataBase
    {
        void CloseConnection();

        void OpenConnection();
        void OpenConnection(DatabaseType type, string newConnectionString);

        bool IsConnected();

        /// <summary>
        /// create tables
        /// </summary>
        void InitTables();

        ProtocolBase? GetServer(int id);

        List<ProtocolBase>? GetServers();

        /// <summary>
        /// insert and return id
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        int AddServer(ProtocolBase server);
        /// <summary>
        /// insert and return count
        /// </summary>
        /// <returns></returns>
        int AddServer(IEnumerable<ProtocolBase> servers);

        /// <summary>
        /// update server by id
        /// </summary>
        bool UpdateServer(ProtocolBase server);
        bool UpdateServer(IEnumerable<ProtocolBase> servers);

        /// <summary>
        /// delete server by id, if id lower than 0 delete all data.
        /// </summary>
        /// <param name="id"></param>
        bool DeleteServer(int id);
        bool DeleteServer(IEnumerable<int> ids);

        string? GetConfig(string key);

        void SetConfig(string key, string value);

        string GetProtocolTemplate(string key);

        void SetProtocolTemplate(string key, string value);

        /// <summary>
        /// set rsa encryption and encrypt or decrypt the data.
        /// </summary>
        /// <param name="privateKeyPath"></param>
        /// <param name="publicKey"></param>
        /// <param name="servers">已加密或已解密的数据</param>
        public bool SetRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers);

        public void SetRsaPrivateKeyPath(string privateKeyPath);
    }


    public static class DbExtensions
    {
        public static string GetSqliteConnectionString(string dbPath)
        {
            return $"Data Source={dbPath}; Pooling=true;Min Pool Size=1";
        }

        private static string TryGetConfig(this IDataBase iDataBase, string key)
        {
            try
            {
                var val = iDataBase.GetConfig(key);
                if (val == null)
                {
                    val = "";
                    iDataBase.SetConfig(key, val);
                }
                return val;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return "";
            }
        }

        private static void TrySetConfig(this IDataBase iDataBase, string key, string value)
        {
            try
            {
                iDataBase.SetConfig(key, value ?? "");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        public static string Get_RSA_SHA1(this IDataBase iDataBase)
        {
            return iDataBase.TryGetConfig("RSA_SHA1");
        }

        public static void Set_RSA_SHA1(this IDataBase iDataBase, string value)
        {
            iDataBase.TrySetConfig("RSA_SHA1", value);
        }

        public static string Get_RSA_PublicKey(this IDataBase iDataBase)
        {
            return iDataBase.TryGetConfig("RSA_PublicKey");
        }

        public static void Set_RSA_PublicKey(this IDataBase iDataBase, string value)
        {
            iDataBase.TrySetConfig("RSA_PublicKey", value);
        }

        public static string GetFromDatabase_RSA_PrivateKeyPath(this IDataBase iDataBase)
        {
            return iDataBase.TryGetConfig("RSA_PrivateKeyPath");
        }

        public static void Set_RSA_PrivateKeyPath(this IDataBase iDataBase, string value)
        {
            iDataBase.TrySetConfig("RSA_PrivateKeyPath", value);
        }
    }
}