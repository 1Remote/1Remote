using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using _1RM.Model.ProtocolRunner;
using _1RM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Settings.ProtocolConfig;

public class ExternalSshRunnerSettingsViewModel : ExternalRunnerSettingsViewModel
{
    public ExternalSshRunnerSettingsViewModel(ExternalRunnerForSSH externalRunner, ILanguageService languageService) : base(externalRunner, languageService)
    {
        ExternalRunnerForSSH = externalRunner;
    }

    public ExternalRunnerForSSH ExternalRunnerForSSH { get; }

}