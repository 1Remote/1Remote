using _1RM.Service;
using Google.Protobuf.WellKnownTypes;
using System.Windows.Controls;
using System.Windows.Input;

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
                    vm.RequireWindowsPasswordBeforeSensitiveOperation = cb.IsChecked == true;
                }
            }
        }
    }
}
