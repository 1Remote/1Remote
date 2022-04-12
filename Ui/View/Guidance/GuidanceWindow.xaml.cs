using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PRM.Model;
using PRM.View.Settings;
using Shawn.Utils.Wpf.PageHost;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace PRM.View.Guidance
{
    /// <summary>
    /// GuidanceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GuidanceWindow : WindowBase
    {
        public SettingsPageViewModel SettingsPageViewModel { get; }
        public GuidanceWindow(PrmContext context, SettingsPageViewModel viewModel)
        {
            SettingsPageViewModel = viewModel;
            InitializeComponent();
            context.ConfigurationService.CanSave = false;

            // stop auto saving configs.

            _step = 0;
            Grid1.Visibility = Visibility.Visible;
            Grid2.Visibility = Visibility.Collapsed;

            WinGrid.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
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

            SettingsPageViewModel.SetLanguage(CultureInfo.CurrentCulture.Name.ToLower());

            Closing += (sender, args) =>
            {
                if (Step >= 0)
                    args.Cancel = true;
            };
            Closed += (sender, args) =>
            {
                // save config when close
                context.ConfigurationService.CanSave = true;
                context.ConfigurationService.Save();
            };
        }



        private int _step = 0;
        public int Step
        {
            get => _step;
            set => SetAndNotifyIfChanged(ref _step, value);
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
            Step = -1;
            this.Close();
        }


        public override void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _isLeftMouseDown = false;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
            }
            else
            {
                _isLeftMouseDown = true;
                var th = new Thread(() =>
                {
                    Thread.Sleep(50);
                    if (_isLeftMouseDown)
                    {
                        _isDragging = true;
                    }
                    _isLeftMouseDown = false;
                });
                th.Start();
            }
        }
    }
}