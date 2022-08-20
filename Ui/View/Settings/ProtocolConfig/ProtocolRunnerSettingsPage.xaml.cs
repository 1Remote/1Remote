using System.Windows;
using System.Windows.Controls;
using _1RM.Model;
using _1RM.Service;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf.Controls;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class ProtocolRunnerSettingsPage : UserControl
    {
        public ProtocolRunnerSettingsPage()
        {
            InitializeComponent();
            var vm = new ProtocolRunnerSettingsPageViewModel(IoC.Get<AppDataContext>().ProtocolConfigurationService ?? new ProtocolConfigurationService(), IoC.Get<ILanguageService>());
            this.DataContext = vm;
        }
    }
}
