using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PRM.Model;
using PRM.Service;
using PRM.Utils;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using Ui;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace PRM.View
{
    public partial class MainWindowView : WindowChromeBase
    {
        public MainWindowViewModel Vm { get; }

        public MainWindowView(MainWindowViewModel vm)
        {
            InitializeComponent();
            Vm = vm;
            this.DataContext = Vm;
            Title = ConfigurationService.AppName;
            // restore the window size from 
            this.Width = Vm.Context.LocalityService.MainWindowWidth;
            this.Height = Vm.Context.LocalityService.MainWindowHeight;
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
                    Vm.Context.LocalityService.MainWindowHeight = this.Height;
                    Vm.Context.LocalityService.MainWindowWidth = this.Width;
                    SimpleLogHelper.Info($"Main window resize to: w = {this.Width}, h = {this.Height}");
                }
            };

            WinTitleBar.PreviewMouseDown += WinTitleBar_OnPreviewMouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            // Startup Location
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BtnClose.Click += (sender, args) =>
            {
                if ((Vm.Context.ConfigurationService.Engagement.DoNotShowAgain == false || AppVersion.VersionData > Vm.Context.ConfigurationService.Engagement.DoNotShowAgainVersion)
                    && Vm.Context.ConfigurationService.Engagement.InstallTime < DateTime.Now.AddDays(-15)
                    && Vm.Context.ConfigurationService.Engagement.LastRequestRatingsTime < DateTime.Now.AddDays(-60)
                    && Vm.Context.ConfigurationService.Engagement.ConnectCount > 30
                   )
                {
                    // 显示“请求应用的评分和评价”页面 https://docs.microsoft.com/zh-cn/windows/uwp/monetize/request-ratings-and-reviews
                    Vm.TopLevelViewModel = IoC.Get<RequestRatingViewModel>();
                    return;
                }
                vm.HideMe();
#if DEV
                App.Close();
#endif
            };

            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };
        }




        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SimpleLogHelper.Debug($"CommandFocusFilter_OnExecuted");
            if (Vm.IsShownList())
                Vm.SearchControlViewModel.IsFocused = true;
        }



        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            SimpleLogHelper.Debug($"MainWindow_OnKeyDown");
            if (Keyboard.FocusedElement is TextBox textBox)
            {
                SimpleLogHelper.Debug($"Current FocusedElement is " + textBox.Name);
            }
            else if (e.Key == Key.Escape && this.DataContext is MainWindowViewModel vm && vm.IsShownList() == false)
            {
                vm.ShowList();
            }
            else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl)
            {
                Vm.SearchControlViewModel.IsFocused = true;
                //TbFilter.Focus();
                //TbFilter.CaretIndex = TbFilter.Text.Length;
            }
        }

        private void ProcessingRing_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
                return;
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }
    }
}