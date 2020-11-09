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
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Dragablz;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Size = System.Windows.Size;
using PRM.Model;
using PRM.ViewModel;
using PRM.Core.Model;
using Shawn.Utils.DragablzTab;
using Shawn.Utils;

namespace PRM.View
{
    /// <summary>
    /// TabWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TabWindow : Window
    {
        public VmTabWindow Vm;
        private HwndSource _source = null;
        private WindowState _lastWindowState = WindowState.Normal;

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
                if (_lastWindowState != this.WindowState)
                    Vm?.SelectedItem?.Content?.MakeItFocus();
                _lastWindowState = this.WindowState;
            };


            WinTitleBar.PreviewMouseDown += WinTitleBar_MouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;
            BtnMaximize.Click += (sender, args) =>
            {
                this.WindowState = this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            };
            BtnMinimize.Click += (sender, args) =>
            {
                this.WindowState = WindowState.Minimized;
            };
            BtnClose.Click += (sender, args) =>
            {
                if (Vm?.SelectedItem != null)
                {
                    RemoteWindowPool.Instance.DelProtocolHost(Vm?.SelectedItem?.Content?.ConnectionId);
                }
                else
                {
                    Vm?.CmdClose.Execute();
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

            MouseUp += (sender, args) =>
            {
                Vm?.SelectedItem?.Content?.MakeItFocus();
            };


            Loaded += (sender, args) =>
            {
                try
                {
                    _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                    _source?.AddHook(WndProc);
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            };

            Closed += (sender, args) =>
            {
                Vm?.CmdClose.Execute();
                try
                {
                    _source?.RemoveHook(WndProc);
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            };
        }

        /// <summary>
        /// Keep content(like putty.exe) focus when darg resize window
        /// </summary>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x00a1: // WM_NCLBUTTONDOWN //case 0x00a2: // WM_NCLBUTTONUP
                    Vm?.SelectedItem?.Content?.MakeItFocus();
                    break;
            }
            return IntPtr.Zero;
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
            Vm?.SelectedItem?.Content?.MakeItFocus();
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
            }
        }

        private void FouseContentWhenMouseKeyUp(object sender, MouseButtonEventArgs e)
        {
            // to click header, then focus content when release mouse press.
            if (sender is Grid grid)
            {
                var dragablzItem = GetVisualParent<DragablzItem>(grid);
                if (dragablzItem.DataContext is TabItemViewModel tivm)
                {
                    tivm.Content?.MakeItFocus();
                }
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

        #region GetVisualParent
        public static T GetVisualParent<T>(object childObject) where T : Visual
        {
            DependencyObject child = childObject as DependencyObject;
            // iteratively traverse the visual tree
            while ((child != null) && !(child is T))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as T;
        }

        public static List<T> GetVisualChildCollection<T>(object parent) where T : Visual
        {
            List<T> visualCollection = new List<T>();
            GetVisualChildCollection(parent as DependencyObject, visualCollection);
            return visualCollection;
        }
        private static void GetVisualChildCollection<T>(DependencyObject parent, List<T> visualCollection) where T : Visual
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T)
                {
                    visualCollection.Add(child as T);
                }
                else if (child != null)
                {
                    GetVisualChildCollection(child, visualCollection);
                }
            }
        } 
        #endregion
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
