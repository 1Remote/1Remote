using System;
using System.Diagnostics;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits;
using Color = System.Drawing.Color;

namespace PRM.Core.Protocol.VNC.Host
{
    /// <summary>
    /// VncHost.xaml 的交互逻辑
    /// </summary>
    public sealed partial class VncHost : ProtocolHostBase
    {
        private readonly ProtocolServerVNC _vncServer = null;
        private bool _isDisconned = false;
        private bool _isConnecting = false;


        public VncHost(ProtocolServerBase protocolServer) : base(protocolServer, false)
        {
            InitializeComponent();
            Debug.Assert(protocolServer.GetType() == typeof(ProtocolServerVNC));

            Vnc.ConnectComplete += RdpOnOnConnected;
            Vnc.ConnectionLost += VncOnConnectionLost;

                _vncServer = (ProtocolServerVNC)protocolServer;
        }

        #region Base Interface
        public override void Conn()
        {
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

        public override void DisConn()
        {
            _isConnecting = false;
            if (!_isDisconned)
            {
                _isDisconned = true;
                if (Vnc.IsConnected)
                    Vnc.Disconnect();
            }
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

        private void RdpOnOnConnected(object sender, EventArgs e)
        {
            Vnc.Visibility = Visibility.Visible;
            GridLoading.Visibility = Visibility.Collapsed;
        }

        private void VncOnConnectionLost(object sender, EventArgs e)
        {
            _isDisconned = true;
            base.OnClosed?.Invoke(base.ConnectionId);
        }

        #endregion
        #endregion


        /*
        private double _normalWidth = 800;
        private double _normalHeight = 600;
        private double _normalTop = 0;
        private double _normalLeft = 0;
        private void MakeNormal2FullScreen(bool saveSize = true)
        {
            Debug.Assert(ParentWindow != null);
            //_rdpServer.AutoSetting.FullScreenLastSessionIsFullScreen = true;

            var screenSize = ScreenInfoEx.GetCurrentScreen(this.ParentWindow).Screen.Bounds;
            if (saveSize)
            {
                _normalWidth = ParentWindow.Width;
                _normalHeight = ParentWindow.Height;
                _normalTop = ParentWindow.Top;
                _normalLeft = ParentWindow.Left;
            }

            ParentWindow.WindowState = WindowState.Normal;
            ParentWindow.WindowStyle = WindowStyle.None;
            ParentWindow.ResizeMode = ResizeMode.NoResize;
            //ParentWindow.WindowState = WindowState.Maximized;

            ParentWindow.Width = screenSize.Width / (_primaryScaleFactor / 100.0);
            ParentWindow.Height = screenSize.Height / (_primaryScaleFactor / 100.0);
            ParentWindow.Left = screenSize.Left / (_primaryScaleFactor / 100.0);
            ParentWindow.Top = screenSize.Top / (_primaryScaleFactor / 100.0);
        }
        */
    }
}
