using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Model;
using PRM.Service;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace PRM.View
{
    public partial class LauncherWindowView : WindowChromeBase
    {
        private readonly LauncherWindowViewModel _vm;
        public LauncherWindowView(LauncherWindowViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
        }


        public override void WinTitleBar_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            try
            {
                this.DragMove();
            }
            catch
            {
            }
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
                        _vm.HideMe();
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
                    _vm.HideActionsList();
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
                    _vm.HideMe();
                    return;
                }

                if (GridMenuActions.Visibility == Visibility.Visible)
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
                                    _vm.ShowActionsList();
                                    return;
                                }
                            }
                            break;

                        case Key.Enter:
                            _vm.OpenSessionAndHide();
                            return;
                        case Key.Down:
                            _vm.AddSelectedIndexOnVisibilityItems(1);
                            return;
                        case Key.PageDown:
                            _vm.AddSelectedIndexOnVisibilityItems(5);
                            return;
                        case Key.Up:
                            _vm.AddSelectedIndexOnVisibilityItems(-1);
                            return;
                        case Key.PageUp:
                            _vm.AddSelectedIndexOnVisibilityItems(-5);
                            return;
                    }
                    e.Handled = false;
                }
            }
        }

        private void ListBoxSelections_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                _vm.OpenSessionAndHide();
        }


        private void ListBoxSelections_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm.SelectedIndex >= 0 &&
                _vm.SelectedIndex < IoC.Get<GlobalData>().VmItemList.Count)
            {
                // TODO BUG 鼠标右键打开菜单时，SelectedIndex 还未改变，打开的菜单实际是上一个选中项目的菜单，可以通过listbox item 中绑定右键action来修复，也可以向上搜索虚拟树找到右键时所选的项
                _vm.ShowActionsList();
            }
        }

        private void ListBoxActions_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _vm.HideActionsList();
        }

        private void ButtonActionBack_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _vm.HideActionsList();
        }

        private void ListBoxActions_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_vm?.SelectedItem?.Server?.Id == null)
                return;
            var si = _vm.SelectedActionIndex;
            _vm.HideMe();
            if (_vm.Actions.Count > 0
                && si >= 0
                && si < _vm.Actions.Count)
            {
                _vm.Actions[si]?.Run();
            }
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Space)
            {
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.F4)
            {
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }


        private void OpenHyperlink(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            try
            {
                HyperlinkHelper.OpenUriBySystem(e.Parameter.ToString());
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
            }
        }

        private void ClickOnImage(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            MessageBox.Show($"URL: {e.Parameter}");
        }
    }
}