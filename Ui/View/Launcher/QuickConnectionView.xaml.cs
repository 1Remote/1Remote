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
                case Key.Tab:
                    _lvm.ToggleQuickConnection();
                    break;
                case Key.Enter:
                    OpenSessionAndHide();
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }


        public void OpenSessionAndHide()
        {
            var pwdDlg = IoC.Get<PasswordPopupDialogViewModel>();
            //pwdDlg.Result = _vm.SelectedProtocol;
            pwdDlg.Title = "TXT: connect to " + _vm.Filter;
            if (_windowManager.ShowDialog(pwdDlg) == true)
            {
                MessageBox.Show($"Your Username = {pwdDlg.Result.UserName}, Pwd = {pwdDlg.Result.Password}, Others NotImplementedException!");
                // todo open a quick connection
                // GlobalEventHelper.OnRequestQuickConnect?.Invoke(item.Id);
            }
            _lvm.HideMe();
        }
    }
}
