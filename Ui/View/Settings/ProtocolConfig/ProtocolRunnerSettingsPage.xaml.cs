using System.Windows;
using System.Windows.Controls;
using _1RM.Service;
using _1RM.Service.DataSource;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf.Controls;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class ProtocolRunnerSettingsPage : UserControl
    {
        public ProtocolRunnerSettingsPage()
        {
            InitializeComponent();
            var vm = new ProtocolRunnerSettingsPageViewModel(IoC.Get<ProtocolConfigurationService>(), IoC.Get<ILanguageService>());
            this.DataContext = vm;
        }
    }
}
