using System;
using System.Windows;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.FileTransmit;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.View.Editor.Forms
{
    public partial class SftpForm : FormBase
    {
        public readonly ProtocolBase Vm;
        public SftpForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            if (Vm.GetType() == typeof(SFTP))
            {
                CbUsePrivateKey.IsChecked = false;
                if (((SFTP)Vm).PrivateKey == vm.ServerEditorDifferentOptions)
                {
                    CbUsePrivateKey.IsChecked = null;
                }
                if (!string.IsNullOrEmpty(((SFTP)Vm).PrivateKey))
                {
                    CbUsePrivateKey.IsChecked = true;
                }
            }
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (Vm.GetType() == typeof(SFTP))
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                ((SFTP)Vm).PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (CbUsePrivateKey.IsChecked == false)
            {
                if (Vm.GetType() == typeof(SFTP))
                    ((SFTP)Vm).PrivateKey = "";
            }
        }
    }
}
