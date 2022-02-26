using System;
using System.Windows;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.Core.Service;
using Shawn.Utils;
using PRM.View;
using PRM.ViewModel;
using PRM.ViewModel.Configuration;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace PRM
{
    public partial class MainWindow : WindowChromeBase
    {
        public VmMain Vm { get; set; }

        public MainWindow(PrmContext context, ConfigurationViewModel configurationViewModel)
        {
            App.UiDispatcher = Dispatcher;
            InitializeComponent();
            Vm = new VmMain(context, configurationViewModel, this);
            this.DataContext = Vm;
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

            WinTitleBar.PreviewMouseDown += WinTitleBar_MouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            // Startup Location
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BtnClose.Click += (sender, args) =>
            {
#if DEV
                HideMe();
                App.Close();
                return;
#else
                if (Shawn.Utils.ConsoleManager.HasConsole)
                    Shawn.Utils.ConsoleManager.Hide();
#endif
                HideMe();
            };
            this.Closing += (sender, args) =>
            {
                if (this.ShowInTaskbar)
                {
                    HideMe();
                    args.Cancel = true;
                }
            };
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };
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

        public void HideMe(bool isShowTip = true)
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
            textBox.Text = "";
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
            else
            {
                TbFilter.Focus();
                TbFilter.CaretIndex = TbFilter.Text.Length;
            }
        }
    }
}