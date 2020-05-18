using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.Model;
using PRM.ViewModel;
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


            this.Width = SystemConfig.GetInstance().Locality.TabWindowWidth;
            this.Height = SystemConfig.GetInstance().Locality.TabWindowHeight;
            this.SizeChanged += (sender, args) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    SystemConfig.GetInstance().Locality.TabWindowHeight = this.Height;
                    SystemConfig.GetInstance().Locality.TabWindowWidth = this.Width;
                    SystemConfig.GetInstance().Locality.Save();
                }
            };

            WinTitleBar.PreviewMouseDown += WinTitleBar_MouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            BtnClose.Click += (sender, args) =>
            {
                if (Vm.SelectedItem != null)
                {
                    WindowPool.DelProtocolHost(Vm.SelectedItem.Content.ConnectionId);
                }
                else
                {
                    Vm.CmdClose.Execute();
                }
            };
            this.Activated += (sender, args) =>
            {
                Vm?.SelectedItem?.Content?.MakeItFocus();
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

        private void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem != null)
            {
                this.Title = Vm.SelectedItem.Header + " - " + "PRemoteM";
                this.Icon =
                this.IconTitleBar.Source = Vm.SelectedItem.Content.ProtocolServer.IconImg;
                var t = new Task(() =>
                {
                    Thread.Sleep(100);
                    Dispatcher.Invoke(() =>
                    {
                        if(IsActive)
                            Vm?.SelectedItem?.Content?.MakeItFocus();
                    });
                });
                t.Start();
            }
        }

        //<!--TODO 从同一位置读取该值-->
        protected Thickness TabContentBorder { get; } = new Thickness(2, 0, 2, 2);
        public Size GetTabContentSize()
        {
            return new Size()
            {
                Width = TabablzControl.ActualWidth - TabContentBorder.Left - TabContentBorder.Right,
                Height = TabablzControl.ActualHeight - 30 - 2 - 1,
            };
        }
    }


    public class Hex2Brush : IValueConverter
    {
        #region IValueConverter
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string hex = value.ToString();
            var brush = (System.Windows.Media.Brush)(new System.Windows.Media.BrushConverter().ConvertFrom(hex));
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return "#FFFFFF";
        }
        #endregion
    }
}
