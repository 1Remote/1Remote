using System;
using System.Collections.ObjectModel;
using System.Linq;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;

namespace PRM.ViewModel
{
    public class VmServerListPage : NotifyPropertyChangedBase
    {
        public readonly VmMain Host;
        public VmServerListPage(VmMain vmMain)
        {
            Host = vmMain;

            RebuildVmServerCardList();
            Global.GetInstance().ServerList.CollectionChanged += (sender, args) =>
            {
                RebuildVmServerCardList();
            };

            SystemConfig.GetInstance().General.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfig.General.ServerOrderBy))
                    RebuildVmServerCardList();
            };
        }

        
        private VmServerCard _selectedServerCard = null;
        public VmServerCard SelectedServerCard
        {
            get => _selectedServerCard;
            set
            {
                DispNameFilter = "";
                SetAndNotifyIfChanged(nameof(SelectedServerCard), ref _selectedServerCard, value);
            }
        }

        private ObservableCollection<VmServerCard> _serverCards = new ObservableCollection<VmServerCard>();
        /// <summary>
        /// AllServerList data source for list view
        /// </summary>
        public ObservableCollection<VmServerCard> ServerCards
        {
            get => _serverCards;
            set
            {
                SetAndNotifyIfChanged(nameof(ServerCards), ref _serverCards, value);
                OrderServerList();
                ServerCards.CollectionChanged += (sender, args) => { OrderServerList(); };
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
                DispNameFilter = "";
                SetAndNotifyIfChanged(nameof(SelectedGroup), ref _selectedGroup, value);
            }
        }


        private string _dispNameFilter = "";
        public string DispNameFilter
        {
            get => _dispNameFilter;
            set => SetAndNotifyIfChanged(nameof(DispNameFilter), ref _dispNameFilter, value);
        }



        private void RebuildVmServerCardList()
        {
            _serverCards.Clear();
            foreach (var serverAbstract in Global.GetInstance().ServerList)
            {
                serverAbstract.PropertyChanged += (sender, args) =>
                {
                    switch (args.PropertyName)
                    {
                        case nameof(ProtocolServerBase.GroupName):
                            RebuildGroupList();
                            break;
                    }
                };
                ServerCards.Add(new VmServerCard(serverAbstract, this));
            }
            OrderServerList();
            RebuildGroupList();
        }

        private void RebuildGroupList()
        {
            var selectedGroup = _selectedGroup;

            ServerGroupList.Clear();
            foreach (var serverAbstract in ServerCards.Select(x => x.Server))
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
            var noneServers = _serverCards.Where(s => s.GetType() == typeof(ProtocolServerNone) || s.Server == null || s.Server.Id == 0).ToArray();
            foreach (var s in noneServers)
            {
                _serverCards.Remove(s);
            }

            // TODO flag to order by LassConnTime
            switch (SystemConfig.GetInstance().General.ServerOrderBy)
            {
                case EnumServerOrderBy.Name:
                    _serverCards = new ObservableCollection<VmServerCard>(ServerCards.OrderBy(s => s.Server.DispName).ThenBy(s => s.Server.Id));
                    break;
                case EnumServerOrderBy.AddTimeAsc:
                    _serverCards = new ObservableCollection<VmServerCard>(ServerCards.OrderBy(s => s.Server.Id));
                    break;
                case EnumServerOrderBy.AddTimeDesc:
                    _serverCards = new ObservableCollection<VmServerCard>(ServerCards.OrderByDescending(s => s.Server.Id));
                    break;
                case EnumServerOrderBy.Protocol:
                    _serverCards = new ObservableCollection<VmServerCard>(ServerCards.OrderByDescending(s => s.Server.Protocol).ThenBy(s => s.Server.DispName));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // add a 'ProtocolServerNone' so that 'add server' button will be shown
            var addServerCard = new VmServerCard(new ProtocolServerNone(), this);
            addServerCard.Server.GroupName = SelectedGroup;
            //addServerCard.OnAction += VmServerCardOnAction;
            _serverCards.Add(addServerCard);


            base.RaisePropertyChanged(nameof(ServerCards));
        }
    }
}
