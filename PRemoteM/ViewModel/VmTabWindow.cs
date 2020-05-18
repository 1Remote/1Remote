using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dragablz;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.UI.VM;
using PRM.Core.Ulits.DragablzTab;
using PRM.Model;
using PRM.View;
using Shawn.Ulits.RDP;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmTabWindow : NotifyPropertyChangedBase
    {
        public readonly string Token;
        public VmTabWindow(string token)
        {
            Token = token;
            Items.CollectionChanged += (sender, args) =>
                RaisePropertyChanged(nameof(BtnCloseAllVisibility));
        }



        public ObservableCollection<TabItemViewModel> Items { get; } = new ObservableCollection<TabItemViewModel>();

        public Visibility BtnCloseAllVisibility => Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        private TabItemViewModel _selectedItem = new TabItemViewModel();
        public TabItemViewModel SelectedItem
        {
            get => _selectedItem;
            set
            {
                SetAndNotifyIfChanged(nameof(SelectedItem), ref _selectedItem, value);
                SelectedItem?.Content?.MakeItFocus();
            }
        }

        #region drag drop tab
        private readonly IInterTabClient _interTabClient = new InterTabClient();
        public IInterTabClient InterTabClient => _interTabClient;
        #endregion


        #region CMD
        private RelayCommand _cmdHostGoFullScreen;
        public RelayCommand CmdHostGoFullScreen
        {
            get
            {
                if (_cmdHostGoFullScreen == null)
                {
                    _cmdHostGoFullScreen = new RelayCommand((o) =>
                    {
                        if (this.SelectedItem?.Content?.CanResizeNow() ?? false)
                            WindowPool.MoveProtocolToFullScreen(SelectedItem.Content.ProtocolServer.Id);
                    }, o => this.SelectedItem != null && (this.SelectedItem.Content?.CanFullScreen ?? false));
                }
                return _cmdHostGoFullScreen;
            }
        }

        private RelayCommand _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                if (_cmdClose == null)
                {
                    _cmdClose = new RelayCommand((o) =>
                    {
                        WindowPool.DelTabWindow(Token);
                    }, o => this.SelectedItem != null);
                }
                return _cmdClose;
            }
        }
        #endregion
    }


    public class InterTabClient : IInterTabClient
    {
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            string token = DateTime.Now.Ticks.ToString();
            var v = new TabWindow(token);
            var vm = v.Vm;
            WindowPool.AddTab(v);
            return new NewTabHost<Window>(v, v.TabablzControl);
        }
        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}
