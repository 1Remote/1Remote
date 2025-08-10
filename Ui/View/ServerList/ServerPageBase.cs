using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.Utils.mRemoteNG;
using _1RM.Utils.PRemoteM;
using _1RM.Utils.RdpFile;
using _1RM.View.Editor;
using _1RM.View.Settings.Launcher;
using _1RM.View.Utils;
using Newtonsoft.Json;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.ServerList
{
    public abstract partial class ServerPageBase : NotifyPropertyChangedBaseScreen
    {
        protected ServerPageBase(DataSourceService sourceService, GlobalData appData)
        {
            SourceService = sourceService;
            AppData = appData;
            TagsPanelViewModel = IoC.Get<TagsPanelViewModel>();

            AppData.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GlobalData.TagList))
                {
                    OnGlobalDataTagListChanged();
                }
            };
            OnGlobalDataTagListChanged();
        }

        public DataSourceService SourceService { get; }
        public GlobalData AppData { get; }
        public LauncherSettingViewModel LauncherSettingViewModel => IoC.Get<LauncherSettingViewModel>();

        public abstract void BuildView();





        public ObservableCollection<ProtocolBaseViewModel> VmServerList { get; set; } = new ObservableCollection<ProtocolBaseViewModel>();
        public void ClearSelection()
        {
            foreach (var item in VmServerList)
            {
                item.IsSelected = false;
            }

            if (this.View is ServerListPageView view)
            {
                view.RefreshHeaderCheckBox();
            }
        }


        private RelayCommand? _cmdCancelSelected;
        public RelayCommand CmdCancelSelected
        {
            get
            {
                Debug.Assert(SourceService != null);
                return _cmdCancelSelected ??= new RelayCommand((o) => { ClearSelection(); });
            }
        }





        private RelayCommand? _cmdConnectSelected;
        public RelayCommand CmdConnectSelected
        {
            get
            {
                return _cmdConnectSelected ??= new RelayCommand((o) =>
                {
                    var selected = VmServerList.Where(x => x.IsSelected == true).Select(x => x.Server).ToArray();
                    GlobalEventHelper.OnRequestServersConnect?.Invoke(selected, fromView: $"{nameof(MainWindowView)}");
                });
            }
        }



        private RelayCommand? _cmdMultiEditSelected;
        public RelayCommand CmdMultiEditSelected
        {
            get
            {
                return _cmdMultiEditSelected ??= new RelayCommand((o) =>
                {
                    var vms = VmServerList.Where(x => x.IsSelected).Select(x => x.Server).ToArray();
                    if (vms.Any() == true)
                    {
                        GlobalEventHelper.OnRequestGoToServerMultipleEditPage?.Invoke(vms, true);
                    }
                }, o => VmServerList.Where(x => x.IsSelected == true).All(x => x.IsEditable));
            }
        }


        private RelayCommand? _cmdDeleteSelected;
        public RelayCommand CmdDeleteSelected
        {
            get
            {
                return _cmdDeleteSelected ??= new RelayCommand((o) =>
                {
                    var ss = VmServerList.Where(x => x.IsSelected == true && x.IsEditable).ToList();
                    if (ss.Count == 0)
                    {
                        MessageBoxHelper.ErrorAlert("Can not delete since they are all not writable.");
                        return;
                    }
                    if (true == MessageBoxHelper.Confirm(IoC.Translate("confirm_to_delete_selected"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                    {
                        MaskLayerController.ShowProcessingRing();
                        Task.Factory.StartNew(() =>
                        {
                            var servers = ss.Select(x => x.Server).ToList() as List<ProtocolBase>;
                            SimpleLogHelper.Debug($" {string.Join(", ", servers.Select(x => x.DisplayName))} to be deleted");
                            var ret = AppData.DeleteServer(servers);
                            if (!ret.IsSuccess)
                            {
                                MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                            }
                            MaskLayerController.HideMask();
                        });
                    }
                }, o => VmServerList.Where(x => x.IsSelected == true).All(x => x.IsEditable));
            }
        }

        private RelayCommand? _cmdCreateDesktopShortcut;
        public RelayCommand CmdCreateDesktopShortcut
        {
            get
            {
                return _cmdCreateDesktopShortcut ??= new RelayCommand((o) =>
                {
                    var selected = VmServerList.Where(x => x.IsSelected == true).ToArray();
                    var ids = selected.Select(x => x.Id);
                    var names = selected.Select(x => x.DisplayName);
                    var icons = selected.Select(x => x.Server.IconImg).ToList();
                    var name = string.Join(" & ", names);
                    if (name.Length > 50)
                    {
                        name = name.Substring(0, 50).Trim().Trim('&') + "...";
                    }
                    var path = AppStartupHelper.MakeIcon(name, icons);
                    AppStartupHelper.InstallDesktopShortcutByUlid(name, ids, path);
                    ClearSelection();
                });
            }
        }



        #region Cmd ADD Import Export



        private RelayCommand? _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand((o) =>
                {
                    if (View is ServerListPageView view)
                        view.CbPopForInExport.IsChecked = false;
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(TagFilters.Where<TagFilter>(x => x.IsIncluded == true).Select(x => x.TagName).ToList(), o as DataSourceBase);
                });
            }
        }



        private RelayCommand? _cmdExportSelectedToJson;
        public RelayCommand CmdExportSelectedToJson
        {
            get
            {
                return _cmdExportSelectedToJson ??= new RelayCommand((o) =>
                {
                    if (this.View is ServerListPageView view)
                    {
                        SecondaryVerificationHelper.VerifyAsyncUiCallBack(b =>
                        {
                            if (b != true) return;
                            Execute.OnUIThreadSync(() =>
                            {
                                try
                                {
                                    MaskLayerController.ShowProcessingRing(IoC.Translate("Caution: Your data will be saved unencrypted!"));
                                    view.CbPopForInExport.IsChecked = false;
                                    var path = SelectFileHelper.SaveFile(
                                        title: IoC.Translate("Caution: Your data will be saved unencrypted!"),
                                        filter: "json|*.json",
                                        selectedFileName: DateTime.Now.ToString("yyyyMMddhhmmss") + ".json");
                                    if (path == null) return;
                                    var list = new List<ProtocolBase>();
                                    foreach (var vs in VmServerList.Where(x => (string.IsNullOrWhiteSpace(SelectedTabName) || x.Server.Tags?.Contains(SelectedTabName) == true) && x.IsSelected == true && x.IsEditable))
                                    {
                                        var serverBase = (ProtocolBase)vs.Server.Clone();
                                        serverBase.DecryptToConnectLevel();
                                        list.Add(serverBase);
                                    }

                                    ClearSelection();
                                    File.WriteAllText(path, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                                    MessageBoxHelper.Info($"{IoC.Translate("Export")}: {IoC.Translate("Done")}!");

                                }
                                finally
                                {
                                    MaskLayerController.HideMask();
                                }
                            });
                        });
                    }

                }, o => VmServerList.Where(x => x.IsSelected == true).All(x => x.IsEditable));
            }
        }


        private Tuple<DataSourceBase?, string?> GetImportParams(string filter)
        {
            // select save to which source
            var source = DataSourceSelectorViewModel.SelectDataSource();
            if (source == null)
            {
                return new Tuple<DataSourceBase?, string?>(null, null);
            }
            if (source?.IsWritable != true)
            {
                MessageBoxHelper.ErrorAlert($"Can not add server to DataSource ({source?.DataSourceName ?? "null"}) since it is not writable.");
                return new Tuple<DataSourceBase?, string?>(null, null);
            }
            // select file with filter
            if (this.View is ServerListPageView view)
                view.CbPopForInExport.IsChecked = false;
            var path = SelectFileHelper.OpenFile(title: IoC.Translate("import_server_dialog_title"), filter: filter);
            return path == null ? new Tuple<DataSourceBase?, string?>(null, null) : new Tuple<DataSourceBase?, string?>(source, path);
        }


        private async Task<Tuple<DataSourceBase?, string?>> GetImportParamsAsync(string filter)
        {
            // select save to which source
            var source = await DataSourceSelectorViewModel.SelectDataSourceAsync();
            if (source == null)
            {
                return new Tuple<DataSourceBase?, string?>(null, null);
            }
            if (source?.IsWritable != true)
            {
                MessageBoxHelper.ErrorAlert($"Can not add server to DataSource ({source?.DataSourceName ?? "null"}) since it is not writable.");
                return new Tuple<DataSourceBase?, string?>(null, null);
            }

            // select file with filter
            if (this.View is ServerListPageView view)
                view.CbPopForInExport.IsChecked = false;
            var path = SelectFileHelper.OpenFile(title: IoC.Translate("import_server_dialog_title"), filter: filter);
            return path == null ? new Tuple<DataSourceBase?, string?>(null, null) : new Tuple<DataSourceBase?, string?>(source, path);
        }



        private RelayCommand? _cmdImportFromJson;
        public RelayCommand CmdImportFromJson
        {
            get
            {
                return _cmdImportFromJson ??= new RelayCommand((o) =>
                {
                    var (source, path) = GetImportParams("json|*.json|*.*|*.*");
                    if (source == null || path == null) return;

                    MaskLayerController.ShowProcessingRing(IoC.Translate("system_options_data_security_info_data_processing"), IoC.Get<MainWindowViewModel>());
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var list = new List<ProtocolBase>();
                            var deserializeObject = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(path, Encoding.UTF8)) ?? new List<object>();
                            foreach (var server in deserializeObject.Select(json => ItemCreateHelper.CreateFromJsonString(json.ToString() ?? "")))
                            {
                                if (server == null) continue;
                                server.Id = string.Empty;
                                server.DecryptToConnectLevel();
                                list.Add(server);
                            }

                            var ret = source.Database_InsertServer(list);
                            if (ret.IsSuccess)
                            {
                                AppData.ReloadAll(true); // reload server list after import
                                MessageBoxHelper.Info(IoC.Translate("import_done_0_items_added", list.Count.ToString()));
                            }
                            else
                            {
                                MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                            }
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Warning(e);
                            MessageBoxHelper.ErrorAlert(IoC.Translate("import_failure_with_data_format_error") + " : " + e.Message);
                        }
                        finally
                        {
                            MaskLayerController.HideMask(IoC.Get<MainWindowViewModel>());
                        }
                    });
                });
            }
        }



        private RelayCommand? _cmdImportFromDatabase;
        public RelayCommand CmdImportFromDatabase
        {
            get
            {
                return _cmdImportFromDatabase ??= new RelayCommand((o) =>
                {
                    var (source, dbPath) = GetImportParams("db|*.db");
                    if (source == null || dbPath == null) return;

                    var dataBase = new DapperDatabaseFree("PRemoteM", DatabaseType.Sqlite);
                    var result = dataBase.OpenNewConnection(DbExtensions.GetSqliteConnectionString(dbPath));
                    if (result.IsSuccess == false)
                    {
                        MessageBoxHelper.ErrorAlert(result.ErrorInfo);
                        return;
                    }



                    MaskLayerController.ShowProcessingRing(IoC.Translate("system_options_data_security_info_data_processing"), IoC.Get<MainWindowViewModel>());
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var list = new List<ProtocolBase>();
                            // PRemoteM db
                            if (dataBase.TableExists("Config").IsSuccess && dataBase.TableExists("Server").IsSuccess)
                            {
                                var ss = PRemoteMTransferHelper.GetServers(dataBase);
                                if (ss != null)
                                {
                                    list.AddRange(ss);
                                }
                            }

                            // 1Remote db
                            if (dataBase.TableExists("Configs").IsSuccess && dataBase.TableExists("Servers").IsSuccess)
                            {
                                var ds = new SqliteSource("1Remote");
                                var ss = ds.GetServers(true).Select(x => x.Server).ToList();
                                if (ss.Count > 0)
                                {
                                    foreach (var s in ss)
                                    {
                                        s.DecryptToConnectLevel();
                                        list.Add(s);
                                    }
                                }
                            }

                            if (list.Count == 0)
                                return;

                            foreach (var server in list)
                            {
                                server.Id = string.Empty;
                            }

                            var ret = source.Database_InsertServer(list);
                            if (ret.IsSuccess)
                            {
                                AppData.ReloadAll(true); // reload server list after import db
                                MessageBoxHelper.Info(IoC.Translate("import_done_0_items_added", list.Count.ToString()));
                            }
                            else
                            {
                                MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                            }
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Warning(e);
                            MessageBoxHelper.ErrorAlert(IoC.Translate("import_failure_with_data_format_error"));
                        }
                        finally
                        {
                            MaskLayerController.HideMask(IoC.Get<MainWindowViewModel>());
                        }
                    });
                });
            }
        }





        private RelayCommand? _cmdImportFromCsv;
        public RelayCommand CmdImportFromCsv
        {
            get
            {
                return _cmdImportFromCsv ??= new RelayCommand((o) =>
                {
                    var (source, path) = GetImportParams("csv|*.csv");
                    if (source == null || path == null) return;

                    MaskLayerController.ShowProcessingRing(IoC.Translate("system_options_data_security_info_data_processing"), IoC.Get<MainWindowViewModel>());
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var list = MRemoteNgImporter.FromCsv(path, ServerIcons.Instance.IconsBase64);
                            if (list?.Count > 0)
                            {
                                var ret = source.Database_InsertServer(list);
                                if (ret.IsSuccess)
                                {
                                    AppData.ReloadAll(true); // reload server list after import db
                                    MessageBoxHelper.Info(IoC.Translate("import_done_0_items_added", list.Count.ToString()));
                                }
                                else
                                {
                                    MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Debug(e);
                            MessageBoxHelper.Info(IoC.Translate("import_failure_with_data_format_error"));
                        }
                        finally
                        {
                            MaskLayerController.HideMask(IoC.Get<MainWindowViewModel>());
                        }
                    });
                });
            }
        }


        private RelayCommand? _cmdImportFromRdp;
        public RelayCommand CmdImportFromRdp
        {
            get
            {
                return _cmdImportFromRdp ??= new RelayCommand((o) =>
                {
                    var (source, path) = GetImportParams("rdp|*.rdp");
                    if (source == null || path == null) return;

                    try
                    {
                        var config = RdpConfig.FromRdpFile(path);
                        if (config == null) return;
                        var rdp = RDP.FromRdpConfig(config, ServerIcons.Instance.IconsBase64);

                        try
                        {
                            // try read user name & password from CredentialManagement.
                            using var cred = _1RM.Utils.WindowsApi.Credential.Credential.Load("TERMSRV/" + rdp.Address);
                            if (cred != null)
                            {
                                rdp.UserName = cred.Username;
                                rdp.Password = cred.Password;
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        var ret = AppData.AddServer(rdp, source);
                        if (ret.IsSuccess)
                        {
                            MessageBoxHelper.Info(IoC.Translate("import_done_0_items_added", "1"));
                        }
                        else
                        {
                            MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                        }
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Debug(e);
                        MessageBoxHelper.Info(IoC.Translate("import_failure_with_data_format_error"));
                    }
                });
            }
        }

        #endregion
    }
}
