using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core;
using PRM.Core.Model;
using Shawn.Utils;
using PRM.ViewModel;

namespace PRM.View
{
    public partial class AboutPage : UserControl
    {
        public readonly VmAboutPage Vm;
        private readonly VmMain _vmMain;

        public AboutPage(VmAboutPage vm, VmMain vmMain)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = Vm;
            _vmMain = vmMain;
            TbVersion.Text = PRMVersion.Version;

#if FOR_MICROSOFT_STORE_ONLY
            TbAppName.Text = SystemConfig.AppName + "(Store)";
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
            _vmMain.TopPage = null;
        }
    }
}