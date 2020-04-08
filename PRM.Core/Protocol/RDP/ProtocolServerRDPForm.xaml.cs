using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace PRM.Core.Protocol.RDP
{
    /// <summary>
    /// ServerRDPEditForm.xaml 的交互逻辑
    /// </summary>
    public partial class ProtocolServerRDPForm : ProtocolServerFormBase
    {
        private ProtocolServerRDP _vm;
        public ProtocolServerRDPForm(ProtocolServerBase vm): base()
        {
            InitializeComponent();
            _vm = (ProtocolServerRDP)vm;
            DataContext = vm;
        }

        public override bool CanSave()
        {
            if (!string.IsNullOrEmpty(_vm.Address?.Trim())
                && !string.IsNullOrEmpty(_vm.UserName?.Trim())
                && !string.IsNullOrEmpty(_vm.Password?.Trim())
                && _vm.Port > 0)
                return true;
            return false;
        }
    }



    public class ConverterERdpFullScreenFlag : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int)((ERdpFullScreenFlag)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ERdpFullScreenFlag)(int.Parse(value.ToString()));
        }
        #endregion
    }

    public class ConverterERdpWindowResizeMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int)((ERdpWindowResizeMode)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ERdpWindowResizeMode)(int.Parse(value.ToString()));
        }
        #endregion
    }



    public class ConverterEDisplayPerformance : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int)((EDisplayPerformance)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (EDisplayPerformance)(int.Parse(value.ToString()));
        }
        #endregion
    }
}
