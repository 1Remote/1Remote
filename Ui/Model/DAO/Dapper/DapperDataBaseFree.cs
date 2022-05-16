using System.Collections.Generic;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.Model.DAO.Dapper
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
        public override int AddServer(ProtocolBase server)
        {
            OpenConnection();
            var ret = base.AddServer(server);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override int AddServer(IEnumerable<ProtocolBase> servers)
        {
            OpenConnection();
            var ret = base.AddServer(servers);
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
        public override bool DeleteServer(int id)
        {
            OpenConnection();
            var ret = base.DeleteServer(id);
            CloseConnection();
            return ret;
        }


        /// <inheritdoc />
        public override bool DeleteServer(IEnumerable<int> ids)
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
        public override void SetConfig(string key, string value)
        {
            OpenConnection();
            base.SetConfig(key, value);
            CloseConnection();
        }

        /// <inheritdoc />
        public override string GetProtocolTemplate(string key)
        {
            OpenConnection();
            var ret = base.GetProtocolTemplate(key);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override void SetProtocolTemplate(string key, string value)
        {
            OpenConnection();
            base.SetProtocolTemplate(key, value);
            CloseConnection();
        }

        /// <inheritdoc />
        public override bool SetRsa(string privateKeyPath, string publicKey, IEnumerable<ProtocolBase> servers)
        {
            OpenConnection();
            var ret = base.SetRsa(privateKeyPath, publicKey, servers);
            CloseConnection();
            return ret;
        }

        /// <inheritdoc />
        public override void SetRsaPrivateKeyPath(string privateKeyPath)
        {
            OpenConnection();
            SetRsaPrivateKeyPath(privateKeyPath);
            CloseConnection();
        }
    }
}