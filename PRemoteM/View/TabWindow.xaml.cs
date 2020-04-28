using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            }
        }

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
}
