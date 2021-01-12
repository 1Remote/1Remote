using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PRM.Core.Model;


namespace PRM.Core.Protocol.RDP
{
    public partial class ProtocolServerRDPForm : ProtocolServerFormBase
    {
        public ProtocolServerRDP Vm;
        public ProtocolServerRDPForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = (ProtocolServerRDP)vm;
            DataContext = vm;
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




    public class ConverterTrueWhenERdpFullScreen : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((ERdpFullScreenFlag)value == ERdpFullScreenFlag.Disable)
                return false;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return 0;
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



    public class ConverterEGatewayMode2Bool : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var e = (EGatewayMode) parameter;
            return e == (EGatewayMode) value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var e = (EGatewayMode) parameter;
            return e;
        }
        #endregion
    }



    public class ConverterEGatewayLogonMethod : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int)((EGatewayLogonMethod)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (EGatewayLogonMethod)(int.Parse(value.ToString()));
        }
        #endregion
    }
}
