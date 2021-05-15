using System;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using PRM.Core.Protocol.BaseClassForm;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;

namespace PRM.Core.Protocol.FileTransmit.FTP
{
    public partial class ProtocolServerFTPForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolServerFTPForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;


            if (Vm.GetType() == typeof(ProtocolServerFTP)
                || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortUserPwdBase))
            {
                GridUserName.Visibility =
                    GridPwd.Visibility = Visibility.Visible;
            }
        }
    }
}
