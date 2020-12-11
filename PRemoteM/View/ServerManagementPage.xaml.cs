using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PRM.Core.Model;
using PRM.ViewModel;
using Shawn.Utils;

namespace PRM.View
{
    public partial class ServerManagementPage : UserControl
    {
        public VmMain Host;
        public VmServerListPage VmDataContext;
        public ServerManagementPage(VmMain host)
        {
            Host = host;
            VmDataContext = new VmServerListPage(host);
            InitializeComponent();
            DataContext = VmDataContext;

            //// hide GridBottom when hover.
            //MouseMove += (sender, args) =>
            //{
            //    var p = args.GetPosition(GridBottom);
            //    GridBottom.Visibility = p.Y > 0 ? Visibility.Collapsed : Visibility.Visible;
            //};
        }
        public ServerManagementPage(VmServerListPage vmDataContext)
        {
            Host = vmDataContext.VmMain;
            VmDataContext = vmDataContext;
            InitializeComponent();
            DataContext = VmDataContext;
        }


        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            VmDataContext.SelectedGroup = "";
        }

        private void LvServerCards_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var items = VmDataContext.ServerListItems.Where(x => x.IsDispNameEditing);
            foreach (var item in items)
            {
                item.CmdIsEditingToggle.Execute();
            }
        }

        private void ButtonBack_OnClick(object sender, RoutedEventArgs e)
        {
            VmDataContext.VmMain.BottomPage = null;
        }
    }
}
