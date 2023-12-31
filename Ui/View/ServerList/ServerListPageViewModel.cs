using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
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
using Shawn.Utils.Wpf.FileSystem;
using Stylet;

namespace _1RM.View.ServerList
{
    public partial class ServerListPageViewModel : NotifyPropertyChangedBaseScreen
    {
        public DataSourceService SourceService { get; }
        public GlobalData AppData { get; }
        public TagsPanelViewModel TagsPanelViewModel { get; }
        public LauncherSettingViewModel LauncherSettingViewModel => IoC.Get<LauncherSettingViewModel>();


        #region properties

        public bool ListPageIsCardView
        {
            get => IoC.Get<ConfigurationService>().General.ListPageIsCardView;
            set
            {
                if (IoC.Get<ConfigurationService>().General.ListPageIsCardView != value)
                {
                    IoC.Get<ConfigurationService>().General.ListPageIsCardView = value;
                    IoC.Get<ConfigurationService>().Save();
                    RaisePropertyChanged();
                }
                if (value == true)
                {
                    _briefNoteVisibility = Visibility.Visible;
                    BriefNoteVisibility = Visibility.Collapsed;
                }
            }
        }

        private ProtocolBaseViewModel? _selectedServerViewModelListItem = null;
        public ProtocolBaseViewModel? SelectedServerViewModelListItem
        {
            get => _selectedServerViewModelListItem;
            set => SetAndNotifyIfChanged(ref _selectedServerViewModelListItem, value);
        }
        public ObservableCollection<ProtocolBaseViewModel> VmServerList { get; set; } = new ObservableCollection<ProtocolBaseViewModel>();

        private bool _isAddToolTipShow = false;
        public bool IsAddToolTipShow
        {
            get => _isAddToolTipShow;
            set => SetAndNotifyIfChanged(ref _isAddToolTipShow, value);
        }

        public int SelectedCount => VmServerList.Count(x => x.IsSelected);

        public EnumServerOrderBy ServerOrderBy
        {
            get => LocalityListViewService.ServerOrderByGet();
            set
            {
                if (value != LocalityListViewService.ServerOrderByGet())
                {
                    LocalityListViewService.ServerOrderBySet(value);
                    RaisePropertyChanged();
                }
            }
        }

        private ObservableCollection<Tag> _headerTags = new ObservableCollection<Tag>();
        public ObservableCollection<Tag> HeaderTags
        {
            get => _headerTags;
            set => SetAndNotifyIfChanged(ref _headerTags, value);
        }


        public bool? IsSelectedAll
        {
            get
            {
                var items = VmServerList.Where(x => x.IsVisible);
                if (items.All(x => x.IsSelected))
                    return true;
                if (items.Any(x => x.IsSelected))
                    return null;
                return false;
            }
            set
            {
                if (value == false)
                {
                    foreach (var vmServerCard in VmServerList)
                    {
                        vmServerCard.IsSelected = false;
                    }
                }
                else
                {
                    foreach (var protocolBaseViewModel in VmServerList)
                    {
                        protocolBaseViewModel.IsSelected = protocolBaseViewModel.IsVisible;
                    }
                }
                RaisePropertyChanged();
            }
        }

        public bool IsAnySelected => VmServerList.Any(x => x.IsSelected == true);



        private Visibility _briefNoteVisibility;
        public Visibility BriefNoteVisibility
        {
            get => _briefNoteVisibility;
            set
            {
                if (SetAndNotifyIfChanged(ref this._briefNoteVisibility, value))
                {
                    UpdateNote();
                }
            }
        }

        private void UpdateNote()
        {
            Execute.OnUIThread(() =>
            {
                foreach (var item in VmServerList.Where(x => x.HoverNoteDisplayControl != null))
                {
                    if (item.HoverNoteDisplayControl != null)
                    {
                        item.HoverNoteDisplayControl.IsBriefNoteShown = BriefNoteVisibility == Visibility.Visible;
                    }
                }
            });
        }

