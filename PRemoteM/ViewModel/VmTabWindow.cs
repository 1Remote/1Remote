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
using PRM.View;
using Shawn.Ulits.RDP;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmTabWindow : NotifyPropertyChangedBase
    {
        public readonly string Token;

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

        public VmTabWindow(string token)
        {
            Token = token;
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
                            var nw = new FullScreenWindow((this.SelectedItem.Content as AxMsRdpClient09Host));
                            nw.Show();
                            (this.SelectedItem.Content as ProtocolHostBase)?.GoFullScreen();
                            Items.Remove(this.SelectedItem);
                            if (Items.Count > 0)
                                this.SelectedItem = Items.First();
                            else
                            {
                                // TODO CloseWindow
                                var w = Global.GetInstance().TabWindows[Token];
                                w.Close();
                                Global.GetInstance().TabWindows.Remove(Token);
                                //((Window)o).Close();
                            }
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
            string token = DateTime.Now.Ticks.ToString();
            var v = new TabWindow(token);
            var vm = v.Vm;
            Global.GetInstance().TabWindows.Add(token, v);
            return new NewTabHost<Window>(v, v.TabablzControl);            
        }
        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}
