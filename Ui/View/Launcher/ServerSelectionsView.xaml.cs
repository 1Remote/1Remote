using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;

namespace _1RM.View.Launcher
{
    /// <summary>
    /// ServerSelections.xaml 的交互逻辑
    /// </summary>
    public partial class ServerSelectionsView : UserControl
    {
        public ServerSelectionsView()
        {
            InitializeComponent();
        }


        private void MenuActions(Key key)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;

            switch (key)
            {
                case Key.Enter:
                    if (vm.Actions.Count > 0
                        && vm.SelectedActionIndex >= 0
                        && vm.SelectedActionIndex < vm.Actions.Count)
                    {
                        if (vm?.SelectedItem?.Server?.Id == null)
                            return;
                        var si = vm.SelectedActionIndex;
                        IoC.Get<LauncherWindowViewModel>().HideMe();
                        vm.Actions[si]?.Run();
                    }
                    break;

                case Key.Down:
                    if (vm.SelectedActionIndex < vm.Actions.Count - 1)
                    {
                        ++vm.SelectedActionIndex;
                        ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                    }
                    break;

                case Key.Up:
                    if (vm.SelectedActionIndex > 0)
                    {
                        --vm.SelectedActionIndex;
                        ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                    }
                    break;

                case Key.PageUp:
                    if (vm.SelectedActionIndex > 0)
                    {
                        vm.SelectedActionIndex =
                            vm.SelectedActionIndex - 5 < 0 ? 0 : vm.SelectedActionIndex - 5;
                        ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                    }
                    break;

                case Key.PageDown:
                    if (vm.SelectedActionIndex < vm.Actions.Count - 1)
                    {
                        vm.SelectedActionIndex =
                            vm.SelectedActionIndex + 5 > vm.Actions.Count - 1
                                ? vm.Actions.Count - 1
                                : vm.SelectedActionIndex + 5;
                        ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                    }
                    break;

                case Key.Left:
                    HideActionsList();
                    break;
            }
        }

        private void TbKeyWord_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;
            if (Visibility != Visibility.Visible) return;

            if (TbKeyWord.IsKeyboardFocused == false)
                TbKeyWord.Focus();

            e.Handled = true;
            lock (this)
            {
                var key = e.Key;

                if (key == Key.Escape)
                {
                    IoC.Get<LauncherWindowViewModel>().HideMe();
                    return;
                }

                if (GridActionsList.Visibility == Visibility.Visible)
                {
                    MenuActions(key);
                }
                else
                {
                    switch (key)
                    {
                        case Key.Right:
                            if (sender is TextBox tb)
                            {
                                if (tb.CaretIndex == tb.Text.Length)
                                {
                                    ShowActionsList();
                                    return;
                                }
                            }
                            break;

                        case Key.Enter:
                            OpenSessionAndHide();
                            return;
                        case Key.Down:
                            AddSelectedIndexOnVisibilityItems(1);
                            return;
                        case Key.PageDown:
                            AddSelectedIndexOnVisibilityItems(5);
                            return;
                        case Key.Up:
                            AddSelectedIndexOnVisibilityItems(-1);
                            return;
                        case Key.Left:
                            if (IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInLauncher == false)
                                vm.CmdShowNoteField?.Execute();
                            else
                                vm.CmdHideNoteField?.Execute();
                            return;
                        case Key.PageUp:
                            AddSelectedIndexOnVisibilityItems(-5);
                            return;

                        case Key.Tab:
                            IoC.Get<LauncherWindowViewModel>().ToggleQuickConnection();
                            return;
                    }
                    e.Handled = false;
                }
            }
        }



        private void ListBoxSelections_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            // 鼠标右键打开菜单时，SelectedIndex 还未改变，打开的菜单实际是上一个选中项目的菜单，可以通过listbox item 中绑定右键action来修复，也可以向上搜索虚拟树找到右键时所选的项
            if (MyVisualTreeHelper.VisualUpwardSearch<ListBoxItem>(e.OriginalSource as DependencyObject) is ListBoxItem { Content: ProtocolBaseViewModel baseViewModel })
            {
                ShowActionsList(baseViewModel);
            }
        }


        private void ListBoxActions_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            HideActionsList();
        }


        private void ListBoxActions_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;
            if (vm?.SelectedItem?.Server?.Id == null) return;

            var si = vm.SelectedActionIndex;
            IoC.Get<LauncherWindowViewModel>().HideMe();
            if (vm.Actions.Count > 0
                && si >= 0
                && si < vm.Actions.Count)
            {
                vm.Actions[si]?.Run();
            }
        }

        private void ButtonActionBack_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            HideActionsList();
        }



        public void ShowActionsList(ProtocolBaseViewModel? protocol = null)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;

            if (protocol == null)
            {
                if (vm.SelectedIndex < 0
                    || vm.SelectedIndex >= vm.VmServerList.Count)
                {
                    return;
                }
                protocol = vm.VmServerList[vm.SelectedIndex];
            }

            vm.Actions = new ObservableCollection<ProtocolAction>(protocol.GetActions());
            vm.SelectedActionIndex = 0;

            GridActionsList.Visibility = Visibility.Visible;
            IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();

            var sb = new Storyboard();
            sb.AddSlideFromLeft(0.3, LauncherWindowViewModel.LAUNCHER_LIST_AREA_WIDTH);
            sb.Begin(GridActionsList);
        }


        public void HideActionsList()
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            var sb = new Storyboard();
            sb.AddSlideToLeft(0.3, LauncherWindowViewModel.LAUNCHER_LIST_AREA_WIDTH);
            sb.Completed += (o, args) =>
            {
                GridActionsList.Visibility = Visibility.Hidden;
                IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();
            };
            sb.Begin(GridActionsList);
        }


        public void OpenSessionAndHide()
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;

            var item = vm.SelectedItem;
            IoC.Get<LauncherWindowViewModel>().HideMe();
            if (item?.Id != null)
            {
                GlobalEventHelper.OnRequestServerConnect?.Invoke(item.Server, fromView: $"{nameof(LauncherWindowView)} - {nameof(ServerSelectionsView)}");
            }
        }


        public void AddSelectedIndexOnVisibilityItems(int step)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;

            var index = vm.SelectedIndex + step;
            if (index < 0)
                index = 0;
            if (index >= vm.VmServerList.Count)
                index = vm.VmServerList.Count - 1;
            vm.SelectedIndex = index;
        }

        private void ListBoxSelections_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;

            if (e.ClickCount == 2)
                OpenSessionAndHide();
        }

        private void ButtonShowNote_OnClick(object sender, RoutedEventArgs e)
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: true }) return;
            if (this.DataContext is not ServerSelectionsViewModel vm) return;

            if (sender is Button { Tag: string id })
            {
                var s = vm.VmServerList.FirstOrDefault(x => x.Id == id);
                if (s != null)
                {
                    vm.SelectedIndex = vm.VmServerList.IndexOf(s);
                }
            }
            vm.CmdShowNoteField.Execute();
        }
    }
}
