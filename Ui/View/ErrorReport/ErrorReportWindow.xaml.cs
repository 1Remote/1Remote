using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using Shawn.Utils;
using System.Windows;
using System.Windows.Media.Animation;
using _1RM.Utils;
using Microsoft.AppCenter.Crashes;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.Wpf.FileSystem;
using Shawn.Utils.Wpf.PageHost;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.ErrorReport
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

            var sb = new StringBuilder();

            sb.AppendLine("<details>");
            sb.AppendLine();

            BuildEnvironment(ref sb);

            if (e.InnerException != null)
            {
                sb.AppendLine("## InnerException Info");
                sb.AppendLine();
                sb.AppendLine(e.InnerException.Message);
                sb.AppendLine();

                sb.AppendLine("## InnerException Stack Trace");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(e.InnerException.StackTrace);
                sb.AppendLine("```");
                sb.AppendLine();
            }

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


            var attachments = new ErrorAttachmentLog[]
            {
                ErrorAttachmentLog.AttachmentWithText(TbErrorInfo.Text, "log.md"),
            };
            MsAppCenterHelper.Error(e, attachments: attachments);
        }

        private void Init()
        {
            WinGrid.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
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
                var osRelease = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "")!.ToString();
                var osName = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "productName", "")!.ToString();
                var osType = Environment.Is64BitOperatingSystem ? "64-bits" : "32-bits";
                var osVersion = Environment.OSVersion.Version.ToString();
                var platform = $"{osName} {osType} {osVersion} ({osRelease})";
                var attributes = Assembly.GetExecutingAssembly().CustomAttributes;
                var framework = attributes.FirstOrDefault(a => a.AttributeType == typeof(TargetFrameworkAttribute));
                var from = "";

#if FOR_MICROSOFT_STORE_ONLY
                from = "Microsoft store";
#else
                from = "EXE Release";
#endif

                sb.AppendLine("## Environment");
                sb.AppendLine("");
                sb.AppendLine("|     Component   |                       Version                      |");
                sb.AppendLine("|:------------------|:--------------------------------------|");
                sb.AppendLine($"|{Assert.APP_DISPLAY_NAME} | `{AppVersion.Version}`({from})|");
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

        private static void BuildRecentLog(ref StringBuilder sb)
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
                Clipboard.SetDataObject(TbErrorInfo.Text);
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
                var path = SelectFileHelper.SaveFile(
                    filter: "log |*.log.md",
                    selectedFileName: Assert.APP_NAME + "_ErrorReport_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".md");
                if (path == null) return;
                File.WriteAllText(path, TbErrorInfo.Text.Replace("\n", "\n\n"), Encoding.UTF8);
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
                HyperlinkHelper.OpenUriBySystem("https://github.com/1Remote/1Remote/issues");
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
                string mailto = string.Format("mailto:{0}?Subject={1}&Body={2}", "veckshawn@gmail.com", $"{Assert.APP_DISPLAY_NAME} error report.", "");
#pragma warning disable CS0618
#pragma warning disable SYSLIB0013 // 类型或成员已过时
                mailto = Uri.EscapeUriString(mailto);
#pragma warning restore SYSLIB0013 // 类型或成员已过时
#pragma warning restore CS0618
                HyperlinkHelper.OpenUriBySystem(mailto);
            }
            catch
            {
                // ignored
            }
        }
    }
}