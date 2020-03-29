using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PRM.Core.Base;
using PRM.RDP;

namespace PRM.Core.Protocol.RDP
{
    /// <summary>
    /// ServerRDPEditForm.xaml 的交互逻辑
    /// </summary>
    public partial class ServerRDPEditForm : UserControl
    {
        private ServerAbstract _vm;
        public ServerRDPEditForm(ServerAbstract vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }
    }

    public class ConverterEStartupDisplaySize : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int) ((EStartupDisplaySize) value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (EStartupDisplaySize)(int.Parse(value.ToString()));
        }
        #endregion
    }

    public class ConverterERdpResizeMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int) ((ERdpResizeMode) value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ERdpResizeMode)(int.Parse(value.ToString()));
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
