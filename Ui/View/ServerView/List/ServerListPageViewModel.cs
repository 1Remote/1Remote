using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.Utils.Tracing;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.ServerView
{
    public partial class ServerListPageViewModel : ServerPageViewModelBase
    {
        #region properties
        private EnumServerViewStatus _currentViewInListPage;
        public EnumServerViewStatus CurrentViewInListPage
        {
            get => _currentViewInListPage;
            set => SetAndNotifyIfChanged(ref _currentViewInListPage, value);
        }
        
        private bool _isAddToolTipShow = false;
        public bool IsAddToolTipShow
        {
            get => _isAddToolTipShow;
            set => SetAndNotifyIfChanged(ref _isAddToolTipShow, value);
        }



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
            
            // Register CollectionChanged handler once to update selection-related properties
            VmServerList.CollectionChanged += (s, e) =>
            {
                RaisePropertyChanged(nameof(IsAnySelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            };
        }

        public double NameWidth
        {
            get => LocalityListViewService.Settings.ServerListNameWidth;
            set
            {
                LocalityListViewService.ServerListNameWidthSet(value);
                RaisePropertyChanged();
            }
        }

        public double NoteWidth
        {
            get => LocalityListViewService.Settings.ServerListNoteWidth;
            set
            {
                LocalityListViewService.ServerListNoteWidthSet(value);
                RaisePropertyChanged();
            }
        }

        protected override void OnViewLoaded()
        {
            ApplySort();
            base.OnViewLoaded();
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
            // Optimize by reducing UI thread operations and batching changes
            var dummiesNeedToAdd = new List<ProtocolBaseViewModelDummy>();
            var dummiesNeedToRemove = new List<ProtocolBaseViewModelDummy>();
            bool shouldShowTooltip = false;

            // Collect all changes first before executing on UI thread
            if (SourceService.LocalDataSource != null)
            {
                if (VmServerList.All(x => x.DataSource != SourceService.LocalDataSource))
                {
                    dummiesNeedToAdd.Add(new ProtocolBaseViewModelDummy(SourceService.LocalDataSource!));
                    SimpleLogHelper.Debug($"Add dummy server for `{SourceService.LocalDataSource.DataSourceName}`");
                }
                else if (VmServerList.Any(x => x.DataSource == SourceService.LocalDataSource && x is not ProtocolBaseViewModelDummy)
                         && VmServerList.FirstOrDefault(x => x.DataSource == SourceService.LocalDataSource && x is ProtocolBaseViewModelDummy) is ProtocolBaseViewModelDummy dummy)
                {
                    dummiesNeedToRemove.Add(dummy);
                    SimpleLogHelper.Debug($"Remove dummy server for `{SourceService.LocalDataSource.DataSourceName}`");
                }
            }

            foreach (var source in SourceService.AdditionalSources)
            {
                if (VmServerList.All(x => x.DataSource != source.Value))
                {
                    dummiesNeedToAdd.Add(new ProtocolBaseViewModelDummy(source.Value));
                    SimpleLogHelper.Debug($"Add dummy server for `{source.Value.DataSourceName}`");
                }
                else if (VmServerList.Any(x => x.DataSource == source.Value && x is not ProtocolBaseViewModelDummy)
                         && VmServerList.FirstOrDefault(x => x.DataSource == source.Value && x is ProtocolBaseViewModelDummy) is ProtocolBaseViewModelDummy dummy)
                {
                    dummiesNeedToRemove.Add(dummy);
                    SimpleLogHelper.Debug($"Remove dummy server for `{source.Value.DataSourceName}`");
                }
            }

            shouldShowTooltip = !VmServerList.Any(x => x is not ProtocolBaseViewModelDummy) 
                               && !IoC.Get<ConfigurationService>().AdditionalDataSource.Any();

            // Apply all changes in a single UI thread operation
            if (dummiesNeedToAdd.Any() || dummiesNeedToRemove.Any() || IsAddToolTipShow != shouldShowTooltip)
            {
                Execute.OnUIThreadSync(() =>
                {
                    foreach (var dummy in dummiesNeedToRemove)
                    {
                        VmServerList.Remove(dummy);
                    }
                    foreach (var dummy in dummiesNeedToAdd)
                    {
                        VmServerList.Add(dummy);
                    }
                    IsAddToolTipShow = shouldShowTooltip;
                });
            }
            
            ApplySort();
        }

        public sealed override void BuildView()
        {
            lock (this)
            {
                var list = AppData.VmItemList.ToList();
                Execute.OnUIThread(() =>
                {
                    // Unsubscribe from existing items
                    foreach (var vs in VmServerList)
                    {
                        vs.PropertyChanged -= VmServerPropertyChanged;
                    }

                    // Prepare new list with proper ordering
                    var newList = list.OrderBy(x => x.CustomOrder).ThenBy(x => x.Id).ToList();
                    
                    // Clear and repopulate the existing collection instead of replacing it
                    // This prevents race conditions with VirtualizingWrapPanel during layout operations
                    VmServerList.Clear();
                    foreach (var item in newList)
                    {
                        VmServerList.Add(item);
                    }

                    SelectedServerViewModel = null;
                    foreach (var vs in VmServerList)
                    {
                        vs.IsSelected = false;
                        vs.PropertyChanged += VmServerPropertyChanged;
                    }

                    RaisePropertyChanged(nameof(IsAnySelected));
                    RaisePropertyChanged(nameof(IsSelectedAll));
                    RaisePropertyChanged(nameof(SelectedCount));
                    UpdateNote();

                    VmServerListDummyNode();
                    ApplySort();
                    CalcServerVisibleAndRefresh(true);

                    SimpleLogHelper.Debug($"[{this.GetHashCode()}] ListView rebuilt with {AppData.VmItemList.Count} servers");
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


        private EnumServerOrderBy _serverOrderBy;
        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set => SetAndNotifyIfChanged(ref this._serverOrderBy, value);
        }

        public override void ApplySort()
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

        private bool _isRefreshScheduled = false;
        private readonly object _refreshLock = new object();

        public sealed override void CalcServerVisibleAndRefresh(bool force = false, bool matchSubTitle = true)
        {
            base.CalcServerVisibleAndRefresh(force, matchSubTitle);
            
            // Debounce UI refresh to avoid excessive CollectionView.Refresh() calls
            lock (_refreshLock)
            {
                if (_isRefreshScheduled) return;
                _isRefreshScheduled = true;
            }

            Execute.OnUIThread(() =>
            {
                try
                {
                    if (this.View is ServerListPageView view && view.LvServerCards.ItemsSource != null)
                        CollectionViewSource.GetDefaultView(view.LvServerCards.ItemsSource).Refresh();
                }
                finally
                {
                    lock (_refreshLock)
                    {
                        _isRefreshScheduled = false;
                    }
                }
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