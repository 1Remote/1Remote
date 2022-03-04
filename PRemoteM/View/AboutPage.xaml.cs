using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shawn.Utils;

namespace PRM.View
{
    public partial class AboutPage : UserControl
    {
        public readonly AboutPageViewModel Vm;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public AboutPage(AboutPageViewModel vm, MainWindowViewModel mainWindowViewModel)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = Vm;
            _mainWindowViewModel = mainWindowViewModel;
            TbVersion.Text = AppVersion.Version;

#if FOR_MICROSOFT_STORE_ONLY
            TbAppName.Text = ConfigurationService.AppName + "(Store)";
#endif
        }

        private void SupportText_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 3)
            {
                ConsoleManager.Toggle();
            }
        }

        private void ButtonBack_OnClick(object sender, RoutedEventArgs e)
        {
            _mainWindowViewModel.AnimationPageAbout = null;
        }
    }
}