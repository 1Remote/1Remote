using System.Collections.Generic;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.Editor
{
    /// <summary>
    /// PasswordPopupDialogView.xaml 的交互逻辑
    /// </summary>
    public partial class PasswordPopupDialogView : WindowChromeBase
    {
        private readonly PasswordPopupDialogViewModel _vm;
        public ProtocolBaseWithAddressPortUserPwd Result { get; } = new FTP();
        public PasswordPopupDialogView(PasswordPopupDialogViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            DataContext = this;
        }
    }
}
