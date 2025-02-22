﻿
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;

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
            if (e.Item is ProtocolBaseViewModel server
                && DataContext is ServerListPageViewModel vm)
            {
                if (vm.IsServerVisible.TryGetValue(server, out var flag) == false
                    || flag)
                {
                    e.Accepted = true;
                }
                else
                {
                    e.Accepted = false;
                    server.IsSelected = false;
                }
                server.SetIsVisible(e.Accepted);


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
                foreach (var obj in group.Items)
                {
                    if (obj is ProtocolBaseViewModel item)
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


        private void ServerList_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
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
        private void ServerList_PreviewMouseMoveEvent(object sender, MouseEventArgs e)
        {
            //SimpleLogHelper.Debug($"{e.LeftButton} + {sender is Grid}");
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    // drag ListBoxItem
                    if (sender is ListBoxItem { DataContext: ProtocolBaseViewModel protocol } listBoxItem
                        && LocalityListViewService.ServerOrderByGet() == EnumServerOrderBy.Custom
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
                    SentryIoHelper.Error(ex, properties: ps);
                }
            }
        }
        private void ServerList_OnDrop(object sender, DragEventArgs e)
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
                if (LocalityListViewService.ServerOrderByGet() == EnumServerOrderBy.Custom
                    && e.Data.GetData("DragSource") is ListBoxItem { DataContext: ProtocolBaseViewModel toBeMovedProtocol } listBoxItem
                    && sender is ListBoxItem { DataContext: ProtocolBaseViewModel target } targetListBoxItem
                    && toBeMovedProtocol != target)
                {
                    var items = LvServerCards.Items.Cast<ProtocolBaseViewModel>().ToList();
                    int removedIdx = items.IndexOf(toBeMovedProtocol);
                    int targetIdx = items.IndexOf(target);
#if DEBUG
                    SimpleLogHelper.Debug($"Before Drop:" + string.Join(", ", items.Select(x => x.Server.DisplayName)));
                    SimpleLogHelper.Debug($"Drop: {toBeMovedProtocol.Server.DisplayName}({removedIdx}) -> {target.Server.DisplayName}({targetIdx})");
#endif
                    bool isNextDoor = Math.Abs(removedIdx - targetIdx) == 1; // 是否相邻

                    int append = 0; // 0: 前面，1: 后面
                    if (isNextDoor && removedIdx < targetIdx) // 如果被移动的item在目标之前且相邻，则插入到目标后面，即 targetIdx += 1;
                    {
                        append = 1;
                    }
                    else if (isNextDoor == false && e.GetPosition(targetListBoxItem).Y > targetListBoxItem.ActualHeight / 2) // 如果二者不相邻，则根据位置判断插入到目标前面还是后面
                    {
                        append = 1;
                    }

                    if (removedIdx >= 0
                        && targetIdx >= 0
                        && removedIdx != targetIdx)
                    {
                        items.RemoveAt(removedIdx);
                        targetIdx = items.IndexOf(target) + append;  // re-calc targetIdx since collection changed
                        if (targetIdx > items.Count)
                        {
                            items.Add(toBeMovedProtocol);
                        }
                        else
                        {
                            items.Insert(targetIdx, toBeMovedProtocol);
                        }
                        LocalityListViewService.ServerCustomOrderSave(items);
                        IoC.Get<ServerListPageViewModel>().RefreshCollectionViewSource();
#if DEBUG
                        SimpleLogHelper.Debug($"After Drop:" + string.Join(", ", items.Select(x => x.Server.DisplayName)));
#endif
                    }
                }
                // group move
                else if (LvServerCards.IsGrouping == true
                    && e.Data.GetData("DragSource") is GroupItem { DataContext: CollectionViewGroup { Name: DataSourceBase toBeMovedDataSource } toBeMovedGroupItem }
                    && IoC.Get<DataSourceService>().AdditionalSources.Any()
                    && LvServerCards?.Items?.Groups?.Count > 0)
                {
                    DataSourceBase? targetGroup = null;
                    // GroupItem drop to ListBoxItem
                    if (sender is ListBoxItem { DataContext: ProtocolBaseViewModel { DataSource: { } } protocol })
                    {
                        targetGroup = protocol.DataSource;
                    }
                    // GroupItem drop to something in GroupItem
                    else if (sender is DependencyObject obj)
                    {
                        var groupItem = (sender is GroupItem gi) ? gi : MyVisualTreeHelper.VisualUpwardSearch<GroupItem>(obj);
                        if (groupItem is { DataContext: CollectionViewGroup { Name: DataSourceBase ds } })
                        {
                            targetGroup = ds;
                        }
                    }

                    if (targetGroup != null && targetGroup != toBeMovedDataSource)
                    {
                        var groups = LvServerCards.Items.Groups.Cast<CollectionViewGroup>().ToList();
                        var targetGroupItem = groups.FirstOrDefault(x => x.Name == targetGroup);
                        if (targetGroupItem != null)
                        {
                            int removedIdx = groups.IndexOf(toBeMovedGroupItem);
                            int targetIdx = groups.IndexOf(targetGroupItem);
#if DEBUG
                            SimpleLogHelper.Debug($"groups Before Drop:" + string.Join(", ", groups.Select(x => x.Name.ToString())));
                            SimpleLogHelper.Debug($"groups Drop: {toBeMovedGroupItem.Name}({removedIdx}) -> {targetGroupItem.Name}({targetIdx})");
#endif
                            // 默认插入到目标前面
                            int append = 0; // 0: 前面，1: 后面
                            if (Math.Abs(removedIdx - targetIdx) == 1 && removedIdx < targetIdx) // 如果被移动的item在目标之前且相邻，则插入到目标后面，即 targetIdx += 1;
                            {
                                append = 1;
                            }
                            if (removedIdx >= 0
                                && targetIdx >= 0
                                && removedIdx != targetIdx)
                            {
                                groups.RemoveAt(removedIdx);
                                targetIdx = groups.IndexOf(targetGroupItem) + append;  // re-calc targetIdx since collection changed
                                if (targetIdx > groups.Count)
                                {
                                    groups.Add(toBeMovedGroupItem);
                                }
                                else
                                {
                                    groups.Insert(targetIdx, toBeMovedGroupItem);
                                }
                                LocalityListViewService.GroupedOrderSave(groups.Select(x => x.Name.ToString() ?? "").Where(x => string.IsNullOrEmpty(x) == false).ToArray());
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
                SentryIoHelper.Error(ex, properties: ps);
            }
        }


        private void TagList_PreviewMouseMoveEvent(object sender, MouseEventArgs e)
        {
            //SimpleLogHelper.Debug($"{e.LeftButton} + {sender is Grid}");
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    // drag ListBoxItem
                    if (sender is Grid { DataContext: Tag tag } listBoxItem)
                    {
                        var dataObj = new DataObject();
                        dataObj.SetData("DragSource", listBoxItem);
                        DragDrop.DoDragDrop(listBoxItem, dataObj, DragDropEffects.Move);
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
                    SentryIoHelper.Error(ex, properties: ps);
                }
            }
        }
        private void TagList_OnDrop(object sender, DragEventArgs e)
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
                if (e.Data.GetData("DragSource") is Grid { DataContext: Tag toBeMoved } _
                    && sender is Grid { DataContext: Tag target } targetGrid
                    && toBeMoved != target
                    && DataContext is ServerListPageViewModel vm)
                {
                    //var items = ListBoxTags.Items.Cast<Tag>().ToList();
                    var items = vm.HeaderTags;
                    int removedIdx = items.IndexOf(toBeMoved);
                    int targetIdx = items.IndexOf(target);
#if DEBUG
                    SimpleLogHelper.Debug($"Before Drop:" + string.Join(", ",
                        items.Select((x, i) => new Tuple<string, bool, int>(x.Name, x.IsPinned, i))
                            .Where(x => x.Item2).Select(x => $"{x.Item1}({x.Item3})")));
                    SimpleLogHelper.Debug($"Drop: {toBeMoved.Name}({removedIdx}) -> {target.Name}({targetIdx})");
#endif
                    bool isNextDoor = false; // 是否相邻
                    {
                        var minIndex = Math.Min(removedIdx, targetIdx);
                        var maxIndex = Math.Max(removedIdx, targetIdx);
                        for (int i = minIndex + 1; i < items.Count; i++)
                        {
                            if (items[i].IsPinned == true)
                            {
                                isNextDoor = i == maxIndex;
                                break;
                            }
                        }
                    }

                    // 默认插入到目标前面
                    int append = 0; // 0: 前面，1: 后面
                    if (isNextDoor && removedIdx < targetIdx) // 如果被移动的item在目标之前且相邻，则插入到目标后面，即 targetIdx += 1;
                    {
                        append = 1;
                    }
                    else if (isNextDoor == false && e.GetPosition(targetGrid).X > targetGrid.ActualWidth / 2) // 如果二者不相邻，则根据位置判断插入到目标前面还是后面
                    {
                        append = 1;
                    }

                    if (removedIdx >= 0
                        && targetIdx >= 0
                        && removedIdx != targetIdx)
                    {
                        items.RemoveAt(removedIdx);
                        targetIdx = items.IndexOf(target) + append;  // re-calc targetIdx since collection changed
                        if (targetIdx > items.Count)
                        {
                            items.Add(toBeMoved);
                        }
                        else
                        {
                            items.Insert(targetIdx, toBeMoved);
                        }
                        LocalityTagService.UpdateTags(items);
                        foreach (var viewModel in vm.VmServerList)
                        {
                            viewModel.ReLoadTags();
                        }
#if DEBUG
                        SimpleLogHelper.Debug($"After Drop:" + string.Join(", ",
                                items.Select((x, i) => new Tuple<string, bool, int>(x.Name, x.IsPinned, i))
                            .Where(x => x.Item2).Select(x => $"{x.Item1}({x.Item3})")));
#endif
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
                SentryIoHelper.Error(ex, properties: ps);
            }
        }

        private void HeaderTag_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Grid { Tag: string name }
                && DataContext is ServerListPageViewModel vm)
            {
                vm.CmdTagAddIncluded.Execute(name);
            }
        }

        private void ServerName_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Grid g && DataContext is ServerListPageViewModel vm)
            {
                vm.NameWidth = e.NewSize.Width;
            }
        }

        private void ServerNote_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Grid g && DataContext is ServerListPageViewModel vm)
            {
                vm.NoteWidth = e.NewSize.Width;
            }
        }
    }

    public class NameMaxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double windowWidth = IoC.Get<MainWindowView>().Width;
            double free = windowWidth;
            free -= 200.0; // subtract the size of fixed columns
            free -= (double)value; // subtract the width of the note column
            free -= 20.0; // leave minimum width for the address column
            return free;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NoteMaxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double windowWidth = IoC.Get<MainWindowView>().Width;
            double free = windowWidth;
            free -= 200.0; // subtract the size of fixed columns
            free -= (double)value; // subtract the width of the name column
            free -= 20.0; // leave minimum width for the address column
            return free;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ConverterTagNameCount : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3
                && values[0] is string tagName
                && values[1] is int count
                && values[2] is bool isPinned)
            {
                return isPinned ? $"📌 {tagName} ({count})" : $"{tagName} ({count})";
            }
            return values[0];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }




    //public class ConverterGroupIsSelected : IMultiValueConverter
    //{
    //    /*****
    //        <DataTrigger.Binding>
    //            <MultiBinding Converter="{StaticResource ConverterIsEqual}" >
    //                <Binding RelativeSource="{RelativeSource FindAncestor, AncestorType=view:ServerListPageView}" Path="DataContext.SelectedTabName" Mode="OneWay"></Binding>
    //                <Binding Path="Name" Mode="OneWay"></Binding>
    //            </MultiBinding>
    //        </DataTrigger.Binding>
    //     */
    //    public object? Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        if (value.Length == 2
    //            && value[0] is IEnumerable<ProtocolBaseViewModel> protocolBaseViewModels
    //            && value[1] is DataSourceBase dataSource)
    //        {
    //            if (protocolBaseViewModels.Where(x => x.Server.DataSource == dataSource).Any(x => x.IsSelected))
    //            {
    //                if (protocolBaseViewModels.Where(x => x.Server.DataSource == dataSource).All(x => x.IsSelected))
    //                    return true;
    //                return null;
    //            }
    //        }
    //        return false;
    //    }
    //    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    //    {
    //        throw new NotSupportedException();
    //    }
    //}
}