using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace PRM.View.Settings.ProtocolConfig;

/// <summary>
/// for auto completion
/// </summary>
public class MarcoCompletionData : ICompletionData
{
    public MarcoCompletionData(string text)
    {
        Text = text;
    }

    public ImageSource? Image => null;

    public string Text { get; }

    public object Content => Text;

    public object Description => this.Text;

    /// <inheritdoc />
    public double Priority { get; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text.StartsWith("%") ? Text.Substring(1) : Text);
    }
}