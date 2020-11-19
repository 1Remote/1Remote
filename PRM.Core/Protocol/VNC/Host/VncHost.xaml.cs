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
        private bool _isDisconned = false;
        private bool _isConnecting = false;


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
                }, o => IsConnected() == true)
            });
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Ctrl + Esc",
                Command = new RelayCommand((o) =>
                {
                    Vnc.SendSpecialKeys(SpecialKeys.CtrlEsc);
                }, o => IsConnected() == true)
            });
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = "Alt + F4",
                Command = new RelayCommand((o) =>
                {
                    Vnc.SendSpecialKeys(SpecialKeys.AltF4);
                }, o => IsConnected() == true)
            });
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "tab_button_reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "button_close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { DisConn(); })
                });
            }
        }

        #region Base Interface
        public override void Conn()
        {
            if(IsConnected())
                Vnc.Disconnect();
            _isConnecting = true;
            _isDisconned = false;
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

        public override void DisConn()
        {
            _isConnecting = false;
            if (!_isDisconned)
            {
                _isDisconned = true;
                if (Vnc.IsConnected)
                    Vnc.Disconnect();
            }
            base.DisConn();
        }

        public override void GoFullScreen()
        {
            if (CanFullScreen)
            {
                //Debug.Assert(this.ParentWindow != null);
                //if (_rdpServer.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen)
                //{
                //    _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = ScreenInfoEx.GetCurrentScreen(this.ParentWindow).Index;
                //}
                //else
                //    _rdpServer.AutoSetting.FullScreenLastSessionScreenIndex = -1;
                //Server.AddOrUpdate(_rdpServer);
                //MakeNormal2FullScreen()
            }
        }

        public override bool IsConnected()
        {
            return this._isDisconned == false && Vnc.IsConnected;
        }

        public override bool IsConnecting()
        {
            return _isConnecting;
        }

        #endregion



        #region event handler

        #region connection

        private void OnConnected(object sender, EventArgs e)
        {
            Vnc.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
        }

        private bool _invokeOnClosedWhenDisconnected = true;
        private void OnConnectionLost(object sender, EventArgs e)
        {
            _isDisconned = true;
            if (_invokeOnClosedWhenDisconnected)
                base.OnClosed?.Invoke(base.ConnectionId);
        }

        #endregion
        #endregion
    }
}
