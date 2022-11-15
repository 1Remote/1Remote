using System;
using System.Windows.Data;

namespace _1RM.View.Settings;

public class StringIsEmpty2BoolConverter : IValueConverter
{
    #region IValueConverter 成员

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        string str = value?.ToString() ?? "";
        return string.IsNullOrEmpty(str);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    #endregion IValueConverter 成员
}