using System;
using System.Windows;
using System.Windows.Controls;
using _1RM.Model.Protocol;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class SftpFormView : UserControl
    {
        public SftpFormView()
        {
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                if (DataContext is SftpFormViewModel { New: var vm })
                {
	                bool? privateKey = false;
                    if (vm.PrivateKey == vm.ServerEditorDifferentOptions)
                    {
                        privateKey = null;
                    }
                    if (!string.IsNullOrEmpty(vm.PrivateKey))
                    {
                        privateKey = true;
                    }
                    CbUsePrivateKey.IsChecked = privateKey;
                }
            };
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is SftpFormViewModel { New: var sftp })
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                sftp.PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && DataContext is SftpFormViewModel { New: var sftp })
            {
                if (cb.IsChecked == false)
                {
                    sftp.PrivateKey = "";
                }
                else
                {
                    sftp.Password = "";
                }
            }
        }
    }
}
