using System;
using System.Diagnostics;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;
using VncSharpWpf;
using Color = System.Drawing.Color;

namespace PRM.Core.Protocol.VNC.Host
{
    public sealed partial class VncHost : ProtocolHostBase
    {
        private readonly ProtocolServerVNC _vncServer = null;


        public VncHost(ProtocolServerBase protocolServer) : base(protocolServer, false)
        {
            InitializeComponent();
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
                tb.SetResourceReference(TextBlock.TextProperty, "word_reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "word_close");
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
            if (Vnc.IsConnected)
                Vnc.Disconnect();
            Status = ProtocolHostStatus.Connecting;
            GridLoading.Visibility = Visibility.Visible;
            Vnc.Visibility = Visibility.Collapsed;
            Vnc.VncPort = _vncServer.GetPort();
            Vnc.GetPassword += () => _vncServer.GetDecryptedPassWord();
            if (Vnc.VncPort <= 0)
                Vnc.VncPort = 5900;
            Vnc.Connect(_vncServer.Address, false, _vncServer.VncWindowResizeMode == ProtocolServerVNC.EVncWindowResizeMode.Stretch);
        }

        public override void ReConn()
        {
            _invokeOnClosedWhenDisconnected = false;
            Conn();
            _invokeOnClosedWhenDisconnected = true;
        }

        public override void Close()
        {
            Status = ProtocolHostStatus.Disconnected;
            if (Vnc.IsConnected)
                Vnc.Disconnect();
            base.Close();
        }

        public override void GoFullScreen()
        {
            throw new NotImplementedException();
        }

        #endregion



        #region event handler

        #region connection

        private void OnConnected(object sender, EventArgs e)
        {
            Status = ProtocolHostStatus.Connected;
            Vnc.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
        }

        private bool _invokeOnClosedWhenDisconnected = true;
        private void OnConnectionLost(object sender, EventArgs e)
        {
            Status = ProtocolHostStatus.Disconnected;
            if (_invokeOnClosedWhenDisconnected)
                base.OnClosed?.Invoke(base.ConnectionId);
        }

        #endregion
        #endregion
    }
}
