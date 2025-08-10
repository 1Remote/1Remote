using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shawn.Utils;
using Shawn.Utils.Wpf;

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

        private UIElement? _lastElement = null;
        private void MainTreeView_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is not ServerTreeViewModel viewModel) return;
            if (e.OriginalSource is UIElement uie)
            {
                if(_lastElement == uie) return;
                _lastElement = uie;
                var ti = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(uie);
                if (ti?.DataContext is ServerTreeViewModel.TreeNode td)
                {
                    viewModel.SetSelectedServer(td.Server);
                    return;
                }
            }
            viewModel.SetSelectedServer(null);
        }



        private ServerTreeViewModel.TreeNode? _shiftSelectStartItem = null;
        private void MainTreeViewItem_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not ServerTreeViewModel viewModel) return;
            if (e.ClickCount != 1 || sender is not DependencyObject obj) return;
            // shift or ctrl + mouse button down to select item
            var treeViewItem = MyVisualTreeHelper.VisualUpwardSearch<TreeViewItem>(obj);
            if (treeViewItem?.DataContext is ServerTreeViewModel.TreeNode vm)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    vm.IsCheckboxSelected = !vm.IsCheckboxSelected;
                }
                else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    if (_shiftSelectStartItem != null)
                    {
                        var path1 = viewModel.GetTreePath(_shiftSelectStartItem, true);
                        var path2 = viewModel.GetTreePath(vm, true);
                        if (path1.Count != path2.Count)
                        {
                            return;
                        }
                        for (int i = 0; i < path1.Count; i++)
                        {
                            if (path1[i] != path2[i])
                            {
                                return;
                            }
                        }

                        // Select all items between _shiftSelectStartItem and vm
                        var parentNode = viewModel.FindParent(_shiftSelectStartItem);
                        if (parentNode == null)
                        {
                            _shiftSelectStartItem = null;
                            return;
                        }

                        
                        int startIdx = parentNode.Children.IndexOf(_shiftSelectStartItem);
                        int endIdx = parentNode.Children.IndexOf(vm);
                        if (startIdx < 0 || endIdx < 0)
                        {
                            _shiftSelectStartItem = null;
                            return;
                        }

                        if (startIdx > endIdx)
                            (startIdx, endIdx) = (endIdx, startIdx);
                        for (int i = 0; i < parentNode.Children.Count; i++)
                        {
                            if (i >= startIdx && i <= endIdx)
                            {
                                parentNode.Children[i].IsCheckboxSelected = true;
                            }
                            else
                            {
                                parentNode.Children[i].IsCheckboxSelected = false;
                            }
                        }
                        e.Handled = true;
                    }
                }
                else
                {
                    _shiftSelectStartItem = vm;
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
                var parentNode = viewModel.FindParent(td);
                if (parentNode == null) return;
                if (parentNode.Children.All(x => x.IsCheckboxSelected == true) && parentNode.IsCheckboxSelected != true)
                {
                    parentNode.IsCheckboxSelected = true;
                    td = parentNode;
                    continue;
                }
                else if (parentNode.Children.Any(x => x.IsCheckboxSelected == true) && parentNode.IsCheckboxSelected != null)
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
