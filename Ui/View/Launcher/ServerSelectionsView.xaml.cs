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
        private readonly ServerSelectionsViewModel _vm;
        private readonly LauncherWindowViewModel _lvm;
        public ServerSelectionsView(ServerSelectionsViewModel vm, LauncherWindowViewModel lvm)
        {
            _vm = vm;
            _lvm = lvm;
            InitializeComponent();
        }


        private void MenuActions(Key key)
        {
            switch (key)
            {
                case Key.Enter:
                    if (_vm.Actions.Count > 0
                        && _vm.SelectedActionIndex >= 0
                        && _vm.SelectedActionIndex < _vm.Actions.Count)
                    {
                        if (_vm?.SelectedItem?.Server?.Id == null)
                            return;
                        var si = _vm.SelectedActionIndex;
                        _lvm.HideMe();
                        _vm.Actions[si]?.Run();
                    }
                    break;

                case Key.Down:
                    if (_vm.SelectedActionIndex < _vm.Actions.Count - 1)
                    {
                        ++_vm.SelectedActionIndex;
                        ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                    }
                    break;

                case Key.Up:
                    if (_vm.SelectedActionIndex > 0)
                    {
                        --_vm.SelectedActionIndex;
                        ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                    }
                    break;

                case Key.PageUp:
                    if (_vm.SelectedActionIndex > 0)
                    {
                        _vm.SelectedActionIndex =
                            _vm.SelectedActionIndex - 5 < 0 ? 0 : _vm.SelectedActionIndex - 5;
                        ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                    }
                    break;

                case Key.PageDown:
                    if (_vm.SelectedActionIndex < _vm.Actions.Count - 1)
                    {
                        _vm.SelectedActionIndex =
                            _vm.SelectedActionIndex + 5 > _vm.Actions.Count - 1
                                ? _vm.Actions.Count - 1
                                : _vm.SelectedActionIndex + 5;
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
            if (Visibility != Visibility.Visible) return;

            if (TbKeyWord.IsKeyboardFocused == false)
                TbKeyWord.Focus();

            e.Handled = true;
            lock (this)
            {
                var key = e.Key;

                if (key == Key.Escape)
                {
                    _lvm.HideMe();
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
                                _vm.CmdShowNoteField?.Execute();
                            else
                                _vm.CmdHideNoteField?.Execute();
                            return;
                        case Key.PageUp:
                            AddSelectedIndexOnVisibilityItems(-5);
                            return;

                        case Key.Tab:
                            _lvm.ToggleQuickConnection();
                            return;
                    }
                    e.Handled = false;
                }
            }
        }



        private void ListBoxSelections_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 鼠标右键打开菜单时，SelectedIndex 还未改变，打开的菜单实际是上一个选中项目的菜单，可以通过listbox item 中绑定右键action来修复，也可以向上搜索虚拟树找到右键时所选的项
            if (MyVisualTreeHelper.VisualUpwardSearch<ListBoxItem>(e.OriginalSource as DependencyObject) is ListBoxItem { Content: ProtocolBaseViewModel baseViewModel })
            {
                ShowActionsList(baseViewModel.Server);
            }
        }


        private void ListBoxActions_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            HideActionsList();
        }


        private void ListBoxActions_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_vm?.SelectedItem?.Server?.Id == null)
                return;
            var si = _vm.SelectedActionIndex;
            _lvm.HideMe();
            if (_vm.Actions.Count > 0
                && si >= 0
                && si < _vm.Actions.Count)
            {
                _vm.Actions[si]?.Run();
            }
        }

        private void ButtonActionBack_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            HideActionsList();
        }



        public void ShowActionsList(ProtocolBase? protocolBase = null)
        {
            if (protocolBase == null)
            {
                if (_vm.SelectedIndex < 0
                    || _vm.SelectedIndex >= _vm.VmServerList.Count)
                {
                    return;
                }
                protocolBase = _vm.VmServerList[_vm.SelectedIndex].Server;
            }

            _vm.Actions = new ObservableCollection<ProtocolAction>(protocolBase.GetActions());
            _vm.SelectedActionIndex = 0;

            GridActionsList.Visibility = Visibility.Visible;
            _lvm.ReSetWindowHeight();

            var sb = new Storyboard();
            sb.AddSlideFromLeft(0.3, LauncherWindowViewModel.LAUNCHER_LIST_AREA_WIDTH);
            sb.Begin(GridActionsList);
        }



        public void HideActionsList()
        {
            var sb = new Storyboard();
            sb.AddSlideToLeft(0.3, LauncherWindowViewModel.LAUNCHER_LIST_AREA_WIDTH);
            sb.Completed += (o, args) =>
            {
                GridActionsList.Visibility = Visibility.Hidden;
                _lvm.ReSetWindowHeight();
            };
            sb.Begin(GridActionsList);
        }


        public void OpenSessionAndHide()
        {
            var item = _vm.SelectedItem;
            _lvm.HideMe();
            if (item?.Id != null)
            {
                GlobalEventHelper.OnRequestServerConnect?.Invoke(item.Id, fromView: nameof(LauncherWindowView));
            }
        }


        public void AddSelectedIndexOnVisibilityItems(int step)
        {
            var index = _vm.SelectedIndex + step;
            if (index < 0)
                index = 0;
            if (index >= _vm.VmServerList.Count)
                index = _vm.VmServerList.Count - 1;
            _vm.SelectedIndex = index;
        }

        private void ListBoxSelections_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                OpenSessionAndHide();
        }

        private void ButtonShowNote_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string id })
            {
                var s = _vm.VmServerList.FirstOrDefault(x => x.Id == id);
                if (s != null)
                {
                    _vm.SelectedIndex = _vm.VmServerList.IndexOf(s);
                }
            }
            _vm.CmdShowNoteField.Execute();
        }
    }
}
