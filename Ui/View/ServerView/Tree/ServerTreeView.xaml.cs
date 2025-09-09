using _1RM.Service.DataSource;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _1RM.View.ServerView.Tree
{
    /// <summary>
    /// ServerTreeView.xaml 的交互逻辑
    /// </summary>
    public partial class ServerTreeView : ServerViewBase
    {
        public ServerTreeView()
        {
            InitializeComponent();
            this.Loaded += (sender, args) => MainTreeView.Focus();
        }

        #region Drag and Drop Support

        private TreeViewItem? _draggedItem = null;
        private Cursor? _draggedCursor = null;
        private ServerView.Tree.ServerTreeViewModel.TreeNode? _draggedNode = null;

        private void MasterGrid_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedItem == null)
            {
                _draggedCursor = null;
                Mouse.OverrideCursor = null;
                return;
            }
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _draggedItem = null;
                _draggedNode = null;
            }
        }

        private void TreeViewItemGrid_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not UIElement uie) return;
            _draggedItem = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(uie);
            if (_draggedItem?.DataContext is not ServerView.Tree.ServerTreeViewModel.TreeNode node) return;
            // Don't allow dragging root folders
            if (node.IsRootFolder) return;
            _draggedNode = node;
            _draggedItem.GiveFeedback += DragSource_GiveFeedback;
            var dataObj = new DataObject();
            dataObj.SetData("DraggedTreeNode", node);
            dataObj.SetData("DraggedTreeViewItem", _draggedItem);
            DragDrop.DoDragDrop(_draggedItem, dataObj, DragDropEffects.Move);
        }

        private void DragSource_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_draggedCursor == null || _draggedCursor == Cursors.Hand)
            {
                e.UseDefaultCursors = true;
                Mouse.OverrideCursor = null;
            }
            else
            {
                e.UseDefaultCursors = false;
                Mouse.OverrideCursor = _draggedCursor;
                e.Handled = true;
            }
        }




        private void TreeViewItem_OnDragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || _draggedNode == null) return;
            if (sender is not TreeViewItem)
            {
                _draggedCursor = Cursors.Arrow;
                return;
            }
            e.Handled = true;
            if (sender is TreeViewItem { DataContext: ServerView.Tree.ServerTreeViewModel.TreeNode targetNode } treeViewItem)
            {
                if (e.Data.GetData("DraggedTreeNode") is ServerView.Tree.ServerTreeViewModel.TreeNode draggedNode)
                {
                    if (DataContext is ServerView.Tree.ServerTreeViewModel viewModel)
                    {
                        e.Effects = DragDropEffects.Move;
                        // Set cursor based on drag operation type
                        SetDragOverCursor(e, treeViewItem, draggedNode, targetNode);
                        return;
                    }
                }
            }
            _draggedCursor = null;
            Mouse.OverrideCursor = null;
            e.Effects = DragDropEffects.None;
        }


        /// <summary>
        /// Set appropriate cursor based on the drag operation that will be performed
        /// </summary>
        private void SetDragOverCursor(DragEventArgs e, TreeViewItem targetTreeViewItem, ServerView.Tree.ServerTreeViewModel.TreeNode draggedNode, ServerView.Tree.ServerTreeViewModel.TreeNode targetNode)
        {
            var element = targetTreeViewItem.FindElementByName("GridNode");
            if (element == null) return;
            double elementActualHeight = element.ActualHeight;
            SimpleLogHelper.Debug($"targetTreeViewItem.ActualHeight = {targetTreeViewItem.ActualHeight} => {elementActualHeight}");
            Cursor? draggedCursor = null;
            try
            {
                if (draggedNode == targetNode
                    || targetNode.GetDataBaseNode() != draggedNode.GetDataBaseNode())
                {
                    draggedCursor = Cursors.No;
                    return;
                }
                // for folder moves, prevent moving into descendants
                if (draggedNode.IsFolder && draggedNode.FindDescendant(targetNode))
                {
                    SimpleLogHelper.Debug("Can not move node: " + draggedNode.Name + ", target is a descendant of source.");
                    draggedCursor = Cursors.No;
                    return;
                }

                if (LocalityTreeViewService.Settings.ServerOrderBy == EnumServerOrderBy.Custom)
                {
                    if (targetNode.IsFolder)
                    {
                        if (targetNode.IsRootFolder)
                        {
                            draggedCursor = Cursors.Hand;
                            return;
                        }
                        var mousePosition = e.GetPosition(element);
                        var relativeY = mousePosition.Y / elementActualHeight;
                        draggedCursor = relativeY switch
                        {
                            < 1.0 / 3.0 => Cursors.ScrollN,
                            <= 2.0 / 3.0 => Cursors.Hand,
                            _ => Cursors.ScrollS,
                        };
                    }
                    else
                    {
                        var mousePosition = e.GetPosition(targetTreeViewItem);
                        draggedCursor = mousePosition.Y < elementActualHeight / 2 ? Cursors.ScrollN : Cursors.ScrollS; // Reorder before or after
                    }
                }
                else
                {
                    if (!targetNode.IsFolder && targetNode.ParentNode == draggedNode.ParentNode)
                    {
                        SimpleLogHelper.Debug("Can not move node: " + draggedNode.Name + ", target server's parent is the same as source parent.");
                        draggedCursor = Cursors.No;
                        return;
                    }
                    draggedCursor = Cursors.Hand;
                }
            }
            finally
            {
                if (draggedCursor == Cursors.Hand && draggedNode.ParentNode == targetNode)
                    draggedCursor = Cursors.No;
                if (draggedCursor == Cursors.Hand)
                {
                    var dbNode = draggedNode.GetDataBaseNode();
                    var dataSource = IoC.Get<DataSourceService>().GetDataSource(dbNode?.Name ?? "");
                    if (dataSource?.IsWritable != true)
                    {
                        draggedCursor = Cursors.No;
                    }
                }
                _draggedCursor = draggedCursor;
            }
        }

        private void TreeViewItem_OnDrop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || _draggedNode == null) return;
            if (sender is not TreeViewItem)
            {
                _draggedItem = null;
                _draggedNode = null;
                _draggedCursor = null;
                Mouse.OverrideCursor = null;
                return;
            }
            e.Handled = true;
            try
            {
                if (sender is TreeViewItem { DataContext: ServerView.Tree.ServerTreeViewModel.TreeNode targetNode } targetTreeViewItem &&
                    e.Data.GetData("DraggedTreeNode") is ServerView.Tree.ServerTreeViewModel.TreeNode draggedNode && DataContext is ServerView.Tree.ServerTreeViewModel viewModel)
                {
                    if (_draggedCursor == Cursors.Hand || _draggedCursor == Cursors.ScrollN || _draggedCursor == Cursors.ScrollS)
                    {
                        var movTarget = targetNode;
                        if (_draggedCursor == Cursors.ScrollN || _draggedCursor == Cursors.ScrollS) // the case move child to front of back of its parent folder
                        {
                            movTarget = targetNode.IsFolder ? targetNode.ParentNode! : targetNode;
                        }
                        if (draggedNode.IsFolder)
                            viewModel.FolderMoveToFolder(draggedNode, movTarget);
                        else
                            viewModel.ServerMoveToFolder(draggedNode, movTarget);
                    }
                    if (_draggedCursor == Cursors.ScrollN)
                    {
                        viewModel.NodeMoveToReorderInSameFolder(draggedNode, targetNode, insertBefore: true);
                    }
                    else if (_draggedCursor == Cursors.ScrollS)
                    {
                        viewModel.NodeMoveToReorderInSameFolder(draggedNode, targetNode, insertBefore: false);
                    }
                }
            }
            catch (System.Exception ex)
            {
                SimpleLogHelper.Warning($"Error during drag and drop: {ex.Message}");
                MessageBoxHelper.ErrorAlert($"Error moving item: {ex.Message}");
            }
            finally
            {
                // Clear drag state
                _draggedItem = null;
                _draggedNode = null;
                // Reset cursor when drag operation completes
                _draggedCursor = null;
                Mouse.OverrideCursor = null;
            }
        }

        #endregion

        private void ServerTreeView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ServerView.Tree.ServerTreeViewModel viewModel)
                return;

            // Don't handle key events when a TextBox has focus
            if (Keyboard.FocusedElement is TextBox)
                return;

            switch (e.Key)
            {
                case Key.Space:
                    // Toggle expand/collapse of a tree node
                    if (viewModel.SelectedNode != null)
                    {
                        viewModel.SelectedNode.IsExpanded = !viewModel.SelectedNode.IsExpanded;
                        e.Handled = true;
                    }
                    break;
            }
        }

        #region Show info when mouse move over tree view item
        private UIElement? _lastElement = null;
        private void MainTreeView_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is not ServerView.Tree.ServerTreeViewModel viewModel) return;
            if (e.OriginalSource is UIElement uie)
            {
                if (_lastElement == uie) return;
                _lastElement = uie;
                var ti = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(uie);
                if (ti?.DataContext is ServerView.Tree.ServerTreeViewModel.TreeNode td)
                {
                    viewModel.SelectedServerViewModel = td.Server;
                    return;
                }
            }
            viewModel.SelectedServerViewModel = null;
        }
        #endregion



        private ServerView.Tree.ServerTreeViewModel.TreeNode? _shiftSelectStartItem = null;
        private void MainTreeViewItem_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not ServerView.Tree.ServerTreeViewModel viewModel) return;
            if (e.ClickCount != 1 || sender is not DependencyObject obj) return;
            if (null != MyVisualTreeHelper.VisualUpwardSearch<CheckBox>(obj)) return;
            // shift or ctrl + mouse button down to select item
            var treeViewItem = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(obj);
            if (treeViewItem?.DataContext is ServerView.Tree.ServerTreeViewModel.TreeNode mouseDownNode)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    mouseDownNode.IsCheckboxSelected = !mouseDownNode.IsCheckboxSelected;
                    _shiftSelectStartItem = null;
                }
                else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    _shiftSelectStartItem ??= mouseDownNode;
                    var startNode = _shiftSelectStartItem;
                    var endNode = mouseDownNode;
                    var currentTreeNodes = viewModel.RootNodes.SelectMany(x => x.GetChildNodes(true)).ToList();
                    int startIdx = currentTreeNodes.IndexOf(startNode);
                    int endIdx = currentTreeNodes.IndexOf(endNode);
                    if (startIdx < 0 || endIdx < 0)
                    {
                        return;
                    }
                    if (startIdx > endIdx)
                        (startIdx, endIdx) = (endIdx, startIdx);
                    for (int i = 0; i < currentTreeNodes.Count; i++)
                    {
                        if (i >= startIdx && i <= endIdx)
                        {
                            currentTreeNodes[i].IsCheckboxSelected = true;
                        }
                        else
                        {
                            currentTreeNodes[i].IsCheckboxSelected = false;
                        }
                    }
                    SyncCheckboxSelected(currentTreeNodes[startIdx]);
                    SyncCheckboxSelected(currentTreeNodes[endIdx]);
                    e.Handled = true;
                }
                else
                {
                    _shiftSelectStartItem = mouseDownNode;
                }
            }
        }

        private void ItemsCheckBox_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ServerView.Tree.ServerTreeViewModel viewModel) return;
            if (e.OriginalSource is not UIElement uie) return;
            var ti = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(uie);
            if (ti?.DataContext is ServerView.Tree.ServerTreeViewModel.TreeNode td)
            {
                SyncCheckboxSelected(td);
            }
        }

        private void SyncCheckboxSelected(ServerView.Tree.ServerTreeViewModel.TreeNode td)
        {
            while (true)
            {
                if (DataContext is not ServerView.Tree.ServerTreeViewModel viewModel) return;
                var parentNode = td.ParentNode;
                if (parentNode == null) return;
                if (parentNode.Children.All(x => x.IsCheckboxSelected == true) && parentNode.IsCheckboxSelected != true)
                {
                    parentNode.IsCheckboxSelected = true;
                    td = parentNode;
                    continue;
                }
                else if (parentNode.Children.Any(x => x.IsCheckboxSelected != false) && parentNode.IsCheckboxSelected != null)
                {
                    parentNode.IsCheckboxSelected = null;
                    td = parentNode;
                    continue;
                }
                else if (parentNode.Children.All(x => x.IsCheckboxSelected == false) && parentNode.IsCheckboxSelected != false)
                {
                    parentNode.IsCheckboxSelected = false;
                    td = parentNode;
                    continue;
                }
                break;
            }
        }
    }
}
