using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using PRM.Core.DB;
using PRM.Core.Protocol;

namespace PRM.Core.Model
{
    public class Global
    {
        #region singleton
        private static Global uniqueInstance;
        private static readonly object InstanceLock = new object();

        public static Global GetInstance()
        {
            lock (InstanceLock)
            {
                if (uniqueInstance == null)
                {
                    uniqueInstance = new Global();
                }
            }
            return uniqueInstance;
        }
        #endregion

        private Global()
        {
        }

        #region Server Data

        public ObservableCollection<ProtocolServerBase> ServerList { get; set; }= new ObservableCollection<ProtocolServerBase>();

        public void ReloadServers()
        {
            ServerList.Clear();
            foreach (var serverAbstract in PRM.Core.DB.Server.ListAllProtocolServerBase())
            {
                if (serverAbstract.OnCmdConn == null)
                    serverAbstract.OnCmdConn += OnCmdConn;
                ServerList.Add(serverAbstract);
            }
        }

        public Action<ProtocolServerBase> OnServerConn = null;
        private void OnCmdConn(uint id)
        {
            Debug.Assert(id > 0);
            Debug.Assert(ServerList.Any(x => x.Id == id));
            var server = ServerList.First(x => x.Id == id);
            //WindowPool.ShowRemoteHost(server);
            OnServerConn?.Invoke(server);
        }

        public void ServerListUpdate(ProtocolServerBase protocolServer)
        {
            // edit
            if (protocolServer.Id > 0 && ServerList.First(x => x.Id == protocolServer.Id) != null)
            {
                ServerList.First(x => x.Id == protocolServer.Id).Update(protocolServer);
                if (ServerList.First(x => x.Id == protocolServer.Id) == null)
                    ServerList.First(x => x.Id == protocolServer.Id).OnCmdConn += OnCmdConn;
                Server.AddOrUpdate(protocolServer);
            }
            // add
            else
            {
                var id = Server.AddOrUpdate(protocolServer, true);
                if (id > 0)
                {
                    protocolServer.OnCmdConn += OnCmdConn;
                    ReloadServers();
                }
            }
        }


        public void ServerListRemove(ProtocolServerBase server)
        {
            Debug.Assert(server.Id > 0);
            if (Server.Delete(server.Id))
            {
                ServerList.Remove(server);
                ReloadServers();
            }
        }

        #endregion
    }
}
