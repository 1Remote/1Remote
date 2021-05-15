using System;
using System.Linq;
using System.Windows.Data;
using PRM.Core.Protocol.BaseClassForm;


namespace PRM.Core.Protocol.VNC
{
    public partial class ProtocolServerVNCForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolServerVNCForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }


    public class ConverterEVncWindowResizeMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(ProtocolServerVNC.EVncWindowResizeMode)).Cast<int>().Max() + 1;
            return ((int)((ProtocolServerVNC.EVncWindowResizeMode)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ProtocolServerVNC.EVncWindowResizeMode)(int.Parse(value.ToString()));
        }
        #endregion
    }

}
