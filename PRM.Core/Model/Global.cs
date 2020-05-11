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
            ReadServerDataFromDb();
            SystemConfig.GetInstance().General.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfig.DataSecurity.DbPath))
                    ReadServerDataFromDb();
            };
        }

        #region Server Data

        public ObservableCollection<ProtocolServerBase> ServerList { get; set; }= new ObservableCollection<ProtocolServerBase>();

        private void ReadServerDataFromDb()
        {
            //#if DEBUG
            //            // TODO 测试用删除数据库
            //            if (File.Exists(SystemConfig.GetInstance().DataSecurity.DbPath))
            //                File.Delete(SystemConfig.GetInstance().DataSecurity.DbPath);
            //            if (PRM_DAO.GetInstance().ListAllServer().Count == 0)
            //            {
            //                var di = new DirectoryInfo(@"D:\rdpjson");
            //                if (di.Exists)
            //                {
            //                    // read from jsonfile 
            //                    var fis = di.GetFiles("*.prmj", SearchOption.AllDirectories);
            //                    var rdp = new ProtocolServerRDP();
            //                    foreach (var fi in fis)
            //                    {
            //                        var newRdp = rdp.CreateFromJsonString(File.ReadAllText(fi.FullName));
            //                        if (newRdp != null)
            //                        {
            //                            PRM_DAO.GetInstance().Insert(ServerOrm.ConvertFrom(newRdp));
            //                        }
            //                    }
            //                }
            //                else
            //                {
            //                    di.Create();
            //                }
            //            }
            //#endif

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
                var server = Server.FromProtocolServerBase(protocolServer);
                if (ServerList.First(x => x.Id == protocolServer.Id) == null)
                    ServerList.First(x => x.Id == protocolServer.Id).OnCmdConn += OnCmdConn;
                server.Update();
            }
            // add
            else
            {
                var server = Server.FromProtocolServerBase(protocolServer);
                if (server.Insert() > 0)
                {
                    var newServer = ServerFactory.GetInstance().CreateFromDbObjectServerOrm(server);
                    newServer.OnCmdConn += OnCmdConn;
                    Global.GetInstance().ServerList.Add(newServer);
                }
            }
        }


        public void ServerListRemove(ProtocolServerBase server)
        {
            Debug.Assert(server.Id > 0);
            Server.FromProtocolServerBase(server).Delete();
            Global.GetInstance().ServerList.Remove(server);
        }

        #endregion
    }
}
