using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using PersonalRemoteManager;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.View;
using PRM.ViewModel;
using Shawn.Ulits;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace PRM
{
    public partial class MainWindow : Window
    {
        public readonly VmMain VmMain;
        public MainWindow()
        {
            InitializeComponent();
            VmMain = new VmMain();
            this.DataContext = VmMain;
            Title = SystemConfig.AppName;


            this.Width = SystemConfig.GetInstance().Locality.MainWindowWidth;
            this.Height = SystemConfig.GetInstance().Locality.MainWindowHeight;

            // Startup Location
            {
                var top = SystemConfig.GetInstance().Locality.MainWindowTop;
                var left = SystemConfig.GetInstance().Locality.MainWindowLeft;
                
                

                if (top >= 0 && top < Screen.PrimaryScreen.Bounds.Height
                    && left >= 0 && left < Screen.PrimaryScreen.Bounds.Width
                )
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Top = top;
                    Left = left;
                }
                else
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }

            BtnClose.Click += (sender, args) =>
            {
#if DEBUG
                Close();
                return;
#endif
                HideMe();
                App.TaskTrayIcon?.ShowBalloonTip(1000);
            };
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };
        }

        /// <summary>
        /// DragMove
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void System_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }


        private void BtnSetting_OnClick(object sender, RoutedEventArgs e)
        {
            VmMain.SysOptionsMenuIsOpen = true;
        }


        public void ActivateMe()
        {
            Dispatcher.Invoke(() =>
            {
                this.Visibility = Visibility.Visible;
                this.ShowInTaskbar = true;
                this.Activate();
            });
        }
        public void HideMe()
        {
            Dispatcher.Invoke(() =>
            {
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
            });
        }

        private void GridAbout_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            VmMain.CmdGoAboutPage.Execute();
        }



        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TbFilter.Focus();
        }

        private void TbFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                (sender as TextBox).Text = "";
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                SystemConfig.GetInstance().Locality.MainWindowHeight = this.Height;
                SystemConfig.GetInstance().Locality.MainWindowWidth = this.Width;
                SystemConfig.GetInstance().Locality.MainWindowTop = this.Top;
                SystemConfig.GetInstance().Locality.MainWindowLeft = this.Left;
                SystemConfig.GetInstance().Locality.Save();
            }
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
