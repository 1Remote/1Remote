using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
                Vm.SelectedTabName = tagName;
            };

            ListBoxTags.SelectionChanged += ((sender, args) =>
            {
                ListBoxTags.ScrollIntoView(ListBoxTags.SelectedItem);
            });
        }

        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            Vm.SelectedTabName = "";
        }

        private void BtnTagsListView_Click(object sender, RoutedEventArgs e)
        {
            Vm.SelectedTabName = VmServerListPage.TabTagsListName;
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
            if(value.Length > 1 
               && value[0] is List<TagFilter> selectedTagNames
               && value[1] is ObservableCollection<Tag> tags)
            {
                if (selectedTagNames.Count == 0)
                    return false;
                else if (selectedTagNames.Count > 1)
                    return true;

                var tag = selectedTagNames[0].TagName;
                return tags.First(x => x.Name == tag).IsPinned == false;
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