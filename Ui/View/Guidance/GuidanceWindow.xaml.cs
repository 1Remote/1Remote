using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using _1RM.Model;
using _1RM.Service;
using _1RM.View.Settings;
using Shawn.Utils.Wpf.PageHost;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.Guidance
{
    /// <summary>
    /// GuidanceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GuidanceWindow : WindowBase
    {
        public GuidanceWindowViewModel SettingsPageViewModel { get; }
        public GuidanceWindow(GuidanceWindowViewModel vm)
        {
            SettingsPageViewModel = vm;
            DataContext = this;
            InitializeComponent();
            _step = 0;
            Grid1.Visibility = Visibility.Visible;
            Grid2.Visibility = Visibility.Collapsed;

            WinGrid.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
            WinGrid.MouseUp += WinTitleBar_OnMouseUp;
            WinGrid.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            Closing += (sender, args) =>
            {
                if (Step >= 0)
                    args.Cancel = true;
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
            if (e.ClickCount == 2)
            {
                // disabled go Maximized
                return;
            }
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }
    }
}