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
using PRM.Core.Protocol.BaseClassForm;


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
            if (value == null)
                return Enum.GetValues(typeof(ERdpFullScreenFlag)).Cast<int>().Max() + 1;
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
            if (value == null)
                return false;
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
            if (value == null)
                return Enum.GetValues(typeof(ERdpWindowResizeMode)).Cast<int>().Max() + 1;
            return ((int)((ERdpWindowResizeMode)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (ERdpWindowResizeMode)(int.Parse(value.ToString()));
        }
        #endregion
    }



    public class ConverterEDisplayPerformance : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EDisplayPerformance)).Cast<int>().Max() + 1;
            return ((int)((EDisplayPerformance)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (EDisplayPerformance)(int.Parse(value.ToString()));
        }
        #endregion
    }



    public class ConverterEGatewayMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EGatewayMode)).Cast<int>().Max() + 1;
            return ((int)((EGatewayMode)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (EGatewayMode)(int.Parse(value.ToString()));
        }
        #endregion
    }



    public class ConverterEGatewayLogonMethod : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EGatewayLogonMethod)).Cast<int>().Max() + 1;
            return ((int)((EGatewayLogonMethod)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (EGatewayLogonMethod)(int.Parse(value.ToString()));
        }
        #endregion
    }

    public class ConverterEAudioRedirectionMode : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EAudioRedirectionMode)).Cast<int>().Max() + 1;
            return ((int)((EAudioRedirectionMode)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (EAudioRedirectionMode)(int.Parse(value.ToString()));
        }
    }
}
