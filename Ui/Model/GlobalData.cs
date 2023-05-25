﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

            CheckUpdateTime = DateTime.Now.AddSeconds(_configurationService.DatabaseCheckPeriod);
            _timer = new Timer(1000)
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
                    var tn = tagName.Trim().ToLower();
                    if (tags.All(x => !string.Equals(x.Name, tn, StringComparison.CurrentCultureIgnoreCase)))
                        tags.Add(new Tag(tn, pinnedTags.Contains(tn), SaveOnPinnedChanged) { ItemsCount = 1 });
                    else
                        tags.First(x => x.Name.ToLower() == tn).ItemsCount++;
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
            try
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
                        // 断线的数据源，除非强制读取，否则都忽略，断线的数据源在 timer 里会自动重连
                        if (additionalSource.Value.Status != EnumDatabaseStatus.OK)
                        {
                            if (!focus) continue;
                            additionalSource.Value.Database_SelfCheck();
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
            finally
            {
            }
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
                var source = protocolServer.DataSource;
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
                    var source = groupedServer.First().DataSource;
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
                    var source = groupedServer.First().DataSource;
                    if (source?.IsWritable == true)
                    {
                        needReload |= source.NeedRead();
                        var tmp = source.Database_DeleteServer(groupedServer.Select(x => x.Id));
                        SimpleLogHelper.Debug($"DeleteServer: {string.Join('、', groupedServer.Select(x => x.Id))}, needReload = {needReload}, tmp.IsSuccess = {tmp.IsSuccess}");
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
                                        SimpleLogHelper.Debug($"Remote server {old.DisplayName} of `{old.DataSourceName}` removed from GlobalData");
                                        VmItemList.Remove(old);
                                        IoC.Get<ServerListPageViewModel>().DeleteServer(old); // invoke main list ui change
                                        Execute.OnUIThread(() =>
                                        {
                                            if (IoC.Get<ServerSelectionsViewModel>().VmServerList.Contains(old))
                                                IoC.Get<ServerSelectionsViewModel>().VmServerList.Remove(old); // invoke launcher ui change
                                        });
                                    }
                                    else
                                    {
                                        SimpleLogHelper.Warning($"Remote server {protocolServer.DisplayName} of `{source.DataSourceName}` removed from GlobalData but not found in VmItemList");
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
                        ReloadServerList(needReload);
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
                    _timer.Start();
                }
            }
        }

        /// <summary>
        /// return time string like 1d 2h 3m 4s
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        private static string GetTime(long seconds)
        {
            var sb = new StringBuilder();
            if (seconds > 86400)
            {
                sb.Append($"{seconds / 86400}d");
                seconds %= 86400;
            }

            if (seconds > 3600)
            {
                sb.Append($"{seconds / 3600}h");
                seconds %= 3600;
            }

            if (seconds > 60)
            {
                sb.Append($"{seconds / 60}m");
                seconds %= 60;
            }

            if (seconds > 0)
            {
                sb.Append($"{seconds}s");
            }
            return sb.ToString();
        }

        public DateTime CheckUpdateTime;
        private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (_sourceService == null)
                    return;

                var ds = new List<DataSourceBase>();
                if (_sourceService.LocalDataSource != null)
                    ds.Add(_sourceService.LocalDataSource);
                ds.AddRange(_sourceService.AdditionalSources.Values);

                var mainWindowViewModel = IoC.Get<MainWindowViewModel>();
                var listPageViewModel = IoC.Get<ServerListPageViewModel>();
                var launcherWindowViewModel = IoC.Get<LauncherWindowViewModel>();
                // do not reload when any selected / launcher is shown / editor view is show
                if (mainWindowViewModel.EditorViewModel != null
                    || listPageViewModel.VmServerList.Any(x => x.IsSelected)
                    || launcherWindowViewModel?.View?.IsVisible == true)
                {
                    var pause = IoC.Get<LanguageService>().Translate("Pause");
                    foreach (var s in ds)
                    {
                        s.ReconnectInfo = pause;
                    }
                    return;
                }


                long checkUpdateEtc = 0;
                if (CheckUpdateTime > DateTime.Now)
                {
                    var ts = CheckUpdateTime - DateTime.Now;
                    checkUpdateEtc = (long)ts.TotalSeconds;
                }
                long minReconnectEtc = int.MaxValue;


                var needReconnect = new List<DataSourceBase>();
                foreach (var s in ds.Where(x => x.Status != EnumDatabaseStatus.OK))
                {
                    if (s.ReconnectTime > DateTime.Now)
                    {
                        minReconnectEtc = Math.Min((long)(s.ReconnectTime - DateTime.Now).TotalSeconds, minReconnectEtc);
                    }
                    else
                    {
                        minReconnectEtc = 0;
                        needReconnect.Add(s);
                    }
                }

                var minEtc = Math.Min(checkUpdateEtc, minReconnectEtc);


                var msgUpdating = IoC.Get<LanguageService>().Translate("Updating");
                var msgNextUpdate = IoC.Get<LanguageService>().Translate("Next update check");
                var msg = minEtc > 0 ? $"{msgNextUpdate} {GetTime(minEtc)}" : msgUpdating;


                var msgNextReconnect = IoC.Get<LanguageService>().Translate("Next auto reconnect");
                var msgReconnecting = IoC.Get<LanguageService>().Translate("Reconnecting");
                foreach (var s in ds)
                {
                    if (s.Status != EnumDatabaseStatus.OK)
                    {
                        if (s.ReconnectTime > DateTime.Now)
                        {
                            var seconds = (long)(s.ReconnectTime - DateTime.Now).TotalSeconds;
                            s.ReconnectInfo = $"{msgNextReconnect} {GetTime(seconds)}";
                        }
                        else
                        {
                            s.ReconnectInfo = msgReconnecting;
                        }
                    }
                    else
                    {
                        s.ReconnectInfo = msg;
                    }
                }

                if (minEtc > 0 && minReconnectEtc > 0)
                {
                    return;
                }

                // reconnect 
                foreach (var dataSource in needReconnect.Where(x => x.ReconnectTime < DateTime.Now))
                {
                    if (dataSource.Database_SelfCheck() == EnumDatabaseStatus.OK)
                    {
                        minEtc = 0;
                    }
                }

                if (minEtc == 0)
                {
                    if (ReloadServerList())
                    {
                        SimpleLogHelper.Debug("check database update - reload data by timer " + _timer.GetHashCode());
                    }
                    else
                    {
                        SimpleLogHelper.Debug("check database update - no need reload by timer " + _timer.GetHashCode());
                    }
                }

                System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
                CheckUpdateTime = DateTime.Now.AddSeconds(_configurationService.DatabaseCheckPeriod);
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
            }
        }
    }
}