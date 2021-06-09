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
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.VNC;
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
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.DispName), ListSortDirection.Ascending));
                    break;

                case EnumServerOrderBy.NameDesc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.DispName), ListSortDirection.Descending));
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
            foreach (var card in ServerListItems)
            {
                var server = card.Server;
                string keyWord = PrmContext.AppData.MainWindowServerFilter;
                string selectedTag = SelectedTagName;

                bool bTagMatched = string.IsNullOrEmpty(selectedTag) || server.Tags?.Contains(selectedTag) == true;
                if (!bTagMatched)
                {
                    card.ObjectVisibilityInList = Visibility.Collapsed;
                    continue;
                }

                if (string.IsNullOrEmpty(keyWord))
                {
                    card.ObjectVisibilityInList = Visibility.Visible;
                    continue;
                }

                var keyWords = keyWord.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                var dispName = server.DispName;
                var subTitle = server.SubTitle;
                var matched = PrmContext.KeywordMatchService.Matchs(new List<string>() { dispName, subTitle }, keyWords).IsMatchAllKeywords;
                if (matched || server.Tags?.Contains(keyWord) == true)
                    card.ObjectVisibilityInList = Visibility.Visible;
                else
                    card.ObjectVisibilityInList = Visibility.Collapsed;
            }

            if (ServerListItems.Where(x => x.ObjectVisibilityInList == Visibility.Visible).All(x => x.IsSelected))
                _isSelectedAll = true;
            else
                _isSelectedAll = false;
            RaisePropertyChanged(nameof(IsSelectedAll));
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
                        if (dlg.ShowDialog() == true)
                        {
                            string getValue(List<string> keyList, List<string> valueList, string fieldName)
                            {
                                var i = keyList.IndexOf(fieldName.ToLower());
                                if (i >= 0)
                                {
                                    var val = valueList[i];
                                    return val;
                                }
                                return "";
                            }
                            try
                            {
                                var tagNames = new Dictionary<string, string>(); // id -> name
                                var list = new List<ProtocolServerBase>();
                                using (var sr = new StreamReader(new FileStream(dlg.FileName, FileMode.Open)))
                                {
                                    var tagParents = new Dictionary<string, string>(); // id -> name
                                    var firstLine = sr.ReadLine();
                                    if (string.IsNullOrWhiteSpace(firstLine))
                                        return;

                                    var title = firstLine.ToLower().Split(';').ToList();
                                    string line;
                                    while (!string.IsNullOrEmpty(line = sr.ReadLine()))
                                    {
                                        var arr = line.Split(';').ToList();
                                        if (arr.Count >= 7)
                                        {
                                            var id = getValue(title, arr, "Id");
                                            var name = getValue(title, arr, "name");
                                            var parentId = getValue(title, arr, "Parent").ToLower();
                                            var nodeType = getValue(title, arr, "NodeType").ToLower();
                                            if (string.Equals("Container", nodeType, StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                tagNames.Add(id, name);
                                                tagParents.Add(id, parentId);
                                            }
                                        }
                                    }

                                    foreach (var kv in tagNames.ToArray())
                                    {
                                        var name = kv.Value;
                                        var pid = tagParents[kv.Key];
                                        while (tagNames.ContainsKey(pid))
                                        {
                                            name = $"{tagNames[pid]}-{name}";
                                            pid = tagParents[pid];
                                        }
                                        tagNames[kv.Key] = name;
                                    }
                                }

                                using (var sr = new StreamReader(new FileStream(dlg.FileName, FileMode.Open)))
                                {
                                    var firstLine = sr.ReadLine();
                                    if (string.IsNullOrWhiteSpace(firstLine))
                                        return;

                                    // title
                                    var title = firstLine.ToLower().Split(';').ToList();

                                    var r = new Random();
                                    string line;
                                    while (!string.IsNullOrEmpty(line = sr.ReadLine()))
                                    {
                                        var arr = line.Split(';').ToList();
                                        if (arr.Count >= 7)
                                        {
                                            ProtocolServerBase server = null;
                                            var name = getValue(title, arr, "name");
                                            var parentId = getValue(title, arr, "Parent").ToLower();
                                            var nodeType = getValue(title, arr, "NodeType").ToLower();
                                            if (!string.Equals("Connection", nodeType, StringComparison.CurrentCultureIgnoreCase))
                                                continue;
                                            List<string> tags = null;
                                            if (tagNames.ContainsKey(parentId))
                                                tags = new List<string>() { tagNames[parentId] };

                                            var protocol = getValue(title, arr, "protocol").ToLower();
                                            var user = getValue(title, arr, "username");
                                            var pwd = getValue(title, arr, "password");
                                            var address = getValue(title, arr, "hostname");
                                            int port = 22;
                                            if (int.TryParse(getValue(title, arr, "port"), out var new_port))
                                            {
                                                port = new_port;
                                            }

                                            switch (protocol)
                                            {
                                                case "rdp":
                                                    server = new ProtocolServerRDP()
                                                    {
                                                        DispName = name,
                                                        Tags = tags,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        RdpWindowResizeMode = ERdpWindowResizeMode.AutoResize, // string.Equals( getValue(title, arr, "AutomaticResize"), "TRUE", StringComparison.CurrentCultureIgnoreCase) ? ERdpWindowResizeMode.AutoResize : ERdpWindowResizeMode.Fixed,
                                                        IsConnWithFullScreen = string.Equals(getValue(title, arr, "Resolution"), "Fullscreen", StringComparison.CurrentCultureIgnoreCase),
                                                        RdpFullScreenFlag = ERdpFullScreenFlag.EnableFullScreen,
                                                        DisplayPerformance = getValue(title, arr, "Colors")?.IndexOf("32") >= 0 ? EDisplayPerformance.High : EDisplayPerformance.Auto,
                                                        IsAdministrativePurposes = string.Equals(getValue(title, arr, "ConnectToConsole"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableClipboard = string.Equals(getValue(title, arr, "RedirectClipboard"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableDiskDrives = string.Equals(getValue(title, arr, "RedirectDiskDrives"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableKeyCombinations = string.Equals(getValue(title, arr, "RedirectKeys"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        // TODO can not divide form LeaveOnRemote
                                                        AudioRedirectionMode = string.Equals(getValue(title, arr, "BringToThisComputer"), "TRUE", StringComparison.CurrentCultureIgnoreCase) == true ? EAudioRedirectionMode.RedirectToLocal : EAudioRedirectionMode.Disabled,
                                                        EnableAudioCapture = string.Equals(getValue(title, arr, "RedirectAudioCapture"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnablePorts = string.Equals(getValue(title, arr, "RedirectPorts"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnablePrinters = string.Equals(getValue(title, arr, "RedirectPrinters"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableSmartCardsAndWinHello = string.Equals(getValue(title, arr, "RedirectSmartCards"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        GatewayMode = string.Equals(getValue(title, arr, "RDGatewayUsageMethod"), "Never", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.DoNotUseGateway :
                                                                            (string.Equals(getValue(title, arr, "RDGatewayUsageMethod"), "Detect", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.AutomaticallyDetectGatewayServerSettings : EGatewayMode.UseTheseGatewayServerSettings),
                                                        GatewayHostName = getValue(title, arr, "RDGatewayHostname"),
                                                        GatewayPassword = getValue(title, arr, "RDGatewayPassword"),
                                                    };

                                                    break;

                                                case "ssh1":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        Tags = tags,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V1
                                                    };
                                                    break;

                                                case "ssh2":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        Tags = tags,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V2
                                                    };
                                                    break;

                                                case "vnc":
                                                    server = new ProtocolServerVNC()
                                                    {
                                                        DispName = name,
                                                        Tags = tags,
                                                        Address = address,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                    };
                                                    break;

                                                case "telnet":
                                                    server = new ProtocolServerTelnet()
                                                    {
                                                        DispName = name,
                                                        Tags = tags,
                                                        Address = address,
                                                        Port = port.ToString(),
                                                    };
                                                    break;
                                            }

                                            if (server != null)
                                            {
                                                server.IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64();
                                                list.Add(server);
                                            }
                                        }
                                    }
                                }
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
                        string newTag = InputWindow.InputBox(SystemConfig.Instance.Language.GetText("server_editor_tag"), SystemConfig.Instance.Language.GetText("server_editor_tag"), t);
                        if (string.IsNullOrEmpty(newTag) || t == newTag)
                            return;
                        var obj = o as Tag;
                        if (obj == null)
                            return;
                        if (t == obj.Name)
                            t = newTag;
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