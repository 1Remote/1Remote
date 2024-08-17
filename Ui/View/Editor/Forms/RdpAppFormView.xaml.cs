using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using _1RM.Model.Protocol;
using Shawn.Utils;

namespace _1RM.View.Editor.Forms
{
    public partial class RdpAppFormView : UserControl
    {
        public RdpAppFormView()
        {
            InitializeComponent();
            TextBoxRdpFileAdditionalSettings.TextArea.TextEntered += TextAreaOnTextEntered;
            TextBoxRdpFileAdditionalSettings.GotFocus += (sender, args) =>
            {
                if (TextBoxRdpFileAdditionalSettings.Text == "")
                {
                    ShowCompletionWindow(RdpFileSettingCompletionData.Settings);
                }
            };
            TextBoxRdpFileAdditionalSettings.TextChanged += (sender, args) =>
            {
                if (TextBoxRdpFileAdditionalSettings.Text == "")
                {
                    ShowCompletionWindow(RdpFileSettingCompletionData.Settings);
                }
            };
        }


        private CompletionWindow? _completionWindow;
        private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
        {
            int offset = TextBoxRdpFileAdditionalSettings.CaretOffset - 1;
            //char newChar = TextBoxRdpFileAdditionalSettings.Document.GetCharAt(offset); // current key down.
            var currentLine = TextBoxRdpFileAdditionalSettings.Document.GetLineByOffset(TextBoxRdpFileAdditionalSettings.CaretOffset);
            var currentLine0ToCaret = TextBoxRdpFileAdditionalSettings.Document.GetText(currentLine.Offset, offset - currentLine.Offset + 1); // currentLine[0: offset]
            var completions = new List<string>();
            foreach (var str in RdpFileSettingCompletionData.Settings)
            {
                if (str.StartsWith(currentLine0ToCaret) && str != currentLine0ToCaret)
                    completions.Add(str);
            }
            ShowCompletionWindow(completions);
        }

        private void ShowCompletionWindow(IEnumerable<string> completions)
        {
            _completionWindow?.Close();
            var enumerable = completions as string[] ?? completions.ToArray();
            if (enumerable?.Any() != true) return;
            // ref: http://avalonedit.net/documentation/html/47c58b63-f30c-4290-a2f2-881d21227446.htm
            _completionWindow = new CompletionWindow(TextBoxRdpFileAdditionalSettings.TextArea)
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
            foreach (var str in enumerable)
            {
                completionData.Add(new RdpFileSettingCompletionData(str));
            }
            _completionWindow.Show();
            if (enumerable.Count() == 1)
                _completionWindow.CompletionList.SelectItem(enumerable.First());
            _completionWindow.Closed += (o, args) => _completionWindow = null;
        }




        
        private void ButtonPreviewRdpFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RdpAppFormViewModel viewModel) return;
            var rdp = viewModel.New;
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{rdp.DisplayName}_{rdp.Port}_{MD5Helper.GetMd5Hash16BitString(rdp.UserName)}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
            File.WriteAllText(rdpFile, rdp.ToRdpConfig().ToString());
            var p = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            p.StandardInput.WriteLine($"notepad " + rdpFile);
            p.StandardInput.WriteLine("exit");
        }
    }
}
