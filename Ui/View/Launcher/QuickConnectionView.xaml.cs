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

namespace _1RM.View.Launcher
{
    /// <summary>
    /// QuickConnectionView.xaml 的交互逻辑
    /// </summary>
    public partial class QuickConnectionView : UserControl
    {
        private readonly LauncherWindowViewModel _lvm;
        public QuickConnectionView(LauncherWindowViewModel lvm)
        {
            _lvm = lvm;
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
                case Key.Tab:
                    _lvm.ToggleQuickConnection();
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }
    }
}
