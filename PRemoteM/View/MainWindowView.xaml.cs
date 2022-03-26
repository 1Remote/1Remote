using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using PRM.Model;
using PRM.Service;
using PRM.Utils;
using PRM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace PRM.View
{
    public partial class MainWindowView : WindowChromeBase
    {
        public MainWindowViewModel Vm { get; set; }

        public MainWindowView(PrmContext context, SettingsPageViewModel settingsPageViewModel)
        {
            App.UiDispatcher = Dispatcher;
            InitializeComponent();
            Vm = new MainWindowViewModel(context, settingsPageViewModel, this);
            this.DataContext = Vm;
            Vm.ShowListPage();
            Title = ConfigurationService.AppName;
            // restore the window size from 
            this.Width = context.LocalityService.MainWindowWidth;
            this.Height = context.LocalityService.MainWindowHeight;
            // check the current screen size
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            if (this.Width > screenEx.VirtualWorkingArea.Width)
                this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
            if (this.Height > screenEx.VirtualWorkingArea.Height)
                this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);

            this.SizeChanged += (sender, args) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    context.LocalityService.MainWindowHeight = this.Height;
                    context.LocalityService.MainWindowWidth = this.Width;
                    Console.WriteLine($"main window w = {this.Width}, h = {this.Height}");
                }
            };

            WinTitleBar.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            // Startup Location
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BtnClose.Click += (sender, args) =>
            {
                if (Vm.AnimationPageEditor == null
                    && (Vm.Context.ConfigurationService.Engagement.DoNotShowAgain == false || AppVersion.VersionData > Vm.Context.ConfigurationService.Engagement.DoNotShowAgainVersion)
                    && Vm.Context.ConfigurationService.Engagement.InstallTime < DateTime.Now.AddDays(-15)
                    && Vm.Context.ConfigurationService.Engagement.LastRequestRatingsTime < DateTime.Now.AddDays(-60)
                    && Vm.Context.ConfigurationService.Engagement.ConnectCount > 100
                   )
                {
                    // 显示“请求应用的评分和评价”页面 https://docs.microsoft.com/zh-cn/windows/uwp/monetize/request-ratings-and-reviews
                    Vm.RequestRatingPopupVisibility = Visibility.Visible;
                    return;
                }

#if DEV
                App.Close();
                return;
#else
                if (Shawn.Utils.ConsoleManager.HasConsole)
                    Shawn.Utils.ConsoleManager.Hide();
                HideMe();
#endif
            };
            this.Closing += (sender, args) =>
            {
                if (this.ShowInTaskbar)
                {
                    HideMe();
                    args.Cancel = true;
#if DEV
                    App.Close();
#endif
                }
            };
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };

            Vm.OnFilterStringChangedByBackend += () =>
            {
                TbFilter.CaretIndex = TbFilter.Text.Length;
            };
        }

        public void ActivateMe(bool isForceActivate = false)
        {
            if (App.MainUi?.WindowState == WindowState.Minimized)
                App.MainUi.WindowState = WindowState.Normal;
            if (isForceActivate)
                HideMe();
            Dispatcher?.Invoke(() =>
            {
                this.Visibility = Visibility.Visible;
                Topmost = true;
                this.ShowInTaskbar = true;
                this.Activate();
                Topmost = false;
            });
        }

        public void HideMe()
        {
            Dispatcher?.Invoke(() =>
            {
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
            });
        }

        public void CloseMe()
        {
            HideMe();
            Close();
        }

        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TbFilter.Focus();
        }

        private void TbFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !(sender is TextBox textBox)) return;
            var s = TagAndKeywordEncodeHelper.DecodeKeyword(Vm.FilterString);
            Vm.SetFilterStringByBackend(TagAndKeywordEncodeHelper.EncodeKeyword(s.Item1, new List<string>()));
            // Kill logical focus
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), null);
            // Kill keyboard focus
            Keyboard.ClearFocus();
            this.Focus();
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {
            CloseMe();
        }

        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
            }
            else if (e.Key == Key.Escape)
            {
                if (Vm.AnimationPageAbout != null)
                {
                    Vm.AnimationPageAbout = null;
                }
                else if (Vm.AnimationPageSettings != null)
                {
                    Vm.AnimationPageSettings = null;
                }
            }
            else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl)
            {
                TbFilter.Focus();
                TbFilter.CaretIndex = TbFilter.Text.Length;
            }
        }

        private void ProcessingRing_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
                return;
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }

        private void ButtonDismissEngagementPopup_OnClick(object sender, RoutedEventArgs e)
        {
            Vm.RequestRatingPopupVisibility = Visibility.Collapsed;
            Vm.Context.ConfigurationService.Engagement.DoNotShowAgain = CbDoNotShowEngagementAgain.IsChecked == true;
            Vm.Context.ConfigurationService.Engagement.LastRequestRatingsTime = DateTime.Now;
            Vm.Context.ConfigurationService.Engagement.ConnectCount = -100;
            Vm.Context.ConfigurationService.Engagement.DoNotShowAgainVersionString = AppVersion.Version;
            Vm.Context.ConfigurationService.Save();

#if DEV
            App.Close();
            return;
#else
                if (Shawn.Utils.ConsoleManager.HasConsole)
                    Shawn.Utils.ConsoleManager.Hide();
                HideMe();
#endif
        }
    }
}