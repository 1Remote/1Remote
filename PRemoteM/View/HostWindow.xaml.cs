using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PRM.Core.Protocol;
using PRM.Core.Ulits.DragablzTab;
using PRM.ViewModel;
using Shawn.Ulits.RDP;

namespace PRM.View
{
    /// <summary>
    /// HostWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HostWindow : Window
    {
        private VmHostWindow _vm;
        public HostWindow()
        {
            InitializeComponent();

            //Closed += (sender, args) => { MessageBox.Show("Tab win close"); };
            BtnClose.Click += (sender, args) =>
            {
                var _vm = ((VmHostWindow) this.DataContext);
                if (_vm?.Items.Count > 1)
                {
                    // TODO 销毁 SelectedItem
                    _vm.Items.Remove(_vm.SelectedItem);
                }
                else
                {
                    Close();
                }
            };
            BtnCloseAll.Click += (sender, args) => Close();
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
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }


        private void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataContext is VmHostWindow _vm
                && _vm?.SelectedItem != null)
            {
                // TODO set ICOm
                this.Title = _vm.SelectedItem.Header.ToString() + " - " + "PRemoteM";
            }
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
