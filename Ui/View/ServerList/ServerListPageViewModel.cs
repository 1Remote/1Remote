using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.Utils.mRemoteNG;
using _1RM.Utils.RdpFile;
using _1RM.View.Editor;
using _1RM.View.Settings;
using _1RM.View.Utils;
using Newtonsoft.Json;
using Shawn.Utils;
using Shawn.Utils.Interface;
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


        #region properties

        public bool ListPageIsCardView
        {
            get => IoC.Get<SettingsPageViewModel>().ListPageIsCardView;
            set
            {
                if (IoC.Get<SettingsPageViewModel>().ListPageIsCardView != value)
                {
                    IoC.Get<SettingsPageViewModel>().ListPageIsCardView = value;
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

        private ObservableCollection<ProtocolBaseViewModel> _vmServerList = new ObservableCollection<ProtocolBaseViewModel>();
        public ObservableCollection<ProtocolBaseViewModel> VmServerList
        {
            get => _vmServerList;
            set
            {
                SetAndNotifyIfChanged(ref _vmServerList, value);
                SelectedServerViewModelListItem = null;
                _vmServerList.CollectionChanged += (s, e) =>
                {
                    RaisePropertyChanged(nameof(IsAnySelected));
                    RaisePropertyChanged(nameof(IsSelectedAll));
                    RaisePropertyChanged(nameof(SelectedCount));
                };
            }
        }

        public int SelectedCount => VmServerList.Count(x => x.IsSelected);

        private EnumServerOrderBy _serverOrderBy = EnumServerOrderBy.IdAsc;
        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set
            {
                if (SetAndNotifyIfChanged(ref _serverOrderBy, value))
                {
                    IoC.Get<LocalityService>().ServerOrderBy = value;
                }
            }
        }


        public bool? IsSelectedAll
        {
            get
            {
                var items = VmServerList.Where(x => x.IsVisible);
                if (items.Any(x => x.IsSelected))
                {
                    if (items.All(x => x.IsSelected))
                        return true;
                    else
                        return null;
                }
                else
                {
                    return false;
                }
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
                    foreach (var item in VmServerList.Where(x => x.HoverNoteDisplayControl != null))
                    {
                        if (item.HoverNoteDisplayControl != null)
                        {
                            item.HoverNoteDisplayControl.IsBriefNoteShown = value == Visibility.Visible;
                        }
                    }
                }
            }
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
                var showNoteFieldInListView = IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInListView;
                // Make sure the update do triggered the first time assign a value 
                _briefNoteVisibility = showNoteFieldInListView == false ? Visibility.Visible : Visibility.Collapsed;
                BriefNoteVisibility = showNoteFieldInListView == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        protected override void OnViewLoaded()
        {
            if (GlobalEventHelper.OnRequestDeleteServer == null)
            {
                GlobalEventHelper.OnRequestDeleteServer += server =>
                {
                    if (string.IsNullOrEmpty(server.Id) == false
                        && true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete_selected"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                    {
                        AppData.DeleteServer(server);
                    }
                };
            }


            AppData.OnDataReloaded += RebuildVmServerList;
            RebuildVmServerList();

            ServerOrderBy = IoC.Get<LocalityService>().ServerOrderBy;
            ApplySort(ServerOrderBy);
        }


        public void AppendServer(ProtocolBaseViewModel viewModel)
        {
            Execute.OnUIThread(() =>
            {
                viewModel.PropertyChanged -= VmServerPropertyChanged;
                viewModel.PropertyChanged += VmServerPropertyChanged;
                VmServerList.Add(viewModel);
            });
        }

        private void RebuildVmServerList()
        {
            lock (this)
            {
                Execute.OnUIThread(() =>
                {
                    VmServerList = new ObservableCollection<ProtocolBaseViewModel>(AppData.VmItemList);
                    ApplySort(ServerOrderBy);
                    RaisePropertyChanged(nameof(IsAnySelected));
                    RaisePropertyChanged(nameof(IsSelectedAll));
                    RaisePropertyChanged(nameof(SelectedCount));
                    foreach (var vs in VmServerList)
                    {
                        vs.IsSelected = false;
                        vs.PropertyChanged -= VmServerPropertyChanged;
                        vs.PropertyChanged += VmServerPropertyChanged;
                    }

                    if (this.View is ServerListPageView v)
                    {
                        var cvs = CollectionViewSource.GetDefaultView(v.LvServerCards.ItemsSource);
                        if (cvs != null)
                        {
                            if (SourceService.AdditionalSources.Count > 0)
                            {
                                if (cvs.GroupDescriptions.Count == 0)
                                {
                                    cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProtocolBase.DataSourceName)));
                                }
                            }

                            if (SourceService.AdditionalSources.Count == 0)
                            {
                                if (cvs.GroupDescriptions.Count > 0)
                                {
                                    cvs.GroupDescriptions.Clear();
                                }
                            }
                        }
                    }
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

        private void ApplySort(EnumServerOrderBy orderBy)
        {
            if (this.View is ServerListPageView v)
            {
                Execute.OnUIThread(() =>
                {
                    var cvs = CollectionViewSource.GetDefaultView(v.LvServerCards.ItemsSource);
                    if (cvs != null)
                    {
                        cvs.SortDescriptions.Clear();
                        switch (orderBy)
                        {
                            case EnumServerOrderBy.IdAsc:
                                break;
                            case EnumServerOrderBy.ProtocolAsc:
                                cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.ProtocolDisplayNameInShort), ListSortDirection.Ascending));
                                break;
                            case EnumServerOrderBy.ProtocolDesc:
                                cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.ProtocolDisplayNameInShort), ListSortDirection.Descending));
                                break;
                            case EnumServerOrderBy.NameAsc:
                                cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.DisplayName), ListSortDirection.Ascending));
                                break;
                            case EnumServerOrderBy.NameDesc:
                                cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.DisplayName), ListSortDirection.Descending));
                                break;
                            case EnumServerOrderBy.AddressAsc:
                                cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.SubTitle), ListSortDirection.Ascending));
                                break;
                            case EnumServerOrderBy.AddressDesc:
                                cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.SubTitle), ListSortDirection.Descending));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        //cvs.Refresh();
                    }
                });
            }
        }

        public void RefreshCollectionViewSource()
        {
            if (this.View is ServerListPageView v)
            {
                Execute.OnUIThread(() => { CollectionViewSource.GetDefaultView(v.LvServerCards.ItemsSource).Refresh(); });
            }
        }

        public void ClearSelection()
        {
            foreach (var item in VmServerList)
            {
                item.IsSelected = false;
            }

            RefreshHeaderCheckBox();
        }

        public void RefreshHeaderCheckBox()
        {
            if (this.View is ServerListPageView view)
            {
                view.RefreshHeaderCheckBox();
            }
        }


        private string _filterString2 = "";
        private TagAndKeywordEncodeHelper.KeywordDecoded? _keywordDecoded = null;
        public bool TestMatchKeywords(ProtocolBase server)
        {
            string filterString = IoC.Get<MainWindowViewModel>().MainFilterString;
            if (_filterString2 != filterString)
            {
                _filterString2 = filterString;
                _keywordDecoded = TagAndKeywordEncodeHelper.DecodeKeyword(filterString);
                TagFilters = _keywordDecoded.TagFilterList;
            }

            if (_keywordDecoded == null)
                return true;

            var s = TagAndKeywordEncodeHelper.MatchKeywords(server, _keywordDecoded);
            return s.Item1;
        }


        #region Commands

        private RelayCommand? _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand((o) =>
                {
                    if (this.View is ServerListPageView view)
                        view.CbPopForInExport.IsChecked = false;
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(Enumerable.Where<TagFilter>(TagFilters, x => x.IsIncluded == true).Select(x => x.TagName).ToList());
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
                        view.CbPopForInExport.IsChecked = false;
                        var path = SelectFileHelper.SaveFile(
                            title: IoC.Get<ILanguageService>().Translate("Caution: Your data will be saved unencrypted!"),
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
                        MessageBoxHelper.Info($"{IoC.Get<ILanguageService>().Translate("Export")}: {IoC.Get<ILanguageService>().Translate("Done")}!");
                    }

                }, o => VmServerList.Where(x => x.IsSelected == true).All(x => x.IsEditable));
            }
        }



        private RelayCommand? _cmdImportFromJson;
        public RelayCommand CmdImportFromJson
        {
            get
            {
                return _cmdImportFromJson ??= new RelayCommand((o) =>
                {
                    // select save to which source
                    DataSourceBase? source = null;
                    if (IoC.Get<ConfigurationService>().AdditionalDataSource.Any(x => x.Status == EnumDatabaseStatus.OK))
                    {
                        var vm = new DataSourceSelectorViewModel();
                        if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) != true)
                            return;
                        source = SourceService.GetDataSource(vm.SelectedSource.DataSourceName);
                    }
                    else
                    {
                        source = SourceService.LocalDataSource;
                    }
                    if (source == null) return;


                    if (this.View is ServerListPageView view)
                        view.CbPopForInExport.IsChecked = false;
                    var path = SelectFileHelper.OpenFile(title: IoC.Get<ILanguageService>().Translate("import_server_dialog_title"), filter: "json|*.json|*.*|*.*");
                    if (path == null) return;

                    var id = MaskLayerController.ShowProcessingRing(IoC.Get<ILanguageService>().Translate("system_options_data_security_info_data_processing"), IoC.Get<MainWindowViewModel>());
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var list = new List<ProtocolBase>();
                            var jobj = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(path, Encoding.UTF8)) ?? new List<object>();
                            foreach (var json in jobj)
                            {
                                var server = ItemCreateHelper.CreateFromJsonString(json.ToString()!);
                                if (server != null)
                                {
                                    server.Id = string.Empty;
                                    list.Add(server);
                                }
                            }

                            source.Database_InsertServer(list);
                            AppData.ReloadServerList(true);
                            MessageBoxHelper.Info(IoC.Get<ILanguageService>().Translate("import_done_0_items_added", list.Count.ToString()));
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Warning(e);
                            MessageBoxHelper.ErrorAlert(IoC.Get<ILanguageService>().Translate("import_failure_with_data_format_error"));
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
                    // select save to which source
                    DataSourceBase? source = null;
                    if (IoC.Get<ConfigurationService>().AdditionalDataSource.Any(x => x.Status == EnumDatabaseStatus.OK))
                    {
                        var vm = new DataSourceSelectorViewModel();
                        if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) != true)
                            return;
                        source = SourceService.GetDataSource(vm.SelectedSource.DataSourceName);
                    }
                    else
                    {
                        source = SourceService.LocalDataSource;
                    }
                    if (source == null) return;


                    var path = SelectFileHelper.OpenFile(title: IoC.Get<ILanguageService>().Translate("import_server_dialog_title"), filter: "csv|*.csv");
                    if (path == null) return;
                    var id = MaskLayerController.ShowProcessingRing(IoC.Get<ILanguageService>().Translate("system_options_data_security_info_data_processing"), IoC.Get<MainWindowViewModel>());
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var list = MRemoteNgImporter.FromCsv(path, ServerIcons.Instance.IconsBase64);
                            if (list?.Count > 0)
                            {
                                source.Database_InsertServer(list);
                                AppData.ReloadServerList(true);
                                MessageBoxHelper.Info(IoC.Get<ILanguageService>().Translate("import_done_0_items_added", list.Count.ToString()));
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Debug(e);
                            MessageBoxHelper.Info(IoC.Get<ILanguageService>().Translate("import_failure_with_data_format_error"));
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
                    // select save to which source
                    DataSourceBase? source = null;
                    if (IoC.Get<ConfigurationService>().AdditionalDataSource.Any(x => x.Status == EnumDatabaseStatus.OK))
                    {
                        var vm = new DataSourceSelectorViewModel();
                        if (IoC.Get<IWindowManager>().ShowDialog(vm, IoC.Get<MainWindowViewModel>()) != true)
                            return;
                        source = SourceService.GetDataSource(vm.SelectedSource.DataSourceName);
                    }
                    else
                    {
                        source = SourceService.LocalDataSource;
                    }
                    if (source == null) return;


                    var path = SelectFileHelper.OpenFile(title: IoC.Get<ILanguageService>().Translate("import_server_dialog_title"), filter: "rdp|*.rdp");
                    if (path == null) return;

                    try
                    {
                        var config = RdpConfig.FromRdpFile(path);
                        if (config != null)
                        {
                            var rdp = RDP.FromRdpConfig(config, ServerIcons.Instance.IconsBase64);

                            try
                            {
                                // try read user name & password from CredentialManagement.
                                using var cred = new CredentialManagement.Credential()
                                {
                                    Target = "TERMSRV/" + rdp.Address,
                                };
                                if (cred.Load())
                                {
                                    rdp.UserName = cred.Username;
                                    rdp.Password = cred.Password;
                                }
                            }
                            catch (Exception)
                            {
                                // ignored
                            }

                            if (AppData.AddServer(rdp, source))
                            {
                                MessageBoxHelper.Info(IoC.Get<ILanguageService>().Translate("import_done_0_items_added", "1"));
                                return;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Debug(e);
                    }
                    MessageBoxHelper.Info(IoC.Get<ILanguageService>().Translate("import_failure_with_data_format_error"));
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
                    if (!(ss?.Count > 0)) return;
                    if (true == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete_selected"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                    {
                        var servers = ss.Select(x => x.Server);
                        AppData.DeleteServer(servers);
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
                    var vms = VmServerList.Where(x => x.IsSelected && x.IsEditable);
                    if (vms.Any() == true)
                    {
                        GlobalEventHelper.OnRequestGoToServerMultipleEditPage?.Invoke(vms.Select(x => x.Server), true);
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
                    if (int.TryParse(o?.ToString() ?? "0", out int ot))
                    {
                        if ((DateTime.Now - _lastCmdReOrder).TotalMilliseconds > 200)
                        {
                            _lastCmdReOrder = DateTime.Now;
                            // cancel order
                            if (ServerOrderBy == (EnumServerOrderBy)(ot + 1))
                            {
                                ot = -1;
                            }
                            else if (ServerOrderBy == (EnumServerOrderBy)ot)
                            {
                                ++ot;
                            }

                            ServerOrderBy = (EnumServerOrderBy)ot;
                            ApplySort(ServerOrderBy);
                        }
                    }
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
                    var selected = VmServerList.Where(x => x.IsSelected == true).ToArray();
                    string token = "";
                    // set tab token, show in new tab
                    if (selected.Length > 1)
                        token = DateTime.Now.Ticks.ToString();
                    foreach (var vmProtocolServer in VmServerList.Where(x => x.IsSelected == true).ToArray())
                    {
                        GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Server, fromView: $"{nameof(MainWindowView)}", assignTabToken: token);
                        Thread.Sleep(50);
                    }
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
                    IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInListView = false;
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
                    IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInListView = true;
                    IoC.Get<ConfigurationService>().Save();
                    BriefNoteVisibility = Visibility.Visible;
                });
            }
        }

        #endregion
    }
}