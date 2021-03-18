using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.VNC;
using Shawn.Utils;

using Shawn.Utils;

using MessageBox = System.Windows.MessageBox;

namespace PRM.ViewModel
{
    public class VmServerListPage : NotifyPropertyChangedBase
    {
        private readonly PrmContext _context;
        private readonly ListBox _list;

        public VmServerListPage(PrmContext context, ListBox list)
        {
            _context = context;
            _list = list;
            RebuildVmServerCardList();
            _context.AppData.VmItemListDataChanged += RebuildVmServerCardList;

            _context.AppData.OnMainWindowServerFilterChanged += new Action<string>(s =>
            {
                CalcVisible();
            });
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

        private ObservableCollection<string> _serverGroupList = new ObservableCollection<string>();

        public ObservableCollection<string> ServerGroupList
        {
            get => _serverGroupList;
            set => SetAndNotifyIfChanged(nameof(ServerGroupList), ref _serverGroupList, value);
        }

        private string _multiEditPropertyName = "Group";

        public string MultiEditPropertyName
        {
            get => _multiEditPropertyName;
            set => SetAndNotifyIfChanged(nameof(MultiEditPropertyName), ref _multiEditPropertyName, value);
        }

        private string _multiEditNewValue = "";

        public string MultiEditNewValue
        {
            get => _multiEditNewValue;
            set => SetAndNotifyIfChanged(nameof(MultiEditNewValue), ref _multiEditNewValue, value);
        }

        private string _selectedGroup = "";

        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (_selectedGroup == value) return;
                _context.AppData.MainWindowServerFilter = "";
                SetAndNotifyIfChanged(nameof(SelectedGroup), ref _selectedGroup, value);
                SystemConfig.Instance.Locality.MainWindowTabSelected = value;
                CalcVisible();
            }
        }

