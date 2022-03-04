using System;
using System.Windows.Controls;
using System.Windows.Data;
using PRM.Model.ProtocolRunner;
using PRM.Service;

namespace PRM.View.Settings.ProtocolConfig
{
    public partial class ProtocolRunnerSettingsPage : UserControl
    {
        public ProtocolRunnerSettingsPage()
        {
            InitializeComponent();
            var vm = new ProtocolRunnerSettingsPageViewModel(App.Context.ProtocolConfigurationService, App.Context.LanguageService);
            this.DataContext = vm;
        }
    }


    public class ExternalRunner2Vm : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ExternalRunner er)
            {
                return new ExternalRunnerSettingsViewModel(er, LanguageService.TmpLanguageService);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ExternalRunnerSettingsViewModel vm)
            {
                return vm.ExternalRunner;
            }
            return null;
        }

        #endregion IValueConverter 成员
    }
}
