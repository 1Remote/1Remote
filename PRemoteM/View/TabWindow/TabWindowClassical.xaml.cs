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

namespace PRM.View.TabWindow
{
    public partial class TabWindowClassical : Window, ITab
    {
        protected VmTabWindow Vm;
        private HwndSource _source = null;

        public TabWindowClassical(string token)
        {
            InitializeComponent();
            Vm = new VmTabWindow(token);
            DataContext = Vm;

            this.Activated += (sender, args) =>
            {
                this.StopFlashingWindow();
            };

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Width = SystemConfig.Instance.Locality.TabWindowWidth;
            this.Height = SystemConfig.Instance.Locality.TabWindowHeight;
            this.MinWidth = this.MinHeight = 300;
            this.WindowState = SystemConfig.Instance.Locality.TabWindowState;

            // save window size when size changed
            var lastWindowState = WindowState.Normal;
            this.SizeChanged += (sizeChangeSender, _) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    SystemConfig.Instance.Locality.TabWindowHeight = this.Height;
                    SystemConfig.Instance.Locality.TabWindowWidth = this.Width;
                    SystemConfig.Instance.Locality.TabWindowState = this.WindowState;
                    SystemConfig.Instance.Locality.Save();
                }
                if (lastWindowState != this.WindowState)
                    Vm?.SelectedItem?.Content?.MakeItFocus();
                lastWindowState = this.WindowState;
                SimpleLogHelper.Debug($"Tab size change to:W = {this.Width}, H = {this.Height}, Child {this.Vm?.SelectedItem?.Content?.Width}, {this.Vm?.SelectedItem?.Content?.Height}");
            };
            this.StateChanged += delegate (object sender, EventArgs args)
            {
                if (this.WindowState != WindowState.Minimized)
                {
                    Vm?.SelectedItem?.Content?.ToggleAutoResize(true);
                    SystemConfig.Instance.Locality.TabWindowHeight = this.Height;
                    SystemConfig.Instance.Locality.TabWindowWidth = this.Width;
                    SystemConfig.Instance.Locality.TabWindowState = this.WindowState;
                    SystemConfig.Instance.Locality.Save();
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

            // focus content when click
            MouseUp += (sender, args) =>
            {
                Vm?.SelectedItem?.Content?.MakeItFocus();
            };


            Loaded += (sender, args) =>
            {
                // focus content(like putty.exe) when drag resize window
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
                Vm?.CmdCloseAll.Execute();
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
        private bool _isDragging = false;
        private void WinTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _isLeftMouseDown = false;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }
            else if (e.ClickCount == 2)
            {
                this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            }
            else
            {
                _isLeftMouseDown = true;
                var th = new Thread(() =>
                {
                    Thread.Sleep(50);
                    if (_isLeftMouseDown)
                    {
                        _isDragging = true;
                    }
                });
                th.Start();
            }
        }
        private void WinTitleBar_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isLeftMouseDown = false;
            _isDragging = false;
            Vm?.SelectedItem?.Content?.MakeItFocus();
        }
        private void WinTitleBar_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_isDragging)
            {
                return;
            }

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

            try
            {
                this.DragMove();
            }
            catch
            {
                // ignored
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

        private void FocusContentWhenPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // to click header, then focus content when release mouse press.
            Vm?.SelectedItem?.Content?.MakeItFocus();
            e.Handled = false;
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
                Height = TabablzControl.ActualHeight - trapezoidHeight - tabContentBorder.Bottom - tabContentBorder.Top - 1,
            };
        }

        public Window GetWindow()
        {
            return this;
        }

        public VmTabWindow GetViewModel()
        {
            return Vm;
        }
    }
}
