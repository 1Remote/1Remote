using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PRM.Core.Model;
using Shawn.Utils.DragablzTab;
using PRM.Model;
using PRM.ViewModel;
using Shawn.Utils;
using Size = System.Windows.Size;

namespace PRM.View
{
    /// <summary>
    /// TabWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TabWindow : Window
    {
        public VmTabWindow Vm;
        public TabWindow(string token)
        {
            InitializeComponent();
            Vm = new VmTabWindow(token);
            DataContext = Vm;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Width = SystemConfig.Instance.Locality.TabWindowWidth;
            this.Height = SystemConfig.Instance.Locality.TabWindowHeight;
            this.SizeChanged += (sender, args) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    SystemConfig.Instance.Locality.TabWindowHeight = this.Height;
                    SystemConfig.Instance.Locality.TabWindowWidth = this.Width;
                    SystemConfig.Instance.Locality.Save();
                }
            };


            WinTitleBar.PreviewMouseDown += WinTitleBar_MouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            BtnMaximize.Click += (sender, args) => this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            BtnMinimize.Click += (sender, args) => { this.WindowState = WindowState.Minimized; };
            BtnClose.Click += (sender, args) =>
            {
                if (Vm.SelectedItem != null)
                {
                    RemoteWindowPool.Instance.DelProtocolHost(Vm?.SelectedItem?.Content?.ConnectionId);
                }
                else
                {
                    Vm.CmdClose.Execute();
                }
            };
            TabablzControl.ClosingItemCallback += args =>
            {
                args.Cancel();
                if (args.DragablzItem.DataContext is TabItemViewModel tivm)
                {
                    var pb = tivm.Content;
                    RemoteWindowPool.Instance.DelProtocolHost(pb?.ConnectionId);
                }
            };

            Activated += (sender, args) => { Vm?.SelectedItem?.Content?.MakeItFocus(); };
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
                    var p = ScreenInfoEx.GetMouseVirtualPosition();
                    var top = p.Y;
                    var left = p.X;
                    this.Top = top - 15;
                    this.Left = left - this.Width / 2;
                    this.WindowState = WindowState.Normal;
                    this.Top = top - 15;
                    this.Left = left - this.Width / 2;
                }
                this.DragMove();
            }
        }
        #endregion

        private void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem != null)
            {
                this.Icon =
                this.IconTitleBar.Source = Vm.SelectedItem.Content.ProtocolServer.IconImg;
                var t = new Task(() =>
                {
                    Thread.Sleep(100);
                    Dispatcher.Invoke(() =>
                    {
                        if (IsActive)
                            Vm?.SelectedItem?.Content?.MakeItFocus();
                    });
                });
                t.Start();
            }
        }

        public Size GetTabContentSize()
        {
            Debug.Assert(this.Resources["TabContentBorder"] != null);
            Debug.Assert(this.Resources["TrapezoidHeight"] != null);
            var tabContentBorder = (Thickness)this.Resources["TabContentBorder"];
            var trapezoidHeight = (double)this.Resources["TrapezoidHeight"];
            return new Size()
            {
                Width = TabablzControl.ActualWidth - tabContentBorder.Left - tabContentBorder.Right,
                Height = TabablzControl.ActualHeight - trapezoidHeight - tabContentBorder.Bottom - 1,
            };
        }


        //private static RelayCommand _myCloseItemCommand = (RoutedCommand)new RoutedUICommand("Close", "Close", typeof(TabablzControl));;
        //public static RelayCommand MyCloseItemCommand
        //{
        //    get
        //    {
        //        if (_cmdConnServer == null)
        //            _cmdConnServer = new RelayCommand((o) =>
        //            {
        //                TabablzControl.CloseItemCommand
        //                GlobalEventHelper.OnServerConnect?.Invoke(Server.Id);
        //            });
        //        return _cmdConnServer;
        //    }
        //}
    }


    public class Hex2Brush : IValueConverter
    {
        #region IValueConverter
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string hex = value.ToString();
            var brush = ColorAndBrushHelper.ColorToMediaBrush(hex);
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return "#FFFFFF";
        }
        #endregion
    }
}
