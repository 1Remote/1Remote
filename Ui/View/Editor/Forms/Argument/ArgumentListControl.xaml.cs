using System;
using System.Windows.Controls;
using System.Windows.Data;
using _1RM.Model.Protocol.Base;

namespace _1RM.View.Editor.Forms.Argument
{
    /// <summary>
    /// ArgumentListControl.xaml 的交互逻辑
    /// </summary>
    public partial class ArgumentListControl : UserControl
    {
        public ArgumentListControl()
        {
            InitializeComponent();
        }
    }



    public class ConverterStringIs1 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value.ToString() == "1")
                    return true;
                if (value.ToString() == ProtocolBase.ServerEditorStaticDifferentOptions)
                    return null;
            }
            catch (Exception e)
            {
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool and true ? "1" : "0";
        }
    }
}
