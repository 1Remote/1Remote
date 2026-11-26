using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.Utils.Tracing;
using _1RM.View.ServerView;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace _1RM.View.ServerView
{
    public partial class ServerListPageView : ServerViewBase
    {
        public ServerListPageView()
        {
            InitializeComponent();
            AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler((_, _) => _dragRow = null), true);
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
                && DataContext is ServerPageViewModelBase vm)
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
                    if (obj is ProtocolBaseViewModel item && item is not ProtocolBaseViewModelDummy)
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

        private ProtocolBaseViewModel? _shiftSelectStartItem;
        private ProtocolBaseViewModel? _dragRow;
        private Point _dragStart;

        private List<ProtocolBaseViewModel> VisibleRows()
        {
            IEnumerable<ProtocolBaseViewModel> rows;
            if (LvServerCards.Items.Groups is { Count: > 0 } groups)
            {
                rows = groups.OfType<CollectionViewGroup>()
                    .Where(group => group.Name is not DataSourceBase source ||
                                    LocalityListViewService.GroupedIsExpandedGet(source.DataSourceName))
                    .SelectMany(group => group.Items.OfType<ProtocolBaseViewModel>());
            }
            else rows = LvServerCards.Items.OfType<ProtocolBaseViewModel>();
            return rows.Where(row => row is not ProtocolBaseViewModelDummy).ToList();
        }

        private void ServerList_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragRow = null;
            if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1 ||
                sender is not FrameworkElement { DataContext: ProtocolBaseViewModel row } ||
                row is ProtocolBaseViewModelDummy || e.OriginalSource is not DependencyObject hit) return;

            // Let checkboxes, menus and the note editor handle their own clicks.
            if (hit is CheckBox || MyVisualTreeHelper.VisualUpwardSearch<CheckBox>(hit) != null)
                _shiftSelectStartItem = row;
            if (hit is ButtonBase or TextBoxBase or NoteDisplayAndEditor ||
                MyVisualTreeHelper.VisualUpwardSearch<ButtonBase>(hit) != null ||
                MyVisualTreeHelper.VisualUpwardSearch<TextBoxBase>(hit) != null ||
                MyVisualTreeHelper.VisualUpwardSearch<NoteDisplayAndEditor>(hit) != null) return;

            var items = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? VisibleRows() : null;
            int start = _shiftSelectStartItem == null ? -1 : items?.IndexOf(_shiftSelectStartItem) ?? -1;
            if (items != null && start >= 0)
            {
                int end = items.IndexOf(row);
                for (int i = 0; i < items.Count; i++)
                    items[i].IsSelected = i >= Math.Min(start, end) && i <= Math.Max(start, end);
            }
            else
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) row.IsSelected = !row.IsSelected;
                else
                {
                    foreach (var item in ((ServerListPageViewModel)DataContext).VmServerList)
                        item.IsSelected = ReferenceEquals(item, row);
                    _dragRow = row;
                    _dragStart = e.GetPosition(LvServerCards);
                }
                _shiftSelectStartItem = row;
            }
            RefreshHeaderCheckBox();
        }

        private void ServerList_OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetData("ListConnection") is not ProtocolBaseViewModel source) return;
            e.Effects = sender is ListBoxItem { DataContext: ProtocolBaseViewModel target } &&
                        target is not ProtocolBaseViewModelDummy && target.IsPinnedInList == source.IsPinnedInList
                ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void RefreshSource_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: DataSourceBase source } && DataContext is ServerListPageViewModel vm)
                vm.CmdRefreshDataSource.Execute(source);
        }

        private void ServerListExpanderIcon_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DependencyObject obj)
            {
                // 阻止 GroupItem 中 expander header 中的移动按钮响应 expander header 点击展开/隐藏事件
                if (MyVisualTreeHelper.VisualUpwardSearch<GroupItem>(obj) != null)
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
                    if (sender is ListBoxItem listBoxItem)
                    {
                        if (_dragRow == null) return;
                        var delta = e.GetPosition(LvServerCards) - _dragStart;
                        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                        var row = _dragRow;
                        _dragRow = null;
                        DragDrop.DoDragDrop(listBoxItem, new DataObject("ListConnection", row), DragDropEffects.Move);
                        e.Handled = true;
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
                    UnifyTracing.Error(ex, properties: ps);
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
                if (e.Data.GetData("ListConnection") is ProtocolBaseViewModel source)
                {
                    e.Handled = true;
                    e.Effects = DragDropEffects.None;
                    if (sender is not ListBoxItem { DataContext: ProtocolBaseViewModel target } ||
                        target is ProtocolBaseViewModelDummy || source.IsPinnedInList != target.IsPinnedInList ||
                        ReferenceEquals(source, target)) return;

                    var items = VisibleRows();
                    int from = items.IndexOf(source);
                    int to = items.IndexOf(target);
                    if (from < 0 || to < 0) return;
                    items.RemoveAt(from);
                    items.Insert(to, source);
                    var vm = (ServerListPageViewModel)DataContext;
                    try
                    {
                        LocalityListViewService.ServerCustomOrderSave(vm.VmServerList, items.Select(row => row.OrderKey).ToList());
                    }
                    catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
                    {
                        MessageBoxHelper.ErrorAlert(ex.Message);
                        return;
                    }
                    IoC.Get<MainWindowViewModel>().ServerOrderBy = EnumServerOrderBy.Custom;
                    vm.ApplySort();
                    e.Effects = DragDropEffects.Move;
                }
                // group move
                else if (LvServerCards.IsGrouping == true
                    && e.Data.GetData("DragSource") is GroupItem { DataContext: CollectionViewGroup { Name: DataSourceBase toBeMovedDataSource } toBeMovedGroupItem }
                    && IoC.Get<DataSourceService>().AdditionalSources.Any()
                    && LvServerCards?.Items?.Groups?.Count > 0)
                {
                    DataSourceBase? targetGroup = null;
                    // GroupItem drop to ListBoxItem
                    if (sender is ListBoxItem { DataContext: ProtocolBaseViewModel { IsPinnedInList: false, DataSource: { } } protocol })
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
                        var groups = LvServerCards.Items.Groups.OfType<CollectionViewGroup>().Where(group => group.Name is DataSourceBase).ToList();
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
                                IoC.Get<ServerListPageViewModel>().CalcServerVisibleAndRefresh();
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
                UnifyTracing.Error(ex, properties: ps);
            }
        }

        private void TagList_PreviewMouseMoveEvent(object sender, MouseEventArgs e)
        {
            ServerPageView_TagListHelper.TagList_PreviewMouseMoveEvent(sender, e);
        }
        private void TagList_OnDrop(object sender, DragEventArgs e)
        {
            ServerPageView_TagListHelper.TagList_OnDrop(DataContext, sender, e);
        }

        private void HeaderTag_OnClick(object sender, RoutedEventArgs e)
        {
            ServerPageView_TagListHelper.HeaderTag_OnClick(DataContext, sender, e);
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