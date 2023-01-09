using System;
using System.Windows;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.Protocol.FileTransmit;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class SftpForm : FormBase
    {
        public SftpForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();

            if (vm.GetType() == typeof(SFTP))
            {
                CbUsePrivateKey.IsChecked = false;
                if (((SFTP)vm).PrivateKey == vm.ServerEditorDifferentOptions)
                {
                    CbUsePrivateKey.IsChecked = null;
                }
                if (!string.IsNullOrEmpty(((SFTP)vm).PrivateKey))
                {
                    CbUsePrivateKey.IsChecked = true;
                }
            }
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (_vm is SFTP sftp)
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                sftp.PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (CbUsePrivateKey.IsChecked == false)
            {
                if (_vm is SFTP sftp)
                    sftp.PrivateKey = "";
            }
            else
            {
                if (_vm is SFTP sftp)
                    sftp.Password = "";
            }
        }
    }
}