        private TagsPanelViewModel? _tagListViewModel = null;
        public TagsPanelViewModel? TagListViewModel
        {
            get => _tagListViewModel;
            set
            {
                SetAndNotifyIfChanged(ref this._tagListViewModel, value);
                TagsPanelViewModel.FilterString = "";
            }
        }

        #endregion

        public ServerListPageViewModel(DataSourceService sourceService, GlobalData appData)
        {
            SourceService = sourceService;
            AppData = appData;
            TagsPanelViewModel = IoC.Get<TagsPanelViewModel>();

            {
                // Make sure the update do triggered the first time assign a value 
                BriefNoteVisibility = IoC.Get<ConfigurationService>().General.ShowNoteFieldInListView ? Visibility.Visible : Visibility.Collapsed;
            }

            AppData.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GlobalData.TagList))
                {
                    OnGlobalDataTagListChanged();
                }
            };
            OnGlobalDataTagListChanged();
        }

        protected override void OnViewLoaded()
        {
            ApplySort();
            AppData.OnDataReloaded += RebuildVmServerList;
            if (AppData.VmItemList.Count > 0)
            {
                // this view may be loaded after the data is loaded(when MainWindow start minimized)
                // so we need to rebuild the list here
                RebuildVmServerList();
            }
        }


        public void AppendServer(ProtocolBaseViewModel viewModel)
        {
            Execute.OnUIThreadSync(() =>
            {
                viewModel.PropertyChanged -= VmServerPropertyChanged;
                viewModel.PropertyChanged += VmServerPropertyChanged;
                VmServerList.Add(viewModel);
                VmServerListDummyNode();
            });
        }

        public void DeleteServer(string id)
        {
            var viewModel = VmServerList.FirstOrDefault(x => x.Id == id);
            if (viewModel != null)
            {
                DeleteServer(viewModel);
            }
        }
        public void DeleteServer(ProtocolBaseViewModel viewModel)
        {
            Execute.OnUIThreadSync(() =>
            {
                viewModel.PropertyChanged -= VmServerPropertyChanged;
                if (VmServerList.Contains(viewModel))
                {
                    VmServerList.Remove(viewModel);
                    SimpleLogHelper.Debug($"Remote server {viewModel.DisplayName} of `{viewModel.DataSourceName}` removed from list");
                }
                else
                {
                    SimpleLogHelper.Debug($"Remote server {viewModel.DisplayName} of `{viewModel.DataSourceName}` removed from list, but not found in list");
                }
                VmServerListDummyNode();
            });
        }

        public void VmServerListDummyNode()
        {
            Execute.OnUIThreadSync(() =>
            {
                if (SourceService.LocalDataSource != null)
                {
                    if (VmServerList.All(x => x.DataSource != SourceService.LocalDataSource))
                    {
                        VmServerList.Add(new ProtocolBaseViewModelDummy(SourceService.LocalDataSource!));
                        SimpleLogHelper.Debug($"Add dummy server for `{SourceService.LocalDataSource.DataSourceName}`");
                    }
                    else if (VmServerList.Any(x => x.DataSource == SourceService.LocalDataSource && x is not ProtocolBaseViewModelDummy)
                             && VmServerList.FirstOrDefault(x => x.DataSource == SourceService.LocalDataSource && x is ProtocolBaseViewModelDummy) is ProtocolBaseViewModelDummy dummy)
                    {
                        VmServerList.Remove(dummy);
                        SimpleLogHelper.Debug($"Remove dummy server for `{SourceService.LocalDataSource.DataSourceName}`");
                    }
                }
                foreach (var source in SourceService.AdditionalSources)
                {
                    if (VmServerList.All(x => x.DataSource != source.Value))
                    {
                        VmServerList.Add(new ProtocolBaseViewModelDummy(source.Value));
                        SimpleLogHelper.Debug($"Add dummy server for `{source.Value.DataSourceName}`");
                    }
                    else if (VmServerList.Any(x => x.DataSource == source.Value && x is not ProtocolBaseViewModelDummy)
                             && VmServerList.FirstOrDefault(x => x.DataSource == source.Value && x is ProtocolBaseViewModelDummy) is ProtocolBaseViewModelDummy dummy)
                    {
                        VmServerList.Remove(dummy);
                        SimpleLogHelper.Debug($"Remove dummy server for `{source.Value.DataSourceName}`");
                    }
                }
                IsAddToolTipShow = !VmServerList.Any(x => x is not ProtocolBaseViewModelDummy);
            });
            ApplySort();
        }

        private void RebuildVmServerList()
        {
            lock (this)
            {
                var list = AppData.VmItemList.ToList();
                Execute.OnUIThread(() =>
                {
                    VmServerList = new ObservableCollection<ProtocolBaseViewModel>(
                        // in order to implement the `custom` order 
                        // !!! VmItemList should order by CustomOrder by default
                        list.OrderBy(x => x.CustomOrder).ThenBy(x => x.Id)
                        );
                    SelectedServerViewModelListItem = null;
                    foreach (var vs in VmServerList)
                    {
                        vs.IsSelected = false;
                        vs.PropertyChanged -= VmServerPropertyChanged;
                        vs.PropertyChanged += VmServerPropertyChanged;
                    }
                    VmServerList.CollectionChanged += (s, e) =>
                    {
                        RaisePropertyChanged(nameof(IsAnySelected));
                        RaisePropertyChanged(nameof(IsSelectedAll));
                        RaisePropertyChanged(nameof(SelectedCount));
                    };

                    RaisePropertyChanged(nameof(IsAnySelected));
                    RaisePropertyChanged(nameof(IsSelectedAll));
                    RaisePropertyChanged(nameof(SelectedCount));
                    UpdateNote();

                    VmServerListDummyNode();
                    RaisePropertyChanged(nameof(VmServerList));
                    ApplySort();
                    RefreshCollectionViewSource(true);
                });
            }
        }

        private void VmServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProtocolBaseViewModel.IsSelected))
            {
                RaisePropertyChanged(nameof(IsAnySelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            }
        }

        public void ApplySort()
        {
            var orderBy = ServerOrderBy;
            if (this.View is ServerListPageView v)
            {
                Execute.OnUIThreadSync(() =>
                {
                    var cvs = CollectionViewSource.GetDefaultView(v.LvServerCards.ItemsSource);
                    if (cvs == null) return;

                    string propertyName = "";
                    var direction = ListSortDirection.Ascending;
                    switch (orderBy)
                    {
                        case EnumServerOrderBy.IdAsc:
                            propertyName = nameof(ProtocolBaseViewModel.Id);
                            direction = ListSortDirection.Ascending;
                            break;
                        case EnumServerOrderBy.Custom:
                            propertyName = nameof(ProtocolBaseViewModel.CustomOrder);
                            direction = ListSortDirection.Ascending;
                            break;
                        case EnumServerOrderBy.ProtocolAsc:
                            propertyName = nameof(ProtocolBaseViewModel.ProtocolDisplayNameInShort);
                            direction = ListSortDirection.Ascending;
                            break;
                        case EnumServerOrderBy.ProtocolDesc:
                            propertyName = nameof(ProtocolBaseViewModel.ProtocolDisplayNameInShort);
                            direction = ListSortDirection.Descending;
                            break;
                        case EnumServerOrderBy.NameAsc:
                            propertyName = nameof(ProtocolBaseViewModel.DisplayName);
                            direction = ListSortDirection.Ascending;
                            break;
                        case EnumServerOrderBy.NameDesc:
                            propertyName = nameof(ProtocolBaseViewModel.DisplayName);
                            direction = ListSortDirection.Descending;
                            break;
                        case EnumServerOrderBy.AddressAsc:
                            propertyName = nameof(ProtocolBaseViewModel.SubTitle);
                            direction = ListSortDirection.Ascending;
                            break;
                        case EnumServerOrderBy.AddressDesc:
                            propertyName = nameof(ProtocolBaseViewModel.SubTitle);
                            direction = ListSortDirection.Descending;
                            break;
                        default:
                            SimpleLogHelper.Error($"ApplySort: type {orderBy} is not supported");
                            MsAppCenterHelper.Error(new NotImplementedException($"ApplySort: type {orderBy} is not supported"));
                            break;
                    }

                    bool needRefresh = true;
                    if (cvs.SortDescriptions.Count == 2 && cvs.SortDescriptions[1] is var sd)
                    {
                        needRefresh = sd.PropertyName != propertyName || sd.Direction != direction;
                    }

                    if (needRefresh)
                    {
                        cvs.SortDescriptions.Clear();
                        cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.GroupedOrder), ListSortDirection.Ascending));
                        cvs.SortDescriptions.Add(new SortDescription(propertyName, direction));
                    }
                    //cvs.Refresh();


                    if (cvs.GroupDescriptions.Count == 0 && SourceService.AdditionalSources.Count > 0)
                    {
                        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProtocolBase.DataSource)));
                        SimpleLogHelper.Debug("GroupDescriptions = ProtocolBase.DataSource");
                    }
                    if (cvs.GroupDescriptions.Count > 0 && SourceService.AdditionalSources.Count == 0)
                    {
                        cvs.GroupDescriptions.Clear();
                        SimpleLogHelper.Debug("GroupDescriptions = null");
                    }
                });
            }
        }

        public Dictionary<ProtocolBaseViewModel, bool> IsServerVisible = new Dictionary<ProtocolBaseViewModel, bool>();
        private string _lastKeyword = string.Empty;
        public void RefreshCollectionViewSource(bool force = false)
        {
            var filter = IoC.Get<MainWindowViewModel>().MainFilterString.Trim();
            if (this.View is not ServerListPageView)
            {
                return;
            }

            if(_lastKeyword != filter || force)
                lock (this)
                {
                    List<ProtocolBaseViewModel> servers;
                    if (filter.StartsWith(_lastKeyword))
                    {
                        // calc only visible servers when filter is appended
                        servers = IsServerVisible.Where(x => x.Value == true).Select(x => x.Key).ToList();
                        foreach (var protocolBaseViewModel in IoC.Get<GlobalData>().VmItemList)
                        {
                            if (!servers.Contains(protocolBaseViewModel))
                                servers.Add(protocolBaseViewModel);
                        }
                    }
                    else
                    {
                        servers = IoC.Get<GlobalData>().VmItemList;
                        IsServerVisible.Clear();
                    }
                    _lastKeyword = filter;

                    var tmp = TagAndKeywordEncodeHelper.DecodeKeyword(filter);
                    TagFilters = tmp.TagFilterList;
                    var matchResults = TagAndKeywordEncodeHelper.MatchKeywords(servers.Select(x => x.Server).ToList(), tmp, false);
                    for (int i = 0; i < servers.Count; i++)
                    {
                        var vm = servers[i];
                        if (IsServerVisible.ContainsKey(vm))
                            IsServerVisible[vm] = matchResults[i].Item1;
                        else
                            IsServerVisible.Add(vm, matchResults[i].Item1);
                    }
                }

            Execute.OnUIThread(() =>
            {
                // MainFilterString changed -> refresh view source -> calc visible in `ServerListItemSource_OnFilter`
                CollectionViewSource.GetDefaultView((this.View as ServerListPageView)!.LvServerCards.ItemsSource).Refresh();
                // invoke ServerListPageView.cs => ServerListItemSource_OnFilter
            });
        }

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

        #region Commands

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
            if (source?.IsWritable != true)
            {
                MessageBoxHelper.ErrorAlert($"Can not add server to DataSource ({source?.DataSourceName ?? "null"}) since it is not writable.");
                return new Tuple<DataSourceBase?, string?>(null, null);
            }
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

                            source.Database_InsertServer(list);
                            AppData.ReloadServerList(true);
                            MessageBoxHelper.Info(IoC.Translate("import_done_0_items_added", list.Count.ToString()));
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

                            if(list.Count == 0)
                                return;

                            foreach (var server in list)
                            {
                                server.Id = string.Empty;
                            }

                            var ret = source.Database_InsertServer(list);
                            if (ret.IsSuccess)
                            {
                                AppData.ReloadServerList(true);
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
                                source.Database_InsertServer(list);
                                AppData.ReloadServerList(true);
                                MessageBoxHelper.Info(IoC.Translate("import_done_0_items_added", list.Count.ToString()));
                                return;
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
                            var servers = ss.Select(x => x.Server);
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



        private RelayCommand? _cmdCancelSelected;
        public RelayCommand CmdCancelSelected
        {
            get
            {
                Debug.Assert(SourceService != null);
                return _cmdCancelSelected ??= new RelayCommand((o) => { ClearSelection(); });
            }
        }



        private DateTime _lastCmdReOrder = DateTime.MinValue;
        private RelayCommand? _cmdReOrder;
        public RelayCommand CmdReOrder
        {
            get
            {
                return _cmdReOrder ??= new RelayCommand((o) =>
                {
                    if ((DateTime.Now - _lastCmdReOrder).TotalMilliseconds < 200)
                        return;
                    _lastCmdReOrder = DateTime.Now;

                    var newOrderBy = EnumServerOrderBy.IdAsc;
                    if (int.TryParse(o?.ToString() ?? "0", out var ot)
                        && ot is >= (int)EnumServerOrderBy.IdAsc and <= (int)EnumServerOrderBy.Custom)
                    {
                        newOrderBy = (EnumServerOrderBy)ot;
                    }
                    else if (o is EnumServerOrderBy x)
                    {
                        newOrderBy = x;
                    }

                    if (newOrderBy is EnumServerOrderBy.IdAsc or EnumServerOrderBy.Custom)
                    {
                        ServerOrderBy = newOrderBy;
                    }
                    else
                    {
                        try
                        {
                            // cancel order
                            if (ServerOrderBy == newOrderBy + 1)
                            {
                                newOrderBy = EnumServerOrderBy.IdAsc;
                            }
                            else if (ServerOrderBy == newOrderBy)
                            {
                                newOrderBy += 1;
                            }
                        }
                        catch
                        {
                            newOrderBy = EnumServerOrderBy.IdAsc;
                        }
                        finally
                        {
                            ServerOrderBy = newOrderBy;
                        }
                    }
                    ApplySort();
                });
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

        #endregion


        #region NoteField

        private RelayCommand? _cmdHideNoteField;
        public RelayCommand CmdHideNoteField
        {
            get
            {
                return _cmdHideNoteField ??= new RelayCommand((o) =>
                {
                    IoC.Get<ConfigurationService>().General.ShowNoteFieldInListView = false;
                    IoC.Get<ConfigurationService>().Save();
                    BriefNoteVisibility = Visibility.Collapsed;
                });
            }
        }

        private RelayCommand? _cmdShowNoteField;

        public RelayCommand CmdShowNoteField
        {
            get
            {
                return _cmdShowNoteField ??= new RelayCommand((o) =>
                {
                    IoC.Get<ConfigurationService>().General.ShowNoteFieldInListView = true;
                    IoC.Get<ConfigurationService>().Save();
                    BriefNoteVisibility = Visibility.Visible;
                });
            }
        }

        #endregion


        private RelayCommand? _cmdRefreshDataSource;
        public RelayCommand CmdRefreshDataSource
        {
            get
            {
                return _cmdRefreshDataSource ??= new RelayCommand((o) =>
                {
                    if (o is DataSourceBase dataSource)
                    {
                        if (dataSource.Status != EnumDatabaseStatus.OK)
                        {
                            dataSource.ReconnectTime = DateTime.MinValue;
                        }
                        else
                        {
                            AppData.CheckUpdateTime = DateTime.MinValue;
                        }
                    }
                });
            }
        }
    }
}