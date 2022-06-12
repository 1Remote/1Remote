using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PRM.Service;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;
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
            Loaded += (sender, args) =>
            {
                var myWindowHandle = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(myWindowHandle);
                source?.AddHook(HookUSBDeviceRedirect);
            };
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
                        case Key.Left:
                            if (IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInLauncher == false)
                                _vm.CmdShowNoteField?.Execute();
                            else
                                _vm.CmdHideNoteField?.Execute();
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
            // 鼠标右键打开菜单时，SelectedIndex 还未改变，打开的菜单实际是上一个选中项目的菜单，可以通过listbox item 中绑定右键action来修复，也可以向上搜索虚拟树找到右键时所选的项
            if (MyVisualTreeHelper.VisualUpwardSearch<ListBoxItem>(e.OriginalSource as DependencyObject) is ListBoxItem { Content: ProtocolBaseViewModel baseViewModel })
            {
                _vm.ShowActionsList(baseViewModel.Server);
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
                var url = e?.Parameter?.ToString();
                if (url != null)
                {
                    HyperlinkHelper.OpenUriBySystem(url);
                }
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



        /// <summary>
        /// Redirect USB Device
        /// </summary>
        /// <returns></returns>
        private IntPtr HookUSBDeviceRedirect(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DEVICECHANGE = 0x0219;
            try
            {
                if (msg == WM_DEVICECHANGE)
                {
                    foreach (var host in IoC.Get<SessionControlService>().ConnectionId2Hosts.Where(x => x.Value is AxMsRdpClient09Host).Select(x => x.Value))
                    {
                        if (host is AxMsRdpClient09Host rdp)
                        {
                            SimpleLogHelper.Debug($"rdp.NotifyRedirectDeviceChange((uint){wParam}, (int){lParam})");
                            rdp.NotifyRedirectDeviceChange(msg, (uint)wParam, (int)lParam);
                        }
                    }
                }
            }
            finally
            {
            }
            return IntPtr.Zero;
        }
    }
}