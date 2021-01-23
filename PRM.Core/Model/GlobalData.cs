using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using PRM.Core.DB;
using PRM.Core.Protocol;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public class GlobalData : NotifyPropertyChangedBase
    {
        public GlobalData()
        {
        }


        public Action<string> OnMainWindowServerFilterChanged;

        private string _mainWindowServerFilter = "";
        public string MainWindowServerFilter
        {
            get => _mainWindowServerFilter;
            set
            {
                if (value != _mainWindowServerFilter)
                {
                    SetAndNotifyIfChanged(nameof(MainWindowServerFilter), ref _mainWindowServerFilter, value);
                    OnMainWindowServerFilterChanged?.Invoke(value);
                }
            }
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
                foreach (var serverAbstract in SystemConfig.Instance.DataSecurity.GetServers())
                {
                    try
                    {
                        SystemConfig.Instance.DataSecurity.DecryptInfo(serverAbstract);
                        tmp.Add(new VmProtocolServer(serverAbstract));
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Info(e);
                    }
                }
                VmItemList = tmp;
            }
            // edit
            else if (protocolServer.Id > 0 && VmItemList.First(x => x.Server.Id == protocolServer.Id) != null)
            {
                SystemConfig.Instance.DataSecurity.DbUpdateServer(protocolServer);
                VmItemList.Remove(VmItemList.First(x => x.Server.Id == protocolServer.Id));
                VmItemList.Add(new VmProtocolServer(protocolServer));
                VmItemListDataChanged?.Invoke();
            }
            // add
            else
            {
                SystemConfig.Instance.DataSecurity.DbAddServer(protocolServer);
                ServerListUpdate();
            }
        }

        public void ServerListRemove(int id)
        {
            Debug.Assert(id > 0);
            if (SystemConfig.Instance.DataSecurity.DbDeleteServer(id))
            {
                ServerListUpdate();
            }
        }
        #endregion
    }
}
