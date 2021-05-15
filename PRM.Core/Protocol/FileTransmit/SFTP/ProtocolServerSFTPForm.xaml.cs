using System;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using PRM.Core.Protocol.BaseClassForm;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;

namespace PRM.Core.Protocol.FileTransmit.SFTP
{
    public partial class ProtocolServerSFTPForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolServerSFTPForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;
            GridPrivateKey.Visibility = Visibility.Collapsed;


            if (Vm.GetType() == typeof(ProtocolServerSFTP)
                || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortUserPwdBase))
            {
                GridPrivateKey.Visibility =
                GridUserName.Visibility =
                    GridPwd.Visibility = Visibility.Visible;
            }

            if (Vm.GetType() == typeof(ProtocolServerSFTP))
            {
                CbUsePrivateKey.IsChecked = false;
                if(((ProtocolServerSFTP)Vm).PrivateKey == vm.Server_editor_different_options)
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
