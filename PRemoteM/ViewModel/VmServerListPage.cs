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
using PRM.Resources.Icons;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerListPage : NotifyPropertyChangedBase
    {
        public const string TagsListViewMark = "tags_selector_for_list@#@1__()!";
        public PrmContext PrmContext { get; }
        private readonly ListBox _list;

        public VmServerListPage(PrmContext prmContext, ListBox list)
        {
            PrmContext = prmContext;
            _list = list;
            RebuildVmServerList();
            PrmContext.AppData.VmItemListDataChanged += RebuildVmServerList;

            PrmContext.AppData.OnMainWindowServerFilterChanged += new Action<string>(s =>
            {
                CalcVisible();
                RaisePropertyChanged(nameof(IsMultipleSelected));
            });

            PrmContext.AppData.PropertyChanged += (sender, args) =>
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

        private string SelectedTagName => PrmContext.AppData.SelectedTagName;

        //private string _selectedTagName = "";
        //public string SelectedTagName
        //{
        //    get => _selectedTagName;
        //    set
        //    {
        //        if (_selectedTagName == value) return;
        //        PrmContext.AppData.MainWindowServerFilter = "";
        //        SetAndNotifyIfChanged(nameof(SelectedTagName), ref _selectedTagName, value);
        //        SystemConfig.Instance.Locality.MainWindowTabSelected = value;
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
            foreach (var vs in PrmContext.AppData.VmItemList)
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
            var keyWord = PrmContext.AppData.MainWindowServerFilter;
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
                        var matched = PrmContext.KeywordMatchService.Matchs(new List<string>() { dispName, subTitle }, keyWords).IsMatchAllKeywords;
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
                foreach (var tag in PrmContext.AppData.Tags)
                {
                    if (string.IsNullOrEmpty(keyWord))
                    {
                        tag.ObjectVisibilityInList = Visibility.Visible;
                    }
                    else
                    {
                        var matched = PrmContext.KeywordMatchService.Matchs(new List<string>() { tag.Name }, keyWords).IsMatchAllKeywords;
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
                    if (PrmContext.AppData.Tags.Any(x => x.Name == SelectedTagName))
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
                if (_cmdExportSelectedToJson == null)
                {
                    _cmdExportSelectedToJson = new RelayCommand((isExportAll) =>
                    {
                        var dlg = new SaveFileDialog
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.Instance.Language.GetText("system_options_data_security_export_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            var list = new List<ProtocolServerBase>();
                            if (isExportAll != null || ServerListItems.All(x => x.IsSelected == false))
                                foreach (var vs in PrmContext.AppData.VmItemList)
                                {
                                    var serverBase = (ProtocolServerBase)vs.Server.Clone();
                                    PrmContext.DbOperator.DecryptPwdIfItIsEncrypted(serverBase);
                                    list.Add(serverBase);
                                }
                            else
                                foreach (var vs in ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true))
                                {
                                    var serverBase = (ProtocolServerBase)vs.Server.Clone();
                                    PrmContext.DbOperator.DecryptPwdIfItIsEncrypted(serverBase);
                                    list.Add(serverBase);
                                }
                            File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                        }
                    });
                }
                return _cmdExportSelectedToJson;
            }
        }

        private RelayCommand _cmdImportFromJson;

        public RelayCommand CmdImportFromJson
        {
            get
            {
                if (_cmdImportFromJson == null)
                {
                    _cmdImportFromJson = new RelayCommand((o) =>
                    {
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.Instance.Language.GetText("import_server_dialog_title"),
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
                                        PrmContext.DbOperator.DbAddServer(server);
                                    }
                                }
                                PrmContext.AppData.ServerListUpdate();
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("import_done_0_items_added").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("import_failure_with_data_format_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                        }
                    });
                }
                return _cmdImportFromJson;
            }
        }

        private RelayCommand _cmdImportFromCsv;

        public RelayCommand CmdImportFromCsv
        {
            get
            {
                if (_cmdImportFromCsv == null)
                {
                    _cmdImportFromCsv = new RelayCommand((o) =>
                    {
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "csv|*.csv",
                            Title = SystemConfig.Instance.Language.GetText("import_server_dialog_title"),
                        };
                        if (dlg.ShowDialog() != true) return;
                        try
                        {
                            var list = MRemoteNgImporter.FromCsv(dlg.FileName, ServerIcons.Instance.Icons);
                            if (list?.Count > 0)
                            {
                                foreach (var serverBase in list)
                                {
                                    PrmContext.DbOperator.DbAddServer(serverBase);
                                }
                                PrmContext.AppData.ServerListUpdate();
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("import_done_0_items_added").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                            }
                            else
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("import_failure_with_data_format_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Debug(e);
                            MessageBox.Show(SystemConfig.Instance.Language.GetText("import_failure_with_data_format_error") + $": {e.Message}", SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        }
                    });
                }
                return _cmdImportFromCsv;
            }
        }

        private RelayCommand _cmdDeleteSelected;

        public RelayCommand CmdDeleteSelected
        {
            get
            {
                if (_cmdDeleteSelected == null)
                {
                    _cmdDeleteSelected = new RelayCommand((o) =>
                    {
                        if (MessageBoxResult.Yes == MessageBox.Show(
                            SystemConfig.Instance.Language.GetText("confirm_to_delete_selected"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo,
                            MessageBoxImage.Question, MessageBoxResult.None))
                        {
                            var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true).ToList();
                            if (!(ss?.Count > 0)) return;
                            foreach (var vs in ss)
                            {
                                PrmContext.DbOperator.DbDeleteServer(vs.Server.Id);
                            }
                            PrmContext.AppData.ServerListUpdate();
                        }
                    }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true));
                }
                return _cmdDeleteSelected;
            }
        }

        private RelayCommand _cmdMultiEditSelected;

        public RelayCommand CmdMultiEditSelected
        {
            get
            {
                if (_cmdMultiEditSelected == null)
                {
                    _cmdMultiEditSelected = new RelayCommand((o) =>
                    {
                        GlobalEventHelper.OnRequestGoToServerMultipleEditPage?.Invoke(ServerListItems.Where(x => x.IsSelected).Select(x => x.Server), true);
                    }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedTagName) || x.Server.Tags?.Contains(SelectedTagName) == true) && x.IsSelected == true));
                }
                return _cmdMultiEditSelected;
            }
        }

        private RelayCommand _cmdCancelSelected;

        public RelayCommand CmdCancelSelected
        {
            get
            {
                if (_cmdCancelSelected == null)
                {
                    _cmdCancelSelected = new RelayCommand((o) =>
                    {
                        PrmContext.AppData.ServerListClearSelect();
                    });
                }
                return _cmdCancelSelected;
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
                if (_cmdConnectSelected == null)
                {
                    _cmdConnectSelected = new RelayCommand((o) =>
                    {
                        foreach (var vmProtocolServer in ServerListItems.Where(x => x.IsSelected == true).ToArray())
                        {
                            vmProtocolServer.CmdConnServer.Execute();
                            Thread.Sleep(50);
                        }
                    });
                }
                return _cmdConnectSelected;
            }
        }








        private RelayCommand _cmdTagSelect;

        public RelayCommand CmdTagSelect
        {
            get
            {
                if (_cmdTagSelect == null)
                {
                    _cmdTagSelect = new RelayCommand((o) =>
                    {
                        if (o == null)
                            return;
                        if (o is Tag obj
                        && PrmContext.AppData.Tags.Any(x => x.Name == obj.Name))
                        {
                            PrmContext.AppData.SelectedTagName = obj.Name;
                        }
                        else if (o is string str
                                 && PrmContext.AppData.Tags.Any(x => x.Name == str))
                        {
                            PrmContext.AppData.SelectedTagName = str;
                        }
                    });
                }
                return _cmdTagSelect;
            }
        }



        private RelayCommand _cmdTagDelete;

        public RelayCommand CmdTagDelete
        {
            get
            {
                if (_cmdTagDelete == null)
                {
                    _cmdTagDelete = new RelayCommand((o) =>
                    {
                        var obj = o as Tag;
                        if (obj == null)
                            return;
                        foreach (var vmProtocolServer in PrmContext.AppData.VmItemList.ToArray())
                        {
                            var s = vmProtocolServer.Server;
                            if (s.Tags.Contains(obj.Name))
                            {
                                s.Tags.Remove(obj.Name);
                            }
                            PrmContext.AppData.ServerListUpdate(s, false);
                        }
                        PrmContext.AppData.ServerListUpdate();
                        PrmContext.AppData.SelectedTagName = "";
                    });
                }
                return _cmdTagDelete;
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
                        string newTag = InputWindow.InputBox(SystemConfig.Instance.Language.GetText("server_editor_tag"), SystemConfig.Instance.Language.GetText("server_editor_tag"), obj.Name);
                        if (t == obj.Name)
                            t = newTag;
                        if (string.IsNullOrEmpty(newTag) || obj.Name == newTag)
                            return;
                        foreach (var vmProtocolServer in PrmContext.AppData.VmItemList.ToArray())
                        {
                            var s = vmProtocolServer.Server;
                            if (s.Tags.Contains(obj.Name))
                            {
                                s.Tags.Remove(obj.Name);
                                s.Tags.Add(newTag);
                            }
                            PrmContext.AppData.ServerListUpdate(s, false);
                        }
                        PrmContext.AppData.ServerListUpdate();
                        PrmContext.AppData.SelectedTagName = t;
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
                if (_cmdTagConnect == null)
                {
                    _cmdTagConnect = new RelayCommand((o) =>
                    {
                        var obj = o as Tag;
                        if (obj == null)
                            return;
                        foreach (var vmProtocolServer in PrmContext.AppData.VmItemList.ToArray())
                        {
                            if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                            {
                                vmProtocolServer.CmdConnServer.Execute();
                                Thread.Sleep(100);
                            }
                        }
                    });
                }

                return _cmdTagConnect;
            }
        }
    }
}