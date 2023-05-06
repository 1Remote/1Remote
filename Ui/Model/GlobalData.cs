using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Launcher;
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
            return VmItemList.FirstOrDefault(x => x.Server.DataSource?.DataSourceName == dataSourceName
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
                    // TODO try to reconnect when network is available
                    if (additionalSource.Value.Status != EnumDatabaseStatus.OK)
                    {
#if DEBUG
                        if (additionalSource.Value.StatueTime.AddMinutes(1) < DateTime.Now)
#else
                        if (additionalSource.Value.StatueTime.AddMinutes(5) < DateTime.Now)
#endif
                        {
                            additionalSource.Value.Database_SelfCheck();
                        }
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

        public Result AddServer(ProtocolBase protocolServer, DataSourceBase dataSource)
        {
            string info = IoC.Get<LanguageService>().Translate("We can not insert into database:");
            StopTick();
            if (dataSource.IsWritable == false)
            {
                return Result.Fail(info, protocolServer.DataSource, $"`{protocolServer.DataSource}` is readonly for you");
            }
            var needReload = dataSource.NeedRead();
            var ret = dataSource.Database_InsertServer(protocolServer);
            if (ret.IsSuccess)
            {
                var @new = new ProtocolBaseViewModel(protocolServer);
                if (needReload == false)
                {
                    VmItemList.Add(@new);
                    IoC.Get<ServerListPageViewModel>()?.AppendServer(@new); // invoke main list ui change
                    IoC.Get<ServerSelectionsViewModel>()?.AppendServer(@new); // invoke launcher ui change


                    if (dataSource != IoC.Get<DataSourceService>().LocalDataSource
                        && IoC.Get<DataSourceService>().AdditionalSources.Select(x => x.Value.CachedProtocols.Count).Sum() <= 1)
                    {
                        // if is additional database and need to set up group by database name!
                        IoC.Get<ServerListPageViewModel>().ApplySort();
                    }
                }
            }

            if (needReload)
            {
                ReloadServerList(focus: true);
            }
            else
            {
                ReadTagsFromServers();
                IoC.Get<ServerListPageViewModel>().ClearSelection();
            }
            StartTick();
            return ret;
        }

        public Result UpdateServer(ProtocolBase protocolServer)
        {
            StopTick();
            string info = IoC.Get<LanguageService>().Translate("We can not update on database:");
            try
            {
                Debug.Assert(protocolServer.IsTmpSession() == false);
                var source = protocolServer.GetDataSource();
                if (source == null)
                {
                    return Result.Fail(info, protocolServer.DataSource, $"`{protocolServer.DataSource}` is not initialized yet");
                }
                else if (source.IsWritable == false)
                {
                    return Result.Fail(info, protocolServer.DataSource, $"`{protocolServer.DataSource}` is readonly for you");
                }

                var needReload = source.NeedRead();
                var ret = source.Database_UpdateServer(protocolServer);
                if (ret.IsSuccess)
                {
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
                }
                return ret;
            }
            finally
            {
                StartTick();
            }
        }

        public Result UpdateServer(IEnumerable<ProtocolBase> protocolServers)
        {
            StopTick();
            try
            {
                var groupedServers = protocolServers.GroupBy(x => x.DataSource);
                bool needReload = false;
                bool isAnySuccess = false;
                var failMsgs = new List<string>();
                foreach (var groupedServer in groupedServers)
                {
                    var source = groupedServer.First().GetDataSource();
                    if (source?.IsWritable == true)
                    {
                        needReload |= source.NeedRead();
                        var tmp = source.Database_UpdateServer(groupedServer);
                        if (tmp.IsSuccess)
                        {
                            isAnySuccess = true;
                            if (needReload == false)
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
                        else
                        {
                            failMsgs.Add(tmp.ErrorInfo);
                        }
                    }
                }

                if (isAnySuccess)
                {
                    if (needReload)
                    {
                        ReloadServerList();
                    }
                    else
                    {
                        ReadTagsFromServers();
                        IoC.Get<ServerListPageViewModel>().ClearSelection();
                    }
                }

                if (failMsgs.Any())
                {
                    return Result.Fail(string.Join("\r\n", failMsgs));
                }
                else
                {
                    return Result.Success();
                }
            }
            finally
            {
                StartTick();
            }
        }

        public Result DeleteServer(IEnumerable<ProtocolBase> protocolServers)
        {
            StopTick();
            try
            {
                var groupedServers = protocolServers.GroupBy(x => x.DataSource);
                bool needReload = false;
                bool isAnySuccess = false;
                var failMsgs = new List<string>();
                foreach (var groupedServer in groupedServers)
                {
                    var source = groupedServer.First().GetDataSource();
                    if (source?.IsWritable == true)
                    {
                        needReload |= source.NeedRead();
                        var tmp = source.Database_DeleteServer(groupedServer.Select(x => x.Id));
                        if (tmp.IsSuccess)
                        {
                            isAnySuccess = true;
                            if (needReload == false)
                            {
                                // update viewmodel
                                foreach (var protocolServer in groupedServer)
                                {
                                    var old = GetItemById(source.DataSourceName, protocolServer.Id);
                                    if (old != null)
                                    {
                                        VmItemList.Remove(old);
                                        Execute.OnUIThread(() =>
                                        {
                                            if (IoC.Get<ServerListPageViewModel>().VmServerList.Contains(old))
                                                IoC.Get<ServerListPageViewModel>().VmServerList.Remove(old); // invoke main list ui change
                                            if (IoC.Get<ServerSelectionsViewModel>().VmServerList.Contains(old))
                                                IoC.Get<ServerSelectionsViewModel>().VmServerList.Remove(old); // invoke launcher ui change
                                        });
                                    }
                                }
                            }
                        }
                        else
                        {
                            failMsgs.Add(tmp.ErrorInfo);
                        }
                    }

                }

                if (isAnySuccess)
                {
                    if (needReload)
                    {
                        ReloadServerList();
                    }
                    else
                    {
                        ReadTagsFromServers();
                        IoC.Get<ServerListPageViewModel>().ClearSelection();
                    }
                }

                if (failMsgs.Any())
                {
                    return Result.Fail(string.Join("\r\n", failMsgs));
                }
                else
                {
                    return Result.Success();
                }
            }
            catch (Exception e)
            {
                MsAppCenterHelper.Error(e);
                throw;
            }
            finally
            {
                StartTick();
            }
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
                    || launcherWindowViewModel?.View?.IsVisible == true)
                {
                    return;
                }

                if (ReloadServerList())
                {
                    SimpleLogHelper.Debug("check database update - reload data by timer " + _timer.GetHashCode());
                }
                else
                {
                    SimpleLogHelper.Debug("check database update - no need reload by timer " + _timer.GetHashCode());
                }
            }
            catch (Exception ex)
            {
                MsAppCenterHelper.Error(ex);
                throw;
            }
            finally
            {
                lock (this)
                {
                    if (_timerStopFlag == false && _configurationService.DatabaseCheckPeriod > 0)
                    {
                        _timer.Start();
                    }
                }
                System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
            }
        }
    }
}