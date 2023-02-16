using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using _1RM.Resources.Icons;
using _1RM.Service;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View
{
    public partial class LauncherWindowView : WindowChromeBase
    {
        private readonly LauncherWindowViewModel _vm;
        public LauncherWindowView(LauncherWindowViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                var myWindowHandle = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(myWindowHandle);
                source?.AddHook(HookUSBDeviceRedirect); 
                ServerIcons.Init();
            };
        }


        public override void WinTitleBar_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            try
            {
                this.DragMove();
            }
            catch
            {
            }
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Space)
            {
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.F4)
            {
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }



        private const uint WP_SYSTEMMENU = 0x02;
        private const uint WM_SYSTEMMENU = 0xa4;

        /// <summary>
        /// Redirect USB Device
        /// </summary>
        /// <returns></returns>
        private IntPtr HookUSBDeviceRedirect(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (((msg == WM_SYSTEMMENU) && (wParam.ToInt32() == WP_SYSTEMMENU)) || msg == 165)
            {
                //ShowContextMenu();
                handled = true;
                return IntPtr.Zero;
            }

            const int WM_DEVICECHANGE = 0x0219;
            if (IsClosing == false && msg == WM_DEVICECHANGE)
            {
                foreach (var host in IoC.Get<SessionControlService>().ConnectionId2Hosts.Where(x => x.Value is AxMsRdpClient09Host).Select(x => x.Value))
                {
                    if (host is AxMsRdpClient09Host rdp)
                    {
                        rdp.NotifyRedirectDeviceChange(WM_DEVICECHANGE, wParam, lParam);
                    }
                }
            }
            return IntPtr.Zero;
        }
    }
}