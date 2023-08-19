using System.Threading.Tasks;
using _1RM.Service;
using Google.Protobuf.WellKnownTypes;
using System.Windows.Controls;
using System.Windows.Input;
using Stylet;

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
}
