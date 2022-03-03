using System;
using System.Windows;
using Microsoft.Win32;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.FileTransmit;
using Shawn.Utils;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.View.ProtocolEditors
{
    public partial class SftpForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public SftpForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            if (Vm.GetType() == typeof(ProtocolServerSFTP))
            {
                CbUsePrivateKey.IsChecked = false;
                if (((ProtocolServerSFTP)Vm).PrivateKey == vm.Server_editor_different_options)
                {
                    CbUsePrivateKey.IsChecked = null;
                }
                if (!string.IsNullOrEmpty(((ProtocolServerSFTP)Vm).PrivateKey))
                {
                    CbUsePrivateKey.IsChecked = true;
                }
            }
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (Vm.GetType() == typeof(ProtocolServerSFTP))
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                ((ProtocolServerSFTP)Vm).PrivateKey = path;
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (CbUsePrivateKey.IsChecked == false)
            {
                if (Vm.GetType() == typeof(ProtocolServerSFTP))
                    ((ProtocolServerSFTP)Vm).PrivateKey = "";
            }
        }
    }
}
