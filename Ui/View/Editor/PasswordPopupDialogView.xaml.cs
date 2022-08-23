using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.Editor
{
    /// <summary>
    /// PasswordPopupDialogView.xaml 的交互逻辑
    /// </summary>
    public partial class PasswordPopupDialogView : WindowChromeBase
    {
        public List<ProtocolBaseViewModel> ProtocolList { get; }
        public ProtocolBaseWithAddressPortUserPwd Result { get; } = new FTP();
        public PasswordPopupDialogView(List<ProtocolBaseViewModel> protocolList)
        {
            ProtocolList = protocolList;

            InitializeComponent();

            BtnClose.Click += (sender, args) =>
            {
                this.DialogResult = false;
            };

            DataContext = this;
        }
    }
}
