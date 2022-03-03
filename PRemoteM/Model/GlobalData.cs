using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using PRM.I;
using PRM.Model.Protocol.Base;
using PRM.Service;
using PRM.View;
using Shawn.Utils;

namespace PRM.Model
{
    public class GlobalData : NotifyPropertyChangedBase
    {
        public GlobalData(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        private IDataService _dataService;
        private readonly ConfigurationService _configurationService;

        public void SetDbOperator(IDataService dataService)
        {
            _dataService = dataService;
        }


        public bool TagListDoInvokeSelectedTabName = true;
        private ObservableCollection<Tag> _tagList = new ObservableCollection<Tag>();
        public ObservableCollection<Tag> TagList
        {
            get => _tagList;
            private set => SetAndNotifyIfChanged(ref _tagList, value);
        }



        #region Server Data

        public Action VmItemListDataChanged;

        public List<VmProtocolServer> VmItemList { get; set; } = new List<VmProtocolServer>();


        private void ReadTagsFromServers()
        {
            var pinnedTags = _configurationService.PinnedTags;
            // set pinned
            // TODO del after 2022.05.31
            if (pinnedTags.Count == 0)
            {
                var allExistedTags = Tag.GetPinnedTags();
                pinnedTags = allExistedTags.Where(x => x.Value == true).Select(x => x.Key).ToList();
            }

            // get distinct tag from servers
            var tags = new List<Tag>();
            foreach (var tagNames in VmItemList.Select(x => x.Server.Tags))
            {
                if (tagNames == null)
                    continue;
                foreach (var tagName in tagNames)
                {
                    if (tags.All(x => x.Name != tagName))
                        tags.Add(new Tag(tagName, pinnedTags.Contains(tagName), SaveOnPinnedChanged) { ItemsCount = 1 });
                    else
                        tags.First(x => x.Name == tagName).ItemsCount++;
                }
            }

            TagListDoInvokeSelectedTabName = false;
            TagList = new ObservableCollection<Tag>(tags.OrderBy(x => x.Name));
            TagListDoInvokeSelectedTabName = true;
        }

        private void SaveOnPinnedChanged()
        {
            _configurationService.PinnedTags = TagList.Where(x => x.IsPinned).Select(x => x.Name).ToList();
            _configurationService.Save();
        }

        public void ReloadServerList()
        {
            if (_dataService == null)
            {
                return;
            }
            // read from db
            var tmp = new List<VmProtocolServer>();
            foreach (var server in _dataService.Database_GetServers())
            {
                var serverAbstract = server;
                try
                {
                    _dataService.DecryptToRamLevel(ref serverAbstract);
                    tmp.Add(new VmProtocolServer(serverAbstract));
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Info(e);
                }
            }

            VmItemList = tmp;
            ReadTagsFromServers();
            VmItemListDataChanged?.Invoke();
        }

        public void UnselectAllServers()
        {
            foreach (var item in VmItemList)
            {
                item.IsSelected = false;
            }
        }

        public void AddServer(ProtocolServerBase protocolServer, bool doInvoke = true)
        {
            _dataService.Database_InsertServer(protocolServer);
            if (doInvoke)
            {
                ReloadServerList();
            }
        }

        public void AddServer(IEnumerable<ProtocolServerBase> protocolServers, bool doInvoke = true)
        {
            if (_dataService == null || protocolServers == null || !protocolServers.Any())
            {
                return;
            }

            _dataService.Database_InsertServer(protocolServers);
            if (doInvoke)
            {
                ReloadServerList();
            }
        }

        public void UpdateServer(ProtocolServerBase protocolServer, bool doInvoke = true)
        {
            Debug.Assert(protocolServer.Id > 0);
            UnselectAllServers();
            _dataService.Database_UpdateServer(protocolServer);
            int i = VmItemList.Count;
            if (VmItemList.Any(x => x.Server.Id == protocolServer.Id))
            {
                var old = VmItemList.First(x => x.Server.Id == protocolServer.Id);
                if (old.Server != protocolServer)
                {
                    i = VmItemList.IndexOf(old);
                    VmItemList.Remove(old);
                    VmItemList.Insert(i, new VmProtocolServer(protocolServer));
                }
            }

            if (doInvoke)
            {
                ReadTagsFromServers();
                VmItemListDataChanged?.Invoke();
            }
        }

        public void UpdateServer(IEnumerable<ProtocolServerBase> protocolServers, bool doInvoke = true)
        {
            if (_dataService == null || protocolServers == null || !protocolServers.Any())
            {
                return;
            }

            if (_dataService.Database_UpdateServer(protocolServers))
                if (doInvoke)
                    ReloadServerList();
        }

        public void DeleteServer(int id, bool doInvoke = true)
        {
            if (_dataService == null)
            {
                return;
            }
            Debug.Assert(id > 0);
            if (_dataService.Database_DeleteServer(id))
            {
                if (doInvoke)
                    ReloadServerList();
            }
        }

        public void DeleteServer(IEnumerable<int> ids, bool doInvoke = true)
        {
            if (_dataService == null || ids == null || !ids.Any())
            {
                return;
            }

            if (_dataService.Database_DeleteServer(ids))
            {
                if (doInvoke)
                    ReloadServerList();
            }
        }

        #endregion Server Data
    }
}