using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using _1RM.Model.Protocol.FileTransmit;
using _1RM.Service;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class ExternalRunnerSettings : ExternalRunnerSettingsBase
    {
        public ExternalRunnerSettings()
        {
            InitializeComponent();
            base.InitBindableAvalonEditor(TextEditor);
        }
    }
}
