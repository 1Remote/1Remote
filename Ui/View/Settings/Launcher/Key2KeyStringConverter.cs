using System;
using System.Windows.Data;
using System.Windows.Input;

namespace _1RM.View.Settings.Launcher;

/// <summary>
/// key board key A -> string "A"
/// </summary>
public class Key2KeyStringConverter : IValueConverter
{
    #region IValueConverter 成员

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var k = (Key)value;
        return k.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    #endregion IValueConverter 成员
}