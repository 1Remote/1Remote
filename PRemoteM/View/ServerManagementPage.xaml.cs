using System.Windows;
using System.Windows.Controls;
using PRM.Core.Model;
using PRM.ViewModel;

namespace PRM.View
{
    public partial class ServerManagementPage : UserControl
    {
        public VmServerListPage Vm;

        public ServerManagementPage(PrmContext context)
        {
            InitializeComponent();
            Vm = new VmServerListPage(context, LvServerCards);
            DataContext = Vm;
        }

        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            Vm.SelectedGroup = "";
        }

        private void ButtonBack_OnClick(object sender, RoutedEventArgs e)
        {
            App.Window.Vm.BottomPage = null;
        }
    }
}