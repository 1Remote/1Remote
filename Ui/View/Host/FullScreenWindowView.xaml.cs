using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using _1RM.Model;
using _1RM.Service;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;

namespace _1RM.View.Host
{
    public partial class FullScreenWindowView : WindowBase
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
                    IoC.Get<SessionControlService>().CloseProtocolHostAsync(Host.ConnectionId);
                }
            };
        }

        private void SetContent()
        {
            // !set content first
            if (this.IsLoaded && Equals(this.Content, Host) == false)
            {
                this.Content = Host;
            }
            // !then set host
            if (this.IsLoaded && Host != null)
            {
                this.Title = Host.ProtocolServer.DisplayName + " - " + Host.ProtocolServer.SubTitle;
                this.Icon = Host.ProtocolServer.IconImg;
                Host.SetParentWindow(this);
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
