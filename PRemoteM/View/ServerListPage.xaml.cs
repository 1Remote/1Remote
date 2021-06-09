using System.Linq;
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
            InitializeComponent();


            Vm = new VmServerListPage(context, LvServerCards);
            DataContext = Vm;

            // hide GridBottom when hover.
            MouseMove += (sender, args) =>
            {
                var p = args.GetPosition(GridBottom);
                GridBottom.Visibility = p.Y > 0 ? Visibility.Collapsed : Visibility.Visible;
            };

            var tagName = SystemConfig.Instance.Locality.MainWindowTabSelected;
            Loaded += (sender, args) =>
            {
                if (Vm.PrmContext.AppData.Tags.Any(x => x.Name == tagName))
                    Vm.PrmContext.AppData.SelectedTagName = tagName;
                else
                    Vm.PrmContext.AppData.SelectedTagName = "";
            };
        }

        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            Vm.PrmContext.AppData.SelectedTagName = "";
        }

        private void BtnTagsListView_Click(object sender, RoutedEventArgs e)
        {
            Vm.PrmContext.AppData.SelectedTagName = VmServerListPage.TagsListViewMark;
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
        {
            PopupMenuInExport.IsOpen = false;
            Vm.CmdAdd?.Execute();
        }

        private void ButtonImport_OnClick(object sender, RoutedEventArgs e)
        {
            PopupMenuInExport.IsOpen = false;
            Vm.CmdImportFromJson?.Execute();
        }

        private void ButtonImportMRemoteNgCsv_OnClick(object sender, RoutedEventArgs e)
        {
            PopupMenuInExport.IsOpen = false;
            Vm.CmdImportFromCsv?.Execute();
        }
    }
}