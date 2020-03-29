using System;
using System.Collections.ObjectModel;
using System.Linq;
using PRM.Core.Base;
using PRM.Core.DB;
using PRM.Core.Model;

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
        }


        private ObservableCollection<VmServerCard> _dispServerList = new ObservableCollection<VmServerCard>();
        /// <summary>
        /// AllServerList data source for list view
        /// </summary>
        public ObservableCollection<VmServerCard> DispServerList
        {
            get => _dispServerList;
            set
            {
                SetAndNotifyIfChanged(nameof(DispServerList), ref _dispServerList, value);
                OrderServerList();
                DispServerList.CollectionChanged += (sender, args) => { OrderServerList(); };
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
            _dispServerList.Clear();
            foreach (var serverAbstract in Global.GetInstance().ServerList)
            {
                serverAbstract.PropertyChanged += (sender, args) =>
                {
                    switch (args.PropertyName)
                    {
                        case nameof(ServerAbstract.GroupName):
                            RebuildGroupList();
                            break;
                    }
                };
                DispServerList.Add(new VmServerCard(serverAbstract, this));
            }
            OrderServerList();
            RebuildGroupList();
        }

        private void RebuildGroupList()
        {
            var selectedGroup = _selectedGroup;

            ServerGroupList.Clear();
            foreach (var serverAbstract in DispServerList.Select(x => x.Server))
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
            // Delete NoneServer
            var noneServers = _dispServerList.Where(s => s.GetType() == typeof(NoneServer) || s.Server == null || s.Server.Id == 0).ToArray();
            foreach (var s in noneServers)
            {
                _dispServerList.Remove(s);
            }

            // TODO flag to order by LassConnTime
            _dispServerList = new ObservableCollection<VmServerCard>(DispServerList.OrderByDescending(s => s.Server.LassConnTime));

            // add a 'NoneServer' so that 'add server' button will be shown
            var addServerCard = new VmServerCard(new NoneServer(), this);
            addServerCard.Server.GroupName = SelectedGroup;
            //addServerCard.OnAction += VmServerCardOnAction;
            _dispServerList.Add(addServerCard);


            base.RaisePropertyChanged(nameof(DispServerList));
        }
    }
}
