using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.ViewModel;
using Shawn.Ulits;

namespace PRM.View
{
    /// <summary>
    /// SearchBoxWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SearchBoxWindow : Window
    {
        private readonly VmSearchBox _vmSearchBox = null;


        public SearchBoxWindow()
        {
            InitializeComponent();
            ShowInTaskbar = false;
            _vmSearchBox = new VmSearchBox();
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
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            // make popup control moves with parent by https://stackoverflow.com/questions/5736359/popup-control-moves-with-parent
            PopupSelections.HorizontalOffset += 1;
            PopupSelections.HorizontalOffset -= 1;
            PopupActions.HorizontalOffset += 1;
            PopupActions.HorizontalOffset -= 1;
            base.OnLocationChanged(e);
        }

        private readonly object _closeLocker = new object();
        private bool _isHidden = false;
        private void HideMe()
        {
            if (_isHidden == false)
                lock (_closeLocker)
                {
                    if (_isHidden == false)
                    {
                        this.Visibility = Visibility.Hidden;
                        _vmSearchBox.PopupSelectionsIsOpen = false;
                        _vmSearchBox.PopupActionsIsOpen = false;
                        _isHidden = true;
                        this.Hide();
                    }
                }
        }




        public void ShowMe()
        {
            SimpleLogHelper.Debug("Call shortcut to invoke quick window.");
            _vmSearchBox.DispNameFilter = "";
            if (SystemConfig.Instance.QuickConnect.Enable)
                if (_isHidden == true)
                    lock (_closeLocker)
                    {
                        if (_isHidden == true)
                        {
                            var p = ScreenInfoEx.GetMouseSystemPosition();
                            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(p);
                            this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
                            this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;
                            this.Show();
                            this.Visibility = Visibility.Visible;
                            this.Activate();
                            this.Topmost = true;  // important
                            this.Topmost = false; // important
                            this.Focus();         // important
                            TbKeyWord.Focus();
                            _isHidden = false;
                            _vmSearchBox.PopupSelectionsIsOpen = false;
                            _vmSearchBox.PopupActionsIsOpen = false;
                        }
                    }
        }










        private void WindowHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }




        private readonly object _keyDownLocker = new object();
        private void TbKeyWord_OnKeyDown(object sender, KeyEventArgs e)
        {
            lock (_keyDownLocker)
            {
                if (_vmSearchBox.Servers.Count == 0)
                {
                    _vmSearchBox.PopupSelectionsIsOpen = false;
                    _vmSearchBox.PopupActionsIsOpen = false;
                    return;
                }

                var key = e.Key;
                if (key == Key.Escape)
                {
                    HideMe();
                    return;
                }
                else if (_vmSearchBox.PopupSelectionsIsOpen)
                {
                    switch (key)
                    {
                        case Key.Enter:
                            HideMe();
                            if (_vmSearchBox.SelectedServerIndex >= 0 && _vmSearchBox.SelectedServerIndex < _vmSearchBox.Servers.Count)
                            {
                                var s = _vmSearchBox.Servers[_vmSearchBox.SelectedServerIndex];
                                GlobalEventHelper.OnServerConnect?.Invoke(s.Server.Id);
                            }
                            break;
                        case Key.Down:
                            if (_vmSearchBox.SelectedServerIndex < _vmSearchBox.Servers.Count - 1)
                            {
                                ++_vmSearchBox.SelectedServerIndex;
                                ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                            }
                            break;
                        case Key.Up:
                            if (_vmSearchBox.SelectedServerIndex > 0)
                            {
                                --_vmSearchBox.SelectedServerIndex;
                                ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                            }
                            break;
                        case Key.PageUp:
                            if (_vmSearchBox.SelectedServerIndex > 0)
                            {
                                _vmSearchBox.SelectedServerIndex =
                                    _vmSearchBox.SelectedServerIndex - 5 < 0 ? 0 : _vmSearchBox.SelectedServerIndex - 5;
                                ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                            }
                            break;
                        case Key.PageDown:
                            if (_vmSearchBox.SelectedServerIndex < _vmSearchBox.Servers.Count - 1)
                            {
                                _vmSearchBox.SelectedServerIndex =
                                    _vmSearchBox.SelectedServerIndex + 5 > _vmSearchBox.Servers.Count - 1
                                        ? _vmSearchBox.Servers.Count - 1
                                        : _vmSearchBox.SelectedServerIndex + 5;
                                ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                            }
                            break;
                        case Key.Right:
                            if (sender is TextBox tb)
                            {
                                if (tb.CaretIndex != tb.Text.Length)
                                {
                                    return;
                                }
                            }
                            if (_vmSearchBox.SelectedServerIndex >= 0 && _vmSearchBox.SelectedServerIndex < _vmSearchBox.Servers.Count)
                            {
                                _vmSearchBox.ShowActionsList();
                            }
                            e.Handled = true;
                            break;
                    }
                }
                else if (_vmSearchBox.PopupActionsIsOpen)
                {
                    switch (key)
                    {
                        case Key.Enter:
                            HideMe();
                            if (_vmSearchBox.Actions.Count > 0
                                && _vmSearchBox.SelectedActionIndex >= 0
                                && _vmSearchBox.SelectedActionIndex < _vmSearchBox.Actions.Count)
                            {
                                _vmSearchBox.Actions[_vmSearchBox.SelectedActionIndex]?.Run();
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
                            _vmSearchBox.PopupSelectionsIsOpen = true;
                            _vmSearchBox.PopupActionsIsOpen = false;
                            e.Handled = true;
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
            var r = GlobalHotkeyHooker.Instance.Regist(this, SystemConfig.Instance.QuickConnect.HotKeyModifiers, SystemConfig.Instance.QuickConnect.HotKeyKey, this.ShowMe);
            var title = SystemConfig.Instance.Language.GetText("messagebox_title_warning");
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    break;
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    {
                        var msg = $"{SystemConfig.Instance.Language.GetText("info_hotkey_registered_fail")}: {r.Item2}";
                        SimpleLogHelper.Warning(msg);
                        MessageBox.Show(msg, title);
                        break;
                    }
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    {
                        var msg = $"{SystemConfig.Instance.Language.GetText("info_hotkey_already_registered")}: {r.Item2}";
                        SimpleLogHelper.Warning(msg);
                        MessageBox.Show(msg, title);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
