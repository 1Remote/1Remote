using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using Dragablz;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Model;
using PRM.ViewModel;
using Shawn.Utils;
using Shawn.Utils.DragablzTab;
using Timer = System.Timers.Timer;

namespace PRM.View.TabWindow
{
    public abstract class TabWindowBase : WindowChromeBase, ITab
    {
        protected VmTabWindow Vm;
        private HwndSource _source = null;
        private TabablzControl _tabablzControl = null;
        public string Token => Vm?.Token;

        private IntPtr _lastActivatedWindowHandle = IntPtr.Zero;
        private IntPtr _myWindowHandle;
        private readonly Timer _timer4CheckForegroundWindow;

        protected TabWindowBase(string token)
        {
            Vm = new VmTabWindow(token);
            DataContext = Vm;
            _timer4CheckForegroundWindow = new Timer();

            this.Loaded += (sender, args) =>
            {
                var wih = new WindowInteropHelper(this);
                _myWindowHandle = wih.Handle;
                _timer4CheckForegroundWindow.Interval = 100;
                _timer4CheckForegroundWindow.AutoReset = true;
                _timer4CheckForegroundWindow.Elapsed += Timer4CheckForegroundWindowOnElapsed;
                _timer4CheckForegroundWindow.Start();
            };
        }

        private void Timer4CheckForegroundWindowOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (Vm?.SelectedItem?.Content == null)
                return;

            var hWnd = this.Vm.SelectedItem.Content.GetHostHwnd();
            if (hWnd == IntPtr.Zero) return;


            var nowActivatedWindowHandle = GetForegroundWindow();
            if (nowActivatedWindowHandle == hWnd && nowActivatedWindowHandle != _lastActivatedWindowHandle)
            {
                SimpleLogHelper.Debug($"TabWindowBase: _lastActivatedWindowHandle = ({_lastActivatedWindowHandle})");
                SimpleLogHelper.Debug($"TabWindowBase: nowActivatedWindowHandle = ({nowActivatedWindowHandle}), hWnd = {hWnd}");
                SimpleLogHelper.Debug($"TabWindowBase: BringWindowToTop({_myWindowHandle})");
                BringWindowToTop(_myWindowHandle);
            }
            _lastActivatedWindowHandle = nowActivatedWindowHandle;
        }

        protected void Init(TabablzControl tabablzControl)
        {
            _tabablzControl = tabablzControl;
            this.Activated += (sender, args) =>
            {
                this.StopFlashingWindow();
                // can't switch the focus directly, otherwise the close button, the minimize button, drag and drop, etc. will become invalid.
                if (Vm?.SelectedItem?.Content.GetProtocolHostType() == ProtocolHostType.Integrate)
                    Task.Factory.StartNew(() =>
                    {
                        Thread.Sleep(150);
                        if ((System.Windows.Forms.Control.MouseButtons != MouseButtons.Left))
                            Vm?.SelectedItem?.Content?.MakeItFocus();
                    });
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
                if (args.DragablzItem.DataContext is TabItemViewModel viewModel)
                {
                    var pb = viewModel.Content;
                    RemoteWindowPool.Instance.DelProtocolHostInSyncContext(pb?.ConnectionId);
                }
            };

            // focus content when SelectionChanged
            _tabablzControl.SelectionChanged += (sender, args) =>
            {
                // can't switch the focus directly, otherwise the tab drag detach. will become invalid.
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(150);
                    if ((System.Windows.Forms.Control.MouseButtons != MouseButtons.Left))
                        Vm?.SelectedItem?.Content?.MakeItFocus();
                });
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
                _timer4CheckForegroundWindow?.Dispose();
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



        public VmTabWindow GetViewModel()
        {
            return Vm;
        }

        public void AddItem(TabItemViewModel newItem)
        {
            if (Vm.Items.Any(x => x.Content.ConnectionId == newItem.Content.ConnectionId))
            {
                Vm.SelectedItem = Vm.Items.First(x => x.Content.ConnectionId == newItem.Content.ConnectionId);
                return;
            }
            Vm.Items.Add(newItem);
            Vm.SelectedItem = Vm.Items.Last();
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



        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hWnd);
    }
}
