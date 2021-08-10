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
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.VNC;
using PRM.Core.Utils.mRemoteNG;
using PRM.Model;
using PRM.Resources.Icons;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerListPage : NotifyPropertyChangedBase
    {
        public const string TagsListViewMark = "tags_selector_for_list@#@1__()!";
        public PrmContext Context { get; }
        private readonly ListBox _list;

        public VmServerListPage(PrmContext context, ListBox list)
        {
            Context = context;
            _list = list;
            RebuildVmServerList();
            Context.AppData.VmItemListDataChanged += RebuildVmServerList;

            Context.AppData.OnMainWindowServerFilterChanged += new Action<string>(s =>
            {
                CalcVisible();
                RaisePropertyChanged(nameof(IsMultipleSelected));
            });

            Context.AppData.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GlobalData.SelectedTagName))
                {
                    CalcVisible();
                }
            };
        }

        private VmProtocolServer _selectedServerListItem = null;

        public VmProtocolServer SelectedServerListItem
        {
            get => _selectedServerListItem;
            set => SetAndNotifyIfChanged(nameof(SelectedServerListItem), ref _selectedServerListItem, value);
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
                SetAndNotifyIfChanged(nameof(ServerListItems), ref _serverListItems, value);
                OrderServerList();
                ServerListItems.CollectionChanged += (sender, args) => { OrderServerList(); };
            }
        }

        public int SelectedCount => ServerListItems.Count(x => x.IsSelected);


        private ObservableCollection<string> _serverTagList = new ObservableCollection<string>();
        public ObservableCollection<string> ServerTagList
        {
            get => _serverTagList;
            set => SetAndNotifyIfChanged(nameof(ServerTagList), ref _serverTagList, value);
        }

        private string SelectedTagName => Context.AppData.SelectedTagName;

        //private string _selectedTagName = "";
        //public string SelectedTagName
        //{
        //    get => _selectedTagName;
        //    set
        //    {
        //        if (_selectedTagName == value) return;
        //        Context.AppData.MainWindowServerFilter = "";
        //        SetAndNotifyIfChanged(nameof(SelectedTagName), ref _selectedTagName, value);
        //        context.LocalityService.MainWindowTabSelected = value;
        //        CalcVisible();
        //    }
        //}

        private bool _isSelectedAll;
        /// <summary>
        /// MultipleSelected all
        /// </summary>
        public bool IsSelectedAll
        {
            get => _isSelectedAll;
            set
            {
                SetAndNotifyIfChanged(nameof(IsSelectedAll), ref _isSelectedAll, value);
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
            switch (SystemConfig.Instance.General.ServerOrderBy)
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
            SimpleLogHelper.Debug($"OrderServerList: {SystemConfig.Instance.General.ServerOrderBy}");
            RaisePropertyChanged(nameof(IsMultipleSelected));
        }

        private void CalcVisible()
        {
            var keyWord = Context.AppData.MainWindowServerFilter;
            var keyWords = keyWord.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (SelectedTagName != TagsListViewMark)
            {
                foreach (var card in ServerListItems)
                {
                    var server = card.Server;
                    bool bTagMatched = string.IsNullOrEmpty(SelectedTagName) || server.Tags?.Contains(SelectedTagName) == true;

                    if (!bTagMatched)
                    {
                        card.ObjectVisibilityInList = Visibility.Collapsed;
                    }
                    else if (string.IsNullOrEmpty(keyWord))
                    {
                        card.ObjectVisibilityInList = Visibility.Visible;
                    }
                    else
                    {
                        var dispName = server.DisplayName;
                        var subTitle = server.SubTitle;
                        var matched = Context.KeywordMatchService.Matchs(new List<string>() { dispName, subTitle }, keyWords).IsMatchAllKeywords;
                        if (matched)
                            card.ObjectVisibilityInList = Visibility.Visible;
                        else
                            card.ObjectVisibilityInList = Visibility.Collapsed;
                    }

                    if (card.ObjectVisibilityInList == Visibility.Collapsed && card.IsSelected)
                        card.IsSelected = false;
                }

                if (ServerListItems.Where(x => x.ObjectVisibilityInList == Visibility.Visible).All(x => x.IsSelected))
                    _isSelectedAll = true;
                else
                    _isSelectedAll = false;
                RaisePropertyChanged(nameof(IsSelectedAll));
            }
            else
            {
                foreach (var tag in Context.AppData.Tags)
                {
                    if (string.IsNullOrEmpty(keyWord))
                    {
                        tag.ObjectVisibilityInList = Visibility.Visible;
                    }
                    else
                    {
                        var matched = Context.KeywordMatchService.Matchs(new List<string>() { tag.Name }, keyWords).IsMatchAllKeywords;
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
                    if (Context.AppData.Tags.Any(x => x.Name == SelectedTagName))
                        GlobalEventHelper.OnGoToServerAddPage?.Invoke(SelectedTagName);
                    else
                        GlobalEventHelper.OnGoToServerAddPage?.Invoke();
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
                    var dlg = new SaveFileDialog
                    {
                        Filter = "PRM json array|*.prma",
                        Title = Context.LanguageService.Translate("system_options_data_security_export_dialog_title"),
                        FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        var list = new List<ProtocolServerBase>();
                        if (isExportAll != null || ServerListItems.All(x => x.IsSelected == false))
                            foreach (var vs in Context.AppData.VmItemList)
                            {
                                var serverBase = (ProtocolServerBase) vs.Server.Clone();
                                Context.DataService.DecryptToConnectLevel(serverBase);
                                list.Add(serverBase);
                            }
                        else
                            foreach (var vs in ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true))
                            {
                                var serverBase = (ProtocolServerBase) vs.Server.Clone();
                                Context.DataService.DecryptToConnectLevel(serverBase);
                                list.Add(serverBase);
                            }

                        File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                    }
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
                    var dlg = new OpenFileDialog()
                    {
                        Filter = "PRM json array|*.prma",
                        Title = Context.LanguageService.Translate("import_server_dialog_title"),
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        try
                        {
                            var list = new List<ProtocolServerBase>();
                            var jobj = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(dlg.FileName, Encoding.UTF8));
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
                    var dlg = new OpenFileDialog()
                    {
                        Filter = "csv|*.csv",
                        Title = Context.LanguageService.Translate("import_server_dialog_title"),
                    };
                    if (dlg.ShowDialog() != true) return;
                    try
                    {
                        var list = MRemoteNgImporter.FromCsv(dlg.FileName, ServerIcons.Instance.Icons);
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

        private RelayCommand _cmdDeleteSelected;

        public RelayCommand CmdDeleteSelected
        {
            get
            {
                return _cmdDeleteSelected ??= new RelayCommand((o) =>
                {
                    if (MessageBoxResult.Yes == MessageBox.Show(
                        Context.LanguageService.Translate("confirm_to_delete_selected"),
                        Context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.None))
                    {
                        var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true).ToList();
                        if (!(ss?.Count > 0)) return;
                        foreach (var vs in ss)
                        {
                            Context.DataService.Database_DeleteServer(vs.Server.Id);
                        }

                        Context.AppData.ReloadServerList();
                    }
                }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true));
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
                    }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true));
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
                if (_cmdReOrder == null)
                {
                    _cmdReOrder = new RelayCommand((o) =>
                    {
                        if (int.TryParse(o.ToString(), out int ot))
                        {
                            if ((DateTime.Now - _lastCmdReOrder).TotalMilliseconds > 200)
                            {
                                // cancel order
                                if (SystemConfig.Instance.General.ServerOrderBy == (EnumServerOrderBy)(ot + 1))
                                {
                                    ot = -1;
                                }
                                else if (SystemConfig.Instance.General.ServerOrderBy == (EnumServerOrderBy)ot)
                                {
                                    ++ot;
                                }

                                SystemConfig.Instance.General.ServerOrderBy = (EnumServerOrderBy)ot;
                                OrderServerList();
                                _lastCmdReOrder = DateTime.Now;
                            }
                        }
                    });
                }
                return _cmdReOrder;
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








        private RelayCommand _cmdTagSelect;

        public RelayCommand CmdTagSelect
        {
            get
            {
                return _cmdTagSelect ??= new RelayCommand((o) =>
                {
                    if (o == null)
                        return;
                    if (o is Tag obj
                        && Context.AppData.Tags.Any(x => x.Name == obj.Name))
                    {
                        Context.AppData.SelectedTagName = obj.Name;
                    }
                    else if (o is string str
                             && Context.AppData.Tags.Any(x => x.Name == str))
                    {
                        Context.AppData.SelectedTagName = str;
                    }
                });
            }
        }



        private RelayCommand _cmdTagDelete;

        public RelayCommand CmdTagDelete
        {
            get
            {
                return _cmdTagDelete ??= new RelayCommand((o) =>
                {
                    var obj = o as Tag;
                    if (obj == null)
                        return;
                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
                    {
                        var s = vmProtocolServer.Server;
                        if (s.Tags.Contains(obj.Name))
                        {
                            s.Tags.Remove(obj.Name);
                        }

                        Context.AppData.UpdateServer(s, false);
                    }

                    Context.AppData.ReloadServerList();
                    Context.AppData.SelectedTagName = "";
                });
            }
        }






        private RelayCommand _cmdTagRename;

        public RelayCommand CmdTagRename
        {
            get
            {
                if (_cmdTagRename == null)
                {
                    _cmdTagRename = new RelayCommand((o) =>
                    {
                        var t = SelectedTagName;
                        var obj = o as Tag;
                        if (obj == null)
                            return;
                        string newTag = InputWindow.InputBox(Context.LanguageService.Translate("server_editor_tag"), Context.LanguageService.Translate("server_editor_tag"), obj.Name);
                        if (t == obj.Name)
                            t = newTag;
                        if (string.IsNullOrEmpty(newTag) || obj.Name == newTag)
                            return;
                        foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
                        {
                            var s = vmProtocolServer.Server;
                            if (s.Tags.Contains(obj.Name))
                            {
                                s.Tags.Remove(obj.Name);
                                s.Tags.Add(newTag);
                            }
                            Context.AppData.UpdateServer(s, false);
                        }
                        Context.AppData.ReloadServerList();
                        Context.AppData.SelectedTagName = t;
                    });
                }
                return _cmdTagRename;
            }
        }






        private RelayCommand _cmdTagConnect;

        public RelayCommand CmdTagConnect
        {
            get
            {
                return _cmdTagConnect ??= new RelayCommand((o) =>
                {
                    if (!(o is Tag obj))
                        return;
                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Id);
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }


        private RelayCommand _cmdTagConnectToNewTab;

        public RelayCommand CmdTagConnectToNewTab
        {
            get
            {
                return _cmdTagConnectToNewTab ??= new RelayCommand((o) =>
                {
                    if (!(o is Tag obj))
                        return;

                    var token = DateTime.Now.Ticks.ToString();
                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Id, token);
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }
    }
}