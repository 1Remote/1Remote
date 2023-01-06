using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using _1RM.Model;
using _1RM.Service.DataSource;
using Shawn.Utils.Wpf;

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

            Loaded += (sender, args) =>
            {
                _checkBoxSelectedAll = CheckBoxSelectedAll;
                _lvServerCards = LvServerCards;
            };
        }

        private void ServerListItemSource_OnFilter(object sender, FilterEventArgs e)
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

                // if any additional database, then clear all selected: 有多个数据源时，清除所有勾选项目，因为这种情况下无法控制 grouped checkbox，显示上会有BUG
                if (IoC.Get<DataSourceService>().AdditionalSources.Any())
                {
                    t.IsSelected = false;
                }
            }
        }

        private void ItemsCheckBox_OnClick(object sender, RoutedEventArgs e)
        {
            ItemsCheckBox_OnClick_Static(sender, e);
        }

        private static CheckBox? _checkBoxSelectedAll;
        private static ListBox? _lvServerCards;
        public static void ItemsCheckBox_OnClick_Static(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not CheckBox checkBox) return;
            if (_checkBoxSelectedAll == null) return;
            if (_lvServerCards == null) return;

            if (checkBox == _checkBoxSelectedAll)
            {
                var expanderList = MyVisualTreeHelper.FindVisualChilds<Expander>(_lvServerCards);
                foreach (var expander in expanderList)
                {
                    if (expander.FindName("HeaderCheckBox") is CheckBox headerCheckBox)
                    {
                        headerCheckBox.IsChecked = checkBox.IsChecked == true;
                    }
                }
            }
            if (checkBox.Name == "HeaderCheckBox")
            {
                var group = (CollectionViewGroup)checkBox.DataContext;
                foreach (ProtocolBaseViewModel item in group.Items)
                {
                    item.IsSelected = checkBox.IsChecked == true;
                }
            }
            else if (string.IsNullOrEmpty(checkBox.Tag?.ToString()))
            {
                var expander = MyVisualTreeHelper.VisualUpwardSearch<Expander>(checkBox);
                if (expander?.FindName("HeaderCheckBox") is CheckBox headerCheckBox)
                {
                    var group = (CollectionViewGroup)expander.DataContext;
                    if (group.Items.OfType<ProtocolBaseViewModel>().Any(x => x.IsSelected))
                    {
                        if (group.Items.OfType<ProtocolBaseViewModel>().All(x => x.IsSelected))
                            headerCheckBox.IsChecked = true;
                        else
                            headerCheckBox.IsChecked = null;
                    }
                    else
                    {
                        headerCheckBox.IsChecked = false;
                    }
                }
            }
        }

        public void ClearHeaderCheckBox()
        {
            if (_lvServerCards == null) return;
            Dispatcher.Invoke(() =>
            {
                var expanderList = MyVisualTreeHelper.FindVisualChilds<Expander>(_lvServerCards);
                foreach (var expander in expanderList)
                {
                    if (expander.FindName("HeaderCheckBox") is CheckBox headerCheckBox)
                    {
                        headerCheckBox.IsChecked = false;
                    }
                }
            });
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




    public class ConverterGroupIsSelected : IMultiValueConverter
    {
        /*****
            <DataTrigger.Binding>
                <MultiBinding Converter="{StaticResource ConverterIsEqual}" >
                    <Binding RelativeSource="{RelativeSource FindAncestor, AncestorType=view:ServerListPageView}" Path="DataContext.SelectedTabName" Mode="OneWay"></Binding>
                    <Binding Path="Name" Mode="OneWay"></Binding>
                </MultiBinding>
            </DataTrigger.Binding>
         */
        public object? Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.Length == 2
                && value[0] is IEnumerable<ProtocolBaseViewModel> protocolBaseViewModels
                && value[1] is string groupName)
            {
                if (protocolBaseViewModels.Where(x => x.Server.DataSourceName == groupName).Any(x => x.IsSelected))
                {
                    if (protocolBaseViewModels.Where(x => x.Server.DataSourceName == groupName).All(x => x.IsSelected))
                        return true;
                    return null;
                }
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}