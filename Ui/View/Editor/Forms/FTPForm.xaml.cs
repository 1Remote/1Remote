using System.Windows;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Model.Protocol.FileTransmit;

namespace PRM.View.Editor.Forms
{
    public partial class FTPForm : FormBase
    {
        public FTPForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;


            if (vm.GetType() == typeof(FTP)
                || vm.GetType().BaseType == typeof(ProtocolBaseWithAddressPortUserPwd))
            {
                GridUserName.Visibility = GridPwd.Visibility = Visibility.Visible;
            }
        }
    }
}
