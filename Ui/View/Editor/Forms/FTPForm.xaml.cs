using System.Windows;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.Protocol.FileTransmit;

namespace _1RM.View.Editor.Forms
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
