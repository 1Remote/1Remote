using System;
using System.Windows.Data;
using PRM.Model.ProtocolRunner;
using Shawn.Utils.Interface;

namespace PRM.View.Settings.ProtocolConfig;

public class ConverterExternalRunner2Vm : IValueConverter
{
    #region IValueConverter 成员

    public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is ExternalRunnerForSSH runnerForSsh)
        {
            return new ExternalSshRunnerSettingsViewModel(runnerForSsh, IoC.Get<ILanguageService>());
        }
        if (value is ExternalRunner externalRunner)
        {
            return new ExternalRunnerSettingsViewModel(externalRunner, IoC.Get<ILanguageService>());
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