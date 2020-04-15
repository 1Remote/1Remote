using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

            SystemConfig.GetInstance().QuickConnect.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfigQuickConnect.HotKeyKey) ||
                    args.PropertyName == nameof(SystemConfigQuickConnect.HotKeyModifiers))
                {
                    SetHotKey();
                }
            };
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
                        _vmSearchBox.DispNameFilter = "";
                        _vmSearchBox.PopupIsOpen = false;
                        _isHidden = true;
                        this.Hide();
                    }
                }
        }

        public void ShowMe()
        {
            if (SystemConfig.GetInstance().QuickConnect.Enable)
                if (_isHidden == true)
                    lock (_closeLocker)
                    {
                        if (_isHidden == true)
                        {
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
            switch (e.Key)
            {
                case Key.Escape:
                    HideMe();
                    break;

                case Key.Enter:
                    {
                        lock (_closeLocker)
                        {
                            var i = _vmSearchBox.SelectedServerTextIndex;
                            var j = _vmSearchBox.DispServerList.Count;
                            if (i < j && i >= 0)
                            {
                                var s = _vmSearchBox.DispServerList[i];
                                s.Server.Conn();
                            }
                        }
                        HideMe();
                        break;
                    }

                case Key.Down:
                    {
                        lock (_keyDownLocker)
                        {
                            if (_vmSearchBox.SelectedServerTextIndex < _vmSearchBox.DispServerList.Count - 1)
                            {
                                ++_vmSearchBox.SelectedServerTextIndex;
                                ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                            }
                        }
                        break;
                    }

                case Key.Up:
                    {
                        lock (_keyDownLocker)
                        {
                            if (_vmSearchBox.SelectedServerTextIndex > 0)
                            {
                                --_vmSearchBox.SelectedServerTextIndex;
                                ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                            }
                        }
                        break;
                    }

                case Key.PageUp:
                    {
                        lock (_keyDownLocker)
                        {
                            var i = _vmSearchBox.SelectedServerTextIndex - 5;
                            if (i < 0)
                                i = 0;
                            _vmSearchBox.SelectedServerTextIndex = i;
                            ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                        }
                        break;
                    }

                case Key.PageDown:
                    {
                        lock (_keyDownLocker)
                        {
                            var i = _vmSearchBox.SelectedServerTextIndex + 5;
                            if (i > _vmSearchBox.DispServerList.Count - 1)
                                i = _vmSearchBox.DispServerList.Count - 1;
                            _vmSearchBox.SelectedServerTextIndex = i;
                            ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// use it after Show() has been called
        /// </summary>
        public void SetHotKey()
        {
            GlobalHotkeyHooker.GetInstance().Unregist(this);
            var r = GlobalHotkeyHooker.GetInstance().Regist(this, SystemConfig.GetInstance().QuickConnect.HotKeyModifiers, SystemConfig.GetInstance().QuickConnect.HotKeyKey, this.ShowMe);
            switch (r.Item1)
            {
                case GlobalHotkeyHooker.RetCode.Success:
                    break;
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                    MessageBox.Show(SystemConfig.GetInstance().Language.GetText("info_hotkey_registered_fail") + ": " + r.Item2);
                    break;
                case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                    MessageBox.Show(SystemConfig.GetInstance().Language.GetText("info_hotkey_already_registered") + ": " + r.Item2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
