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
using PRM.ViewModel;
using Shawn.Utils;

namespace PRM.View
{
    public partial class ServerListPage : UserControl
    {
        public VmServerListPage Vm;
        public ServerListPage()
        {
            Vm = new VmServerListPage();
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
