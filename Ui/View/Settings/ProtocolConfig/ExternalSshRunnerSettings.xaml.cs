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
            TextEditor.TextArea.TextEntering += (sender, args) =>
            {
                if (args.Text.IndexOf("\n", StringComparison.Ordinal) >= 0
                    || args.Text.IndexOf("\r", StringComparison.Ordinal) >= 0)
                    args.Handled = true;
            };
            TextEditor.TextArea.TextEntered += TextAreaOnTextEntered;
            TextEditorForSshWithPrivateKey.TextArea.TextEntered += TextAreaOnTextEntered;
        }
    }
}
