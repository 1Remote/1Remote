using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.VNC;
using Shawn.Utils;
using VncSharpCore;

namespace PRM.View.ProtocolHosts
{
    public sealed partial class VncHost : HostBase
    {
        private readonly ProtocolServerVNC _vncServer = null;

        public VncHost(PrmContext context, ProtocolServerBase protocolServer) : base(context, protocolServer, false)
        {
            InitializeComponent();
            GridMessageBox.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;

            Debug.Assert(protocolServer.GetType() == typeof(ProtocolServerVNC));

            Vnc.ConnectComplete += OnConnected;
            Vnc.ConnectionLost += OnConnectionLost;

            _vncServer = (ProtocolServerVNC)protocolServer;

            MenuItems.Clear();
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Ctrl + Alt + Del",
                Command = new RelayCommand((o) =>
                {
                    Vnc.SendSpecialKeys(SpecialKeys.CtrlAltDel);
                }, o => Status == ProtocolHostStatus.Connected)
            });
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Ctrl + Esc",
                Command = new RelayCommand((o) =>
                {
                    Vnc.SendSpecialKeys(SpecialKeys.CtrlEsc);
                }, o => Status == ProtocolHostStatus.Connected)
            });
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Alt + F4",
                Command = new RelayCommand((o) =>
                {
                    Vnc.SendSpecialKeys(SpecialKeys.AltF4);
                }, o => Status == ProtocolHostStatus.Connected)
            });
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { Close(); })
                });
            }
        }

        #region Base Interface

        public override void Conn()
        {
            Status = ProtocolHostStatus.Connecting;
            if (Vnc.IsConnected)
                Vnc.Disconnect();
            Status = ProtocolHostStatus.Connecting;
            GridLoading.Visibility = Visibility.Visible;
            VncFormsHost.Visibility = Visibility.Collapsed;
            Vnc.VncPort = _vncServer.GetPort();
            Vnc.GetPassword = () => Context.DataService.DecryptOrReturnOriginalString(_vncServer.Password);
            if (Vnc.VncPort <= 0)
                Vnc.VncPort = 5900;
            try
            {
                Vnc.Connect(_vncServer.Address, false, _vncServer.VncWindowResizeMode == ProtocolServerVNC.EVncWindowResizeMode.Stretch);
                VncFormsHost.Visibility = Visibility.Visible;
                GridLoading.Visibility = Visibility.Collapsed;
                GridMessageBox.Visibility = Visibility.Collapsed;
                Status = ProtocolHostStatus.Connected;
            }
            catch (Exception e)
            {
                _invokeOnClosedWhenDisconnected = false;
                VncFormsHost.Visibility = Visibility.Collapsed;
                GridLoading.Visibility = Visibility.Visible;
                GridMessageBox.Visibility = Visibility.Visible;

                TbMessageTitle.Visibility = Visibility.Collapsed;
                BtnReconn.Visibility = Visibility.Visible;
                TbMessage.Text = e.Message;
            }
        }

        public override void ReConn()
        {
            VncFormsHost.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;
            GridMessageBox.Visibility = Visibility.Collapsed;
            _invokeOnClosedWhenDisconnected = false;
            Conn();
            _invokeOnClosedWhenDisconnected = true;
        }

        public override void Close()
        {
            Status = ProtocolHostStatus.Disconnected;
            if (Vnc.IsConnected)
                Vnc.Disconnect();
            Status = ProtocolHostStatus.Disconnected;
            base.Close();
        }

        public override void GoFullScreen()
        {
            throw new NotImplementedException();
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Native;
        }

        public override IntPtr GetHostHwnd()
        {
            return IntPtr.Zero;
        }

        #endregion Base Interface

        #region event handler

        #region connection

        private void OnConnected(object sender, EventArgs e)
        {
            Status = ProtocolHostStatus.Connected;
            VncFormsHost.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Collapsed;
        }

        private bool _invokeOnClosedWhenDisconnected = true;

        private void OnConnectionLost(object sender, EventArgs e)
        {
            Status = ProtocolHostStatus.Disconnected;
            VncFormsHost.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Collapsed;
            GridMessageBox.Visibility = Visibility.Visible;
            TbMessageTitle.Visibility = Visibility.Collapsed;
            BtnReconn.Visibility = Visibility.Visible;
            TbMessage.Text = "Connection lost...";
            if (_invokeOnClosedWhenDisconnected)
                base.OnClosed?.Invoke(base.ConnectionId);
        }

        #endregion connection

        #endregion event handler

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnReconn_OnClick(object sender, RoutedEventArgs e)
        {
            ReConn();
        }
    }
}