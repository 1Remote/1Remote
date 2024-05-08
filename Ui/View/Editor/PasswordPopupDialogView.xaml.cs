using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
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
        public ProtocolBaseWithAddressPortUserPwd Result { get; } = new FTP();
        public PasswordPopupDialogView()
        {
            InitializeComponent();
            PreviewKeyUp += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    BtnQuit.Command.Execute(null);
                }
            };
        }

        private void TbUserName_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TbPwd.Focus();
            }
        }

        private void TbPwd_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSave.Command.Execute(null);
            }
        }
    }
}
