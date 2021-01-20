using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core;
using PRM.ViewModel;

namespace PRM.View
{
    public partial class AboutPage : UserControl
    {
        public readonly VmMain Vm;
        public AboutPage(VmMain vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = Vm;
            TbVersion.Text = PRMVersion.Version;
        }

        private void SupportText_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 3)
            {
                Shawn.Utils.ConsoleManager.Toggle();
            }
        }

        private void ButtonBack_OnClick(object sender, RoutedEventArgs e)
        {
            Vm.TopPage = null;
        }
    }
}
