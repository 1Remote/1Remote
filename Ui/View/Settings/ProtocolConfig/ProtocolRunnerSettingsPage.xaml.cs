using System.Windows;
using System.Windows.Controls;
using PRM.Model;
using PRM.Service;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf.Controls;

namespace PRM.View.Settings.ProtocolConfig
{
    public partial class ProtocolRunnerSettingsPage : UserControl
    {
        public ProtocolRunnerSettingsPage()
        {
            InitializeComponent();
            var vm = new ProtocolRunnerSettingsPageViewModel(IoC.Get<PrmContext>().ProtocolConfigurationService ?? new ProtocolConfigurationService(), IoC.Get<ILanguageService>());
            this.DataContext = vm;
        }
    }
}
