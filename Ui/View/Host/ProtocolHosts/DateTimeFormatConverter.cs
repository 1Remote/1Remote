using System;
using System.Globalization;
using System.Windows.Data;
using _1RM.Service;

namespace _1RM.View.Host.ProtocolHosts
{
    /// <summary>
    /// Converter for DateTime formatting in FileTransmitHost
    /// </summary>
    [ValueConversion(typeof(DateTime), typeof(string))]
    public class DateTimeFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dateTime)
                return "";

            var configService = IoC.TryGet<ConfigurationService>();
            if (configService == null)
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss");

            var format = configService.General.FileTransmitDateTimeFormat;
            
            return format switch
            {
                0 => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                1 => dateTime.ToString("yyyy-MM-dd hh:mm:ss tt"),
                2 => dateTime.ToString("HH:mm:ss yyyy-MM-dd"),
                3 => dateTime.ToString("hh:mm:ss tt yyyy-MM-dd"),
                4 => dateTime.ToString("MM/dd/yyyy HH:mm:ss"),
                5 => dateTime.ToString("MM/dd/yyyy hh:mm:ss tt"),
                _ => dateTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
