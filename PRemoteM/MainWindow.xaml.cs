using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.Core.ViewModel;
using PRM.View;
using Shawn.Ulits;
//using System.Drawing; //添加引用
//using System.Windows.Forms;
using PRM.Core.Base;

//添加引用，必须用到的

namespace PRM
{
    public partial class MainWindow : Window
    {
        //实例化notifyIOC控件最小化托盘
        private System.Windows.Forms.NotifyIcon notifyIcon = null;
        public MainWindow()
        {
            InitializeComponent();

            var vm = new VmMain();
            this.DataContext = vm;

            BtnClose.Click += (sender, args) => Close();
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => this.WindowState = WindowState.Minimized;


            Loaded += (sender, args) =>
            {
                var r = GlobalHotkeyHooker.GetInstance().Regist(this, GlobalHotkeyHooker.HotkeyModifiers.MOD_CONTROL,
                    Key.M,
                    () =>
                    {
                        var sb = new SearchBoxWindow(this.DataContext as VmMain);
                        sb.Show();
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
        }

        ~MainWindow()
        {
            //notifyIcon.Visible = false;
            //notifyIcon.Icon.Dispose();
            //notifyIcon.Icon = null;
            //notifyIcon.Dispose();
            //notifyIcon = null;
            //while (notifyIcon.Visible)
            //{
            //}
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
            //this.Visibility = Visibility.Hidden;
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.BalloonTipText = "TXT:最小化到托盘...";
            notifyIcon.Text = nameof(PersonalRemoteManager);
            notifyIcon.Visible = true;
            notifyIcon.Icon = ServerAbstract.IconFromImage(ServerAbstract.ImageFromBase64(ServerAbstract.Base64Icon4));//托盘中显示的图标
            notifyIcon.ShowBalloonTip(1000);



            //右键菜单--打开菜单项
            //System.Windows.Forms.MenuItem version = new System.Windows.Forms.MenuItem("Ver:" + Version);
            System.Windows.Forms.MenuItem link = new System.Windows.Forms.MenuItem("TXT:主页");
            link.Click += NotifyIconMenuBtnLinkOnClick;
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("TXT:退出");
            exit.Click += new EventHandler(NotifyIconMenuBtnExitOnClick);
            System.Windows.Forms.MenuItem[] child = new System.Windows.Forms.MenuItem[] { link, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(child);
            notifyIcon.MouseDoubleClick += OnNotifyIconDoubleClick;

            this.StateChanged += MainWindow_StateChanged;
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
                //ActivateMe();

            
                this.Show();
                this.Visibility = Visibility.Visible;
                this.ShowInTaskbar = true;
            }
        }

        private void NotifyIconMenuBtnExitOnClick(object sender, EventArgs e)
        {
            Close();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                //this.Visibility = Visibility.Hidden;
                this.Hide();
            }
        }


        public void ActivateMe()
        {
            Dispatcher.Invoke(() =>
            {
                this.Show();
                this.Visibility = Visibility.Visible;
                this.ShowInTaskbar = true;
                this.Activate();
            });
        }












        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            ((VmMain)this.DataContext).SelectedGroup = "";
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
