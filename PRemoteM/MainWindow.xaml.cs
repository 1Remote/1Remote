using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PRM;
using PRM.Core;
using PRM.Core.Base;
using PRM.Core.DB;
using PRM.Core.ViewModel;
using PRM.RDP;

namespace PersonalRemoteManager
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

        private string langName = "zh-cn";
        private void BtnSetting_OnClick(object sender, RoutedEventArgs e)
        {
            langName = langName == "zh-cn" ? "en-us" : "zh-cn";
            //CultureInfo currentCultureInfo = CultureInfo.CurrentCulture;
            ResourceDictionary langRd = null;
            try
            {
                langRd = MultiLangHelper.LangDictFromJsonFile(@"Languages\" + langName + ".json");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            if (langRd != null)
            {
                if (this.Resources.MergedDictionaries.Count > 0)
                {
                    this.Resources.MergedDictionaries.Clear();
                }
                this.Resources.MergedDictionaries.Add(langRd);
            }
        }
    }
}
