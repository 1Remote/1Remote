using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;

namespace _1RM.Model.DAO.Dapper
{
    /// <summary>
    /// DapperDb no occupation version
    /// </summary>
    public sealed class DapperDataBaseFree : DapperDataBase
    {
        /// <inheritdoc />
        public override void InitTables()
        {
            OpenConnection();
            base.InitTables();
            CloseConnection();
        }

        /// <inheritdoc />
        public override ProtocolBase? GetServer(int id)
        {
            OpenConnection();
            var ret = base.GetServer(id);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override List<ProtocolBase>? GetServers()
        {
            OpenConnection();
            var ret = base.GetServers();
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override string AddServer(ProtocolBase protocolBase)
        {
            OpenConnection();
            var ret = base.AddServer(protocolBase);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override int AddServer(IEnumerable<ProtocolBase> protocolBases)
        {
            OpenConnection();
            var ret = base.AddServer(protocolBases);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override bool UpdateServer(ProtocolBase server)
        {
            OpenConnection();
            var ret = base.UpdateServer(server);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override bool UpdateServer(IEnumerable<ProtocolBase> servers)
        {
            OpenConnection();
            var ret = base.UpdateServer(servers);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override bool DeleteServer(string id)
        {
            OpenConnection();
            var ret = base.DeleteServer(id);
            CloseConnection();
            return ret;
        }


        /// <inheritdoc />
        public override bool DeleteServer(IEnumerable<string> ids)
        {
            OpenConnection();
            var ret = base.DeleteServer(ids);
            CloseConnection();
            return ret;
        }


        /// <inheritdoc />
        public override string? GetConfig(string key)
        {
            OpenConnection();
            var ret = base.GetConfig(key);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override bool SetConfig(string key, string value)
        {
            OpenConnection();
            var ret = base.SetConfig(key, value);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override bool SetConfigRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers)
        {
            OpenConnection();
            var ret = base.SetConfigRsa(privateKeyPath, publicKey, servers);
            CloseConnection();
            return ret;
        }

        public override long DataUpdateTimestamp
        {
            get
            {
                OpenConnection();
                var ret = base.DataUpdateTimestamp;
                CloseConnection();
                return ret;
            }
            set
            {
                OpenConnection();
                base.DataUpdateTimestamp = value;
                CloseConnection();
            }
        }

    }
}