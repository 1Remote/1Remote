using System.Collections.Generic;
using PRM.Core.Protocol;

namespace PRM.Core.DB.IDB
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

        void OpenConnection(DatabaseType? type = null, string newConnectionString = "");

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
        /// update server by id
        /// </summary>
        bool UpdateServer(ProtocolServerBase server);

        /// <summary>
        /// delete server by id, if id lower than 0 delete all data.
        /// </summary>
        /// <param name="id"></param>
        bool DeleteServer(int id);

        string GetConfig(string key);

        void SetConfig(string key, string value);

        string GetProtocolTemplate(string key);

        void SetProtocolTemplate(string key, string value);
    }

    //public static class IdbHelper
    //{
    //    public static void SetDbUpdateTimeMark(this IDb db)
    //    {
    //        db.SetConfig("DbUpdateTimeMark", DateTime.Now.ToFileTimeUtc().ToString());
    //    }
    //    public static DateTime GetDbUpdateTimestamp(this IDb db)
    //    {
    //        var str = db.GetConfig("DbUpdateTimeMark");
    //        if (long.TryParse(str, out var fileTime))
    //        {
    //            return DateTime.FromFileTime(fileTime);
    //        }
    //        return DateTime.MinValue;
    //    }
    //}
}