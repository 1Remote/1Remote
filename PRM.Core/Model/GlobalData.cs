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

        public void ServerListUpdate(ProtocolServerBase protocolServer = null, bool doInvoke = true)
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
                ServerListClearSelect();
                _dbOperator.DbUpdateServer(protocolServer);
                int i = VmItemList.Count;
                if (VmItemList.Any(x => x.Server.Id == protocolServer.Id))
                {
                    var old = VmItemList.First(x => x.Server.Id == protocolServer.Id);
                    i = VmItemList.IndexOf(old);
                    VmItemList.Remove(old);
                }

                VmItemList.Insert(i, new VmProtocolServer(protocolServer));
                if (doInvoke)
                    VmItemListDataChanged?.Invoke();
            }
            // add
            else
            {
                _dbOperator.DbAddServer(protocolServer);
                ServerListUpdate(null, doInvoke);
            }
        }

        public void ServerListClearSelect()
        {
            foreach (var item in VmItemList)
            {
                item.IsSelected = false;
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

        #endregion Server Data
    }
}