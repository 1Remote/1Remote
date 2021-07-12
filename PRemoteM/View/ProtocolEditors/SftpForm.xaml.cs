using System.Windows;
using Microsoft.Win32;
using PRM.Core.Protocol;
using PRM.Core.Protocol.FileTransmit.SFTP;

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
                var dlg = new OpenFileDialog();
                dlg.Filter = "ppk|*.*";
                if (dlg.ShowDialog() == true)
                {
                    ((ProtocolServerSFTP)Vm).PrivateKey = dlg.FileName;
                }
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
