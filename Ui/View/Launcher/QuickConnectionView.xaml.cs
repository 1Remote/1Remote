using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using _1RM.Model;
using _1RM.Service;
using _1RM.View.Editor;
using Stylet;

namespace _1RM.View.Launcher
{
    /// <summary>
    /// QuickConnectionView.xaml 的交互逻辑
    /// </summary>
    public partial class QuickConnectionView : UserControl
    {
        public QuickConnectionView()
        {
            InitializeComponent();
        }

        private void TbKeyWord_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;
            if (this.DataContext is not QuickConnectionViewModel vm) return;

            if (TbKeyWord.IsKeyboardFocused == false)
                TbKeyWord.Focus();

            var key = e.Key;
            if (key == Key.Escape)
            {
                IoC.Get<LauncherWindowViewModel>().HideMe();
                return;
            }

            e.Handled = true;
            switch (key)
            {
                default:
                    e.Handled = false;
                    break;
                case Key.Tab:
                    IoC.Get<LauncherWindowViewModel>().ToggleQuickConnection();
                    break;
                case Key.Enter:
                    vm.OpenConnection();
                    break;

                case Key.Down:
                    if (vm.SelectedIndex < vm.ConnectHistory.Count - 1)
                    {
                        ++vm.SelectedIndex;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;

                case Key.Up:
                    if (vm.SelectedIndex > 0)
                    {
                        --vm.SelectedIndex;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;

                case Key.PageUp:
                    if (vm.SelectedIndex > 0)
                    {
                        vm.SelectedIndex =
                            vm.SelectedIndex - 5 < 0 ? 0 : vm.SelectedIndex - 5;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;

                case Key.PageDown:
                    if (vm.SelectedIndex < vm.ConnectHistory.Count - 1)
                    {
                        vm.SelectedIndex =
                            vm.SelectedIndex + 5 > vm.ConnectHistory.Count - 1
                                ? vm.ConnectHistory.Count - 1
                                : vm.SelectedIndex + 5;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;
            }
        }

        private void ListBoxHistory_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;
            if (this.DataContext is not QuickConnectionViewModel vm) return;

            if (e.ClickCount == 2)
                vm.OpenConnection();
        }

        private void ButtonDeleteItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;
            if (this.DataContext is not QuickConnectionViewModel vm) return;

            if (sender is Button { Tag: QuickConnectionItem qci })
            {
                if (vm.ConnectHistory.Contains(qci))
                {
                    vm.ConnectHistory.Remove(qci);
                    IoC.Get<LocalityService>().QuickConnectionHistoryRemove(qci);
                }
                vm.SelectedIndex = 0;
            }
        }
    }
}
