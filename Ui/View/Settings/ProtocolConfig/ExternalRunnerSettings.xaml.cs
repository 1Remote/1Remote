using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using PRM.Model.Protocol.FileTransmit;
using PRM.Service;

namespace PRM.View.Settings.ProtocolConfig
{
    public partial class ExternalRunnerSettings : UserControl
    {
        public ExternalRunnerSettings()
        {
            InitializeComponent();
            TextEditor.TextArea.TextEntering += (sender, args) =>
            {
                if (args.Text.IndexOf("\n", StringComparison.Ordinal) >= 0
                   || args.Text.IndexOf("\r", StringComparison.Ordinal) >= 0)
                    args.Handled = true;
            };
            TextEditor.TextArea.TextEntered += TextAreaOnTextEntered;
        }

        private CompletionWindow? _completionWindow;
        private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == "%"
            && this.DataContext is ExternalRunnerSettingsViewModel vm)
            {
                _completionWindow = new CompletionWindow(TextEditor.TextArea);
                _completionWindow.CloseWhenCaretAtBeginning = true;
                _completionWindow.CloseAutomatically = true;
                var completionData = _completionWindow.CompletionList.CompletionData;
                foreach (var marcoName in vm.ExternalRunner.MarcoNames)
                {
                    completionData.Add(new MarcoCompletionData(marcoName));
                }
                _completionWindow.Show();
                _completionWindow.Closed += (o, args) => _completionWindow = null;
                return;
            }
            _completionWindow?.Close();
        }
    }
}
