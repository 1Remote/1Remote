using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PRM.Core.Protocol;
using Shawn.Utils;

namespace PRM.Resources.Converter
{
    public class ConverterIsGreaterThan : IValueConverter
    {
        public int CompareValue { get; set; } = 0;
        // Converter={StaticResource ConverterIsGreaterThan},ConverterParameter=50}
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int v = (int)value;
            return v > CompareValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
    public class ConverterIsLowerThan : IValueConverter
    {
        public int CompareValue { get; set; } = 0;
        // Converter={StaticResource ConverterIsGreaterThan},ConverterParameter=50}
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int v = (int)value;
            return v < CompareValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }

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
                var server = (ProtocolServerBase)values[0];
                string keyWord = values[1].ToString();
                string selectedGroup = values[2].ToString();

                bool bGroupMatched = string.IsNullOrEmpty(selectedGroup) || server.GroupName == selectedGroup || server.GetType() == typeof(ProtocolServerNone);
                if (!bGroupMatched)
                    return false;

                if (string.IsNullOrEmpty(keyWord))
                    return true;
                //var f1 = KeyWordMatchHelper.IsMatchPinyinKeyWords(server.DispName, keyWord, out var m1);
                //if (f1)
                //{
                //    return true;
                //}
                //return false;


                var keyWords = keyWord.Split(new string[]{" "}, StringSplitOptions.RemoveEmptyEntries);
                var keyWordIsMatch = new List<bool>(keyWords.Length);
                for (var i = 0; i < keyWords.Length; i++)
                    keyWordIsMatch.Add(false);

                var dispName = server.DispName;
                var subTitle = server.SubTitle;
                for (var i = 0; i < keyWordIsMatch.Count; i++)
                {
                    var f1 = dispName.IsMatchPinyinKeyWords(keyWords[i], out var m1);
                    var f2 = subTitle.IsMatchPinyinKeyWords(keyWords[i], out var m2);
                    keyWordIsMatch[i] = f1 || f2;
                }

                if (keyWordIsMatch.All(x => x == true))
                    return true;
                return false;
            }
            catch (Exception)
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
