using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;


namespace PRM.Core.Protocol.Putty
{
    /// <summary>
    /// ServerRDPEditForm.xaml 的交互逻辑
    /// </summary>
    public partial class ProtocolPuttyForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolPuttyForm(ProtocolServerBase vm) : base()
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;
            SpSsh.Visibility = Visibility.Collapsed;
            GridPrivateKey.Visibility = Visibility.Collapsed;


            if (Vm.GetType() == typeof(ProtocolServerSSH)
                || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortUserPwdBase))
            {
                GridPrivateKey.Visibility =
                GridUserName.Visibility =
                    GridPwd.Visibility =
                SpSsh.Visibility = Visibility.Visible;
            }


            if (Vm.GetType() == typeof(ProtocolServerTelnet)
                || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortBase))
            {
                
            }

            if (Vm.GetType() == typeof(ProtocolServerSSH))
            {
                CbUsePrivateKey.IsChecked = false;
                if (!string.IsNullOrEmpty(((ProtocolServerSSH)Vm).PrivateKey))
                {
                    CbUsePrivateKey.IsChecked = true;
                }
            }
        }

        public override bool CanSave()
        {
            if ( Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortUserPwdBase))
            {
                var protocol = (ProtocolServerWithAddrPortUserPwdBase) Vm;
                if (!string.IsNullOrEmpty(protocol.Address?.Trim())
                    && !string.IsNullOrEmpty(protocol.UserName?.Trim())
                    && protocol.GetPort() > 0 && protocol.GetPort() < 65536)
                    return true;
                return false;
            }
            if ( Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortBase))
            {
                var protocol = (ProtocolServerWithAddrPortBase) Vm;
                if (!string.IsNullOrEmpty(protocol.Address?.Trim())
                    && protocol.GetPort() > 0 && protocol.GetPort() < 65536)
                    return true;
                return false;
            }
            return false;
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (Vm.GetType() == typeof(ProtocolServerSSH))
            {
                var dlg = new OpenFileDialog();
                dlg.Filter = "ppk|*.*";
                if (dlg.ShowDialog() == true)
                {
                    ((ProtocolServerSSH)Vm).PrivateKey = dlg.FileName;
                }
            }
        }

        private void CbUsePrivateKey_OnChecked(object sender, RoutedEventArgs e)
        {
            if (CbUsePrivateKey.IsChecked == false)
            {
                if (Vm.GetType() == typeof(ProtocolServerSSH))
                    ((ProtocolServerSSH)Vm).PrivateKey = "";
            }
        }
    }



    public class ConverterESshVersion : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int)((ProtocolServerSSH.ESshVersion)value) - 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ProtocolServerSSH.ESshVersion)(int.Parse(value.ToString()));
        }
        #endregion
    }
}
