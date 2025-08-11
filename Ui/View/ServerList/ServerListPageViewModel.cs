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
using _1RM.Utils.Tracing;
using _1RM.View.Editor;
using _1RM.View.Utils;
using Newtonsoft.Json;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;

namespace _1RM.View.ServerList
{
    public partial class ServerListPageViewModel : ServerPageBase
    {


        #region properties

        public TagsPanelViewModel TagsPanelViewModel { get; }
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

        private bool _isAddToolTipShow = false;
        public bool IsAddToolTipShow
        {
            get => _isAddToolTipShow;
            set => SetAndNotifyIfChanged(ref _isAddToolTipShow, value);
        }

        public int SelectedCount => VmServerList.Count(x => x.IsSelected);


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

        #endregion

        public ServerListPageViewModel(DataSourceService sourceService, GlobalData appData) : base(sourceService, appData)
        {
            // Make sure the update do triggered the first time assign a value 
            BriefNoteVisibility = IoC.Get<ConfigurationService>().General.ShowNoteFieldInListView ? Visibility.Visible : Visibility.Collapsed;

        }

        public double NameWidth
        {
            get => LocalityListViewService.ServerListNameWidthGet();
            set
            {
                if (Math.Abs(value - LocalityListViewService.ServerListNameWidthGet()) > 0.001)
                {
                    LocalityListViewService.ServerListNameWidthSet(value);
                    RaisePropertyChanged();
                }
            }
        }

        public double NoteWidth
        {
            get => LocalityListViewService.ServerListNoteWidthGet();
            set
            {
                if (Math.Abs(value - LocalityListViewService.ServerListNoteWidthGet()) > 0.001)
                {
                    LocalityListViewService.ServerListNoteWidthSet(value);
                    RaisePropertyChanged();
                }
            }
        }

        protected override void OnViewLoaded()
        {
            ApplySort();
            IoC.Get<GlobalData>().OnReloadAll -= BuildView;
            IoC.Get<GlobalData>().OnReloadAll += BuildView;
            if (AppData.VmItemList.Count > 0)
            {
                // this view may be loaded after the data is loaded(when MainWindow start minimized)
                // so we need to rebuild the list here
                BuildView();
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

                if (VmServerList.Any(x => x is not ProtocolBaseViewModelDummy)
                    || IoC.Get<ConfigurationService>().AdditionalDataSource.Any())
                {
                    IsAddToolTipShow = false;
                }
                else
                {
                    IsAddToolTipShow = true;
                }
            });
            ApplySort();
        }

        public sealed override void BuildView()
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

        public override void ClearSelection()
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

        private void VmServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProtocolBaseViewModel.IsSelected))
            {
                RaisePropertyChanged(nameof(IsAnySelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            }
        }


        private EnumServerOrderBy _serverOrderBy;
        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set => SetAndNotifyIfChanged(ref this._serverOrderBy, value);
        }

        public void ApplySort()
        {
            var orderBy = IoC.Get<MainWindowViewModel>().ServerOrderBy;
            ServerOrderBy = orderBy;
            if (this.View is ServerListPageView v)
            {
                Execute.OnUIThreadSync(() =>
                {
                    if (CollectionViewSource.GetDefaultView(v.LvServerCards.ItemsSource) is not ListCollectionView cvs) return;

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
                            UnifyTracing.Error(new NotImplementedException($"ApplySort: type {orderBy} is not supported"));
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
                        if (propertyName == nameof(ProtocolBaseViewModel.SubTitle))
                        {
                            cvs.CustomSort = new SubTitleSortByNaturalIp(direction == ListSortDirection.Ascending);
                        }
                        else
                        {
                            cvs.SortDescriptions.Add(new SortDescription(nameof(ProtocolBaseViewModel.GroupedOrder), ListSortDirection.Ascending));
                            cvs.SortDescriptions.Add(new SortDescription(propertyName, direction));
                        }
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

            if (_lastKeyword != filter || force)
            {
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
                    var matchResults = TagAndKeywordEncodeHelper.MatchKeywords(servers.Select(x => x.Server).ToList(), tmp, true);
                    for (int i = 0; i < servers.Count; i++)
                    {
                        var vm = servers[i];
                        if (i < 0 || i >= matchResults.Count)
                        {
                            // we get error report here that i is out of range, so we add this check 2024.10.31 https://appcenter.ms/users/VShawn/apps/1Remote-1/crashes/errors/859400306/overview
                            UnifyTracing.Error(new Exception($"MatchKeywords: i({i}) is out of range(0-{matchResults.Count})"), new Dictionary<string, string>()
                            {
                                { "filter", filter },
                                { "servers.Count", servers.Count.ToString() },
                            });
                            continue;
                        }
                        else
                        {
                            if (IsServerVisible.ContainsKey(vm))
                                IsServerVisible[vm] = matchResults[i].Item1;
                            else
                                IsServerVisible.Add(vm, matchResults[i].Item1);
                        }
                    }
                }
            }

            Execute.OnUIThread(() =>
            {
                // MainFilterString changed -> refresh view source -> calc visible in `ServerListItemSource_OnFilter`
                if (this.View is ServerListPageView view && view.LvServerCards.ItemsSource != null)
                    CollectionViewSource.GetDefaultView(view.LvServerCards.ItemsSource).Refresh();
                // invoke ServerListPageView.cs => ServerListItemSource_OnFilter
            });
        }


        #region Commands







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
                    IoC.Get<MainWindowViewModel>().SetServerOrderBy(o);
                    ApplySort();
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