using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.View;
using _1RM.View.Launcher;
using _1RM.View.ServerList;
using Shawn.Utils;
using Stylet;
using ServerListPageViewModel = _1RM.View.ServerList.ServerListPageViewModel;

namespace _1RM.Model
{
    public class GlobalData : NotifyPropertyChangedBase
    {
        private readonly Timer _timer;
        private bool _timerStopFlag = false;
        public GlobalData(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            ConnectTimeRecorder.Init(AppPathHelper.Instance.ConnectTimeRecord);
            ReloadServerList();

            _timer = new Timer(_configurationService.DatabaseCheckPeriod * 1000)
            {
                AutoReset = false,
            };
            _timer.Elapsed += TimerOnElapsed;
            _timer.Start();
        }

        private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var mainWindowViewModel = IoC.Get<MainWindowViewModel>();
                var listPageViewModel = IoC.Get<ServerListPageViewModel>();
                var launcherWindowViewModel = IoC.Get<LauncherWindowViewModel>();
                // do not reload when any selected / launcher is shown / editor view is show
                if (mainWindowViewModel.EditorViewModel != null
                    || listPageViewModel.VmServerList.Any(x => x.IsSelected)
                    || launcherWindowViewModel.View?.IsVisible == true)
                {
                    return;
                }

                if (ReloadServerList())
                {
                    SimpleLogHelper.Debug("check database update - reload data");
                }
                else
                {
                    SimpleLogHelper.Debug("check database update - no need reload");
                }
            }
            finally
            {
                lock (this)
                {
                    if (_timerStopFlag == false && _configurationService.DatabaseCheckPeriod > 0)
                    {
                        _timer.Interval = _configurationService.DatabaseCheckPeriod * 1000;
                        _timer.Start();
                    }
                }
                System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
            }
        }

        private DataSourceService? _sourceService;
        private readonly ConfigurationService _configurationService;

        public void SetDataSourceService(DataSourceService sourceService)
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

        public Action? OnDataReloaded;

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

        public ProtocolBaseViewModel? GetItemById(string dataSourceName, string serverId)
        {
            return VmItemList.FirstOrDefault(x => x.Server.DataSourceName == dataSourceName
                                                  && x.Id == serverId);
        }


        /// <summary>
        /// reload data based on `LastReadFromDataSourceMillisecondsTimestamp` and `DataSourceDataUpdateTimestamp`
        /// return true if read data
        /// </summary>
        /// <param name="focus"></param>
        public bool ReloadServerList(bool focus = false)
        {
            if (_sourceService == null)
            {
                return false;
            }


            var needRead = false;
            if (focus == false)
            {
                needRead = _sourceService.LocalDataSource?.NeedRead() ?? false;
                foreach (var additionalSource in _sourceService.AdditionalSources)
                {
                    // 对于断线的数据源，隔一段时间后尝试重连
                    if (additionalSource.Value.Status == EnumDatabaseStatus.LostConnection)
                    {
                        if (additionalSource.Value.StatueTime.AddMinutes(10) < DateTime.Now)
                        {
                            additionalSource.Value.Database_SelfCheck();
                        }
                        continue;
                    }

                    if (needRead == false)
                    {
                        needRead |= additionalSource.Value.NeedRead();
                    }
                }
            }

            if (focus || needRead)
            {
                // read from db
                VmItemList = _sourceService.GetServers(focus);
                ConnectTimeRecorder.Cleanup();
                ReadTagsFromServers();
                OnDataReloaded?.Invoke();

                return true;
            }
            return false;
        }

        public bool AddServer(ProtocolBase protocolServer, DataSourceBase dataSource)
        {
            bool ret = false;
            StopTick();
            var needReload = dataSource.NeedRead();
            if (dataSource.Database_InsertServer(protocolServer))
            {
                ret = true;
                var @new = new ProtocolBaseViewModel(protocolServer);
                if (needReload == false)
                {
                    VmItemList.Add(@new);
                    IoC.Get<ServerListPageViewModel>()?.AppendServer(@new); // invoke main list ui change
                    IoC.Get<ServerSelectionsViewModel>()?.AppendServer(@new); // invoke launcher ui change
                }
            }

            if (needReload)
            {
                ReloadServerList();
            }
            else
            {
                ReadTagsFromServers();
                IoC.Get<ServerListPageViewModel>().ClearSelection();
            }
            StartTick();
            return ret;
        }

        public void UpdateServer(ProtocolBase protocolServer)
        {
            StopTick();
            Debug.Assert(protocolServer.IsTmpSession() == false);
            if (_sourceService == null) return;
            var source = protocolServer.GetDataSource();
            var needReload = source?.NeedRead() ?? false;
            if (source == null || source.IsWritable == false) return;
            source.Database_UpdateServer(protocolServer);


            if (needReload)
            {
                ReloadServerList();
            }
            else
            {
                // invoke main list ui change & invoke launcher ui change
                var old = GetItemById(source.DataSourceName, protocolServer.Id);
                if (old != null)
                    old.Server = protocolServer;
                ReadTagsFromServers();
                IoC.Get<ServerListPageViewModel>().ClearSelection();
            }

            StartTick();
        }

        public void UpdateServer(IEnumerable<ProtocolBase> protocolServers)
        {
            StopTick();
            if (_sourceService == null) return;
            var groupedServers = protocolServers.GroupBy(x => x.DataSourceName);
            bool needReload = false;
            foreach (var groupedServer in groupedServers)
            {
                var source = groupedServer.First().GetDataSource();
                if (source?.IsWritable == true)
                {
                    needReload |= source.NeedRead();
                    if (source.Database_UpdateServer(groupedServer)
                        && needReload == false)
                    {
                        // update viewmodel
                        foreach (var protocolServer in groupedServer)
                        {
                            var old = GetItemById(source.DataSourceName, protocolServer.Id);
                            // invoke main list ui change & invoke launcher ui change
                            if (old != null)
                                old.Server = protocolServer;
                        }
                    }
                }
            }

            if (needReload)
            {
                ReloadServerList();
            }
            else
            {
                ReadTagsFromServers();
                IoC.Get<ServerListPageViewModel>().ClearSelection();
            }
            StartTick();
        }

        public void DeleteServer(ProtocolBase protocolServer)
        {
            StopTick();
            Debug.Assert(protocolServer.IsTmpSession() == false);

            if (_sourceService == null) return;
            var source = protocolServer.GetDataSource();
            var needReload = source?.NeedRead() ?? false;
            if (source == null || source.IsWritable == false) return;
            if (source.Database_DeleteServer(protocolServer.Id))
            {
                if (needReload == false)
                {
                    var old = GetItemById(source.DataSourceName, protocolServer.Id);
                    if (old != null)
                    {
                        VmItemList.Remove(old);
                        IoC.Get<ServerListPageViewModel>()?.VmServerList?.Remove(old); // invoke main list ui change
                        IoC.Get<ServerSelectionsViewModel>()?.VmServerList?.Remove(old); // invoke launcher ui change
                    }
                }
            }

            if (needReload)
            {
                ReloadServerList();
            }
            else
            {
                ReadTagsFromServers();
                IoC.Get<ServerListPageViewModel>().ClearSelection();
            }
            StartTick();
        }

        public void DeleteServer(IEnumerable<ProtocolBase> protocolServers)
        {
            StopTick();
            bool needReload = false;
            if (_sourceService == null) return;
            var groupedServers = protocolServers.GroupBy(x => x.DataSourceName);
            foreach (var groupedServer in groupedServers)
            {
                var source = groupedServer.First().GetDataSource();
                needReload |= source?.NeedRead() ?? false;
                if (source?.IsWritable == true
                    && source.Database_DeleteServer(groupedServer.Select(x => x.Id))
                    && needReload == false)
                {
                    // update viewmodel
                    foreach (var protocolServer in groupedServer)
                    {

                        var old = GetItemById(source.DataSourceName, protocolServer.Id);
                        if (old != null)
                        {
                            VmItemList.Remove(old);
                            IoC.Get<ServerListPageViewModel>()?.VmServerList?.Remove(old); // invoke main list ui change
                            IoC.Get<ServerSelectionsViewModel>()?.VmServerList?.Remove(old); // invoke launcher ui change
                        }
                    }
                }
            }

            if (needReload)
            {
                ReloadServerList();
            }
            else
            {
                ReadTagsFromServers();
                IoC.Get<ServerListPageViewModel>().ClearSelection();
            }
            StartTick();
        }

        #endregion Server Data

        public void StopTick()
        {
            lock (this)
            {
                _timer.Stop();
                _timerStopFlag = true;
            }
        }
        public void StartTick()
        {
            lock (this)
            {
                _timerStopFlag = false;
                if (_timer.Enabled == false && _configurationService.DatabaseCheckPeriod > 0)
                {
                    _timer.Interval = _configurationService.DatabaseCheckPeriod * 1000;
                    _timer.Start();
                }
            }
        }
    }
}