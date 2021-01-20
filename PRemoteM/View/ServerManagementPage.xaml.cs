using System.Windows;
using System.Windows.Controls;
using PRM.ViewModel;

namespace PRM.View
{
    public partial class ServerManagementPage : UserControl
    {
        public VmServerListPage VmDataContext;
        public ServerManagementPage()
        {
            VmDataContext = new VmServerListPage();
            InitializeComponent();
            DataContext = VmDataContext;

            //// hide GridBottom when hover.
            //MouseMove += (sender, args) =>
            //{
            //    var p = args.GetPosition(GridBottom);
            //    GridBottom.Visibility = p.Y > 0 ? Visibility.Collapsed : Visibility.Visible;
            //};
        }

        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            VmDataContext.SelectedGroup = "";
        }

        private void ButtonBack_OnClick(object sender, RoutedEventArgs e)
        {
            App.Window.Vm.BottomPage = null;
        }
    }
}
