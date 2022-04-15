using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using PRM.Model;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;

namespace PRM.View.Host
{
    public partial class FullScreenWindowView : Window
    {
        public HostBase? Host { get; private set; } = null;
        public FullScreenWindowView()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                if (Host != null)
                {
                    if (Equals(Content, Host) == false)
                        Content = Host;
                    Host.GoFullScreen();
                }
                //var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                //source.AddHook(new HwndSourceHook(WndProc));
            };
            Closed += (sender, args) =>
            {
                if (Host != null)
                {
                    IoC.Get<RemoteWindowPool>().DelProtocolHostInSyncContext(Host.ConnectionId);
                }
            };
        }

        //private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        //{
        //    const int WM_DEVICECHANGE = 0x0219;
        //    if (msg == WM_DEVICECHANGE)
        //    {
        //        if (Host is AxMsRdpClient09Host rdp)
        //        {
        //            SimpleLogHelper.Debug($"rdp.NotifyRedirectDeviceChange((uint){wParam}, (int){lParam})");
        //            rdp.NotifyRedirectDeviceChange(msg, (uint) wParam, (int) lParam);
        //        }
        //    }
        //    return IntPtr.Zero;
        //}

        public void SetProtocolHost(HostBase content)
        {
            Debug.Assert(content != null);
            this.Content = null;
            Host = content;
            this.Title = Host.ProtocolServer.DisplayName + " - " + Host.ProtocolServer.SubTitle;
            this.Icon = Host.ProtocolServer.IconImg;
            Host.ParentWindow = this;
            if (IsLoaded)
            {
                this.Content = content;
                content.GoFullScreen();
            }
        }
        public string LastTabToken { get; set; }= "";
    }
}
