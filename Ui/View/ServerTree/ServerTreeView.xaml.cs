using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ServerTreeViewModel viewModel && e.NewValue is ServerTreeViewModel.TreeNode node)
            {
                viewModel.SelectedNode = node;
                if (!node.IsTag && node.Server != null)
                {
                    viewModel.CmdServerSelected(node.Server);
                }
                else
                {
                    viewModel.CmdCancelSelected();
                }
            }
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
    }
}
