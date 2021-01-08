using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Dragablz;
using PRM.Core.Model;
using PRM.Model;
using PRM.ViewModel;
using Shawn.Utils;
using Shawn.Utils.DragablzTab;

namespace PRM.View.TabWindow
{
    public abstract class TabWindowBase: WindowChromeBase
    {
        protected VmTabWindow Vm;
        private HwndSource _source = null;
        private TabablzControl _tabablzControl = null;

        protected TabWindowBase(string token)
        {
            Vm = new VmTabWindow(token);
            DataContext = Vm;
        }

        protected void Init(TabablzControl tabablzControl)
        {
            _tabablzControl = tabablzControl;
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

            _tabablzControl.ClosingItemCallback += args =>
            {
                args.Cancel();
                if (args.DragablzItem.DataContext is TabItemViewModel tivm)
                {
                    var pb = tivm.Content;
                    RemoteWindowPool.Instance.DelProtocolHostInSyncContext(pb?.ConnectionId);
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
                DataContext = null;
                Vm?.CmdCloseAll.Execute();
                Vm?.Dispose();
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

        protected override void WinTitleBar_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            base.WinTitleBar_OnMouseUp(sender, e);
            Vm?.SelectedItem?.Content?.MakeItFocus();
        }



        protected virtual void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Vm?.SelectedItem != null)
            {
                this.Icon = Vm.SelectedItem.Content.ProtocolServer.IconImg;
            }
        }

        protected void FocusContentWhenPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // to click header, then focus content when release mouse press.
            Vm?.SelectedItem?.Content?.MakeItFocus();
            e.Handled = false;
        }





        public Window GetWindow()
        {
            return this;
        }

        public VmTabWindow GetViewModel()
        {
            return Vm;
        }

        public Size GetTabContentSize()
        {
            Debug.Assert(this.Resources["TabContentBorder"] != null);
            Debug.Assert(this.Resources["TitleBarHeight"] != null);
            var tabContentBorder = (Thickness)this.Resources["TabContentBorder"];
            var trapezoidHeight = (double)this.Resources["TitleBarHeight"];
            return new Size()
            {
                Width = _tabablzControl.ActualWidth - tabContentBorder.Left - tabContentBorder.Right,
                Height = _tabablzControl.ActualHeight - trapezoidHeight - tabContentBorder.Bottom - tabContentBorder.Top - 1,
            };
        }
    }
}
