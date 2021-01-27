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
        private DbOperator _dbOperator;

        public void SetDbOperator(DbOperator dbOperator)
        {
            _dbOperator = dbOperator;
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
            if (_dbOperator == null)
            {
                return;
            }
            // read from db
            if (protocolServer == null)
            {
                var tmp = new ObservableCollection<VmProtocolServer>();
                foreach (var serverAbstract in _dbOperator.GetServers())
                {
                    try
                    {
                        _dbOperator.DecryptInfo(serverAbstract);
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
                _dbOperator.DbUpdateServer(protocolServer);
                VmItemList.Remove(VmItemList.First(x => x.Server.Id == protocolServer.Id));
                VmItemList.Add(new VmProtocolServer(protocolServer));
                VmItemListDataChanged?.Invoke();
            }
            // add
            else
            {
                _dbOperator.DbAddServer(protocolServer);
                ServerListUpdate();
            }
        }

        public void ServerListRemove(int id)
        {
            if (_dbOperator == null)
            {
                return;
            }
            Debug.Assert(id > 0);
            if (_dbOperator.DbDeleteServer(id))
            {
                ServerListUpdate();
            }
        }
        #endregion
    }
}
