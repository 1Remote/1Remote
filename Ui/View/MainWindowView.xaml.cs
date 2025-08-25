using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace _1RM.View
{
    public partial class MainWindowView : WindowChromeBase
    {
        public MainWindowViewModel Vm { get; }
        private readonly ConfigurationService _configurationService;


        public MainWindowView(MainWindowViewModel vm, LocalityService localityService, ConfigurationService configurationService)
        {
            InitializeComponent();
            Vm = vm;
            _configurationService = configurationService;
            this.DataContext = Vm;
            Title = Assert.APP_DISPLAY_NAME;
            // restore the window size from 
            this.Width = localityService.MainWindowWidth;
            this.Height = localityService.MainWindowHeight;
            this.WindowState = localityService.MainWindowState;

            this.SizeChanged += (sender, args) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    localityService.MainWindowState = this.WindowState;
                    localityService.MainWindowHeight = this.Height;
                    localityService.MainWindowWidth = this.Width;
                    SimpleLogHelper.Debug($"Main window resize to: w = {this.Width}, h = {this.Height}");
                }
            };

            this.LocationChanged += (sender, args) =>
            {
                localityService.MainWindowTop = this.Top;
                localityService.MainWindowLeft = this.Left;
                SimpleLogHelper.Debug($"Main window move to: top = {this.Top}, left = {this.Left}");
            };

            this.StateChanged += (sender, args) =>
            {
                localityService.MainWindowState = this.WindowState;
            };

            WinTitleBar.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            // Restore or reset window location
            if (double.IsNaN(localityService.MainWindowTop) || double.IsNaN(localityService.MainWindowLeft))
            {
                ResetLocation();
            }
            else
            {
                this.Top = localityService.MainWindowTop;
                this.Left = localityService.MainWindowLeft;
            }

            BtnClose.Click += (sender, args) =>
            {
                if ((_configurationService.Engagement.DoNotShowAgain == false || AppVersion.VersionData > _configurationService.Engagement.DoNotShowAgainVersion)
                    && _configurationService.Engagement.InstallTime < DateTime.Now.AddDays(-15)
                    && _configurationService.Engagement.LastRequestRatingsTime < DateTime.Now.AddDays(-60)
                    && _configurationService.Engagement.ConnectCount > 100
                   )
                {
                    // 显示“请求应用的评分和评价”页面 https://docs.microsoft.com/zh-cn/windows/uwp/monetize/request-ratings-and-reviews
                    MaskLayerController.ShowMask(IoC.Get<RequestRatingViewModel>(), Vm);
                    return;
                }
                vm.HideMe();
#if DEBUG
                App.Close();
#else
                switch (IoC.Get<ConfigurationService>().General.CloseButtonBehavior)
                {
                    case (int)GeneralConfig.EnumCloseButtonBehavior.Exit:
                        App.Close();
                        break;
                    case (int)GeneralConfig.EnumCloseButtonBehavior.Minimize:
                        // Minimize to system tray - just hide
                    default:
                        break;
                }
#endif
            };

            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };
        }

        public void ResetLocation()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            // Check the current screen size
            this.WindowState = WindowState.Normal;
            if (this.Width > screenEx.VirtualWorkingArea.Width)
                this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
            if (this.Height > screenEx.VirtualWorkingArea.Height)
                this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);
            // Place the window in the center of the current screen
            this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
            this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.ShowInTaskbar)
            {
                Vm.HideMe();
                e.Cancel = true;
            }
            else
            {
                base.OnClosing(e);
            }
        }


        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SimpleLogHelper.Debug($"CommandFocusFilter_OnExecuted");
            if (Vm.IsShownList)
            {
                if (Vm.ServerListViewModel.TagListViewModel == null)
                {
                    Vm.MainFilterIsFocused = true;
                }
                else
                {
                    Vm.ServerListViewModel.TagsPanelViewModel.FilterIsFocused = true;
                }
            }
        }



        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                if (Keyboard.FocusedElement is TextBox)
                {
                    //SimpleLogHelper.Debug($"Current FocusedElement is " + textBox.Name);
                }
                else if (e.Key == Key.Escape && vm.IsShownList == false)
                {
                    vm.ShowList(false);
                }
                else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && vm.IsShownList)
                {
                    if (Vm.ServerListViewModel.TagListViewModel == null)
                    {
                        Vm.MainFilterIsFocused = true;
                        Vm.MainFilterIsFocused = true;
                    }
                    else
                    {
                        Vm.ServerListViewModel.TagsPanelViewModel.FilterIsFocused = true;
                        Vm.ServerListViewModel.TagsPanelViewModel.FilterIsFocused = true;
                    }
                }
            }
        }

        private void ProcessingRing_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 等待动画实现拖拽
            if (e.ClickCount >= 2)
                return;
            WinTitleBar_OnPreviewMouseDown(sender, e);
        }


        private void MainFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            // When press Esc, clear all of the search keywords, but keep selected tags;
            if (e.Key != Key.Escape || sender is TextBox == false) return;
            var s = TagAndKeywordEncodeHelper.DecodeKeyword(Vm.MainFilterString);
            Vm.SetMainFilterString(s.KeyWords.Count == 0 ? null : s.TagFilterList, null);
        }


        public override void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm.TopLevelViewModel != null)
                return;
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }
    }
}