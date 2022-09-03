using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Timers;
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
        private readonly Timer _timer;
        public GlobalData(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            ConnectTimeRecorder.Init(AppPathHelper.Instance.ConnectTimeRecord);
            ReloadServerList();

            _timer = new Timer(5 * 1000)
            {
                AutoReset = false,
            };
            _timer.Elapsed += TimerOnElapsed;
            _timer.Start();
        }

        private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            ReloadServerList();
            _timer.Start();
        }

        private DataSourceService? _sourceService;
        private readonly ConfigurationService _configurationService;

        public void SetDbOperator(DataSourceService sourceService)
        {
            _sourceService = sourceService;
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
            if (_sourceService?.LocalDataSource == null)
            {
                return;
            }

            if (_sourceService.NeedRead())
            {
                // read from db
                VmItemList = _sourceService.GetServers();
                ConnectTimeRecorder.Cleanup();
                ReadTagsFromServers();
                VmItemListDataChanged?.Invoke();
            }
        }

        public void UnselectAllServers()
        {
            foreach (var item in VmItemList)
            {
                item.IsSelected = false;
            }
        }

        public void AddServer(ProtocolBase protocolServer)
        {
            // TODO Assign data source
            if (_sourceService?.LocalDataSource == null) return;
            _sourceService.LocalDataSource.Database_InsertServer(protocolServer);
            ReloadServerList();
        }

        public void UpdateServer(ProtocolBase protocolServer)
        {
            Debug.Assert(string.IsNullOrEmpty(protocolServer.Id) == false);
            if (_sourceService == null) return;
            var source = _sourceService.GetDataSource(protocolServer.DataSourceId);
            if (source == null || source.IsWritable == false) return;
            UnselectAllServers();
            source.Database_UpdateServer(protocolServer);
            int i = VmItemList.Count;
            {
                var old = VmItemList.FirstOrDefault(x => x.Id == protocolServer.Id && x.Server.DataSourceId == source.DataSourceId);
                if (old != null
                    && old.Server != protocolServer)
                {
                    i = VmItemList.IndexOf(old);
                    VmItemList.Remove(old);
                    VmItemList.Insert(i, new ProtocolBaseViewModel(protocolServer, source));
                }
            }

            ReloadServerList();
            //{
            //    ReadTagsFromServers();
            //    VmItemListDataChanged?.Invoke();
            //}
        }

        public void UpdateServer(IEnumerable<ProtocolBase> protocolServers)
        {
            if (_sourceService == null) return;
            var groupedServers = protocolServers.GroupBy(x => x.DataSourceId);
            foreach (var groupedServer in groupedServers)
            {
                var source = _sourceService.GetDataSource(groupedServer.First().DataSourceId);
                if (source?.IsWritable == true)
                    source.Database_UpdateServer(groupedServer);
            }
            ReloadServerList();
        }

        public void DeleteServer(ProtocolBase protocolServer)
        {
            if (_sourceService == null) return;
            Debug.Assert(string.IsNullOrEmpty(protocolServer.Id) == false);
            if (_sourceService == null) return;
            var source = _sourceService.GetDataSource(protocolServer.DataSourceId);
            if (source == null || source.IsWritable == false) return;
            if (source.Database_DeleteServer(protocolServer.Id))
            {
                ReloadServerList();
            }
        }

        public void DeleteServer(IEnumerable<ProtocolBase> protocolServers)
        {
            if (_sourceService == null) return;
            var groupedServers = protocolServers.GroupBy(x => x.DataSourceId);
            foreach (var groupedServer in groupedServers)
            {
                var source = _sourceService.GetDataSource(groupedServer.First().DataSourceId);
                if (source?.IsWritable == true)
                    source.Database_DeleteServer(groupedServer.Select(x => x.Id));
            }
            ReloadServerList();
        }

        #endregion Server Data
    }
}