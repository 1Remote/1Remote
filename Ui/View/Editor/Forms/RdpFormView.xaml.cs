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
    public partial class RdpFormView : UserControl
    {
        private CompletionWindow? _completionWindow;
        private void ShowCompletionWindow(IEnumerable<string> completions, TextArea textArea)
        {
            _completionWindow?.Close();
            var enumerable = completions as string[] ?? completions.ToArray();
            if (enumerable?.Any() != true) return;
            // ref: http://avalonedit.net/documentation/html/47c58b63-f30c-4290-a2f2-881d21227446.htm
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
            foreach (var str in enumerable)
            {
                completionData.Add(new RdpFileSettingCompletionData(str));
            }
            _completionWindow.Show();
            if (enumerable.Count() == 1)
                _completionWindow.CompletionList.SelectItem(enumerable.First());
            _completionWindow.Closed += (o, args) => _completionWindow = null;
        }


        public RdpFormView()
        {
            InitializeComponent();
            TextBoxRdpFileAdditionalSettings.TextArea.TextEntered += OnTextEntered_TextBoxRdpFileAdditionalSettings;
            TextBoxRdpFileAdditionalSettings.GotFocus += (sender, args) =>
            {
                if (TextBoxRdpFileAdditionalSettings.Text == "")
                {
                    ShowCompletionWindow(RdpFileSettingCompletionData.Settings, TextBoxRdpFileAdditionalSettings.TextArea);
                }
            };
            TextBoxRdpFileAdditionalSettings.TextChanged += (sender, args) =>
            {
                if (TextBoxRdpFileAdditionalSettings.Text == "")
                {
                    ShowCompletionWindow(RdpFileSettingCompletionData.Settings, TextBoxRdpFileAdditionalSettings.TextArea);
                }
            };




            TextBoxRdpControlAdditionalSettings.TextArea.TextEntered += OnTextEntered_TextBoxRdpControlAdditionalSettings;
            TextBoxRdpControlAdditionalSettings.GotFocus += (sender, args) =>
            {
                if (TextBoxRdpControlAdditionalSettings.Text == "")
                {
                    ShowCompletionWindow(RDP.GetRdpControlAdditionalSettingKeys(), TextBoxRdpControlAdditionalSettings.TextArea);
                }
            };
            TextBoxRdpControlAdditionalSettings.TextChanged += (sender, args) =>
            {
                if (TextBoxRdpControlAdditionalSettings.Text == "")
                {
                    ShowCompletionWindow(RDP.GetRdpControlAdditionalSettingKeys(), TextBoxRdpControlAdditionalSettings.TextArea);
                }
            };
        }


        private void OnTextEntered_TextBoxRdpFileAdditionalSettings(object sender, TextCompositionEventArgs e)
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
            ShowCompletionWindow(completions, TextBoxRdpFileAdditionalSettings.TextArea);
        }



        private void OnTextEntered_TextBoxRdpControlAdditionalSettings(object sender, TextCompositionEventArgs e)
        {
            int offset = TextBoxRdpControlAdditionalSettings.CaretOffset - 1;
            var currentLine = TextBoxRdpControlAdditionalSettings.Document.GetLineByOffset(TextBoxRdpControlAdditionalSettings.CaretOffset);
            var currentLine0ToCaret = TextBoxRdpControlAdditionalSettings.Document.GetText(currentLine.Offset, offset - currentLine.Offset + 1); // currentLine[0: offset]
            var completions = new List<string>();
            foreach (var str in RDP.GetRdpControlAdditionalSettingKeys())
            {
                if (str.StartsWith(currentLine0ToCaret) && str != currentLine0ToCaret)
                    completions.Add(str);
            }
            ShowCompletionWindow(completions, TextBoxRdpControlAdditionalSettings.TextArea);
        }






        private void ButtonShowMonitorsNum_OnClick(object sender, RoutedEventArgs e)
        {
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
            p.StandardInput.WriteLine($"mstsc /l");
            p.StandardInput.WriteLine("exit");
        }
        private void ButtonPreviewRdpFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RdpFormViewModel viewModel) return;
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


    public class RdpFileSettingCompletionData : ICompletionData
    {
        public RdpFileSettingCompletionData(string text)
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
            int offset = textArea.Caret.Offset;
            var currentLine = textArea.Document.GetLineByOffset(offset);
            textArea.Document.Replace(completionSegment, Text.Substring(offset - currentLine.Offset));
        }

        public static readonly string[] Settings =
        {
            "full address:s:",
            "alternate full address:s:",
            "username:s:",
            "domain:s:",
            "gatewayhostname:s:",
            "gatewaycredentialssource:i:",
            "gatewayprofileusagemethod:i:",
            "gatewayusagemethod:i:",
            "promptcredentialonce:i:",
            "authentication level:i:",
            "enablecredsspsupport:i:",
            "disableconnectionsharing:i:",
            "alternate shell:s:",
            "autoreconnection enabled:i:",
            "bandwidthautodetect:i:",
            "networkautodetect:i:",
            "compression:i:",
            "videoplaybackmode:i:",
            "audiocapturemode:i:",
            "encode redirected video capture:i:",
            "redirected video capture encoding quality:i:",
            "audiomode:i:",
            "camerastoredirect:s:",
            "devicestoredirect:s:",
            "drivestoredirect:s:",
            "keyboardhook:i:",
            "redirectclipboard:i:",
            "redirectcomports:i:",
            "redirectprinters:i:",
            "redirectsmartcards:i:",
            "usbdevicestoredirect:s:",
            "use multimon:i:",
            "selectedmonitors:s:",
            "maximizetocurrentdisplays:i:",
            "singlemoninwindowedmode:i:",
            "screen mode id:i:",
            "smart sizing:i:",
            "dynamic resolution:i:",
            "desktop size id:i:",
            "desktopheight:i:",
            "desktopwidth:i:",
            "desktopscalefactor:i:",
            "remoteapplicationcmdline:s:",
            "remoteapplicationexpandcmdline:i:",
            "remoteapplicationexpandworkingdir:i:",
            "remoteapplicationfile:s:",
            "remoteapplicationicon:s:",
            "remoteapplicationmode:i:",
            "remoteapplicationname:s:",
            "remoteapplicationprogram:s:"
        };
    }


    public class ConverterERdpFullScreenFlag : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(ERdpFullScreenFlag)).Cast<int>().Max() + 1;
            return ((int)((ERdpFullScreenFlag)value)).ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (ERdpFullScreenFlag)(int.Parse(value.ToString() ?? "0"));
        }
        #endregion
    }


    public class ConverterERdpWindowResizeMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(ERdpWindowResizeMode)).Cast<int>().Max() + 1;
            return ((int)((ERdpWindowResizeMode)value)).ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (ERdpWindowResizeMode)(int.Parse(value.ToString() ?? "0"));
        }
        #endregion
    }



    public class ConverterEDisplayPerformance : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EDisplayPerformance)).Cast<int>().Max() + 1;
            return ((int)((EDisplayPerformance)value)).ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (EDisplayPerformance)(int.Parse(value.ToString() ?? "0"));
        }
        #endregion
    }



    public class ConverterEGatewayMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EGatewayMode)).Cast<int>().Max() + 1;
            return ((int)((EGatewayMode)value)).ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (EGatewayMode)(int.Parse(value.ToString() ?? "0"));
        }
        #endregion
    }



    public class ConverterEGatewayLogonMethod : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EGatewayLogonMethod)).Cast<int>().Max() + 1;
            return ((int)((EGatewayLogonMethod)value)).ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (EGatewayLogonMethod)(int.Parse(value.ToString() ?? "0"));
        }
        #endregion
    }

    public class ConverterEAudioRedirectionMode : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EAudioRedirectionMode)).Cast<int>().Max() + 1;
            return ((int)((EAudioRedirectionMode)value)).ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (EAudioRedirectionMode)(int.Parse(value.ToString() ?? "0"));
        }
    }

    public class ConverterEAudioQualityMode: IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return Enum.GetValues(typeof(EAudioRedirectionMode)).Cast<int>().Max() + 1;
            return ((int)((EAudioQualityMode)value)).ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;
            return (EAudioQualityMode)(int.Parse(value.ToString() ?? "0"));
        }
    }
}
