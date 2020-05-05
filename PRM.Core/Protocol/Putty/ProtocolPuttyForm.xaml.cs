using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PRM.Core.Protocol.Putty.SSH;


namespace PRM.Core.Protocol.Putty
{
    /// <summary>
    /// ServerRDPEditForm.xaml 的交互逻辑
    /// </summary>
    public partial class ProtocolPuttyForm : ProtocolServerFormBase
    {
        public readonly ProtocolPutttyBase Vm;
        public ProtocolPuttyForm(ProtocolPutttyBase vm) : base()
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;
            SpSsh.Visibility = Visibility.Collapsed;
            if (Vm.GetType() == typeof(ProtocolServerSSH))
            {
                GridUserName.Visibility =
                GridPwd.Visibility =
                SpSsh.Visibility = Visibility.Visible;
            }
        }

        public override bool CanSave()
        {
            if (Vm.GetType() == typeof(ProtocolServerSSH))
            {
                var protocol = (ProtocolServerSSH) Vm;
                if (!string.IsNullOrEmpty(protocol.Address?.Trim())
                    && !string.IsNullOrEmpty(protocol.UserName?.Trim())
                    && !string.IsNullOrEmpty(protocol.Password?.Trim())
                    && protocol.Port > 0)
                    return true;
            }
            else
            {

            }
            //if (!string.IsNullOrEmpty(_vm.Address?.Trim())
            //    && !string.IsNullOrEmpty(_vm.UserName?.Trim())
            //    && !string.IsNullOrEmpty(_vm.Password?.Trim())
            //    && _vm.Port > 0)
            //    return true;
            return false;
        }
    }
}
