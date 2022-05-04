using System;
using System.Linq;
using System.Windows.Data;

namespace PRM.Resources.Converter
{
    public class GetClearNote : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string markDown)
            {
                if (markDown.Any(@char => @char != ' ' && @char != '\t' && @char != '\r' && @char != '\n'))
                    return markDown;
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }
}