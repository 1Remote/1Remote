using System;
using System.Linq;
using System.Windows.Data;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor.Forms
{
    public partial class VncForm : FormBase
    {
        public VncForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
        }
    }


    public class ConverterEVncWindowResizeMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(VNC.EVncWindowResizeMode)).Cast<int>().Max() + 1;
            return ((int)((VNC.EVncWindowResizeMode)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (VNC.EVncWindowResizeMode)(int.Parse(value.ToString() ?? "0"));
        }
        #endregion
    }

}
