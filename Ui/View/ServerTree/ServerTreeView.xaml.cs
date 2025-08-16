using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using _1RM.Utils;
using _1RM.View;
using _1RM.Model;
using _1RM.Service.Locality;
using Stylet;

namespace _1RM.View.ServerTree
{
    /// <summary>
    /// ServerTreeView.xaml 的交互逻辑
    /// </summary>
    public partial class ServerTreeView : UserControl
    {
        public ServerTreeView()
        {
            InitializeComponent();
            this.Loaded += (sender, args) => MainTreeView.Focus();
        }

        #region Drag and Drop Support

        private TreeViewItem? _draggedItem = null;
        private ServerTreeViewModel.TreeNode? _draggedNode = null;


        private void TreeViewItemGrid_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not UIElement uie) return;
            _draggedItem = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(uie);
            if (_draggedItem?.DataContext is not ServerTreeViewModel.TreeNode node) return;
            // Don't allow dragging root folders
            if (node.IsRootFolder) return;
            _draggedNode = node;
            var dataObj = new DataObject();
            dataObj.SetData("DraggedTreeNode", node);
            dataObj.SetData("DraggedTreeViewItem", _draggedItem);
            DragDrop.DoDragDrop(_draggedItem, dataObj, DragDropEffects.Move);
        }

        private void TreeViewItemGrid_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedItem = null;
            _draggedNode = null;
        }



        private void TreeViewItem_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedItem == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _draggedItem = null;
                _draggedNode = null;
            }
        }

        private void TreeViewItem_OnDragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || _draggedNode == null) return;
            if (sender is TreeViewItem { DataContext: ServerTreeViewModel.TreeNode targetNode } treeViewItem)
            {
                if (e.Data.GetData("DraggedTreeNode") is ServerTreeViewModel.TreeNode draggedNode)
                {
                    if (DataContext is ServerTreeViewModel viewModel && viewModel.CanMoveNode(draggedNode, targetNode))
                    {
                        e.Effects = DragDropEffects.Move;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void TreeViewItem_OnDrop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || _draggedNode == null) return;
            try
            {
                if (sender is TreeViewItem targetTreeViewItem &&
                    targetTreeViewItem.DataContext is ServerTreeViewModel.TreeNode targetNode &&
                    e.Data.GetData("DraggedTreeNode") is ServerTreeViewModel.TreeNode draggedNode &&
                    DataContext is ServerTreeViewModel viewModel)
                {
                    if (viewModel.CanMoveNode(draggedNode, targetNode))
                    {
                        bool success = false;

                        if (draggedNode.IsFolder)
                        {
                            // Move folder to target location
                            success = viewModel.FolderMoveToFolder(draggedNode, targetNode);
                            if (success)
                            {
                                SimpleLogHelper.Debug($"Successfully moved folder '{draggedNode.Name}' to '{targetNode.Name}'");
                            }
                        }
                        else if (!draggedNode.IsFolder)
                        {
                            // Move server to folder first
                            success = viewModel.ServerMoveToFolder(draggedNode, targetNode);
                            
                            // For custom ordering, also handle reordering within the same folder
                            if (success && IoC.Get<MainWindowViewModel>().ServerOrderBy == EnumServerOrderBy.Custom)
                            {
                                if (targetNode.IsFolder)
                                {
                                    // Server moved to folder - put at end, no reordering needed
                                }
                                else
                                {
                                    // Determine insert position based on mouse position for server-to-server drops
                                    var mousePosition = e.GetPosition(targetTreeViewItem);
                                    bool insertBefore = mousePosition.Y < targetTreeViewItem.ActualHeight / 2;
                                    viewModel.ReorderServersInSameFolder(draggedNode, targetNode, insertBefore);
                                }
                            }
                            
                            if (success)
                            {
                                SimpleLogHelper.Debug($"Successfully moved server '{draggedNode.Name}' to '{targetNode.Name}'");
                            }
                        }

                        if (!success)
                        {
                            SimpleLogHelper.Warning($"Failed to move '{draggedNode.Name}' to '{targetNode.Name}'");
                        }
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
            }

            e.Handled = true;
        }

        #endregion

        private void ServerTreeView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ServerTreeViewModel viewModel)
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
            if (DataContext is not ServerTreeViewModel viewModel) return;
            if (e.OriginalSource is UIElement uie)
            {
                if (_lastElement == uie) return;
                _lastElement = uie;
                var ti = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(uie);
                if (ti?.DataContext is ServerTreeViewModel.TreeNode td)
                {
                    viewModel.SelectedServerViewModel = td.Server;
                    return;
                }
            }
            viewModel.SelectedServerViewModel = null;
        }
        #endregion



        private ServerTreeViewModel.TreeNode? _shiftSelectStartItem = null;
        private void MainTreeViewItem_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not ServerTreeViewModel viewModel) return;
            if (e.ClickCount != 1 || sender is not DependencyObject obj) return;
            if (null != MyVisualTreeHelper.VisualUpwardSearch<CheckBox>(obj)) return;
            // shift or ctrl + mouse button down to select item
            var treeViewItem = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(obj);
            if (treeViewItem?.DataContext is ServerTreeViewModel.TreeNode mouseDownNode)
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
            if (DataContext is not ServerTreeViewModel viewModel) return;
            if (e.OriginalSource is not UIElement uie) return;
            var ti = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(uie);
            if (ti?.DataContext is ServerTreeViewModel.TreeNode td)
            {
                SyncCheckboxSelected(td);
            }
        }

        private void SyncCheckboxSelected(ServerTreeViewModel.TreeNode td)
        {
            while (true)
            {
                if (DataContext is not ServerTreeViewModel viewModel) return;
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
