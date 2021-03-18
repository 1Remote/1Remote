using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Animation;
using PRM.Core.Model;
using Shawn.Utils;
using Shawn.Utils.PageHost;

namespace PRM.View.Guidance
{
    /// <summary>
    /// GuidanceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GuidanceWindow : WindowChromeBase
    {
        public GuidanceWindow(SystemConfig config)
        {
            InitializeComponent();

            // stop auto saving configs.
            SystemConfig = config;
            SystemConfig.StopAutoSaveConfig = true;

            _step = 0;
            Grid1.Visibility = Visibility.Visible;
            Grid2.Visibility = Visibility.Collapsed;

            WinGrid.PreviewMouseDown += WinTitleBar_MouseDown;
            WinGrid.MouseUp += WinTitleBar_OnMouseUp;
            WinGrid.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            DataContext = this;

            // set default language
            CultureInfo ci = CultureInfo.CurrentCulture;
            Console.WriteLine("CultureInfo.CurrentCulture");
            Console.WriteLine(CultureInfo.CurrentCulture);
            Console.WriteLine("CultureInfo.CurrentUICulture");
            Console.WriteLine(CultureInfo.CurrentUICulture);
            Console.WriteLine("CultureInfo.DefaultThreadCurrentCulture");
            Console.WriteLine(CultureInfo.DefaultThreadCurrentCulture);
            Console.WriteLine("CultureInfo.DefaultThreadCurrentUICulture");
            Console.WriteLine(CultureInfo.DefaultThreadCurrentUICulture);

            Console.WriteLine("Default Language Info:");
            Console.WriteLine("* Name: {0}", ci.Name);
            Console.WriteLine("* Display Name: {0}", ci.DisplayName);
            Console.WriteLine("* English Name: {0}", ci.EnglishName);
            Console.WriteLine("* 2-letter ISO Name: {0}", ci.TwoLetterISOLanguageName);
            Console.WriteLine("* 3-letter ISO Name: {0}", ci.ThreeLetterISOLanguageName);
            Console.WriteLine("* 3-letter Win32 API Name: {0}", ci.ThreeLetterWindowsLanguageName);
            string code = ci.Name.ToLower();
            SystemConfig.Instance.Language.CurrentLanguageCode = code;

            // saving config when this window close
            Closed += (sender, args) =>
            {
                SystemConfig.StopAutoSaveConfig = false;
                SystemConfig.Save();
            };
        }

        public SystemConfig SystemConfig { get; set; }

        private int _step = 0;

        public int Step
        {
            get => _step;
            set => SetAndNotifyIfChanged(nameof(Step), ref _step, value);
        }

        private void ButtonNext_OnClick(object sender, RoutedEventArgs e)
        {
            if (Step != 0) return;
            Step = 1;
            Grid2.Visibility = Visibility.Visible;

            var sb = new Storyboard();
            sb.AddSlideToLeft(0.5, ActualWidth);
            sb.Begin(Grid1);
            var sb2 = new Storyboard();
            sb2.AddSlideFromRight(0.5, ActualWidth);
            sb2.Begin(Grid2);
        }

        private void ButtonPrevious_OnClick(object sender, RoutedEventArgs e)
        {
            if (Step != 1) return;
            Step = 0;
            Grid2.Visibility = Visibility.Visible;
            var sb = new Storyboard();
            sb.AddSlideFromLeft(0.5, ActualWidth);
            sb.Begin(Grid1);
            var sb2 = new Storyboard();
            sb2.AddSlideToRight(0.5, ActualWidth);
            sb2.Begin(Grid2);
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {
#if !DEV
            System.Diagnostics.Process.Start("https://github.com/VShawn/PRemoteM");
#endif
            this.Close();
        }
    }
}