using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PRM.Model;
using PRM.Model.ProtocolRunner;
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


    public class ExternalRunner2Vm : IValueConverter
    {
        #region IValueConverter 成员

        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ExternalRunner er)
            {
                return new ExternalRunnerSettingsViewModel(er, IoC.Get<ILanguageService>());
            }
            return null;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
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
