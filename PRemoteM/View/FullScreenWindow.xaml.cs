using System;
using PRM.Core.Protocol;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using PRM.Model;
using PRM.View.ProtocolHosts;
using Shawn.Utils;

namespace PRM.View
{
    public partial class FullScreenWindow : Window
    {
        public HostBase HostBase { get; private set; } = null;
        public FullScreenWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                this.Content = HostBase;
                var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(new HwndSourceHook(WndProc));
            };
            Closed += (sender, args) =>
            {
                if (HostBase != null)
                {
                    RemoteWindowPool.Instance.DelProtocolHostInSyncContext(HostBase.ConnectionId);
                }
            };
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DEVICECHANGE = 0x0219;
            if (msg == WM_DEVICECHANGE)
            {
                if (HostBase is AxMsRdpClient09Host rdp)
                {
                    SimpleLogHelper.Debug($"rdp.NotifyRedirectDeviceChange((uint){wParam}, (int){lParam})");
                    rdp.NotifyRedirectDeviceChange(msg, (uint) wParam, (int) lParam);
                }
            }

            return IntPtr.Zero;
        }

        public void SetProtocolHost(HostBase content)
        {
            Debug.Assert(content != null);
            this.Content = null;
            HostBase = content;
            this.Title = HostBase.ProtocolServer.DisplayName + " - " + HostBase.ProtocolServer.SubTitle;
            this.Icon = HostBase.ProtocolServer.IconImg;
            HostBase.ParentWindow = this;
            if (IsLoaded)
                this.Content = content;
        }

        public string LastTabToken = "";
    }
}
