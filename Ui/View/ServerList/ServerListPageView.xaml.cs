
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using _1RM.Controls.NoteDisplay;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Utils;
using Microsoft.AppCenter.Crashes;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

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
            // MainFilterString changed -> refresh view source -> calc visible in `ServerListItemSource_OnFilter`
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


                if (IoC.Get<DataSourceService>().AdditionalSources.Any())
                {
                    RefreshHeaderCheckBox();
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
            else
            {
                var expander = MyVisualTreeHelper.VisualUpwardSearch<Expander>(checkBox);
                RefreshCheckExpanderHeaderCheckBoxState(expander);
            }
        }

        private static void RefreshCheckExpanderHeaderCheckBoxState(Expander? expander)
        {
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

        private readonly DebounceDispatcher _debounceDispatcher = new();
        public void RefreshHeaderCheckBox()
        {
            if (_lvServerCards == null) return;
            Execute.OnUIThreadSync(() =>
            {
                _debounceDispatcher.Debounce(200, (obj) =>
                {
                    if (_lvServerCards != null)
                    {
                        var expanderList = MyVisualTreeHelper.FindVisualChilds<Expander>(_lvServerCards);
                        foreach (var expander in expanderList)
                        {
                            RefreshCheckExpanderHeaderCheckBoxState(expander);
                        }
                    }
                });
            });
        }


        private void UIElement_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 阻止 GroupItem 中 expander header 中的移动按钮响应 expander header 点击展开/隐藏事件
            if (sender is DependencyObject obj)
            {
                var item = MyVisualTreeHelper.VisualUpwardSearch<GroupItem>(obj);
                if (item != null)
                {
                    e.Handled = true;
                }
            }
        }
        void UIElement_PreviewMouseMoveEvent(object sender, MouseEventArgs e)
        {
            //SimpleLogHelper.Debug($"{e.LeftButton} + {sender is Grid}");
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    // drag ListBoxItem
                    if (sender is ListBoxItem { DataContext: ProtocolBaseViewModel protocol } listBoxItem
                        && IoC.Get<LocalityService>().ServerOrderBy == EnumServerOrderBy.Custom
                        && protocol.HoverNoteDisplayControl?.PopupNoteContent.Content == null)
                    {
                        var dataObj = new DataObject();
                        dataObj.SetData("DragSource", listBoxItem);
                        DragDrop.DoDragDrop(listBoxItem, dataObj, DragDropEffects.Move);
                        listBoxItem.IsSelected = true;
                    }
                    // drag GroupItem
                    else if (sender is DependencyObject obj)
                    {
                        if (e.OriginalSource is DependencyObject os)
                        {
                            if (null != MyVisualTreeHelper.VisualUpwardSearch<NoteDisplayAndEditor>(os))
                            {
                                return;
                            }
                        }
                        GroupItem? groupItem = null;
                        if (sender is GroupItem gi) // 直接 drag GroupItem
                        {
                            groupItem = gi;
                        }
                        else // drag GroupItem header 中的元素
                        {
                            groupItem = MyVisualTreeHelper.VisualUpwardSearch<GroupItem>(obj);
                        }
                        if (groupItem != null)
                        {
                            var dataObj = new DataObject();
                            dataObj.SetData("DragSource", groupItem);
                            DragDrop.DoDragDrop(groupItem, dataObj, DragDropEffects.Move);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var ps = new Dictionary<string, string>
                    {
                        { "Sender", sender.GetType().Name },
                        { "e.Source", e.Source.GetType().Name },
                        { "e.OriginalSource", e.OriginalSource.GetType().Name }
                    };
                    MsAppCenterHelper.Error(ex, properties: ps);
                }
            }
        }

        void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.OriginalSource is DependencyObject os)
                {
                    if (null != MyVisualTreeHelper.VisualUpwardSearch<NoteDisplayAndEditor>(os))
                    {
                        return;
                    }
                }

                // item move
                if (IoC.Get<LocalityService>().ServerOrderBy == EnumServerOrderBy.Custom
                    && e.Data.GetData("DragSource") is ListBoxItem { DataContext: ProtocolBaseViewModel toBeMoved } listBoxItem
                    && sender is ListBoxItem { DataContext: ProtocolBaseViewModel target }
                    && toBeMoved != target)
                {
                    var items = LvServerCards.Items.Cast<ProtocolBaseViewModel>().ToList();
                    int removedIdx = items.IndexOf(toBeMoved);
                    int targetIdx = items.IndexOf(target);
#if DEBUG
                    SimpleLogHelper.Debug($"Before Drop:" + string.Join(", ", items.Select(x => x.Server.DisplayName)));
                    SimpleLogHelper.Debug($"Drop: {toBeMoved.Server.DisplayName}({removedIdx}) -> {target.Server.DisplayName}({targetIdx})");
#endif
                    if (removedIdx == targetIdx - 1)
                    {
                        (toBeMoved, target) = (target, toBeMoved);// swap
                        removedIdx = items.IndexOf(toBeMoved);
                        targetIdx = items.IndexOf(target);
                    }
                    if (removedIdx >= 0
                        && targetIdx >= 0
                        && removedIdx != targetIdx)
                    {
                        items.RemoveAt(removedIdx);
                        targetIdx = items.IndexOf(target);
                        items.Insert(targetIdx, toBeMoved);
                        IoC.Get<LocalityService>().ServerCustomOrderRebuild(items);
                        IoC.Get<ServerListPageViewModel>().RefreshCollectionViewSource();
#if DEBUG
                        SimpleLogHelper.Debug($"After Drop:" + string.Join(", ", items.Select(x => x.Server.DisplayName)));
#endif
                    }
                }
                // group move
                else if (LvServerCards.IsGrouping == true
                    && e.Data.GetData("DragSource") is GroupItem { DataContext: CollectionViewGroup { Name: string toBeMovedName } toBeMovedGroupItem }
                    && IoC.Get<DataSourceService>().AdditionalSources.Any()
                    && LvServerCards?.Items?.Groups?.Count > 0)
                {
                    string targetGroupName = toBeMovedName;
                    // GroupItem drop to ListBoxItem
                    if (sender is ListBoxItem { DataContext: ProtocolBaseViewModel protocol })
                    {
                        targetGroupName = protocol.DataSourceName;
                    }
                    // GroupItem drop to something in GroupItem
                    else if (sender is DependencyObject obj)
                    {
                        GroupItem? groupItem = null;
                        if (sender is GroupItem gi) // 直接 drag GroupItem
                        {
                            groupItem = gi;
                        }
                        else // drag GroupItem header 中的元素
                        {
                            groupItem = MyVisualTreeHelper.VisualUpwardSearch<GroupItem>(obj);
                        }
                        if (groupItem is GroupItem { DataContext: CollectionViewGroup { Name: string name } })
                        {
                            targetGroupName = name;
                        }
                    }

                    if (targetGroupName != toBeMovedName)
                    {
                        var groups = LvServerCards.Items.Groups.Cast<CollectionViewGroup>().ToList();
                        var targetGroupItem = groups.FirstOrDefault(x => x.Name.ToString() == targetGroupName);
                        if (targetGroupItem != null)
                        {
                            int removedIdx = groups.IndexOf(toBeMovedGroupItem);
                            int targetIdx = groups.IndexOf(targetGroupItem);
#if DEBUG
                            SimpleLogHelper.Debug($"groups Before Drop:" + string.Join(", ", groups.Select(x => x.Name.ToString())));
                            SimpleLogHelper.Debug($"groups Drop: {toBeMovedGroupItem.Name.ToString()}({removedIdx}) -> {targetGroupItem.Name.ToString()}({targetIdx})");
#endif
                            if (removedIdx == targetIdx - 1)
                            {
                                (toBeMovedGroupItem, targetGroupItem) = (targetGroupItem, toBeMovedGroupItem);// swap
                                removedIdx = groups.IndexOf(toBeMovedGroupItem);
                                targetIdx = groups.IndexOf(targetGroupItem);
                            }
                            if (removedIdx >= 0
                                && targetIdx >= 0
                                && removedIdx != targetIdx)
                            {
                                groups.RemoveAt(removedIdx);
                                targetIdx = groups.IndexOf(targetGroupItem);
                                groups.Insert(targetIdx, toBeMovedGroupItem);
                                IoC.Get<LocalityService>().ServerGroupedOrderRebuild(groups.Select(x => x.Name.ToString()).ToArray());
                                IoC.Get<ServerListPageViewModel>().RefreshCollectionViewSource();
#if DEBUG
                                SimpleLogHelper.Debug($"groups After Drop:" + string.Join(", ", groups.Select(x => x.Name.ToString())));
#endif
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var ps = new Dictionary<string, string>
                {
                    { "Sender", sender.GetType().Name },
                    { "e.Source", e.Source.GetType().Name },
                    { "e.OriginalSource", e.OriginalSource.GetType().Name }
                };
                MsAppCenterHelper.Error(ex, properties: ps);
            }
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