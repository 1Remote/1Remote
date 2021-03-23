using System.Windows.Input;

namespace PRM.View.TabWindow
{
    public partial class TabWindowChrome : TabWindowBase
    {
        public TabWindowChrome(string token) : base(token)
        {
            InitializeComponent();
            base.Init(TabablzControl);
        }

        protected override void WinTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (!Vm.SelectedItem.CanResizeNow)
                    return;
            }
            base.WinTitleBar_MouseDown(sender, e);
        }
    }
}