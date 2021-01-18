using System.Windows.Controls;
using System.Windows.Input;

namespace PRM.View.TabWindow
{
    public partial class TabWindowClassical : TabWindowBase
    {
        public TabWindowClassical(string token) : base(token)
        {
            InitializeComponent();
            base.Init(TabablzControl);
        }

        protected override void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem != null)
            {
                this.Icon =
                this.IconTitleBar.Source = Vm.SelectedItem.Content.ProtocolServer.IconImg;
            }
        }


        protected override void WinTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if(!Vm.SelectedItem.CanResizeNow)
                    return;
            }
            base.WinTitleBar_MouseDown(sender, e);
        }
    }
}
