using System;
using System.Threading.Tasks;
using _1RM.Service;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using _1RM.Utils;
using Stylet;
using Shawn.Utils;

namespace _1RM.View.Settings.General
{
    /// <summary>
    /// GeneralSettingView.xaml 的交互逻辑
    /// </summary>
    public partial class GeneralSettingView : UserControl
    {
        public GeneralSettingView()
        {
            InitializeComponent();
            Task.Factory.StartNew(async () =>
            {
                var b = await SecondaryVerificationHelper.GetEnabled();
                Execute.OnUIThread(() =>
                {
                    MsAppCenterHelper.TraceSpecial($"App start with - Windows Hello", b.ToString());
                    CbRequireWindowsPasswordBeforeSensitiveOperation.IsChecked = b;
                });
            });
        }

        private async void CbRequireWindowsPasswordBeforeSensitiveOperation_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (DataContext is GeneralSettingViewModel vm && sender is CheckBox cb)
            {
                var ret = await SecondaryVerificationHelper.VerifyAsyncUi();
                if (ret == true)
                {
                    cb.IsChecked = cb.IsChecked != true;
                    SecondaryVerificationHelper.SetEnabled(cb.IsChecked == true);
                }
            }
        }
    }





    public class ConverterEnumLogLevel : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return 0;
            return (int)((SimpleLogHelper.EnumLogLevel)value);
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (SimpleLogHelper.EnumLogLevel)(int.Parse(value.ToString() ?? "0"));
        }
    }
}
