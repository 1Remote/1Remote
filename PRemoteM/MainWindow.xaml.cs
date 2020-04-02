using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        //实例化notifyIOC控件最小化托盘
        private System.Windows.Forms.NotifyIcon _notifyIcon = null;
        private SearchBoxWindow _searchBoxWindow = null;

        public MainWindow()
        {
            InitializeComponent();
            
            var vm = new VmMain();
            this.DataContext = vm;

            BtnClose.Click += (sender, args) => Close();
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) =>
            {
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
                //托盘中显示的图标
                _notifyIcon?.ShowBalloonTip(1000);
            };

            Loaded += (sender, args) =>
            {
                _searchBoxWindow = new SearchBoxWindow();
                _searchBoxWindow.Show();
                var r = GlobalHotkeyHooker.GetInstance().Regist(this, GlobalHotkeyHooker.HotkeyModifiers.MOD_CONTROL,
                    Key.M,
                    () =>
                    {
                        _searchBoxWindow.ShowMe();
                    });
                switch (r)
                {
                    case GlobalHotkeyHooker.RetCode.Success:
                        break;
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                        MessageBox.Show(Global.GetInstance().GetText("info_hotkey_registered_fail"));
                        break;
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                        MessageBox.Show(Global.GetInstance().GetText("info_hotkey_already_registered"));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                InitTaskTray();
            };


            Closed += (sender, args) =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _searchBoxWindow.Close();
                    _searchBoxWindow = null;
                }
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
        }





        private void InitTaskTray()
        {
            if (_notifyIcon == null)
            {
                //右键菜单--打开菜单项
                //System.Windows.Forms.MenuItem version = new System.Windows.Forms.MenuItem("Ver:" + Version);
                System.Windows.Forms.MenuItem link = new System.Windows.Forms.MenuItem("TXT:主页");
                link.Click += NotifyIconMenuBtnLinkOnClick;
                System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("TXT:退出");
                exit.Click += new EventHandler(NotifyIconMenuBtnExitOnClick);
                System.Windows.Forms.MenuItem[] child = new System.Windows.Forms.MenuItem[] {link, exit};

                // 设置托盘
                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = "TXT:XXXX系统",
                    Icon = NetImageProcessHelper.ToIcon(NetImageProcessHelper.BitmapFromBytes(Convert.FromBase64String(ProtocolServerBase.Base64Icon4))),
                    ContextMenu = new System.Windows.Forms.ContextMenu(child),
                    BalloonTipText = "TXT:正在后台运行...",
                    Visible = true
                };
                _notifyIcon.MouseDoubleClick += OnNotifyIconDoubleClick;
            }
        }

        private void NotifyIconMenuBtnLinkOnClick(object sender, EventArgs e)
        {
            string home_uri = "http://172.20.65.78:3300/";
            System.Diagnostics.Process.Start(home_uri);
        }

        private void OnNotifyIconDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ActivateMe();
            }
        }

        private void NotifyIconMenuBtnExitOnClick(object sender, EventArgs e)
        {
            Close();
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
    }
}
