using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerListPage : NotifyPropertyChangedBase
    {
        public readonly VmMain VmMain;
        public VmServerListPage(VmMain vmMainMain)
        {
            Debug.Assert(vmMainMain != null);
            VmMain = vmMainMain;

            var lastSelectedGroup = "";
            if (!string.IsNullOrEmpty(SystemConfig.Instance.Locality.MainWindowTabSelected))
            {
                lastSelectedGroup = SystemConfig.Instance.Locality.MainWindowTabSelected;
            }

            RebuildVmServerCardList();
            GlobalData.Instance.VmItemListDataChanged += RebuildVmServerCardList;

            SystemConfig.Instance.General.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfig.General.ServerOrderBy))
                    RebuildVmServerCardList();
            };

            if (!string.IsNullOrEmpty(lastSelectedGroup) && ServerGroupList.Contains(lastSelectedGroup))
            {
                SelectedGroup = lastSelectedGroup;
            }
        }

        private VmServerListItem _selectedServerListItem = null;
        public VmServerListItem SelectedServerListItem
        {
            get => _selectedServerListItem;
            set => SetAndNotifyIfChanged(nameof(SelectedServerListItem), ref _selectedServerListItem, value);
        }

        private ObservableCollection<VmServerListItem> _serverListItems = new ObservableCollection<VmServerListItem>();
        /// <summary>
        /// AllServerList data source for list view
        /// </summary>
        public ObservableCollection<VmServerListItem> ServerListItems
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

        private string _selectedGroup = "";
        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                _dispNameFilter = "";
                RaisePropertyChanged(nameof(DispNameFilter));
                SetAndNotifyIfChanged(nameof(SelectedGroup), ref _selectedGroup, value);
                SystemConfig.Instance.Locality.MainWindowTabSelected = value;
                SystemConfig.Instance.Locality.Save();
                CalcVisible();
            }
        }


        private string _dispNameFilter = "";
        public string DispNameFilter
        {
            get => _dispNameFilter;
            set
            {
                SetAndNotifyIfChanged(nameof(DispNameFilter), ref _dispNameFilter, value);
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
                    if (vmServerCard.Visible == Visibility.Visible)
                        vmServerCard.IsSelected = value;
                }
            }
        }


        private void RebuildVmServerCardList()
        {
            _serverListItems.Clear();
            foreach (var vs in GlobalData.Instance.VmItemList)
            {
                vs.Server.PropertyChanged += (sender, args) =>
                {
                    switch (args.PropertyName)
                    {
                        case nameof(ProtocolServerBase.GroupName):
                            RebuildGroupList();
                            break;
                    }
                };
                ServerListItems.Add(new VmServerListItem(vs.Server));
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
            else
                SelectedGroup = "";
        }

        private void OrderServerList()
        {
            // Delete ProtocolServerNone
            var noneServers = _serverListItems.Where(s => s.GetType() == typeof(ProtocolServerNone) || s.Server == null || s.Server.Id == 0).ToArray();
            foreach (var s in noneServers)
            {
                _serverListItems.Remove(s);
            }

            switch (SystemConfig.Instance.General.ServerOrderBy)
            {
                case EnumServerOrderBy.Name:
                    _serverListItems = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(s => s.Server.DispName).ThenBy(s => s.Server.Id));
                    break;
                case EnumServerOrderBy.AddTimeAsc:
                    _serverListItems = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(s => s.Server.Id));
                    break;
                case EnumServerOrderBy.AddTimeDesc:
                    _serverListItems = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(s => s.Server.Id));
                    break;
                case EnumServerOrderBy.Protocol:
                    _serverListItems = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(s => s.Server.Protocol).ThenBy(s => s.Server.DispName));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // add a 'ProtocolServerNone' so that 'add server' button will be shown
            var addServerCard = new VmServerListItem(new ProtocolServerNone());
            addServerCard.Server.GroupName = SelectedGroup;
            //addServerCard.OnAction += VmServerCardOnAction;
            _serverListItems.Add(addServerCard);

            CalcVisible();
            base.RaisePropertyChanged(nameof(ServerListItems));
        }


        private void CalcVisible()
        {
            foreach (var card in ServerListItems)
            {
                var server = card.Server;
                string keyWord = DispNameFilter;
                string selectedGroup = SelectedGroup;


                if (server.Id <= 0)
                {
                    card.Visible = Visibility.Collapsed;
                    continue;
                }

                bool bGroupMatched = string.IsNullOrEmpty(selectedGroup) || server.GroupName == selectedGroup || server.GetType() == typeof(ProtocolServerNone);
                if (!bGroupMatched)
                {
                    card.Visible = Visibility.Collapsed;
                    continue;
                }

                if (string.IsNullOrEmpty(keyWord))
                {
                    card.Visible = Visibility.Visible;
                    continue;
                }

                var keyWords = keyWord.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                var keyWordIsMatch = new List<bool>(keyWords.Length);
                for (var i = 0; i < keyWords.Length; i++)
                    keyWordIsMatch.Add(false);

                var dispName = server.DispName;
                var subTitle = server.SubTitle;
                for (var i = 0; i < keyWordIsMatch.Count; i++)
                {
                    var f1 = dispName.IsMatchPinyinKeywords(keyWords[i], out var m1);
                    var f2 = subTitle.IsMatchPinyinKeywords(keyWords[i], out var m2);
                    keyWordIsMatch[i] = f1 || f2;
                }

                if (keyWordIsMatch.All(x => x == true))
                    card.Visible = Visibility.Visible;
                card.Visible = Visibility.Collapsed;
            }

            if (ServerListItems.Where(x => x.Visible == Visibility.Visible).All(x => x.IsSelected))
                _isSelectedAll = true;
            else
                _isSelectedAll = false;
            RaisePropertyChanged(nameof(IsSelectedAll));
        }
    }
}
