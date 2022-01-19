using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using Dapper;
using PRM.Core.I;
using PRM.Core.Protocol;

namespace PRM.Core.DB.Dapper
{
    /// <summary>
    /// DapperDb no occupation version
    /// </summary>
    public sealed class DapperDbFree : DapperDb
    {
        public override void InitTables()
        {
            OpenConnection();
            base.InitTables();
            CloseConnection();
        }

        public override ProtocolServerBase GetServer(int id)
        {
            OpenConnection();
            var ret = base.GetServer(id);
            CloseConnection();
            return ret;
        }

        public override List<ProtocolServerBase> GetServers()
        {
            OpenConnection();
            var ret = base.GetServers();
            CloseConnection();
            return ret;
        }

        public override int AddServer(ProtocolServerBase server)
        {
            OpenConnection();
            var ret = base.AddServer(server);
            CloseConnection();
            return ret;
        }

        public override bool UpdateServer(ProtocolServerBase server)
        {
            OpenConnection();
            var ret = base.UpdateServer(server);
            CloseConnection();
            return ret;
        }

        public override bool DeleteServer(int id)
        {
            OpenConnection();
            var ret = base.DeleteServer(id);
            CloseConnection();
            return ret;
        }

        public override string GetConfig(string key)
        {
            OpenConnection();
            var ret = base.GetConfig(key);
            CloseConnection();
            return ret;
        }

        public override void SetConfig(string key, string value)
        {
            OpenConnection();
            base.SetConfig(key, value);
            CloseConnection();
        }

        public override string GetProtocolTemplate(string key)
        {
            OpenConnection();
            var ret = base.GetProtocolTemplate(key);
            CloseConnection();
            return ret;
        }

        public override void SetProtocolTemplate(string key, string value)
        {
            OpenConnection();
            base.SetProtocolTemplate(key, value);
            CloseConnection();
        }
    }
}