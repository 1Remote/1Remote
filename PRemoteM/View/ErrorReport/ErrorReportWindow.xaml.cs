using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using PRM.Core;
using PRM.Core.Model;
using Shawn.Utils;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

using Shawn.Utils;

using Shawn.Utils.PageHost;

namespace PRM.View.ErrorReport
{
    /// <summary>
    /// Interaction logic for ErrorReportWindow.xaml
    /// </summary>
    public partial class ErrorReportWindow : WindowChromeBase
    {
        public ErrorReportWindow(Exception e)
        {
            InitializeComponent();
            Init();

            TbErrorInfo.Text = e.Message;
            TbErrorInfo.Text += e.Message;

            var sb = new StringBuilder();

            sb.AppendLine("<details>");
            sb.AppendLine();

            BuildEnvironment(ref sb);

            sb.AppendLine("## Error Info");
            sb.AppendLine();
            sb.AppendLine(e.Message);
            sb.AppendLine();

            sb.AppendLine("## Stack Trace");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(e.StackTrace);
            sb.AppendLine("```");
            sb.AppendLine();

            BuildRecentLog(ref sb);

            sb.AppendLine("</details>");
            sb.AppendLine();

            TbErrorInfo.Text = sb.ToString();
        }

        private void Init()
        {
            WinGrid.PreviewMouseDown += WinTitleBar_MouseDown;
            WinGrid.MouseUp += WinTitleBar_OnMouseUp;
            WinGrid.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            IconCopyDone.Opacity = IconSaveDone.Opacity = 0;

            BtnClose.Click += (sender, args) =>
            {
                Close();
            };
        }

        private void BuildEnvironment(ref StringBuilder sb)
        {
            try
            {
                var osRelease = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString();
                var osName = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "productName", "").ToString();
                var osType = Environment.Is64BitOperatingSystem ? "64-bits" : "32-bits";
                var osVersion = Environment.OSVersion.Version.ToString();
                var platform = $"{osName} {osType} {osVersion} ({osRelease})";
                var attributes = Assembly.GetExecutingAssembly().CustomAttributes;
                var framework = attributes.FirstOrDefault(a => a.AttributeType == typeof(TargetFrameworkAttribute));
                var from = "";

                // TODO: identify Chocolatey release
#if FOR_MICROSOFT_STORE_ONLY
                from = "Microsoft store";
#else
                from = "EXE Release";
#endif

                sb.AppendLine("## Environment");
                sb.AppendLine("");
                sb.AppendLine("|     Component   |                       Version                      |");
                sb.AppendLine("|:------------------|:--------------------------------------|");
                sb.AppendLine($"|{SystemConfig.AppName} | `{PRMVersion.Version}`({from})|");
                sb.AppendLine($"|.NET Framework | `{framework?.NamedArguments?[0].TypedValue.Value?.ToString()}`    |");
                sb.AppendLine($"|CLR            | `{Environment.Version}`       |");
                sb.AppendLine($"|OS             | `{platform}`                  |");
                sb.AppendLine();
            }
            catch
            {
                // ignored
            }
        }

        private void BuildRecentLog(ref StringBuilder sb)
        {
            sb.AppendLine("## Recent Log ");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(SimpleLogHelper.GetLog());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        private void ButtonCopyToClipboard_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TbErrorInfo.Text);
                var sb = new Storyboard();
                sb.AddFadeOut(1);
                sb.Begin(IconCopyDone);
            }
            catch
            {
                // ignored
            }
        }

        private void ButtonSaveAs_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = $"log |*.log.md",
                    FileName = SystemConfig.AppName + "_ErrorReport_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".md",
                    CheckFileExists = false,
                };
                if (dlg.ShowDialog() != true) return;
                File.WriteAllText(dlg.FileName, TbErrorInfo.Text.Replace("\n", "\n\n"), Encoding.UTF8);
                var sb = new Storyboard();
                sb.AddFadeOut(1);
                sb.Begin(IconSaveDone);
            }
            catch
            {
                // ignored
            }
        }

        private void ButtonSendByGithub_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/VShawn/PRemoteM/issues"));
            }
            catch
            {
                // ignored
            }
        }

        private void ButtonSendByEmail_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string mailto = string.Format("mailto:{0}?Subject={1}&Body={2}", "mailto:veckshawn@gmail.com", $"{SystemConfig.AppName} error report.", "");
                mailto = Uri.EscapeUriString(mailto);
                System.Diagnostics.Process.Start(mailto);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:veckshawn@gmail.com"));
            }
            catch
            {
                // ignored
            }
        }
    }
}