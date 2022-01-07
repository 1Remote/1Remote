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

            var tagName = context.LocalityService.MainWindowTabSelected;
            Loaded += (sender, args) =>
            {
                if (Vm.Context.AppData.Tags.Any(x => x.Name == tagName))
                    Vm.Context.AppData.SelectedTagName = tagName;
                else
                    Vm.Context.AppData.SelectedTagName = "";
            };

            ListBoxTags.SelectionChanged += ((sender, args) =>
            {
                ListBoxTags.ScrollIntoView(ListBoxTags.SelectedItem);
            });
        }

        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            Vm.Context.AppData.SelectedTagName = "";
        }

        private void BtnTagsListView_Click(object sender, RoutedEventArgs e)
        {
            Vm.Context.AppData.SelectedTagName = VmServerListPage.TagsListViewMark;
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