using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Controls;
using PRM.Core;
using PRM.Core.Helper;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Service;
using PRM.Core.Utils.mRemoteNG;
using PRM.Model;
using PRM.Resources.Icons;
using PRM.ViewModel.Configuration;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public partial class VmServerListPage : NotifyPropertyChangedBase
    {
        #region singleton
        private static VmServerListPage _uniqueInstance = null;
        private static readonly object InstanceLock = new object();
        public static VmServerListPage Instance() => _uniqueInstance;
        public static VmServerListPage Instance(PrmContext context, ConfigurationViewModel configurationView, ListBox list)
        {
            if (_uniqueInstance == null)
                _uniqueInstance = new VmServerListPage(context, configurationView, list);
            return _uniqueInstance;
        } 

        #endregion singleton


        public PrmContext Context { get; }
        public ConfigurationViewModel ConfigurationViewModel { get; }
        private readonly ListBox _list;


        public bool ListPageIsCardView
        {
            get => ConfigurationViewModel.ListPageIsCardView;
            set
            {
                if (ConfigurationViewModel.ListPageIsCardView != value)
                {
                    ConfigurationViewModel.ListPageIsCardView = value;
                    RaisePropertyChanged();
                }
            }
        }

        protected VmServerListPage(PrmContext context, ConfigurationViewModel configurationView, ListBox list)
        {
            Context = context;
            ConfigurationViewModel = configurationView;
            _list = list;
            RebuildVmServerList();
            Context.AppData.VmItemListDataChanged += RebuildVmServerList;

            Context.AppData.OnMainWindowServerFilterChanged += new Action<string>(s =>
            {
                CalcVisible();
                RaisePropertyChanged(nameof(IsMultipleSelected));
            });

            if (GlobalEventHelper.OnRequestDeleteServer == null)
                GlobalEventHelper.OnRequestDeleteServer += id =>
                {
                    if (MessageBoxResult.Yes == MessageBox.Show(
                        Context.LanguageService.Translate("confirm_to_delete_selected"),
                        Context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.None))
                    {
                        Context.AppData.DeleteServer(id, true);
                    }
                };
        }


        private VmProtocolServer _selectedServerListItem = null;
        public VmProtocolServer SelectedServerListItem
        {
            get => _selectedServerListItem;
            set => SetAndNotifyIfChanged(ref _selectedServerListItem, value);
        }

        private ObservableCollection<VmProtocolServer> _serverListItems = new ObservableCollection<VmProtocolServer>();
        /// <summary>
        /// AllServerList data source for list view
        /// </summary>
        public ObservableCollection<VmProtocolServer> ServerListItems
        {
            get => _serverListItems;
            set
            {
                SetAndNotifyIfChanged(ref _serverListItems, value);
                OrderServerList();
                ServerListItems.CollectionChanged += (sender, args) => { OrderServerList(); };
            }
        }
        public int SelectedCount => ServerListItems.Count(x => x.IsSelected);


        private bool _isSelectedAll;
        /// <summary>
        /// MultipleSelected all
        /// </summary>
        public bool IsSelectedAll
        {
            get => _isSelectedAll;
            set
            {
                SetAndNotifyIfChanged(ref _isSelectedAll, value);
                foreach (var vmServerCard in ServerListItems)
                {
                    if (vmServerCard.ObjectVisibilityInList == Visibility.Visible)
                        vmServerCard.IsSelected = value;
                    else
                        vmServerCard.IsSelected = false;
                }
            }
        }

        public bool IsMultipleSelected => ServerListItems.Count(x => x.IsSelected) > 0;

        private void RebuildVmServerList()
        {
            _serverListItems.Clear();
            foreach (var vs in Context.AppData.VmItemList)
            {
                ServerListItems.Add(vs);
                try
                {
                    vs.PropertyChanged -= VmServerPropertyChanged;
                }
                finally
                {
                    vs.PropertyChanged += VmServerPropertyChanged;
                }
            }
            OrderServerList();
            ReadTagsFromServers();
            RaisePropertyChanged(nameof(IsMultipleSelected));
        }

        private void VmServerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VmProtocolServer.IsSelected))
            {
                var displayCount = ServerListItems.Count(x => x.ObjectVisibilityInList == Visibility.Visible);
                var selectedCount = ServerListItems.Count(x => x.IsSelected);

                RaisePropertyChanged(nameof(IsMultipleSelected));
                if (IsSelectedAll == true && selectedCount < displayCount)
                {
                    _isSelectedAll = false;
                }
                else if (IsSelectedAll == false && selectedCount >= displayCount)
                {
                    _isSelectedAll = true;
                }
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            }
        }

        private void OrderServerList()
        {
            if (!(ServerListItems?.Count > 0) || _list?.ItemsSource == null) return;

            ICollectionView dataView = CollectionViewSource.GetDefaultView(_list.ItemsSource);
            dataView.SortDescriptions.Clear();
            switch (ServerOrderBy)
            {
                case EnumServerOrderBy.ProtocolAsc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.Protocol), ListSortDirection.Ascending));
                    break;

                case EnumServerOrderBy.ProtocolDesc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.Protocol), ListSortDirection.Descending));
                    break;

                case EnumServerOrderBy.NameAsc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.DisplayName), ListSortDirection.Ascending));
                    break;

                case EnumServerOrderBy.NameDesc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.DisplayName), ListSortDirection.Descending));
                    break;

                //case EnumServerOrderBy.TagAsc:
                //    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.Tags), ListSortDirection.Ascending));
                //    break;

                //case EnumServerOrderBy.TagDesc:
                //    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.Tags), ListSortDirection.Descending));
                //    break;

                case EnumServerOrderBy.AddressAsc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerWithAddrPortBase.Address), ListSortDirection.Ascending));
                    break;

                case EnumServerOrderBy.AddressDesc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerWithAddrPortBase.Address), ListSortDirection.Descending));
                    break;

                default:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerWithAddrPortBase.Id), ListSortDirection.Ascending));
                    break;
            }

            dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server.Id), ListSortDirection.Ascending));
            dataView.Refresh();
            SimpleLogHelper.Debug($"OrderServerList: by {ServerOrderBy}");
            RaisePropertyChanged(nameof(IsMultipleSelected));
        }

        private EnumServerOrderBy _serverOrderBy = EnumServerOrderBy.IdAsc;
        public EnumServerOrderBy ServerOrderBy
        {
            get => _serverOrderBy;
            set
            {
                if (SetAndNotifyIfChanged(ref _serverOrderBy, value))
                {
                    Context.LocalityService.ServerOrderBy = value;
                }
            }
        }

        private void CalcVisible()
        {
            var keyWord = Context.AppData.MainWindowServerFilter;
            var keyWords = keyWord.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (SelectedTabName != TabTagsListName)
            {
                var tagFilters = TagFilters;
                foreach (var vm in ServerListItems)
                {
                    var server = vm.Server;
                    bool bTagMatched = true;
                    foreach (var tagFilter in tagFilters)
                    {
                        if (tagFilter.IsNegative == false && server.Tags.Contains(tagFilter.TagName) == false)
                        {
                            bTagMatched = false;
                            break;
                        }
                        if (tagFilter.IsNegative == true && server.Tags.Contains(tagFilter.TagName) == true)
                        {
                            bTagMatched = false;
                            break;
                        }
                    }

                    if (!bTagMatched)
                    {
                        vm.ObjectVisibilityInList = Visibility.Collapsed;
                    }
                    else if (keyWords.Length == 0)
                    {
                        vm.ObjectVisibilityInList = Visibility.Visible;
                    }
                    else
                    {
                        var dispName = server.DisplayName;
                        var subTitle = server.SubTitle;
                        var matched = Context.KeywordMatchService.Match(new List<string>() { dispName, subTitle }, keyWords).IsMatchAllKeywords;
                        if (matched)
                            vm.ObjectVisibilityInList = Visibility.Visible;
                        else
                            vm.ObjectVisibilityInList = Visibility.Collapsed;
                    }

                    if (vm.ObjectVisibilityInList == Visibility.Collapsed && vm.IsSelected)
                        vm.IsSelected = false;
                }

                if (ServerListItems.Where(x => x.ObjectVisibilityInList == Visibility.Visible).All(x => x.IsSelected))
                    _isSelectedAll = true;
                else
                    _isSelectedAll = false;
                RaisePropertyChanged(nameof(IsSelectedAll));
            }
            else
            {
                foreach (var tag in Tags)
                {
                    if (string.IsNullOrEmpty(keyWord))
                    {
                        tag.ObjectVisibilityInList = Visibility.Visible;
                    }
                    else
                    {
                        var matched = Context.KeywordMatchService.Match(new List<string>() { tag.Name }, keyWords).IsMatchAllKeywords;
                        if (matched)
                            tag.ObjectVisibilityInList = Visibility.Visible;
                        else
                            tag.ObjectVisibilityInList = Visibility.Collapsed;
                    }
                }
            }
        }


        private RelayCommand _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(TagFilters.Where(x => x.IsNegative == false).Select(x => x.TagName).ToList());
                });
            }
        }



        private RelayCommand _cmdExportSelectedToJson;
        public RelayCommand CmdExportSelectedToJson
        {
            get
            {
                return _cmdExportSelectedToJson ??= new RelayCommand((isExportAll) =>
                {
                    var path = SelectFileHelper.SaveFile(title: Context.LanguageService.Translate("system_options_data_security_export_dialog_title"),
                        filter: "PRM json array|*.prma",
                        selectedFileName: DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma");
                    if (path == null) return;
                    var list = new List<ProtocolServerBase>();
                    if (isExportAll != null || ServerListItems.All(x => x.IsSelected == false))
                        foreach (var vs in Context.AppData.VmItemList)
                        {
                            var serverBase = (ProtocolServerBase)vs.Server.Clone();
                            Context.DataService.DecryptToConnectLevel(serverBase);
                            list.Add(serverBase);
                        }
                    else
                        foreach (var vs in ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedTabName) || x.Server.Tags?.Contains(SelectedTabName) == true) && x.IsSelected == true))
                        {
                            var serverBase = (ProtocolServerBase)vs.Server.Clone();
                            Context.DataService.DecryptToConnectLevel(serverBase);
                            list.Add(serverBase);
                        }

                    File.WriteAllText(path, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                });
            }
        }



        private RelayCommand _cmdImportFromJson;
        public RelayCommand CmdImportFromJson
        {
            get
            {
                return _cmdImportFromJson ??= new RelayCommand((o) =>
                {
                    var path = SelectFileHelper.OpenFile(title: Context.LanguageService.Translate("import_server_dialog_title"), filter: "PRM json array|*.prma");
                    if (path == null) return;
                    try
                    {
                        var list = new List<ProtocolServerBase>();
                        var jobj = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(path, Encoding.UTF8));
                        foreach (var json in jobj)
                        {
                            var server = ItemCreateHelper.CreateFromJsonString(json.ToString());
                            if (server != null)
                            {
                                server.Id = 0;
                                list.Add(server);
                                Context.DataService.Database_InsertServer(server);
                            }
                        }

                        Context.AppData.ReloadServerList();
                        MessageBox.Show(Context.LanguageService.Translate("import_done_0_items_added").Replace("{0}", list.Count.ToString()), Context.LanguageService.Translate("messagebox_title_info"), MessageBoxButton.OK,
                            MessageBoxImage.None, MessageBoxResult.None);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Debug(e);
                        MessageBox.Show(Context.LanguageService.Translate("import_failure_with_data_format_error"), Context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error,
                            MessageBoxResult.None);
                    }
                });
            }
        }



        private RelayCommand _cmdImportFromCsv;
        public RelayCommand CmdImportFromCsv
        {
            get
            {
                return _cmdImportFromCsv ??= new RelayCommand((o) =>
                {
                    var path = SelectFileHelper.OpenFile(title: Context.LanguageService.Translate("import_server_dialog_title"), filter: "csv|*.csv");
                    if (path == null) return;
                    try
                    {
                        var list = MRemoteNgImporter.FromCsv(path, ServerIcons.Instance.Icons);
                        if (list?.Count > 0)
                        {
                            foreach (var serverBase in list)
                            {
                                Context.DataService.Database_InsertServer(serverBase);
                            }

                            Context.AppData.ReloadServerList();
                            MessageBox.Show(Context.LanguageService.Translate("import_done_0_items_added").Replace("{0}", list.Count.ToString()), Context.LanguageService.Translate("messagebox_title_info"), MessageBoxButton.OK,
                                MessageBoxImage.None, MessageBoxResult.None);
                        }
                        else
                            MessageBox.Show(Context.LanguageService.Translate("import_failure_with_data_format_error"), Context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error,
                                MessageBoxResult.None);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Debug(e);
                        MessageBox.Show(Context.LanguageService.Translate("import_failure_with_data_format_error") + $": {e.Message}", Context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK,
                            MessageBoxImage.Error, MessageBoxResult.None);
                    }
                });
            }
        }



        private RelayCommand _cmdDelete;
        public RelayCommand CmdDelete
        {
            get
            {
                return _cmdDelete ??= new RelayCommand((o) =>
                {
                    if (o is int id)
                    {
                        GlobalEventHelper.OnRequestDeleteServer?.Invoke(id);
                    }
                });
            }
        }

        private RelayCommand _cmdDeleteSelected;
        public RelayCommand CmdDeleteSelected
        {
            get
            {
                return _cmdDeleteSelected ??= new RelayCommand((o) =>
                {
                    var ss = ServerListItems.Where(x => x.ObjectVisibilityInList == Visibility.Visible && x.IsSelected == true).ToList();
                    if (!(ss?.Count > 0)) return;
                    if (MessageBoxResult.Yes == MessageBox.Show(
                        Context.LanguageService.Translate("confirm_to_delete_selected"),
                        Context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.None))
                    {
                        foreach (var vs in ss)
                        {
                            Context.AppData.DeleteServer(vs.Server.Id, false);
                        }
                        Context.AppData.ReloadServerList();
                    }
                }, o => ServerListItems.Any(x => x.ObjectVisibilityInList == Visibility.Visible && x.IsSelected == true));
            }
        }



        private RelayCommand _cmdMultiEditSelected;
        public RelayCommand CmdMultiEditSelected
        {
            get
            {
                return _cmdMultiEditSelected ??= new RelayCommand((o) =>
                    {
                        GlobalEventHelper.OnRequestGoToServerMultipleEditPage?.Invoke(ServerListItems.Where(x => x.IsSelected).Select(x => x.Server), true);
                    }, o => App.MainUi?.Vm?.DispPage == null && ServerListItems.Any(x => x.ObjectVisibilityInList == Visibility.Visible && x.IsSelected == true));
            }
        }



        private RelayCommand _cmdCancelSelected;
        public RelayCommand CmdCancelSelected
        {
            get
            {
                return _cmdCancelSelected ??= new RelayCommand((o) => { Context.AppData.UnselectAllServers(); });
            }
        }



        private DateTime _lastCmdReOrder;
        private RelayCommand _cmdReOrder;
        public RelayCommand CmdReOrder
        {
            get
            {
                return _cmdReOrder ??= new RelayCommand((o) =>
                {
                    if (int.TryParse(o.ToString(), out int ot))
                    {
                        if ((DateTime.Now - _lastCmdReOrder).TotalMilliseconds > 200)
                        {
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
                            OrderServerList();
                            _lastCmdReOrder = DateTime.Now;
                        }
                    }
                });
            }
        }



        private RelayCommand _cmdConnectSelected;
        public RelayCommand CmdConnectSelected
        {
            get
            {
                return _cmdConnectSelected ??= new RelayCommand((o) =>
                {
                    var token = DateTime.Now.Ticks.ToString();
                    foreach (var vmProtocolServer in ServerListItems.Where(x => x.IsSelected == true).ToArray())
                    {
                        GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Id, token);
                        Thread.Sleep(50);
                    }
                });
            }
        }

    }
}