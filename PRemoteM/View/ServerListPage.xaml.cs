using System.Windows;
using System.Windows.Controls;
using PRM.Core.Model;
using PRM.ViewModel;

namespace PRM.View
{
    public partial class ServerListPage : UserControl
    {
        public VmServerListPage Vm;

        public ServerListPage(PrmContext context)
        {
            Vm = new VmServerListPage(context, LvServerCards);
            InitializeComponent();
            DataContext = Vm;

            // hide GridBottom when hover.
            MouseMove += (sender, args) =>
            {
                var p = args.GetPosition(GridBottom);
                GridBottom.Visibility = p.Y > 0 ? Visibility.Collapsed : Visibility.Visible;
            };
        }

        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            Vm.SelectedGroup = "";
        }
    }
}