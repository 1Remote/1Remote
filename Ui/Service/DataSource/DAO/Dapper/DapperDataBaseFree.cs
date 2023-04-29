﻿using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Dapper;

namespace _1RM.Service.DataSource.DAO.Dapper
{
    /// <summary>
    /// DapperDb no occupation version
    /// </summary>
    public sealed class DapperDatabaseFree : DapperDatabase
    {
        public DapperDatabaseFree(string databaseName, DatabaseType databaseType) : base(databaseName, databaseType)
        {
        }

        /// <inheritdoc />
        public override Result InitTables()
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.InitTables();
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override ResultSelects GetServers()
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.GetServers();
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override Result AddServer(ref ProtocolBase protocolBase)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.AddServer(ref protocolBase);
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override Result AddServer(IEnumerable<ProtocolBase> protocolBases)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.AddServer(protocolBases);
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override Result UpdateServer(ProtocolBase server)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.UpdateServer(server);
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override Result UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.UpdateServer(servers);
                CloseConnection();
                return ret;
            }
        }



        /// <inheritdoc />
        public override Result DeleteServer(IEnumerable<string> ids)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.DeleteServer(ids);
                CloseConnection();
                return ret;
            }
        }


        /// <inheritdoc />
        public override ResultString GetConfig(string key)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.GetConfig(key);
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override Result SetConfig(string key, string? value)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.SetConfig(key, value);
                CloseConnection();
                return ret;
            }
        }


        public override Result SetDataUpdateTimestamp(long time = -1)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.SetDataUpdateTimestamp(time);
                CloseConnection();
                return ret;
            }
        }

        public override ResultLong GetDataUpdateTimestamp()
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.GetDataUpdateTimestamp();
                CloseConnection();
                return ret;
            }
        }
    }
}