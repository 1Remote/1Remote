using System.ComponentModel;
using _1RM.Model.ProtocolRunner;
using Shawn.Utils.Interface;
using _1RM.Service;

namespace _1RM.View.Settings.ProtocolConfig;

public class ExternalSshRunnerSettingsViewModel : ExternalRunnerSettingsViewModel
{
    private readonly PropertyChangedEventHandler? _propertyChangedHandler;

    public ExternalSshRunnerSettingsViewModel(ExternalRunnerForSSH externalRunner, ILanguageService languageService) : base(externalRunner, languageService)
    {
        ExternalRunnerForSSH = externalRunner;
        _propertyChangedHandler = (sender, args) =>
        {
            IoC.Get<ProtocolConfigurationService>().Save();
        };
        ExternalRunnerForSSH.PropertyChanged += _propertyChangedHandler;
    }

    public ExternalRunnerForSSH ExternalRunnerForSSH { get; }

    public new void Dispose()
    {
        // Unsubscribe from SSH-specific event to prevent memory leak
        if (_propertyChangedHandler != null)
        {
            ExternalRunnerForSSH.PropertyChanged -= _propertyChangedHandler;
        }
        // Call base dispose to unsubscribe from base class event
        base.Dispose();
    }
}
