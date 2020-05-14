using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
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

            BtnClose.Click += (sender, args) =>
            {
                if (Vm.SelectedItem != null)
                {
                    WindowPool.DelProtocolHost(Vm.SelectedItem.Content.ProtocolServer.Id);
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

        /// <summary>
        /// DragMove
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void System_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed)
            {
                this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem != null)
            {
                this.Title = Vm.SelectedItem.Header + " - " + "PRemoteM";
                this.Icon =
                this.IconTitleBar.Source = Vm.SelectedItem.Content.ProtocolServer.IconImg;
                Vm?.SelectedItem.Content?.MakeItFocus();
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
