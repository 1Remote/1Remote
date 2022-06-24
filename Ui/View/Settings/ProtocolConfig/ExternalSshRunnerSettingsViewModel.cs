using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using PRM.Model.Protocol;
using PRM.Model.ProtocolRunner;
using PRM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.View.Settings.ProtocolConfig;

public class ExternalSshRunnerSettingsViewModel : ExternalRunnerSettingsViewModel
{
    public ExternalSshRunnerSettingsViewModel(ExternalRunnerForSSH externalRunner, ILanguageService languageService) : base(externalRunner, languageService)
    {
        ExternalRunnerForSSH = externalRunner;
    }

    public ExternalRunnerForSSH ExternalRunnerForSSH { get; }

}