using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using PRM.Core.Base;
using PRM.Core.DB;
using PRM.Core.ViewModel;
using PRM.RDP;

namespace PRM.Core.Protocol.RDP
{
    public partial class AddRdp : Window
    {
        public ServerRDP Server
        {
            get => this.DataContext as ServerRDP;
            set => this.DataContext = value;
        }

        public bool IsSave { get; set; }

        public AddRdp()
        {
            InitializeComponent();

            // TODO 载入配置模板
            var rdp = new ServerRDP();
            //rdp = (ServerRDP)rdp.CreateFromJsonString("");
            this.DataContext = rdp;



            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            BtnClose.Click += (sender, args) => Close();
            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => this.WindowState = WindowState.Minimized;

            //Close();
            //PRMSqliteHelper psh = new PRMSqliteHelper();
            //psh.CreateDb();
        }




        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        private Point startPos;
        private void System_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount >= 2)
                {
                    this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
                }
                else
                {
                    startPos = e.GetPosition(null);
                }
            }
        }

        private void System_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (this.WindowState == WindowState.Maximized && Math.Abs(startPos.Y - e.GetPosition(null).Y) > 2)
                {
                    var point = PointToScreen(e.GetPosition(null));

                    this.WindowState = WindowState.Normal;

                    this.Left = point.X - this.ActualWidth / 2;
                    this.Top = point.Y - WinTitleBar.ActualHeight / 2;
                }
                DragMove();
            }
        }

        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            IsSave = true;
            Close();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            IsSave = false;
            Close();
        }
    }
}
