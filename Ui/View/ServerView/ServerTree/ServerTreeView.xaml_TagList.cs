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
using _1RM.View.ServerList;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.ServerView.ServerTree
{
    /// <summary>
    /// ServerTreeView.xaml 的交互逻辑
    /// </summary>
    public partial class ServerTreeView : UserControl
    {
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
    }
}
