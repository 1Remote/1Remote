using System;
using System.Windows;
using _1RM.Model.Protocol.FileTransmit;
using _1RM.Service;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class ExternalSshRunnerSettings : ExternalRunnerSettingsBase
    {
        public ExternalSshRunnerSettings()
        {
            InitializeComponent();
            base.InitBindableAvalonEditor(TextEditor);
            base.InitBindableAvalonEditor(TextEditorForSshWithPrivateKey);
        }
    }
}
