using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Service.Locality;
using _1RM.Utils.Tracing;
using _1RM.View.ServerView;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.ServerView
{
    /// <summary>
    /// ServerTreeView.xaml 的交互逻辑
    /// </summary>
    public static class ServerPageView_TagListHelper
    {
        public static void TagList_PreviewMouseMoveEvent(object sender, MouseEventArgs e)
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
                    UnifyTracing.Error(ex, properties: ps);
                }
            }
        }
        public static void TagList_OnDrop(object DataContext, object sender, DragEventArgs e)
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
                    && DataContext is ServerPageViewModelBase vm)
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
                UnifyTracing.Error(ex, properties: ps);
            }
        }

        public static void HeaderTag_OnClick(object DataContext, object sender, RoutedEventArgs e)
        {
            if (sender is Grid { Tag: string name }
                && DataContext is ServerPageViewModelBase vm)
            {
                vm.CmdTagAddIncluded.Execute(name);
            }
        }
    }
}
