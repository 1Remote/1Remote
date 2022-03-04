using System.Windows;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.FileTransmit;

namespace PRM.View.Editor.Forms
{
    public partial class FTPForm : FormBase
    {
        public readonly ProtocolBase Vm;
        public FTPForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;


            if (Vm.GetType() == typeof(FTP)
                || Vm.GetType().BaseType == typeof(ProtocolBaseWithAddressPortUserPwd))
            {
                GridUserName.Visibility =
                    GridPwd.Visibility = Visibility.Visible;
            }
        }
    }
}
