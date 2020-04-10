using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PersonalRemoteManager;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.View;
using PRM.ViewModel;
using Shawn.Ulits;

//添加引用，必须用到的

namespace PRM
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            
            var vm = new VmMain();
            this.DataContext = vm;



            BtnClose.Click += (sender, args) => Close();
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) =>
            {
                HideMe();
                //托盘中显示的图标
                App.TaskTrayIcon?.ShowBalloonTip(1000);
            };

            Loaded += (sender, args) =>
            {
            };
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
            PopupSettingMenu.IsOpen = true;
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

        private void GridAbout_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            PopupSettingMenu.IsOpen = false;
            //TODO ADD about page
        }
    }
}
