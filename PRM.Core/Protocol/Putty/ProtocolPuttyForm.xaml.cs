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
        public readonly ProtocolServerBase Vm;
        public ProtocolPuttyForm(ProtocolServerBase vm) : base()
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;

            GridUserName.Visibility = Visibility.Collapsed;
            GridPwd.Visibility = Visibility.Collapsed;
            SpSsh.Visibility = Visibility.Collapsed;
            if (Vm.GetType() == typeof(ProtocolServerSSH)
             || Vm.GetType().BaseType == typeof(ProtocolServerWithAddrBase))
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
                    && !string.IsNullOrEmpty(protocol.UserName?.Trim()))
                    return true;
            }
            return false;
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
