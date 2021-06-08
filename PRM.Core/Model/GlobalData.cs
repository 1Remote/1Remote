using System;
using System.Collections.Generic;
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
                    SetAndNotifyIfChanged(ref _mainWindowServerFilter, value);
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
                SetAndNotifyIfChanged(ref _vmItemList, value);
                VmItemListDataChanged?.Invoke();
            }
        }

        private ObservableCollection<Tag> _tags = new ObservableCollection<Tag>();
        public ObservableCollection<Tag> Tags
        {
            get => _tags;
            set => SetAndNotifyIfChanged(ref _tags, value);
        }

        private string _selectedTagName = "";
        public string SelectedTagName
        {
            get => _selectedTagName;
            set
            {
                if (_selectedTagName == value) return;
                MainWindowServerFilter = "";
                SetAndNotifyIfChanged(nameof(SelectedTagName), ref _selectedTagName, value);
                SystemConfig.Instance.Locality.MainWindowTabSelected = value;
            }
        }

        private void UpdateTags()
        {
            var t = SelectedTagName;

            // set pinned
            var allExistedTags = Tag.GetPinnedTags();
            var pinnedTags = allExistedTags.Where(x => x.Value == true).Select(x => x.Key).ToList();

            // get distinct tag from servers
            var tags = new List<Tag>();
            foreach (var tagNames in VmItemList.Select(x => x.Server.Tags))
            {
                foreach (var tagName in tagNames)
                {
                    if (tags.All(x => x.Name != tagName))
                        tags.Add(new Tag(tagName, allExistedTags.ContainsKey(tagName) == false || pinnedTags.Contains(tagName) ? true : false) { ItemsCount = 1 });
                    else
                        tags.First(x => x.Name == tagName).ItemsCount++;
                }
            }

            Tags = new ObservableCollection<Tag>(tags.OrderBy(x => x.Name));
            Tag.UpdateTagsCache(tags);
            SelectedTagName = t;
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

            UpdateTags();
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