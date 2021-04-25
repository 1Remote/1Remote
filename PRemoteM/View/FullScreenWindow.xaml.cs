using System;
using PRM.Core.Protocol;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using PRM.Core.Protocol.RDP.Host;
using PRM.Model;
using Shawn.Utils;

namespace PRM.View
{
    public partial class FullScreenWindow : Window
    {
        public ProtocolHostBase ProtocolHostBase { get; private set; } = null;
        public FullScreenWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                this.Content = ProtocolHostBase;
                var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(new HwndSourceHook(WndProc));
            };
            Closed += (sender, args) =>
            {
                if (ProtocolHostBase != null)
                {
                    RemoteWindowPool.Instance.DelProtocolHostInSyncContext(ProtocolHostBase.ConnectionId);
                }
            };
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DEVICECHANGE = 0x0219;
            if (msg == WM_DEVICECHANGE)
            {
                if (ProtocolHostBase is AxMsRdpClient09Host rdp)
                {
                    SimpleLogHelper.Debug($"rdp.NotifyRedirectDeviceChange((uint){wParam}, (int){lParam})");
                    rdp.NotifyRedirectDeviceChange(msg, (uint) wParam, (int) lParam);
                }
            }

            return IntPtr.Zero;
        }

        public void SetProtocolHost(ProtocolHostBase content)
        {
            Debug.Assert(content != null);
            this.Content = null;
            ProtocolHostBase = content;
            this.Title = ProtocolHostBase.ProtocolServer.DispName + " - " + ProtocolHostBase.ProtocolServer.SubTitle;
            this.Icon = ProtocolHostBase.ProtocolServer.IconImg;
            ProtocolHostBase.ParentWindow = this;
            if (IsLoaded)
                this.Content = content;
        }

        public string LastTabToken = "";
    }
}
