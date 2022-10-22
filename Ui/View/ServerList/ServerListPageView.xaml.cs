using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using _1RM.Model;

namespace _1RM.View.ServerList
{
    public partial class ServerListPageView
    {
        public ServerListPageView()
        {
            InitializeComponent();
            // hide GridBottom when hover.
            MouseMove += (sender, args) =>
            {
                var p = args.GetPosition(GridBottom);
                GridBottom.Visibility = p.Y > 0 ? Visibility.Collapsed : Visibility.Visible;
            };
        }

        private void GroupedItems_OnFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is ProtocolBaseViewModel t
                && DataContext is ServerListPageViewModel vm)
            // If filter is turned on, filter completed items.
            {
                if (vm.TestMatchKeywords(t.Server))
                {
                    e.Accepted = true;
                }
                else
                {
                    e.Accepted = false;
                    t.IsSelected = false;
                }
                t.IsVisible = e.Accepted;
            }
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
            throw new NotSupportedException();
        }
    }
}