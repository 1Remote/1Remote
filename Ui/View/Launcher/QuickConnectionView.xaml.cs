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
using _1RM.View.Editor;
using Stylet;

namespace _1RM.View.Launcher
{
    /// <summary>
    /// QuickConnectionView.xaml 的交互逻辑
    /// </summary>
    public partial class QuickConnectionView : UserControl
    {
        private readonly IWindowManager _windowManager;
        private readonly LauncherWindowViewModel _lvm;
        private readonly QuickConnectionViewModel _vm;
        public QuickConnectionView(QuickConnectionViewModel qvm, LauncherWindowViewModel lvm, IWindowManager windowManager)
        {
            _vm = qvm;
            _lvm = lvm;
            _windowManager = windowManager;
            InitializeComponent();
        }

        private void TbKeyWord_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Visibility != Visibility.Visible) return;

            if (TbKeyWord.IsKeyboardFocused == false)
                TbKeyWord.Focus();

            var key = e.Key;
            if (key == Key.Escape)
            {
                _lvm.HideMe();
                return;
            }

            e.Handled = true;
            switch (key)
            {
                default:
                    e.Handled = false;
                    break;
                case Key.Tab:
                    _lvm.ToggleQuickConnection();
                    break;
                case Key.Enter:
                    _vm.OpenConnection();
                    break;

                case Key.Down:
                    if (_vm.SelectedIndex < _vm.ConnectHistory.Count - 1)
                    {
                        ++_vm.SelectedIndex;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;

                case Key.Up:
                    if (_vm.SelectedIndex > 0)
                    {
                        --_vm.SelectedIndex;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;

                case Key.PageUp:
                    if (_vm.SelectedIndex > 0)
                    {
                        _vm.SelectedIndex =
                            _vm.SelectedIndex - 5 < 0 ? 0 : _vm.SelectedIndex - 5;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;

                case Key.PageDown:
                    if (_vm.SelectedIndex < _vm.ConnectHistory.Count - 1)
                    {
                        _vm.SelectedIndex =
                            _vm.SelectedIndex + 5 > _vm.ConnectHistory.Count - 1
                                ? _vm.ConnectHistory.Count - 1
                                : _vm.SelectedIndex + 5;
                        ListBoxHistory.ScrollIntoView(ListBoxHistory.SelectedItem);
                    }
                    break;
            }
        }
    }
}
