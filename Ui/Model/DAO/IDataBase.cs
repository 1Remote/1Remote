using System;
using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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

    public abstract class IDatabase
    {
        public abstract void CloseConnection();
        public abstract void OpenConnection();
        public abstract void OpenNewConnection(DatabaseType type, string newConnectionString);

        public abstract bool IsConnected();

        /// <summary>
        /// create tables
        /// </summary>
        public abstract void InitTables();

        public abstract ProtocolBase? GetServer(int id);

        public abstract List<ProtocolBase>? GetServers();

        public abstract int GetServerCount();

        /// <summary>
        /// insert and return id
        /// </summary>
        /// <param name="protocolBase"></param>
        /// <returns></returns>
        public abstract string AddServer(ProtocolBase protocolBase);
        /// <summary>
        /// insert and return count
        /// </summary>
        /// <returns></returns>
        public abstract int AddServer(IEnumerable<ProtocolBase> protocolBases);

        /// <summary>
        /// update server by id
        /// </summary>
        public abstract bool UpdateServer(ProtocolBase server);
        public abstract bool UpdateServer(IEnumerable<ProtocolBase> servers);

        /// <summary>
        /// delete server by id, if id lower than 0 delete all data.
        /// </summary>
        /// <param name="id"></param>
        public abstract bool DeleteServer(string id);
        public abstract bool DeleteServer(IEnumerable<string> ids);

        public abstract string? GetConfig(string key);

        public abstract bool SetConfig(string key, string value);

        /// <summary>
        /// set rsa encryption and encrypt or decrypt the data.
        /// </summary>
        /// <param name="privateKeyPath"></param>
        /// <param name="publicKey"></param>
        /// <param name="servers">已加密或已解密的数据</param>
        public abstract bool SetConfigRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers);

        public abstract void SetDataUpdateTimestamp(long time = -1);
        public abstract long GetDataUpdateTimestamp();

        protected void SetEncryptionTest()
        {
            if (string.IsNullOrEmpty(GetConfig("EncryptionTest")))
            {
                SetConfig("EncryptionTest", UnSafeStringEncipher.SimpleEncrypt("EncryptionTest"));
            }
        }

        public bool CheckEncryptionTest()
        {
            var et = GetConfig("EncryptionTest");
            if (UnSafeStringEncipher.SimpleDecrypt(et ?? "") != "EncryptionTest")
            {
                return false;
            }
            return true;
        }
    }


    public static class DbExtensions
    {
        public static string GetSqliteConnectionString(string dbPath)
        {
            return $"Data Source={dbPath}; Pooling=true;Min Pool Size=1";
        }
        public static string GetMysqlConnectionString(string host, int port, string dbName, string user, string password, int connectTimeOutSeconds)
        {
            return $"server={host};port={port};database={dbName};Character Set=utf8;Uid={user};password={password};Connect Timeout={connectTimeOutSeconds};";
        }



        private static string? TryGetConfig(this IDatabase iDatabase, string key)
        {
            try
            {
                var val = iDatabase.GetConfig(key) ?? "";
                return val;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return null;
            }
        }

        private static bool TrySetConfig(this IDatabase iDatabase, string key, string value)
        {
            try
            {
                iDatabase.SetConfig(key, value ?? "");
                return true;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return false;
            }
        }

        public static bool CheckWritable(this IDatabase iDatabase)
        {
            try
            {
                iDatabase.SetConfig("permission_check", "true"); // insert
                iDatabase.SetConfig("permission_check", "true"); // update
                return true;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Info(e);
                return false;
            }
        }

        public static bool CheckReadable(this IDatabase iDatabase)
        {
            try
            {
                iDatabase.SetConfig("permission_check", "true"); // update
            }
            catch
            {
                // ignored
            }

            try
            {
                var val = iDatabase.GetConfig("permission_check");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}