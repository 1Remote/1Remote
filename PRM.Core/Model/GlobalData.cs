using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using PRM.Core.DB;
using PRM.Core.Protocol;

namespace PRM.Core.Model
{
    public class GlobalData : NotifyPropertyChangedBase
    {
        #region singleton
        private static GlobalData uniqueInstance;
        private static readonly object InstanceLock = new object();

        public static GlobalData GetInstance()
        {
            lock (InstanceLock)
            {
                if (uniqueInstance == null)
                {
                    throw new NullReferenceException($"{nameof(GlobalData)} has not been inited!");
                }
            }
            return uniqueInstance;
        }
        public static GlobalData Instance => GetInstance();
        #endregion

        public static void Init()
        {
            lock (InstanceLock)
            {
                if (uniqueInstance == null)
                {
                    uniqueInstance = new GlobalData();
                }
            }
        }

        private GlobalData()
        {
        }

        #region Server Data

        public Action VmItemListDataChanged;
        private ObservableCollection<VmProtocolServer> _vmItemList = new ObservableCollection<VmProtocolServer>();
        public ObservableCollection<VmProtocolServer> VmItemList
        {
            get => _vmItemList;
            set
            {
                SetAndNotifyIfChanged(nameof(VmItemList), ref _vmItemList, value);
                VmItemListDataChanged?.Invoke();
            }
        }


        public void ServerListUpdate(ProtocolServerBase protocolServer = null)
        {
            // read from db
            if (protocolServer == null)
            {
                var tmp = new ObservableCollection<VmProtocolServer>();
                foreach (var serverAbstract in PRM.Core.DB.Server.ListAllProtocolServerBase())
                {
                    tmp.Add(new VmProtocolServer(serverAbstract));
                }
                VmItemList = tmp;
            }
            // edit
            else if (protocolServer.Id > 0 && VmItemList.First(x => x.Server.Id == protocolServer.Id) != null)
            {
                VmItemList.First(x => x.Server.Id == protocolServer.Id).Server.Update(protocolServer);
                Server.AddOrUpdate(protocolServer);
            }
            // add
            else
            {
                Server.AddOrUpdate(protocolServer, true);
                ServerListUpdate();
            }
        }

        public void ServerListRemove(ProtocolServerBase server)
        {
            Debug.Assert(server.Id > 0);
            if (Server.Delete(server.Id))
            {
                ServerListUpdate();
            }
        }
        #endregion
    }
}
