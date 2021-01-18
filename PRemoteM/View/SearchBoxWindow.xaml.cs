using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.ViewModel;
using Shawn.Utils;

namespace PRM.View
{
    public partial class SearchBoxWindow : WindowChromeBase
    {
        private readonly VmSearchBox _vmSearchBox = null;


        public SearchBoxWindow()
        {
            InitializeComponent();
            ShowInTaskbar = false;


            double gridMainWidth = (double)FindResource("GridMainWidth");
            double oneItemHeight = (double)FindResource("OneItemHeight");
            double oneActionItemHeight = (double)FindResource("OneActionItemHeight");
            double cornerRadius = (double)FindResource("CornerRadius");
            _vmSearchBox = new VmSearchBox(gridMainWidth, oneItemHeight, oneActionItemHeight, cornerRadius, GridSelections, GridMenuActions);

            DataContext = _vmSearchBox;
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

            SystemConfig.Instance.QuickConnect.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfigQuickConnect.HotKeyKey) ||
                    args.PropertyName == nameof(SystemConfigQuickConnect.HotKeyModifiers))
                {
                    SetHotKey();
                }
            };

            _vmSearchBox.PropertyChanged += (sender, args) =>
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
                        _vmSearchBox.HideActionsList();
                        _vmSearchBox.Filter = "";
                    }
                }
        }

        private string _assignTabTokenThisTime = null;



        public void ShowMe()
        {
            ShowMe(null);
        }
        public void ShowMe(string assignTabTokenThisTime)
        {
            _assignTabTokenThisTime = assignTabTokenThisTime;

            if (!SystemConfig.Instance.QuickConnect.Enable)
                return;

            SimpleLogHelper.Debug("Call shortcut to invoke quick window.");
            if (_isHidden == true)
                lock (_hideToggleLocker)
                {
                    if (this.WindowState != WindowState.Normal)
                        this.WindowState = WindowState.Normal;
                    if (_isHidden == true)
                    {
                        _vmSearchBox.Filter = "";
                        var p = ScreenInfoEx.GetMouseSystemPosition();
                        var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(p);
                        this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
                        this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;
                        _vmSearchBox.UpdateItemsList("");
                        this.Show();
                        this.Visibility = Visibility.Visible;
                        this.Activate();
                        this.Topmost = true;  // important
                        this.Topmost = false; // important
                        this.Focus();         // important
                        TbKeyWord.Focus();
                        _isHidden = false;
                    }
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
        private void TbKeyWord_OnKeyDown(object sender, KeyEventArgs e)
        {
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
                    switch (key)
                    {
                        case Key.Enter:
                            if (_vmSearchBox.Actions.Count > 0
                                && _vmSearchBox.SelectedActionIndex >= 0
                                && _vmSearchBox.SelectedActionIndex < _vmSearchBox.Actions.Count)
                            {
                                if(_vmSearchBox?.SelectedItem?.Server?.Id == null)
                                    return;
                                var id = _vmSearchBox.SelectedItem.Server.Id;
                                var si = _vmSearchBox.SelectedActionIndex;
                                HideMe();
                                _vmSearchBox.Actions[si]?.Run(id);
                            }
                            break;
                        case Key.Down:
                            if (_vmSearchBox.SelectedActionIndex < _vmSearchBox.Actions.Count - 1)
                            {
                                ++_vmSearchBox.SelectedActionIndex;
                                ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                            }
                            break;
                        case Key.Up:
                            if (_vmSearchBox.SelectedActionIndex > 0)
                            {
                                --_vmSearchBox.SelectedActionIndex;
                                ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                            }
                            break;
                        case Key.PageUp:
                            if (_vmSearchBox.SelectedActionIndex > 0)
                            {
                                _vmSearchBox.SelectedActionIndex =
                                    _vmSearchBox.SelectedActionIndex - 5 < 0 ? 0 : _vmSearchBox.SelectedActionIndex - 5;
                                ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                            }
                            break;
                        case Key.PageDown:
                            if (_vmSearchBox.SelectedActionIndex < _vmSearchBox.Actions.Count - 1)
                            {
                                _vmSearchBox.SelectedActionIndex =
                                    _vmSearchBox.SelectedActionIndex + 5 > _vmSearchBox.Actions.Count - 1
                                        ? _vmSearchBox.Actions.Count - 1
                                        : _vmSearchBox.SelectedActionIndex + 5;
                                ListBoxActions.ScrollIntoView(ListBoxActions.SelectedItem);
                            }
                            break;
                        case Key.Left:
                            _vmSearchBox.HideActionsList();
                            break;
                    }
                    e.Handled = true;
                }
                else
                {
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

                            if (_vmSearchBox.SelectedIndex >= 0 &&
                                _vmSearchBox.SelectedIndex < GlobalData.Instance.VmItemList.Count)
                            {
                                _vmSearchBox.ShowActionsList();
                            }
                            e.Handled = true;
                            break;
                        case Key.Enter:
                            OpenSessionAndHide();
                            break;
                        case Key.Down:
                            if (_vmSearchBox.SelectedIndex < GlobalData.Instance.VmItemList.Count - 1)
                            {
                                var index = _vmSearchBox.SelectedIndex;
                                for (int i = _vmSearchBox.SelectedIndex + 1; i < GlobalData.Instance.VmItemList.Count; i++)
                                {
                                    if (GlobalData.Instance.VmItemList[i].ObjectVisibility == Visibility)
                                    {
                                        index = i;
                                        break;
                                    }
                                }
                                _vmSearchBox.SelectedIndex = index;
                            }
                            break;
                        case Key.Up:
                            if (_vmSearchBox.SelectedIndex > 0)
                            {
                                var index = _vmSearchBox.SelectedIndex;
                                for (int i = _vmSearchBox.SelectedIndex - 1; i >= 0; i--)
                                {
                                    if (GlobalData.Instance.VmItemList[i].ObjectVisibility == Visibility)
                                    {
                                        index = i;
                                        break;
                                    }
                                }
                                _vmSearchBox.SelectedIndex = index;
                            }
                            break;
                        case Key.PageUp:
                            if (_vmSearchBox.SelectedIndex > 0)
                            {
                                var index = _vmSearchBox.SelectedIndex;
                                int count = 0;
                                for (int i = _vmSearchBox.SelectedIndex - 1; i >= 0; i--)
                                {
                                    if (GlobalData.Instance.VmItemList[i].ObjectVisibility == Visibility)
                                    {
                                        ++count;
                                        index = i;
                                        if (count == 5)
                                            break;
                                    }
                                }
                                _vmSearchBox.SelectedIndex = index;
                            }
                            break;
                        case Key.PageDown:
                            if (_vmSearchBox.SelectedIndex < GlobalData.Instance.VmItemList.Count - 1)
                            {
                                var index = _vmSearchBox.SelectedIndex;
                                int count = 0;
                                for (int i = _vmSearchBox.SelectedIndex + 1; i < GlobalData.Instance.VmItemList.Count; i++)
                                {
                                    if (GlobalData.Instance.VmItemList[i].ObjectVisibility == Visibility)
                                    {
                                        ++count;
                                        index = i;
                                        if (count == 5)
                                            break;
                                    }
                                }
                                _vmSearchBox.SelectedIndex = index;
                            }
                            break;
                    }
                }
            }
        }


        /// <summary>
        /// use it after Show() has been called
        /// </summary>
        public void SetHotKey()
        {
            GlobalHotkeyHooker.Instance.Unregist(this);
            var r = GlobalHotkeyHooker.Instance.Regist(this, (uint)SystemConfig.Instance.QuickConnect.HotKeyModifiers, SystemConfig.Instance.QuickConnect.HotKeyKey, this.ShowMe);
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
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ListBoxSelections_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                OpenSessionAndHide();
        }

        private void OpenSessionAndHide()
        {
            var si = _vmSearchBox.SelectedIndex;
            HideMe();
            if (si >= 0 && si < GlobalData.Instance.VmItemList.Count)
            {
                var s = GlobalData.Instance.VmItemList[si];
                GlobalEventHelper.OnRequireServerConnect?.Invoke(s.Server.Id, _assignTabTokenThisTime);
            }
        }

        private void ListBoxSelections_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_vmSearchBox.SelectedIndex >= 0 &&
                _vmSearchBox.SelectedIndex < GlobalData.Instance.VmItemList.Count)
            {
                _vmSearchBox.ShowActionsList();
            }
        }

        private void ListBoxActions_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _vmSearchBox.HideActionsList();
        }

        private void ButtonActionBack_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _vmSearchBox.HideActionsList();
        }

        private void ListBoxActions_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_vmSearchBox?.SelectedItem?.Server?.Id == null)
                return;
            var id = _vmSearchBox.SelectedItem.Server.Id;
            var si = _vmSearchBox.SelectedActionIndex;
            HideMe();
            if (_vmSearchBox.Actions.Count > 0
                && si >= 0
                && si < _vmSearchBox.Actions.Count)
            {
                _vmSearchBox.Actions[si]?.Run(id);
            }
        }
    }
}
