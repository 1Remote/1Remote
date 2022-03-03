using System;
using System.Windows.Controls;
using System.Windows.Data;
using PRM.Model.Protocol.Runner;
using PRM.Service;

namespace PRM.View.Settings.ProtocolConfig
{
    public partial class ProtocolConfiger : UserControl
    {
        public ProtocolConfiger()
        {
            InitializeComponent();
            var vm = new ProtocolConfigerViewModel(App.Context.ProtocolConfigurationService, App.Context.LanguageService);
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
                return new ExternalRunnerConfigerVM(er, LanguageService.TmpLanguageService);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ExternalRunnerConfigerVM vm)
            {
                return vm.ExternalRunner;
            }
            return null;
        }

        #endregion IValueConverter 成员
    }
}
