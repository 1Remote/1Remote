using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Dapper;

namespace _1RM.Model.DAO.Dapper
{
    /// <summary>
    /// DapperDb no occupation version
    /// </summary>
    public sealed class DapperDatabaseFree : DapperDatabase
    {
        /// <inheritdoc />
        public override void InitTables()
        {
            lock (this)
            {
                OpenConnection();
                base.InitTables();
                CloseConnection();
            }
        }

        /// <inheritdoc />
        public override ProtocolBase? GetServer(int id)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.GetServer(id);
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override List<ProtocolBase>? GetServers()
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.GetServers();
                CloseConnection();
                return ret;
            }
        }
        public override int GetServerCount()
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.GetServerCount();
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override string AddServer(ProtocolBase protocolBase)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.AddServer(protocolBase);
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override int AddServer(IEnumerable<ProtocolBase> protocolBases)
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
        public override bool UpdateServer(ProtocolBase server)
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
        public override bool UpdateServer(IEnumerable<ProtocolBase> servers)
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
        public override bool DeleteServer(string id)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.DeleteServer(id);
                CloseConnection();
                return ret;
            }
        }


        /// <inheritdoc />
        public override bool DeleteServer(IEnumerable<string> ids)
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
        public override string? GetConfig(string key)
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
        public override bool SetConfig(string key, string value)
        {
            lock (this)
            {
                OpenConnection();
                var ret = base.SetConfig(key, value);
                CloseConnection();
                return ret;
            }
        }

        /// <inheritdoc />
        public override bool SetConfigRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers)
        {
            OpenConnection();
            var ret = base.SetConfigRsa(privateKeyPath, publicKey, servers);
            CloseConnection();
            return ret;
        }


        public override void SetDataUpdateTimestamp(long time = -1)
        {
            lock (this)
            {
                OpenConnection();
                base.SetDataUpdateTimestamp(time);
                CloseConnection();
            }
        }

        public override long GetDataUpdateTimestamp()
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