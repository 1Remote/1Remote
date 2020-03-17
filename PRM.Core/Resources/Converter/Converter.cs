using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PRM.Core.Base;
using PRM.Core.Ulits;

namespace PRM.Resources.Converter
{
    public class ConverterBool2Visible : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool ss = (bool)value;
            return ss ? "Visible" : "Collapsed";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }


    public class ConverterBool2VisibleInv : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool ss = (bool)value;
            return !ss ? "Visible" : "Collapsed";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }


    //public class ConverterDouble2Negate : IValueConverter
    //{
    //    #region IValueConverter 成员  
    //    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        double ss = (double)value;
    //        return ss * -1;
    //    }
    //    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        throw new NotSupportedException();
    //    }
    //    #endregion
    //}



    public class ConverterDouble2Half : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double ss = (double)value;
            return ss * 0.5;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }




    public class ConverterTextWidthAndContent2FontSize : IMultiValueConverter
    {
        private static Size MeasureText(TextBlock tb, int fontsize)
        {
            var formattedText = new FormattedText(tb.Text, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                fontsize, Brushes.Black); // always uses MaxFontSize for desiredSize
            return new Size(formattedText.Width, formattedText.Height);
        }

        #region IValueConverter 成员  
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                var tb = new TextBlock();
                tb.Text = values[0].ToString();
                tb.Width = int.Parse(values[1].ToString());
                tb.FontFamily = (FontFamily)values[2];
                tb.FontStyle = (FontStyle)values[3];
                tb.FontWeight = (FontWeight)values[4];
                tb.FontStretch = (FontStretch)values[5];
                var size = MeasureText(tb, 20);
                double k = 1.0 * tb.Width / size.Width;
                double fs = (int) (20 * k);
                if (fs > 16)
                    fs = 16;
                if (fs < 4)
                    fs = 4;
                return fs;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 12;
            }
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }







    public class ConverterStringIsContainXXX : IMultiValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                var server = (ServerAbstract)values[0];
                string nameFilter = values[1].ToString();
                string selectedGroup = values[2].ToString();

                if (string.IsNullOrEmpty(selectedGroup) || server.GroupName == selectedGroup)
                {
                    if (string.IsNullOrEmpty(nameFilter)
                        || KeyWordMatchHelper.IsMatchPinyinKeyWords(server.DispName, nameFilter, out var m))
                    {
                        return true;
                    }
                    //if (string.IsNullOrEmpty(nameFilter) 
                    //    || string.IsNullOrEmpty(server.DispName) 
                    //    || server.DispName.ToLower().IndexOf(nameFilter.ToLower()) >= 0
                    //    || server.DispNamePinyin.ToLower().IndexOf(nameFilter.ToLower()) >= 0
                    //    || server.DispNamePinyinInitials.ToLower().IndexOf(nameFilter.ToLower()) >= 0
                    //    )
                    //    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                return true;
            }
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
}
