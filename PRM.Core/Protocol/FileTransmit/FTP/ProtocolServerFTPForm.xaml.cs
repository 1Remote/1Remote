using System;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
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
            GridPrivateKey.Visibility = Visibility.Collapsed;


            if (Vm.GetType() == typeof(ProtocolServerFTP)
                || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrPortUserPwdBase))
            {
                GridPrivateKey.Visibility =
                GridUserName.Visibility =
                    GridPwd.Visibility = Visibility.Visible;
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
    }



    public class ConverterESshVersion : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int)((ProtocolServerFTP.ESshVersion)value) - 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ProtocolServerFTP.ESshVersion)(int.Parse(value.ToString()));
        }
        #endregion
    }
}
