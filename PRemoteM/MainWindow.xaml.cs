using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.ViewModel;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace PRM
{
    public partial class MainWindow : Window
    {
        public VmMain VmMain { get; private set; }
        public MainWindow()
        {
            InitializeComponent();
            VmMain = new VmMain();
            this.DataContext = VmMain;
            Title = SystemConfig.AppName;


            this.Width = SystemConfig.Instance.Locality.MainWindowWidth;
            this.Height = SystemConfig.Instance.Locality.MainWindowHeight;
            WinTitleBar.MouseUp += (sender, e) =>
            {
                if (e.LeftButton == MouseButtonState.Released)
                {
                    if (this.WindowState == WindowState.Normal)
                    {
                        SystemConfig.Instance.Locality.MainWindowTop = this.Top;
                        SystemConfig.Instance.Locality.MainWindowLeft = this.Left;
                        SystemConfig.Instance.Locality.Save();
                        Console.WriteLine($"main window Top = {this.Top}, Left = {this.Left}");
                    }
                }
            };
            this.SizeChanged += (sender, args) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    SystemConfig.Instance.Locality.MainWindowHeight = this.Height;
                    SystemConfig.Instance.Locality.MainWindowWidth = this.Width;
                    SystemConfig.Instance.Locality.Save();
                    Console.WriteLine($"main window w = {this.Width}, h = {this.Height}");
                }
            };

            WinTitleBar.PreviewMouseDown += WinTitleBar_MouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;




            // Startup Location
            {
                var top = SystemConfig.Instance.Locality.MainWindowTop;
                var left = SystemConfig.Instance.Locality.MainWindowLeft;

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
                CloseMe();
                return;
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

        #region DragMove
        private bool _isLeftMouseDown = false;
        private bool _isDraging = false;
        private void WinTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraging = false;
            _isLeftMouseDown = false;

            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed)
            {
                this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isLeftMouseDown = true;
                var th = new Thread(new ThreadStart(() =>
                {
                    Thread.Sleep(50);
                    if (_isLeftMouseDown)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _isDraging = true;
                        });
                    }
                }));
                th.Start();
            }
        }
        private void WinTitleBar_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isLeftMouseDown = false;
            _isDraging = false;
        }
        private void WinTitleBar_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _isDraging)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    var top = Mouse.GetPosition(this).Y;
                    var left = Mouse.GetPosition(this).X;
                    this.Top = 0;
                    this.Left = 0;
                    this.WindowState = WindowState.Normal;
                    this.Top = top - 15;
                    this.Left = left - this.Width / 2;
                }
                this.DragMove();
            }
        }
        #endregion

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
                if (!string.IsNullOrEmpty(App.TaskTrayIcon.BalloonTipText))
                    App.TaskTrayIcon?.ShowBalloonTip(1000);
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
            if (e.Key == Key.Escape)
            {
                (sender as TextBox).Text = "";
            }
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {
            CloseMe();
        }
    }
}
