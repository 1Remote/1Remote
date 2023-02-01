using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace _1RM.Controls.NoteDisplay
{
    public class ConverterNoteToVisibility : IValueConverter
    {
        public static bool IsVisible(string? markDown)
        {
            if (markDown == null || string.IsNullOrWhiteSpace(markDown))
                return false;
            return markDown.Any(@char => @char != ' ' && @char != '\t' && @char != '\r' && @char != '\n');
        }

        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string markDown)
            {
                if (IsVisible(markDown))
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }

    public class ConverterNoteToSingleLineNote : IValueConverter
    {
        public static string MarkdownToSingleLine(string? markDown)
        {
            if (markDown == null || string.IsNullOrWhiteSpace(markDown))
                return "";
            markDown = markDown.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
            while (markDown.IndexOf("  ", StringComparison.Ordinal) > 0)
            {
                markDown = markDown.Replace("  ", " ");
            }
            return markDown.Trim();
        }

        public static bool IsEmpty(string? markDown)
        {
            return string.IsNullOrEmpty(MarkdownToSingleLine(markDown));
        }

        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string markDown)
            {
                return MarkdownToSingleLine(markDown);
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
