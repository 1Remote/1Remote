using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Model;
using PRM.Service;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
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

            ShowActivated = true;
            ShowInTaskbar = false;


            Loaded += (sender, args) =>
            {
                Hide();
                SetHotKey();
                GlobalEventHelper.OnLauncherHotKeyChanged += SetHotKey;
                Deactivated += (s, a) => { HideMe(); };
                KeyDown += (s, a) => { if (a.Key == Key.Escape) HideMe(); };
            };
        }

        private readonly object _hideToggleLocker = new object();
        private bool _isHidden = false;
        public void HideMe()
        {
            if (_isHidden == false)
                lock (_hideToggleLocker)
                {
                    if (_isHidden == false)
                    {
                        this.Visibility = Visibility.Hidden;
                        _isHidden = true;
                        this.Hide();
                        GridMenuActions.Visibility = Visibility.Hidden;
                        _vm.Filter = "";
                    }
                }
            SimpleLogHelper.Debug("Call HideMe()");
        }

        private string _assignTabTokenThisTime = null;

        public void ShowMe()
        {
            ShowMe(null);
        }

        public void ShowMe(string assignTabTokenThisTime)
        {
            SimpleLogHelper.Debug($"Call shortcut to invoke launcher _isHidden = {_isHidden}");
            _assignTabTokenThisTime = assignTabTokenThisTime;
            GridMenuActions.Visibility = Visibility.Hidden;

            if (IoC.Get<MainWindowViewModel>().TopLevelViewModel != null) return;
            if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false) return;
            if (_isHidden != true) return;

            lock (_hideToggleLocker)
            {
                WindowState = WindowState.Normal;

                if (_isHidden != true) return;


                _vm.Filter = "";
                _vm.CalcVisibleByFilter();
                // show position
                var p = ScreenInfoEx.GetMouseSystemPosition();
                var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(p);
                this.Top = screenEx.VirtualWorkingAreaCenter.Y - _vm.GridMainHeight / 2;
                this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;

                this.Show();
                this.Visibility = Visibility.Visible;
                this.Activate();
                this.Topmost = true;  // important
                this.Topmost = false; // important
                this.Topmost = true;  // important
                this.Focus();         // important
                TbKeyWord.Focus();
                _isHidden = false;
            }
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

        private readonly object _keyDownLocker = new object();

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
                        HideMe();
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
            if (_isHidden) return;

            if (TbKeyWord.IsKeyboardFocused == false)
                TbKeyWord.Focus();

            e.Handled = true;
            lock (_keyDownLocker)
            {
                var key = e.Key;

                if (key == Key.Escape)
                {
                    HideMe();
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
                            OpenSessionAndHide();
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

        /// <summary>
        /// use it after Show() has been called
        /// </summary>
        public void SetHotKey()
        {
            GlobalHotkeyHooker.Instance.Unregist(this);
            if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false)
                return;
            var r = GlobalHotkeyHooker.Instance.Register(this, (uint)IoC.Get<ConfigurationService>().Launcher.HotKeyModifiers, IoC.Get<ConfigurationService>().Launcher.HotKeyKey, this.ShowMe);
            var title = IoC.Get<ILanguageService>().Translate("messagebox_title_warning");
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    break;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    {
                        var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_registered_fail")}: {r.Item2}";
                        SimpleLogHelper.Warning(msg);
                        MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.None);
                        break;
                    }
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    {
                        var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_already_registered")}: {r.Item2}";
                        SimpleLogHelper.Warning(msg);
                        MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.None);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(r.Item1.ToString());
            }
        }

        private void ListBoxSelections_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                OpenSessionAndHide();
        }

        private void OpenSessionAndHide()
        {
            HideMe();
            var item = _vm.SelectedItem;
            if (item?.Id != null)
            {
                GlobalEventHelper.OnRequestServerConnect?.Invoke(item.Id);
            }
        }

        private void ListBoxSelections_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm.SelectedIndex >= 0 &&
                _vm.SelectedIndex < IoC.Get<GlobalData>().VmItemList.Count)
            {
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
            HideMe();
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
    }
}