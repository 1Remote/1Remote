using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace PRM.View.Settings.ProtocolConfig;

public abstract class ExternalRunnerSettingsBase : UserControl
{
    private CompletionWindow? _completionWindow;
    public void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == "%"
            && sender is ICSharpCode.AvalonEdit.TextEditor editor
            && this.DataContext is ExternalRunnerSettingsViewModel vm)
        {
            _completionWindow = new CompletionWindow(editor.TextArea)
            {
                CloseWhenCaretAtBeginning = true,
                CloseAutomatically = true
            };
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