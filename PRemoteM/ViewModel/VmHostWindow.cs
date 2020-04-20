using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dragablz;
using PRM.Core.Protocol;
using PRM.Core.UI.VM;
using PRM.Core.Ulits.DragablzTab;
using PRM.View;
using Shawn.Ulits.RDP;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmHostWindow : NotifyPropertyChangedBase
    {

        private ObservableCollection<TabItemViewModel> _items = new ObservableCollection<TabItemViewModel>();
        public ObservableCollection<TabItemViewModel> Items
        {
            get => _items;
            set => SetAndNotifyIfChanged(nameof(Items), ref _items, value);
        }
        
        
        private TabItemViewModel _selectedItem = new TabItemViewModel();
        public TabItemViewModel SelectedItem
        {
            get => _selectedItem;
            set => SetAndNotifyIfChanged(nameof(SelectedItem), ref _selectedItem, value);
        }


        private readonly IInterTabClient _interTabClient = new InterTabClient();
        public IInterTabClient InterTabClient => _interTabClient;

        public VmHostWindow()
        {
        }




        #region CMD
        private RelayCommand _cmdGoFullScreen;
        public RelayCommand CmdGoFullScreen
        {
            get
            {
                if (_cmdGoFullScreen == null)
                {
                    _cmdGoFullScreen = new RelayCommand((o) =>
                    {
                        // TODO 开启全屏
                        if (this.SelectedItem != null)
                        {
                            var nw = new Window();
                            (this.SelectedItem.Content as AxMsRdpClient09Host)?.SetHostWindow4FulScreen(nw);
                            nw.Loaded += (o1, eventArgs) =>
                            {
                                nw.Content = this.SelectedItem.Content as ProtocolHostBase;
                                (this.SelectedItem.Content as ProtocolHostBase)?.GoFullScreen();
                            };
                            nw.Show();
                            Items.Remove(this.SelectedItem);
                            this.SelectedItem = Items.First();
                        }
                    }, o => this.SelectedItem != null);
                }
                return _cmdGoFullScreen;
            }
        }
        #endregion
    }


    public class InterTabClient : IInterTabClient
    {
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            var v = new HostWindow();
            var vm = new VmHostWindow();
            v.DataContext = vm;
            return new NewTabHost<Window>(v, v.TabablzControl);            
        }
        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}
