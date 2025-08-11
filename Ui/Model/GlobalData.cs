using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils.Tracing;
using _1RM.View;
using _1RM.View.Launcher;
using Shawn.Utils;
using ServerListPageViewModel = _1RM.View.ServerList.ServerListPageViewModel;

namespace _1RM.Model
{
    public partial class GlobalData : NotifyPropertyChangedBase
    {
        public GlobalData(ConfigurationService configurationService)
        {
            InitTimer();
            _configurationService = configurationService;
        }

        private DataSourceService? _sourceService;
        private readonly ConfigurationService _configurationService;

        public void SetDataSourceService(DataSourceService sourceService)
        {
            _sourceService = sourceService;
        }


        #region Server Data

        public Action? OnReloadAll;

        public List<ProtocolBaseViewModel> VmItemList { get; set; } = new List<ProtocolBaseViewModel>();


        public ProtocolBaseViewModel? GetItemById(string dataSourceName, string serverId)
        {
            return VmItemList.FirstOrDefault(x => x.Server.DataSource?.DataSourceName == dataSourceName
                                                  && x.Id == serverId);
        }


        /// <summary>
        /// reload data based on `LastReadFromDataSourceMillisecondsTimestamp` and `DataSourceDataUpdateTimestamp`
        /// return true if read data
        /// </summary>
        public bool ReloadAll(bool force = false)
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
                    needRead |= _sourceService.LocalDataSource?.NeedRead(TableServer.TABLE_NAME) ?? false;
                    needRead |= _sourceService.LocalDataSource?.NeedRead(TableCredential.TABLE_NAME) ?? false;
                    if (needRead == false)
                    {
                        foreach (var additionalSource in _sourceService.AdditionalSources)
                        {
                            if (additionalSource.Value.Status != EnumDatabaseStatus.OK)
                            {
                                // if this source is not connected, we skip it
                                continue;
                            }

                            if (needRead == false)
                            {
                                needRead |= additionalSource.Value.NeedRead(TableServer.TABLE_NAME);
                            }
                            if (needRead == false)
                            {
                                needRead |= additionalSource.Value.NeedRead(TableCredential.TABLE_NAME);
                            }
                            if (needRead)
                            {
                                // if any additional source need read, we read all servers
                                break;
                            }
                        }
                    }
                }

                if (force || needRead)
                {
                    // read from db
                    VmItemList = _sourceService.GetServers(force);
                    _sourceService.GetCredentials(force);
                    LocalityConnectRecorder.ConnectTimeCleanup();
                    ReloadTagsFromServers();
                    OnReloadAll?.Invoke();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
                UnifyTracing.Error(ex);
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
            var ret = dataSource.Database_InsertServer(protocolServer);
            if (ret.IsSuccess)
            {
                ReloadAll(force: true); // AddServer & needReload
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

                var needReload = source.NeedRead(TableServer.TABLE_NAME);
                var ret = source.Database_UpdateServer(protocolServer);
                if (ret.IsSuccess)
                {
                    if (needReload)
                    {
                        ReloadAll(); // UpdateServer & needReload
                    }
                    else
                    {
                        // invoke main list ui change & invoke launcher ui change
                        var old = GetItemById(source.DataSourceName, protocolServer.Id);
                        if (old != null)
                        {
                            old.Server = protocolServer;
                            old.DataSourceNameForLauncher = _sourceService?.AdditionalSources.Any() == true ? old.DataSourceName : "";
                        }
                        ReloadTagsFromServers();
                    }
                    IoC.Get<ServerListPageViewModel>().ClearSelection();
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
                var failMessages = new List<string>();
                foreach (var groupedServer in groupedServers)
                {
                    var source = groupedServer.First().DataSource;
                    if (source?.IsWritable != true)
                    {
                        failMessages.Add($"Can not update on DataSource({source?.DataSourceName ?? "null"}) since it is not writable.");
                        continue;
                    }
                    needReload |= source.NeedRead(TableServer.TABLE_NAME);
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
                        {
                            old.Server = protocolServer;
                            old.DataSourceNameForLauncher = _sourceService?.AdditionalSources.Any() == true ? old.DataSourceName : "";
                        }
                    }
                }

                if (isAnySuccess)
                {
                    if (needReload)
                    {
                        ReloadAll(); // UpdateServers & needReload
                    }
                    else
                    {
                        ReloadTagsFromServers();
                        // TODO: 树状列表建好后，将不再有一个全局的 ServerListPageViewModel
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
                    ReloadAll(true); // DeleteServers
                }

                return failMessages.Any() ? Result.Fail(string.Join("\r\n", failMessages)) : Result.Success();
            }
            catch (Exception e)
            {
                UnifyTracing.Error(e);
                throw;
            }
            finally
            {
                StartTick();
            }
        }

        #endregion Server Data


    }
}