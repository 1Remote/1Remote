using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.Core.ViewModel;
using PRM.View;

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
            BtnMinimize.Click += (sender, args) => this.WindowState = WindowState.Minimized;


            //Loaded += (sender, args) =>
            //{
            //    var r = GlobalHotkeyHooker.GetInstance().Regist(this, GlobalHotkeyHooker.HotkeyModifiers.MOD_CONTROL,
            //        Key.M,
            //        () =>
            //        {
            //            var sb = new SearchBoxWindow(this.DataContext as VmMain);
            //            sb.Show();
            //        });
            //    switch (r)
            //    {
            //        case GlobalHotkeyHooker.RetCode.Success:
            //            break;
            //        case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
            //            MessageBox.Show(Global.GetInstance().GetText("info_hotkey_registered_fail"));
            //            break;
            //        case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
            //            MessageBox.Show(Global.GetInstance().GetText("info_hotkey_already_registered"));
            //            break;
            //        default:
            //            throw new ArgumentOutOfRangeException();
            //    }
            //};
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

        private void BtnSetting_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private void BtnAllServer_Click(object sender, RoutedEventArgs e)
        {
            ((VmMain) this.DataContext).SelectedGroup = "";
        }

        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TbFilter.Focus();
        }

        private void TbFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ((TextBox)sender).Text = "";
            }
        }

        //private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        //{
        //    var x = LogoSelector.Logo;
        //    ImgRet.Source = x;
        //}
    }
}
