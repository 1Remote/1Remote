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
                    SetWindowTitle();
                }
            }
        }

        public FullScreenWindowView()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                SetWindowTitle();
            };
            Closed += (sender, args) =>
            {
                if (Host != null)
                {
                    IoC.Get<SessionControlService>().DelProtocolHost(Host.ConnectionId);
                }
            };
        }

        private void SetWindowTitle()
        {
            if (Host != null && this.IsLoaded)
            {
                this.Title = Host.ProtocolServer.DisplayName + " - " + Host.ProtocolServer.SubTitle;
                this.Icon = Host.ProtocolServer.IconImg;
                Host.ParentWindow = this;
                if (Equals(this.Content, Host) == false)
                {
                    this.Content = Host;
                }
                Host.GoFullScreen();
            }
        }

        public void SetProtocolHost(HostBase content)
        {
            Debug.Assert(content != null);
            this.Content = null;
            Host = content;
        }

        public string LastTabToken { get; set; } = "";
    }
}
