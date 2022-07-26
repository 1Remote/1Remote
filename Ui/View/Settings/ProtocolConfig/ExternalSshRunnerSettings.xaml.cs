using System;
using System.Windows;
using PRM.Model.Protocol.FileTransmit;
using PRM.Service;

namespace PRM.View.Settings.ProtocolConfig
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
