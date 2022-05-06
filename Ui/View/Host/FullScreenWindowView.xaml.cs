using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using PRM.Model;
using PRM.Service;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;

namespace PRM.View.Host
{
    public partial class FullScreenWindowView : Window
    {
        private HostBase? _host = null;

        public HostBase? Host
        {
            get => _host;
            private set
            {
                if (Equals(_host, value) == false)
                {
                    _host = value;
                    SetContent();
                }
            }
        }

        public const int DESIGN_WIDTH = 600;
        public const int DESIGN_HEIGHT = 480;
        public FullScreenWindowView()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                this.Width = DESIGN_WIDTH;
                this.Height = DESIGN_HEIGHT;
                SetContent();
            };
            Closed += (sender, args) =>
            {
                if (Host != null)
                {
                    IoC.Get<SessionControlService>().DelProtocolHost(Host.ConnectionId);
                }
            };
        }

        private void SetContent()
        {
            if (this.IsLoaded && Host != null)
            {
                this.Title = Host.ProtocolServer.DisplayName + " - " + Host.ProtocolServer.SubTitle;
                this.Icon = Host.ProtocolServer.IconImg;
                Host.SetParentWindow(this);
            }

            if (this.IsLoaded && Equals(this.Content, Host) == false)
            {
                this.Content = Host;
            }
        }

        public void SetProtocolHost(HostBase? host)
        {
            Host = null;
            Host = host;
        }

        public string LastTabToken { get; set; } = "";
    }
}
