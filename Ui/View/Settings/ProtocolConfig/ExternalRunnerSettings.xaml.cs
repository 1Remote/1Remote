using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Newtonsoft.Json;
using PRM.Model.Protocol;
using PRM.Model.Protocol.FileTransmit;
using PRM.Model.ProtocolRunner;
using PRM.Service;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace PRM.View.Settings.ProtocolConfig
{
    public class ExternalRunnerSettingsViewModel
    {
        private readonly ILanguageService _languageService;
        public ExternalRunner ExternalRunner { get; }

        public ExternalRunnerSettingsViewModel(ILanguageService languageService)
        {
            ExternalRunner = new ExternalRunner("");
            _languageService = languageService;
        }
        public ExternalRunnerSettingsViewModel(ExternalRunner externalRunner, ILanguageService languageService)
        {
            ExternalRunner = externalRunner;
            _languageService = languageService;
        }




        private RelayCommand? _cmdSelectDbPath;
        [JsonIgnore]
        public RelayCommand CmdSelectExePath
        {
            get
            {
                return _cmdSelectDbPath ??= new RelayCommand((o) =>
                {
                    string? initPath = null;
                    try
                    {
                        initPath = new FileInfo(ExternalRunner.ExePath).DirectoryName;
                    }
                    catch
                    {
                        // ignored
                    }


                    var path = SelectFileHelper.OpenFile(filter: "exe|*.exe", checkFileExists: true, initialDirectory: initPath);
                    if (path == null) return;
                    ExternalRunner.ExePath = path;
                    if (string.IsNullOrEmpty(ExternalRunner.Arguments))
                    {
                        var name = new FileInfo(path).Name.ToLower();
                        if (name == "winscp.exe".ToLower())
                        {
                            if (ExternalRunner.ProtocolType == typeof(SFTP))
                            {
                                ExternalRunner.Arguments = "sftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%:%PRM_PORT%";
                            }
                            if (ExternalRunner.ProtocolType == typeof(FTP))
                            {
                                ExternalRunner.Arguments = "ftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%:%PRM_PORT%";
                            }
                            ExternalRunner.RunWithHosting = true;
                        }
                        else if (name == "filezilla.exe".ToLower() || path.ToLower().IndexOf("uvnc", StringComparison.Ordinal) > 0)
                        {
                            if (ExternalRunner.ProtocolType == typeof(SFTP))
                            {
                                ExternalRunner.Arguments = "sftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%";
                            }
                            if (ExternalRunner.ProtocolType == typeof(FTP))
                            {
                                ExternalRunner.Arguments = "ftp://%PRM_USERNAME%:%PRM_PASSWORD%@%PRM_HOSTNAME%";
                            }
                            ExternalRunner.Arguments = @"%PRM_HOSTNAME%::%PRM_PORT% -password=%PRM_PASSWORD% -scale=auto";
                            ExternalRunner.RunWithHosting = false;
                        }
                        else if (name == "VpxClient.exe".ToLower())
                        {
                            ExternalRunner.Arguments = @"-s %PRM_HOSTNAME% -u %PRM_USERNAME% -p %PRM_PASSWORD%";
                            ExternalRunner.RunWithHosting = true;
                        }
                        else if (name.IndexOf("kitty", StringComparison.Ordinal) >= 0 || name.IndexOf("putty", StringComparison.Ordinal) >= 0)
                        {
                            ExternalRunner.Arguments = @"-ssh %PRM_HOSTNAME% -P %PRM_PORT% -l %PRM_USERNAME% -pw %PRM_PASSWORD% -%PRM_SSH_VERSION% -cmd ""%PRM_STARTUP_AUTO_COMMAND%""";
                            ExternalRunner.RunWithHosting = true;
                        }
                        else if (name == "tvnviewer.exe".ToLower())
                        {
                            ExternalRunner.Arguments = @"%PRM_HOSTNAME%::%PRM_PORT% -password=%PRM_PASSWORD% -scale=auto";
                            ExternalRunner.RunWithHosting = true;
                        }
                        else if (name == "vncviewer.exe".ToLower() || path.ToLower().IndexOf("uvnc", StringComparison.Ordinal) > 0)
                        {
                            ExternalRunner.Arguments = @"%PRM_HOSTNAME%::%PRM_PORT% -password=%PRM_PASSWORD% -scale=auto";
                            ExternalRunner.RunWithHosting = false;
                        }
                    }
                });
            }
        }


        private RelayCommand? _cmdAddEnvironmentVariable;
        public RelayCommand CmdAddEnvironmentVariable
        {
            get
            {
                return _cmdAddEnvironmentVariable ??= new RelayCommand((o) =>
                {
                    ExternalRunner.EnvironmentVariables.Add(new ExternalRunner.ObservableKvp<string, string>("", ""));
                });
            }
        }

        private RelayCommand? _cmdDelEnvironmentVariable;
        public RelayCommand CmdDelEnvironmentVariable
        {
            get
            {
                return _cmdDelEnvironmentVariable ??= new RelayCommand((o) =>
                {
                    if (o is ExternalRunner.ObservableKvp<string, string> item
                        && ExternalRunner.EnvironmentVariables.Contains(item)
                        && ((item.Key == "" && item.Value == "")
                            || MessageBox.Show(_languageService.Translate("confirm_to_delete"), _languageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None) == MessageBoxResult.Yes)
                        )
                    {
                        ExternalRunner.EnvironmentVariables.Remove(item);
                    }
                });
            }
        }
    }
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
}
