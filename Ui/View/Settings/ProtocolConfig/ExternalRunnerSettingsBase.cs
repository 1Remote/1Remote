using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using Newtonsoft.Json.Bson;
using _1RM.Controls;
using _1RM.Service;
using System.Windows;

namespace _1RM.View.Settings.ProtocolConfig;

public abstract class ExternalRunnerSettingsBase : UserControl
{
    private CompletionWindow? _completionWindow;
    public void InitBindableAvalonEditor(BindableAvalonEditor avalonEditor)
    {
        {
            var c = IoC.Get<ConfigurationService>().Theme.GetBackgroundTextColor;
            var highlighting = avalonEditor.SyntaxHighlighting;
            foreach (var color in highlighting.NamedHighlightingColors)
            {
                color.Foreground = new SimpleHighlightingBrush(c);
                color.FontWeight = null;
            }
            avalonEditor.SyntaxHighlighting = null;
            avalonEditor.SyntaxHighlighting = highlighting;
        }
        avalonEditor.GotFocus += (sender, args) =>
        {
            _completionWindow?.Close();
            _completionWindow = null;
        };

        avalonEditor.TextArea.TextEntering += (sender, args) =>
        {
            if (args.Text.IndexOf("\n", StringComparison.Ordinal) >= 0
                || args.Text.IndexOf("\r", StringComparison.Ordinal) >= 0)
                args.Handled = true;
        };

        avalonEditor.TextArea.TextEntered += (sender, e) =>
        {
            if (sender is ICSharpCode.AvalonEdit.Editing.TextArea textArea && this.DataContext is ExternalRunnerSettingsViewModel vm)
            {
                string s = string.Empty;
                if (e.Text == "%")
                {
                    s = "%";
                }
                //else if (_completionWindow != null && avalonEditor.CaretOffset > 0)
                //{
                //    s = avalonEditor.Text.Substring(0, avalonEditor.CaretOffset);
                //    var i = s.LastIndexOf("%", StringComparison.Ordinal);
                //    if (i >= 0)
                //    {
                //        s = s.Substring(i);
                //    }
                //}

                if (string.IsNullOrEmpty(s) == false)
                {
                    var list = _completionWindow != null ? 
                        (from completionData in _completionWindow.CompletionList.CompletionData where completionData.Text.StartsWith(s, StringComparison.OrdinalIgnoreCase) select completionData.Text).ToList() 
                        : vm.ExternalRunner.MarcoNames;
                    if (list.Count > 0)
                    {
                        _completionWindow?.Close();
                        _completionWindow = new CompletionWindow(textArea)
                        {
                            CloseWhenCaretAtBeginning = true,
                            CloseAutomatically = true,
                            BorderThickness = new System.Windows.Thickness(0),
                            Background = App.ResourceDictionary["BackgroundBrush"] as Brush,
                            Foreground = App.ResourceDictionary["BackgroundTextBrush"] as Brush,
                            ResizeMode = ResizeMode.NoResize,
                            WindowStyle = WindowStyle.None,
                        };
                        var completionData = _completionWindow.CompletionList.CompletionData;
                        foreach (var marcoName in list)
                        {
                            completionData.Add(new MarcoCompletionData(marcoName));
                        }
                        _completionWindow.Show();
                        _completionWindow.Closed += (o, args) => _completionWindow = null;
                        return;
                    }
                }
            }

            _completionWindow?.Close();
        };
    }
}