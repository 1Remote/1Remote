using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.View;
using Shawn.Utils;
using Stylet;

namespace _1RM.Model
{
    public class GlobalData : NotifyPropertyChangedBase
    {
        public GlobalData(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            ConnectTimeRecorder.Init(AppPathHelper.Instance.ConnectTimeRecord);
            ReloadServerList();
        }

        private IDataService? _localDataService;
        private readonly ConfigurationService _configurationService;

        public void SetDbOperator(IDataSource dataService)
        {
            _localDataService = dataService;
        }


        private ObservableCollection<Tag> _tagList = new ObservableCollection<Tag>();
        public ObservableCollection<Tag> TagList
        {
            get => _tagList;
            private set => SetAndNotifyIfChanged(ref _tagList, value);
        }



        #region Server Data

        public Action? VmItemListDataChanged;

        public List<ProtocolBaseViewModel> VmItemList { get; set; } = new List<ProtocolBaseViewModel>();


        private void ReadTagsFromServers()
        {
            var pinnedTags = _configurationService.PinnedTags;

            // get distinct tag from servers
            var tags = new List<Tag>();
            foreach (var tagNames in VmItemList.Select(x => x.Server.Tags))
            {
                foreach (var tagName in tagNames)
                {
                    if (tags.All(x => x.Name != tagName))
                        tags.Add(new Tag(tagName, pinnedTags.Contains(tagName), SaveOnPinnedChanged) { ItemsCount = 1 });
                    else
                        tags.First(x => x.Name == tagName).ItemsCount++;
                }
            }

            TagList = new ObservableCollection<Tag>(tags.OrderBy(x => x.Name));
        }

        private void SaveOnPinnedChanged()
        {
            _configurationService.PinnedTags = TagList.Where(x => x.IsPinned).Select(x => x.Name).ToList();
            _configurationService.Save();
        }

        public void ReloadServerList()
        {
            if (_localDataService == null)
            {
                return;
            }
            // read from db
            var tmp = new List<ProtocolBaseViewModel>();
            var dbServers = _localDataService.Database_GetServers();
            foreach (var server in dbServers)
            {
                var serverAbstract = server;
                try
                {
                    Execute.OnUIThread(() =>
                    {
                        _localDataService.DecryptToRamLevel(ref serverAbstract);
                        var vm = new ProtocolBaseViewModel(serverAbstract, _localDataService)
                        {
                            LastConnectTime = ConnectTimeRecorder.Get(serverAbstract.Id)
                        };
                        tmp.Add(vm);
                    });
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Info(e);
                }
            }

            VmItemList = tmp;
            ConnectTimeRecorder.Cleanup();
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

        public void AddServer(ProtocolBase protocolServer, bool doInvokeReload = true)
        {
            if (_localDataService == null) return;
            _localDataService.Database_InsertServer(protocolServer);
            if (doInvokeReload)
            {
                ReloadServerList();
            }
        }

        public void AddServer(IEnumerable<ProtocolBase> protocolServers, bool doInvokeReload = true)
        {
            if (_localDataService == null)
            {
                return;
            }

            _localDataService.Database_InsertServer(protocolServers);
            if (doInvokeReload)
            {
                ReloadServerList();
            }
        }

        public void UpdateServer(ProtocolBase protocolServer, bool doInvokeReload = true)
        {
            if (_localDataService == null) return;
            Debug.Assert(string.IsNullOrEmpty(protocolServer.Id) == false);
            UnselectAllServers();
            _localDataService.Database_UpdateServer(protocolServer);
            int i = VmItemList.Count;
            if (VmItemList.Any(x => x.Id == protocolServer.Id))
            {
                var old = VmItemList.First(x => x.Id == protocolServer.Id);
                if (old.Server != protocolServer)
                {
                    i = VmItemList.IndexOf(old);
                    VmItemList.Remove(old);
                    VmItemList.Insert(i, new ProtocolBaseViewModel(protocolServer));
                }
            }

            if (doInvokeReload)
            {
                ReadTagsFromServers();
                VmItemListDataChanged?.Invoke();
            }
        }

        public void UpdateServer(IEnumerable<ProtocolBase> protocolServers, bool doInvokeReload = true)
        {
            if (_localDataService == null)
            {
                return;
            }

            if (_localDataService.Database_UpdateServer(protocolServers))
                if (doInvokeReload)
                    ReloadServerList();
        }

        public void DeleteServer(string id, bool doInvokeReload = true)
        {
            if (_localDataService == null)
            {
                return;
            }
            if (_localDataService.Database_DeleteServer(id))
            {
                if (doInvokeReload)
                    ReloadServerList();
            }
        }

        public void DeleteServer(IEnumerable<string> ids, bool doInvokeReload = true)
        {
            if (_localDataService == null)
            {
                return;
            }

            if (_localDataService.Database_DeleteServer(ids))
            {
                if (doInvokeReload)
                    ReloadServerList();
            }
        }

        #endregion Server Data
    }
}