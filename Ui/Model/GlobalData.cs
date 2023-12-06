using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Timers;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
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
            _timer = new Timer(500)
            {
                AutoReset = false,
            };
            _timer.Elapsed += (sender, args) => TimerOnElapsed();
        }

        private DataSourceService? _sourceService;
        private readonly ConfigurationService _configurationService;

        public void SetDataSourceService(DataSourceService sourceService)
        {
            _sourceService = sourceService;
        }


        private List<Tag> _tagList = new List<Tag>();
        public List<Tag> TagList
        {
            get => _tagList;
            private set => SetAndNotifyIfChanged(ref _tagList, value);
        }



        #region Server Data

        public Action? OnDataReloaded;

        public List<ProtocolBaseViewModel> VmItemList { get; set; } = new List<ProtocolBaseViewModel>();


        public void ReadTagsFromServers()
        {
            // get distinct tag from servers
            var tags = new List<Tag>();
            var lts = LocalityTagService.TagDict;
            foreach (var tagNames in VmItemList.Select(x => x.Server.Tags))
            {
                foreach (var tn in tagNames.Select(tagName => tagName.Trim().ToLower()))
                {
                    if (tags.All(x => !string.Equals(x.Name, tn, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        bool isPinned = false;
                        int customOrder = int.MaxValue;
                        if (lts.ContainsKey(tn))
                        {
                            isPinned = lts[tn].IsPinned;
                            customOrder = lts[tn].CustomOrder;
                        }
                        else if (_configurationService.PinnedTags.Contains(tn))
                        {
                            isPinned = true;
                        }
                        tags.Add(new Tag(tn, isPinned, customOrder, SaveTagChanged) { ItemsCount = 1 });
                    }
                    else
                        tags.First(x => x.Name.ToLower() == tn).ItemsCount++;
                }
            }

            TagList = new List<Tag>(tags.OrderBy(x => x.Name));
            foreach (var viewModel in VmItemList)
            {
                if (viewModel.Server.Tags.Count > 0)
                    viewModel.ReLoadTags();
            }

        }

        private void SaveTagChanged()
        {
            LocalityTagService.UpdateTags(TagList);
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
        public bool ReloadServerList(bool force = false)
        {
            try
            {
                if (_sourceService == null)
                {
                    return false;
                }


                var needRead = false;
                if (force == false)
                {
                    needRead = _sourceService.LocalDataSource?.NeedRead() ?? false;
                    foreach (var additionalSource in _sourceService.AdditionalSources)
                    {
                        // 断线的数据源，除非强制读取，否则都忽略，断线的数据源在 timer 里会自动重连
                        if (additionalSource.Value.Status != EnumDatabaseStatus.OK)
                        {
                            if (!force) continue;
                            additionalSource.Value.Database_SelfCheck();
                        }

                        if (needRead == false)
                        {
                            needRead |= additionalSource.Value.NeedRead();
                        }
                    }
                }

                if (force || needRead)
                {
                    // read from db
                    VmItemList = _sourceService.GetServers(force);
                    LocalityConnectRecorder.Cleanup();
                    ReadTagsFromServers();
                    OnDataReloaded?.Invoke();

                    // old app turning to new app protocol, todo: remove after 2024
                    foreach (var viewModel in VmItemList)
                    {
                        if (viewModel.Server is LocalApp app)
                        {
                            if (app.ArgumentList.Count == 0 && !string.IsNullOrEmpty(app.Arguments))
                            {
                                app.ArgumentList.Add(new AppArgument()
                                {
                                    Name = "Arg",
                                    Key = "",
                                    Type = AppArgumentType.Normal,
                                    AddBlankAfterValue = false,
                                    AddBlankAfterKey = false,
                                    IsNullable = true,
                                    Value = app.Arguments,
                                });
                                app.Arguments = "";
                                UpdateServer(app);
                            }
                        }
                    }
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
                MsAppCenterHelper.Error(ex);
            }
            finally
            {
            }
            return false;
        }



        public Result AddServer(ProtocolBase protocolServer, DataSourceBase dataSource)
        {
            string info = IoC.Translate("We can not insert into database:");
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
                    IoC.Get<ServerListPageViewModel>().RefreshCollectionViewSource(true);
                }
            }

            if (needReload)
            {
                ReloadServerList(force: true);
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
            string info = IoC.Translate("We can not update on database:");
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
                        {
                            old.Server = protocolServer;
                        }
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

        public Result UpdateServer(IEnumerable<ProtocolBase> protocolServers, bool reloadTag = true)
        {
            StopTick();
            try
            {
                var groupedServers = protocolServers.GroupBy(x => x.DataSource);
                bool needReload = false;
                bool isAnySuccess = false;
                var failMessages = new List<string>();
                foreach (var groupedServer in groupedServers)
                {
                    var source = groupedServer.First().DataSource;
                    if (source?.IsWritable != true)
                    {
                        failMessages.Add($"Can not update on DataSource({ source?.DataSourceName ?? "null"}) since it is not writable.");
                        continue;
                    }
                    needReload |= source.NeedRead();
                    var tmp = source.Database_UpdateServer(groupedServer);
                    isAnySuccess = tmp.IsSuccess;
                    if (!tmp.IsSuccess)
                    {
                        failMessages.Add(tmp.ErrorInfo);
                        continue;
                    }

                    if (needReload) continue;

                    // update viewmodel
                    foreach (var protocolServer in groupedServer)
                    {
                        var old = GetItemById(source.DataSourceName, protocolServer.Id);
                        // invoke main list ui change & invoke launcher ui change
                        if (old != null)
                            old.Server = protocolServer;
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
                        if (reloadTag)
                            ReadTagsFromServers();
                        IoC.Get<ServerListPageViewModel>().ClearSelection();
                    }
                }

                return failMessages.Any() ? Result.Fail(string.Join("\r\n", failMessages)) : Result.Success();
            }
            finally
            {
                StartTick();
            }
        }


        private void RemoveOneServerFromUI(ProtocolBase server)
        {
            var old = GetItemById(server.DataSource!.DataSourceName, server.Id);
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
        }
        public Result DeleteServer(IEnumerable<ProtocolBase> protocolServers)
        {
            StopTick();
            try
            {
                var groupedServers = protocolServers.GroupBy(x => x.DataSource);
                bool isAnySuccess = false;
                var failMessages = new List<string>();
                foreach (var groupedServer in groupedServers)
                {
                    var source = groupedServer.First().DataSource;
                    if (source?.IsWritable != true)
                    {
                        failMessages.Add($"Can not update on DataSource({source?.DataSourceName ?? "null"}) since it is not writable.");
                        continue;
                    }

                    var tmp = source.Database_DeleteServer(groupedServer.Select(x => x.Id));
                    SimpleLogHelper.DebugInfo($"DeleteServer: {string.Join(", ", groupedServer.Select(x => x.Id))}, tmp.IsSuccess = {tmp.IsSuccess}");
                    isAnySuccess = tmp.IsSuccess;
                    if (!tmp.IsSuccess)
                    {
                        failMessages.Add(tmp.ErrorInfo);
                    }
                }

                // update viewmodel
                if (isAnySuccess)
                {
                    if (protocolServers.Count() == 1)
                    {
                        RemoveOneServerFromUI(protocolServers.First());
                        ReadTagsFromServers();
                        IoC.Get<ServerListPageViewModel>().ClearSelection();
                    }
                    else
                    {
                        ReloadServerList(true);
                    }
                }

                return failMessages.Any() ? Result.Fail(string.Join("\r\n", failMessages)) : Result.Success();
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
            CheckUpdateTime = DateTime.Now.AddSeconds(_configurationService.DatabaseCheckPeriod);
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
        private void TimerOnElapsed()
        {
            try
            {
                if (_sourceService == null)
                    return;

                var ds = new List<DataSourceBase>();
                if (_sourceService.LocalDataSource != null)
                    ds.Add(_sourceService.LocalDataSource);
                ds.AddRange(_sourceService.AdditionalSources.Values);

                var mainWindowViewModel = IoC.TryGet<MainWindowViewModel>();
                var listPageViewModel = IoC.TryGet<ServerListPageViewModel>();
                var launcherWindowViewModel = IoC.TryGet<LauncherWindowViewModel>();

                if(mainWindowViewModel == null
                   || listPageViewModel == null
                   || launcherWindowViewModel == null)
                    return;

                // do not reload when any selected / launcher is shown / editor view is show
                if (mainWindowViewModel.EditorViewModel != null
                    || listPageViewModel.VmServerList.Any(x => x.IsSelected)
                    || launcherWindowViewModel?.View?.IsVisible == true)
                {
                    var pause = IoC.Translate("Pause");
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


                var msgUpdating = IoC.Translate("Updating");
                var msgNextUpdate = IoC.Translate("Next update check");
                var msg = minEtc > 0 ? $"{msgNextUpdate} {GetTime(minEtc)}" : msgUpdating;


                var msgNextReconnect = IoC.Translate("Next auto reconnect");
                var msgReconnecting = IoC.Translate("Reconnecting");
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
                    if (dataSource.Database_SelfCheck().Status == EnumDatabaseStatus.OK)
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