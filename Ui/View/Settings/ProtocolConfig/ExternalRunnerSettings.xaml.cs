using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using PRM.Model.Protocol.FileTransmit;
using PRM.Service;

namespace PRM.View.Settings.ProtocolConfig
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
