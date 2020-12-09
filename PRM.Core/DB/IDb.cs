using System;
using System.Collections.Generic;
using PRM.Core.Protocol;

namespace PRM.Core.DB
{
    public interface IDb
    {
        /// <summary>
        /// create tables
        /// </summary>
        void InitTables();

        ProtocolServerBase GetServer(uint id);
        List<ProtocolServerBase> GetServers();
        /// <summary>
        /// insert and return id
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        uint AddServer(ProtocolServerBase server);
        /// <summary>
        /// update server by id
        /// </summary>
        void UpdateServer(ProtocolServerBase server);
        /// <summary>
        /// delete server by id, if id lower than 0 delete all data.
        /// </summary>
        /// <param name="id"></param>
        void DeleteServer(uint id);

        //string GetValueByKeyFromKeyValueTable(string tableName, string key);
        //void SetValueByKeyFromKeyValueTable(string tableName, string key, string value);

        string GetConfig(string key);
        string SetConfig(string key, string value);
        string GetProtocolTemplate(string key);
        string SetProtocolTemplate(string key, string value);
    }

    public static class IdbHelper
    {
        public static void SetDbUpdateTimeMark(this IDb db)
        {
            db.SetConfig("DbUpdateTimeMark", DateTime.Now.ToFileTimeUtc().ToString());
        }
        public static DateTime GetDbUpdateTimestamp(this IDb db)
        {
            var str = db.GetConfig("DbUpdateTimeMark");
            if (long.TryParse(str, out var fileTime))
            {
                return DateTime.FromFileTime(fileTime);
            }
            return DateTime.MinValue;
        }
    }
}