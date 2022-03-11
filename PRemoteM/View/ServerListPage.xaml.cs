using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PRM.Model;
using PRM.View.Settings;

namespace PRM.View
{
    public partial class ServerListPage : UserControl
    {
        public ServerListPageViewModel Vm;

        public ServerListPage(PrmContext context, SettingsPageViewModel settingsPageViewModel, MainWindowViewModel mainWindowViewModel)
        {
            InitializeComponent();

            Vm = ServerListPageViewModel.Instance(context, settingsPageViewModel, LvServerCards, mainWindowViewModel);
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
            Vm.SelectedTabName = "";
        }

        private void BtnTagsListView_Click(object sender, RoutedEventArgs e)
        {
            Vm.SelectedTabName = ServerListPageViewModel.TabTagsListName;
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


    public class ConverterTagsIndicatorIsShow : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.Length > 1
               && value[0] is List<TagFilter> selectedTagNames
               && value[1] is ObservableCollection<Tag> tags)
            {
                if (selectedTagNames.Count == 0)
                    return false;
                else
                    return true;
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }



    public class ConverterStringIsEqual : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.Length > 1
                && value[0] is string s1
                && value[1] is string s2)
            {
                return s1 == s2;
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class ConverterTagName : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string tagName)
            {
                return "#" + tagName;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}