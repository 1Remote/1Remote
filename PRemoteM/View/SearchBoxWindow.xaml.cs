using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.Model;
using Shawn.Utils;
using PRM.ViewModel;

namespace PRM.View
{
    public partial class SearchBoxWindow : WindowChromeBase
    {
        private readonly VmSearchBox _vm;
        private readonly PrmContext _context;

        public SearchBoxWindow(PrmContext context)
        {
            _context = context;
            InitializeComponent();

            ShowActivated = true;
            ShowInTaskbar = false;

            double gridMainWidth = (double)FindResource("GridMainWidth");
            double oneItemHeight = (double)FindResource("OneItemHeight");
            double oneActionItemHeight = (double)FindResource("OneActionItemHeight");
            double cornerRadius = (double)FindResource("CornerRadius");
            _vm = new VmSearchBox(context, gridMainWidth, oneItemHeight, oneActionItemHeight, cornerRadius, GridSelections, GridMenuActions);

            DataContext = _vm;
            Loaded += (sender, args) =>
            {
                HideMe();
                Deactivated += (sender1, args1) => { Dispatcher.Invoke(HideMe); };
                KeyDown += (sender1, args1) =>
                {
                    if (args1.Key == Key.Escape) HideMe();
                };
            };
            Show();

            SystemConfig.Instance.Launcher.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfigLauncher.HotKeyKey) ||
                    args.PropertyName == nameof(SystemConfigLauncher.HotKeyModifiers))
                {
                    SetHotKey();
                }
            };

            _vm.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(VmSearchBox.SelectedIndex))
                {
                    ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                }
            };
        }

        private readonly object _hideToggleLocker = new object();
        private bool _isHidden = false;
        private void HideMe()
        {
            if (_isHidden == false)
                lock (_hideToggleLocker)
                {
                    if (_isHidden == false)
                    {
                        this.Visibility = Visibility.Hidden;
                        _isHidden = true;
                        this.Hide();
                        _vm.HideActionsList();
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

            if (!SystemConfig.Instance.Launcher.Enable) return;
            if (_isHidden != true) return;

            lock (_hideToggleLocker)
            {
                WindowState = WindowState.Normal;

                if (_isHidden != true) return;

                // show position
                var p = ScreenInfoEx.GetMouseSystemPosition();
                var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(p);
                this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
                this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;

                _vm.Filter = "";

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

        protected override void WinTitleBar_OnPreviewMouseMove(object sender, MouseEventArgs e)
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

        private void TbKeyWord_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isHidden) return;
            lock (_keyDownLocker)
            {
                var key = e.Key;

                if (key == Key.Escape)
                {
                    HideMe();
                    return;
                }
                else if (GridMenuActions.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    switch (key)
                    {
                        case Key.Enter:
                            if (_vm.Actions.Count > 0
                                && _vm.SelectedActionIndex >= 0
                                && _vm.SelectedActionIndex < _vm.Actions.Count)
                            {
                                if (_vm?.SelectedItem?.Server?.Id == null)
                                    return;
                                var id = _vm.SelectedItem.Server.Id;
                                var si = _vm.SelectedActionIndex;
                                HideMe();
                                _vm.Actions[si]?.Run(id);
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
                else
                {
                    e.Handled = true;
                    switch (key)
                    {
                        case Key.Right:
                            if (sender is TextBox tb)
                            {
                                if (tb.CaretIndex != tb.Text.Length)
                                {
                                    return;
                                }
                            }

                            if (_vm.SelectedIndex >= 0 &&
                                _vm.SelectedIndex < _vm.Context.AppData.VmItemList.Count)
                            {
                                _vm.ShowActionsList();
                            }
                            return;

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
            var r = GlobalHotkeyHooker.Instance.Register(this, (uint)SystemConfig.Instance.Launcher.HotKeyModifiers, SystemConfig.Instance.Launcher.HotKeyKey, this.ShowMe);
            var title = SystemConfig.Instance.Language.GetText("messagebox_title_warning");
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    break;

                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    {
                        var msg = $"{SystemConfig.Instance.Language.GetText("hotkey_registered_fail")}: {r.Item2}";
                        SimpleLogHelper.Warning(msg);
                        MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.None);
                        break;
                    }
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    {
                        var msg = $"{SystemConfig.Instance.Language.GetText("hotkey_already_registered")}: {r.Item2}";
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
            var si = _vm.SelectedIndex;
            HideMe();
            if (si >= 0 && si < _vm.Context.AppData.VmItemList.Count)
            {
                var s = _vm.Context.AppData.VmItemList[si];
                GlobalEventHelper.OnRequestServerConnect?.Invoke(s.Server.Id, _assignTabTokenThisTime);
            }
        }

        private void ListBoxSelections_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm.SelectedIndex >= 0 &&
                _vm.SelectedIndex < _vm.Context.AppData.VmItemList.Count)
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
            var id = _vm.SelectedItem.Server.Id;
            var si = _vm.SelectedActionIndex;
            HideMe();
            if (_vm.Actions.Count > 0
                && si >= 0
                && si < _vm.Actions.Count)
            {
                _vm.Actions[si]?.Run(id);
            }
        }
    }
}