using System;
using System.Linq;
using System.Windows.Data;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;

namespace _1RM.View.Editor.Forms
{
    public partial class VncFormView : FormBase
    {
        public VncFormView()
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
