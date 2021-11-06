using System.Windows;
using PRM.Core.Protocol;
using PRM.Core.Protocol.FileTransmit.FTP;

namespace PRM.View.ProtocolEditors
{
    public partial class FTPForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public FTPForm(ProtocolServerBase vm) : base(vm)
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