        private bool _isSelectedAll;

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
                }
            }
        }

        private void RebuildVmServerCardList()
        {
            _serverListItems.Clear();
            foreach (var vs in _context.AppData.VmItemList)
            {
                ServerListItems.Add(new VmProtocolServer(vs.Server));
            }
            OrderServerList();
            RebuildGroupList();
        }

        private void RebuildGroupList()
        {
            var selectedGroup = _selectedGroup;

            ServerGroupList.Clear();
            foreach (var serverAbstract in ServerListItems.Select(x => x.Server))
            {
                if (!string.IsNullOrEmpty(serverAbstract.GroupName) &&
                    !ServerGroupList.Contains(serverAbstract.GroupName))
                {
                    ServerGroupList.Add(serverAbstract.GroupName);
                }
            }
            if (ServerGroupList.Contains(selectedGroup))
                SelectedGroup = selectedGroup;
            else if (!string.IsNullOrEmpty(SystemConfig.Instance.Locality.MainWindowTabSelected)
                && ServerGroupList.Contains(SystemConfig.Instance.Locality.MainWindowTabSelected))
            {
                SelectedGroup = SystemConfig.Instance.Locality.MainWindowTabSelected;
            }
            else
                SelectedGroup = "";
        }

        private void OrderServerList()
        {
            if (!(ServerListItems?.Count > 0) || _list?.ItemsSource == null) return;

            ICollectionView dataView = CollectionViewSource.GetDefaultView(_list.ItemsSource);
            dataView.SortDescriptions.Clear();
            switch (SystemConfig.Instance.General.ServerOrderBy)
            {
                case EnumServerOrderBy.Protocol:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.Protocol), ListSortDirection.Ascending));
                    break;

                case EnumServerOrderBy.ProtocolDesc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.Protocol), ListSortDirection.Descending));
                    break;

                case EnumServerOrderBy.Name:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.DispName), ListSortDirection.Ascending));
                    break;

                case EnumServerOrderBy.NameDesc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.DispName), ListSortDirection.Descending));
                    break;

                case EnumServerOrderBy.GroupName:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.GroupName), ListSortDirection.Ascending));
                    break;

                case EnumServerOrderBy.GroupNameDesc:
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VmProtocolServer.Server) + "." + nameof(ProtocolServerBase.GroupName), ListSortDirection.Descending));
                    break;

                case EnumServerOrderBy.Address:
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
        }

        private void CalcVisible()
        {
            foreach (var card in ServerListItems)
            {
                var server = card.Server;
                string keyWord = _context.AppData.MainWindowServerFilter;
                string selectedGroup = SelectedGroup;

                if (server.Id <= 0)
                {
                    card.ObjectVisibilityInList = Visibility.Collapsed;
                    continue;
                }

                bool bGroupMatched = string.IsNullOrEmpty(selectedGroup) || server.GroupName == selectedGroup;
                if (!bGroupMatched)
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
                var matched = _context.KeywordMatchService.Matchs(new List<string>() { dispName, subTitle }, keyWords).IsMatchAllKeywords;
                if (matched)
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
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(SelectedGroup);
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
                                foreach (var vs in _context.AppData.VmItemList)
                                {
                                    var serverBase = (ProtocolServerBase)vs.Server.Clone();
                                    _context.DbOperator.DecryptPwdIfItIsEncrypted(serverBase);
                                    list.Add(serverBase);
                                }
                            else
                                foreach (var vs in ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true))
                                {
                                    var serverBase = (ProtocolServerBase)vs.Server.Clone();
                                    _context.DbOperator.DecryptPwdIfItIsEncrypted(serverBase);
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
                            Title = SystemConfig.Instance.Language.GetText("managementpage_import_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
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
                                        _context.DbOperator.DbAddServer(server);
                                    }
                                }
                                _context.AppData.ServerListUpdate();
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_done").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
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
                            Title = SystemConfig.Instance.Language.GetText("managementpage_import_dialog_title"),
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
                                var groupNames = new Dictionary<string, string>(); // id -> name
                                var list = new List<ProtocolServerBase>();
                                using (var sr = new StreamReader(new FileStream(dlg.FileName, FileMode.Open)))
                                {
                                    var groupParents = new Dictionary<string, string>(); // id -> name
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
                                                groupNames.Add(id, name);
                                                groupParents.Add(id, parentId);
                                            }
                                        }
                                    }

                                    foreach (var kv in groupNames.ToArray())
                                    {
                                        var name = kv.Value;
                                        var pid = groupParents[kv.Key];
                                        while (groupNames.ContainsKey(pid))
                                        {
                                            name = $"{groupNames[pid]}-{name}";
                                            pid = groupParents[pid];
                                        }
                                        groupNames[kv.Key] = name;
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
                                            var group = "";
                                            if (groupNames.ContainsKey(parentId))
                                                group = groupNames[parentId];
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
                                                        GroupName = group,
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
                                                        EnableSounds = string.Equals(getValue(title, arr, "BringToThisComputer"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
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
                                                        GroupName = group,
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
                                                        GroupName = group,
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
                                                        GroupName = group,
                                                        Address = address,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                    };
                                                    break;

                                                case "telnet":
                                                    server = new ProtocolServerTelnet()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        Port = port.ToString(),
                                                    };
                                                    break;
                                            }

                                            if (server != null)
                                            {
                                                server.IconImg = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)];
                                                list.Add(server);
                                            }
                                        }
                                    }
                                }
                                if (list?.Count > 0)
                                {
                                    foreach (var serverBase in list)
                                    {
                                        _context.DbOperator.DbAddServer(serverBase);
                                    }
                                    _context.AppData.ServerListUpdate();
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_done").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                                }
                                else
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_error") + $": {e.Message}", SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
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
                            SystemConfig.Instance.Language.GetText("managementpage_delete_selected_confirm"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo,
                            MessageBoxImage.Question, MessageBoxResult.None))
                        {
                            var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                            if (!(ss?.Count > 0)) return;
                            foreach (var vs in ss)
                            {
                                _context.DbOperator.DbDeleteServer(vs.Server.Id);
                            }
                            _context.AppData.ServerListUpdate();
                        }
                    }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true));
                }
                return _cmdDeleteSelected;
            }
        }

        private RelayCommand _cmdMultiEditSelected;

        public RelayCommand CmdMultiEditSelectedSave
        {
            get
            {
                if (_cmdMultiEditSelected == null)
                {
                    _cmdMultiEditSelected = new RelayCommand((o) =>
                    {
                        _multiEditNewValue = MultiEditNewValue.Trim();
                        if (MultiEditPropertyName == "Name" || MultiEditPropertyName == "Address"
                                                            || MultiEditPropertyName == "Port")
                        {
                            if (string.IsNullOrWhiteSpace(MultiEditNewValue))
                            {
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_edit_selected_error_empty_string"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                                return;
                            }

                            if (MultiEditPropertyName == "Port"
                            && !int.TryParse(MultiEditNewValue, out var port))
                            {
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_edit_selected_error_port"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                                return;
                            }
                        }

                        if (MessageBoxResult.Yes == MessageBox.Show(
                        SystemConfig.Instance.Language.GetText("managementpage_edit_selected_confirm") + MultiEditNewValue + "'?",
                        SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.None))
                        {
                            switch (MultiEditPropertyName)
                            {
                                case "Name":
                                    {
                                        var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                                        if (ss?.Count > 0)
                                        {
                                            foreach (var vs in ss)
                                            {
                                                vs.Server.DispName = MultiEditNewValue;
                                                _context.DbOperator.DbUpdateServer(vs.Server);
                                            }
                                        }
                                        break;
                                    }
                                case "Group":
                                    {
                                        var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                                        if (ss?.Count > 0)
                                        {
                                            foreach (var vs in ss)
                                            {
                                                vs.Server.GroupName = MultiEditNewValue;
                                                _context.DbOperator.DbUpdateServer(vs.Server);
                                            }
                                        }
                                        break;
                                    }
                                case "Address":
                                    {
                                        var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                                        if (ss?.Count > 0 && int.TryParse(MultiEditNewValue, out var port))
                                        {
                                            foreach (var vs in ss)
                                            {
                                                if (vs.Server is ProtocolServerWithAddrPortBase p)
                                                {
                                                    p.Address = MultiEditNewValue;
                                                    _context.DbOperator.DbUpdateServer(p);
                                                }
                                            }
                                        }
                                        break;
                                    }
                                case "Port":
                                    {
                                        var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                                        if (ss?.Count > 0)
                                        {
                                            foreach (var vs in ss)
                                            {
                                                if (vs.Server is ProtocolServerWithAddrPortBase p)
                                                {
                                                    p.Port = MultiEditNewValue;
                                                    _context.DbOperator.DbUpdateServer(p);
                                                }
                                            }
                                        }
                                        break;
                                    }
                                case "UserName":
                                    {
                                        var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                                        if (ss?.Count > 0)
                                        {
                                            foreach (var vs in ss)
                                            {
                                                if (vs.Server is ProtocolServerWithAddrPortUserPwdBase p)
                                                {
                                                    p.UserName = MultiEditNewValue;
                                                    _context.DbOperator.DbUpdateServer(p);
                                                }
                                            }
                                        }
                                        break;
                                    }
                                case "Password":
                                    {
                                        var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                                        if (ss?.Count > 0)
                                        {
                                            foreach (var vs in ss)
                                            {
                                                if (vs.Server is ProtocolServerWithAddrPortUserPwdBase p)
                                                {
                                                    p.Password = MultiEditNewValue;
                                                    _context.DbOperator.DbUpdateServer(p);
                                                }
                                            }
                                        }
                                        break;
                                    }
                            }
                        }
                    }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true));
                }
                return _cmdMultiEditSelected;
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
    }
}