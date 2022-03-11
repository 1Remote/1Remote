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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
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
using PRM.Utils.Filters;
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
        public static VmServerListPage Instance(PrmContext context, ConfigurationViewModel configurationView, ListBox list, VmMain vmMain)
        {
            if (_uniqueInstance == null)
                _uniqueInstance = new VmServerListPage(context, configurationView, list, vmMain);
            return _uniqueInstance;
        }
        #endregion singleton


        public PrmContext Context { get; }
        public ConfigurationViewModel ConfigurationViewModel { get; }
        private readonly ListBox _list;
        private readonly VmMain _vmMain;

        #region properties

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

        private VmProtocolServer _selectedServerListItem = null;
        public VmProtocolServer SelectedServerListItem
        {
            get => _selectedServerListItem;
            set => SetAndNotifyIfChanged(ref _selectedServerListItem, value);
        }

        private ObservableCollection<VmProtocolServer> _serverListItems = new ObservableCollection<VmProtocolServer>();
        public ObservableCollection<VmProtocolServer> ServerListItems
        {
            get => _serverListItems;
            set
            {
                _serverListItems = new ObservableCollection<VmProtocolServer>(GetOrderedVmProtocolServers(value, ServerOrderBy));
                ServerListItems.CollectionChanged += (sender, args) =>
                {
                    ServerListItems = new ObservableCollection<VmProtocolServer>(ServerListItems);
                };
                RaisePropertyChanged();
            }
        }
        public int SelectedCount => ServerListItems.Count(x => x.IsSelected);

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

        public bool IsSelectedAll
        {
            get
            {
                return ServerListItems.All(x => x.IsSelected);
            }
            set
            {
                foreach (var vmServerCard in ServerListItems)
                {
                    vmServerCard.IsSelected = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool IsMultipleSelected => ServerListItems.Count(x => x.IsSelected) > 0;

        #endregion

        protected VmServerListPage(PrmContext context, ConfigurationViewModel configurationView, ListBox list, VmMain vmMain)
        {
            Context = context;
            ConfigurationViewModel = configurationView;
            _list = list;
            _vmMain = vmMain;
            RebuildVmServerList();
            Context.AppData.VmItemListDataChanged += RebuildVmServerList;


            _vmMain.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(VmMain.FilterString))
                {

                    Task.Factory.StartNew(() =>
                    {
                        var filter = _vmMain.FilterString;
                        Thread.Sleep(100);
                        if (filter == _vmMain.FilterString)
                        {
                            CalcVisibleByFilter();
                            RaisePropertyChanged(nameof(IsMultipleSelected));
                        }
                    });
                }
            };

            if (GlobalEventHelper.OnRequestDeleteServer == null)
                GlobalEventHelper.OnRequestDeleteServer += id =>
                {
                    if (MessageBoxResult.Yes == MessageBox.Show(
                        Context.LanguageService.Translate("confirm_to_delete_selected"),
                        Context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.None))
                    {
                        Context.AppData.DeleteServer(id);
                    }
                };
        }

        private void RebuildVmServerList()
        {
            App.UiDispatcher.Invoke(() =>
            {
                foreach (var vs in Context.AppData.VmItemList)
                {
                    try
                    {
                        vs.PropertyChanged -= VmServerPropertyChanged;
                    }
                    finally
                    {
                        vs.PropertyChanged += VmServerPropertyChanged;
                    }
                }
                _serverListItems = new ObservableCollection<VmProtocolServer>(Context.AppData.VmItemList);

                if (string.IsNullOrEmpty(SelectedTabName) == false
                    && false == Context.AppData.TagList.Any(x => String.Equals(x.Name, SelectedTabName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    SelectedTabName = TabAllName;
                }
                else
                {
                    CalcVisibleByFilter();
                }

                RaisePropertyChanged(nameof(IsMultipleSelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            });
        }

        private void VmServerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VmProtocolServer.IsSelected))
            {
                RaisePropertyChanged(nameof(IsMultipleSelected));
                RaisePropertyChanged(nameof(IsSelectedAll));
                RaisePropertyChanged(nameof(SelectedCount));
            }
        }

        private static IEnumerable<VmProtocolServer> GetOrderedVmProtocolServers(IEnumerable<VmProtocolServer> servers, EnumServerOrderBy orderBy)
        {
            if (!(servers?.Count() > 0)) return new List<VmProtocolServer>();

            switch (orderBy)
            {
                case EnumServerOrderBy.ProtocolAsc:
                    return servers.OrderBy(x => x.Server.Protocol).ThenBy(x => x.Server.Id);

                case EnumServerOrderBy.ProtocolDesc:
                    return servers.OrderByDescending(x => x.Server.Protocol).ThenBy(x => x.Server.Id);

                case EnumServerOrderBy.NameAsc:
                    return servers.OrderBy(x => x.Server.DisplayName).ThenBy(x => x.Server.Id);

                case EnumServerOrderBy.NameDesc:
                    return servers.OrderByDescending(x => x.Server.DisplayName).ThenBy(x => x.Server.Id);

                case EnumServerOrderBy.AddressAsc:
                    return servers.OrderBy(x => x.Server.SubTitle).ThenBy(x => x.Server.Id);

                case EnumServerOrderBy.AddressDesc:
                    return servers.OrderByDescending(x => x.Server.SubTitle).ThenBy(x => x.Server.Id);

                default:
                    return servers.OrderBy(x => x.Server.Id);
            }
        }

        private void CalcVisibleByFilter()
        {
            Debug.Assert(_vmMain != null);
            var keyword = _vmMain.FilterString;
            var tmp = TagAndKeywordFilter.DecodeKeyword(keyword);
            var tagFilters = tmp.Item1;
            var keyWords = tmp.Item2;
            TagFilters = tagFilters;
            SetSelectedTabName(tagFilters);

            var newList = new List<VmProtocolServer>();
            foreach (var vm in Context.AppData.VmItemList)
            {
                var server = vm.Server;
                var s = TagAndKeywordFilter.MatchKeywords(server, tagFilters, keyWords);
                if (s.Item1 == true)
                {
                    newList.Add(vm);
                }
            }

            ServerListItems = new ObservableCollection<VmProtocolServer>(newList);
            RaisePropertyChanged(nameof(IsSelectedAll));
        }


        #region Commands

        private RelayCommand _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(TagFilters.Where(x => x.IsIncluded == true).Select(x => x.TagName).ToList());
                });
            }
        }



        private RelayCommand _cmdExportSelectedToJson;
        public RelayCommand CmdExportSelectedToJson
        {
            get
            {
                return _cmdExportSelectedToJson ??= new RelayCommand((o) =>
                {
                    var path = SelectFileHelper.SaveFile(title: Context.LanguageService.Translate("system_options_data_security_export_dialog_title"),
                        filter: "PRM json array|*.prma",
                        selectedFileName: DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma");
                    if (path == null) return;
                    var list = new List<ProtocolServerBase>();
                    foreach (var vs in ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedTabName) || x.Server.Tags?.Contains(SelectedTabName) == true) && x.IsSelected == true))
                    {
                        var serverBase = (ProtocolServerBase)vs.Server.Clone();
                        Context.DataService.DecryptToConnectLevel(ref serverBase);
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
                    GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Visible, Context.LanguageService.Translate("system_options_data_security_info_data_processing"));
                    Task.Factory.StartNew(() =>
                    {
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
                                }
                            }
                            Context.AppData.AddServer(list);
                            GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Collapsed, "");
                            App.UiDispatcher.Invoke(() =>
                            {
                                MessageBox.Show(Context.LanguageService.Translate("import_done_0_items_added").Replace("{0}", list.Count.ToString()), Context.LanguageService.Translate("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                            });
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Debug(e);
                            GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Collapsed, "");
                            App.UiDispatcher.Invoke(() =>
                            {
                                MessageBox.Show(Context.LanguageService.Translate("import_failure_with_data_format_error"), Context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            });
                        }
                    });
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
                    GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Visible, Context.LanguageService.Translate("system_options_data_security_info_data_processing"));
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var list = MRemoteNgImporter.FromCsv(path, ServerIcons.Instance.Icons);
                            if (list?.Count > 0)
                            {
                                Context.AppData.AddServer(list);
                                GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Collapsed, "");
                                App.UiDispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(Context.LanguageService.Translate("import_done_0_items_added").Replace("{0}", list.Count.ToString()), Context.LanguageService.Translate("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                                });
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Debug(e);
                        }


                        GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Collapsed, "");
                        App.UiDispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Context.LanguageService.Translate("import_failure_with_data_format_error"), Context.LanguageService.Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        });
                    });
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
                    var ss = ServerListItems.Where(x => x.IsSelected == true).ToList();
                    if (!(ss?.Count > 0)) return;
                    if (MessageBoxResult.Yes == MessageBox.Show(
                        Context.LanguageService.Translate("confirm_to_delete_selected"),
                        Context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.None))
                    {
                        var ids = ss.Select(x => x.Id);
                        Context.AppData.DeleteServer(ids);
                    }
                }, o => ServerListItems.Any(x => x.IsSelected == true));
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
                    }, o => App.MainUi?.Vm?.AnimationPageEditor == null && ServerListItems.Any(x => x.IsSelected == true));
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
                            ServerListItems = new ObservableCollection<VmProtocolServer>(GetOrderedVmProtocolServers(ServerListItems, ServerOrderBy));
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
        #endregion
    }
}