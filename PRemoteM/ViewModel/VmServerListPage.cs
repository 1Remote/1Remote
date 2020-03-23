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

        private ObservableCollection<VmServerCard> _dispServerlist = new ObservableCollection<VmServerCard>();
        /// <summary>
        /// AllServerList data source for list view
        /// </summary>
        public ObservableCollection<VmServerCard> DispServerList
        {
            get => _dispServerlist;
            set
            {
                SetAndNotifyIfChanged(nameof(DispServerList), ref _dispServerlist, value);
                OrderServerList();
                DispServerList.CollectionChanged += (sender, args) => { OrderServerList(); };
            }
        }


        private ObservableCollection<string> _serverGroups = new ObservableCollection<string>();
        public ObservableCollection<string> ServerGroups
        {
            get => _serverGroups;
            set => SetAndNotifyIfChanged(nameof(ServerGroups), ref _serverGroups, value);
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







        public VmServerListPage()
        {
            ServerGroups.Clear();
            foreach (var serverAbstract in Global.GetInstance().ServerDict.Values)
            {
                DispServerList.Add(new VmServerCard(serverAbstract));
                DispServerList.Last().OnAction += VmServerCardOnAction;

                if (!string.IsNullOrEmpty(serverAbstract.GroupName) &&
                    !ServerGroups.Contains(serverAbstract.GroupName))
                {
                    ServerGroups.Add(serverAbstract.GroupName);
                }
            }
            OrderServerList();
        }











        private void VmServerCardOnAction(VmServerCard sender, VmServerCard.EServerAction action)
        {
            switch (action)
            {
                case VmServerCard.EServerAction.Delete:
                    {
                        var id = ((VmServerCard)sender).Server.Id;
                        var groupName = ((VmServerCard)sender).Server.GroupName;
                        PRM_DAO.GetInstance().DeleteServer(id);
                        DispServerList.Remove(((VmServerCard)sender));
                        if (DispServerList.All(s => s.Server.GroupName != groupName))
                        {
                            ServerGroups.Remove(groupName);
                        }
                        break;
                    }
                case VmServerCard.EServerAction.Add:
                    {
                        var serverOrm = ServerOrm.ConvertFrom(sender.Server);
                        if (PRM_DAO.GetInstance().Insert(serverOrm))
                        {
                            var newCard = new VmServerCard(ServerFactory.GetInstance().CreateFromDb(serverOrm));
                            DispServerList.Add(newCard);
                            DispServerList.Last().OnAction += VmServerCardOnAction;
                            if (!string.IsNullOrEmpty(newCard.Server.GroupName) && DispServerList.All(s => s.Server.GroupName != newCard.Server.GroupName))
                            {
                                ServerGroups.Add(newCard.Server.GroupName);
                            }
                        }
                        break;
                    }
                case VmServerCard.EServerAction.Edit:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
            OrderServerList();
        }
        private void OrderServerList()
        {
            // Delete NoneServer
            var noneServers = _dispServerlist.Where(s => s.GetType() == typeof(NoneServer) || s.Server == null || s.Server.Id == 0).ToArray();
            foreach (var s in noneServers)
            {
                _dispServerlist.Remove(s);
            }

            // TODO flag to order by LassConnTime
            _dispServerlist = new ObservableCollection<VmServerCard>(DispServerList.OrderByDescending(s => s.Server.LassConnTime));

            // add a 'NoneServer' so that 'add server' button will be shown
            var addServerCard = new VmServerCard(new NoneServer());
            addServerCard.Server.GroupName = SelectedGroup;
            addServerCard.OnAction += VmServerCardOnAction;
            _dispServerlist.Add(addServerCard);


            base.RaisePropertyChanged(nameof(DispServerList));
        }
    }
}
