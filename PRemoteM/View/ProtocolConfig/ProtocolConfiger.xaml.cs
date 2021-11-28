using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PRM.Core.Model;
using PRM.Core.Protocol.Runner;
using PRM.Core.Service;
using PRM.ViewModel.Configuration;

namespace PRM.View.ProtocolConfig
{
    /// <summary>
    /// ProtocolConfiger.xaml 的交互逻辑
    /// </summary>
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
