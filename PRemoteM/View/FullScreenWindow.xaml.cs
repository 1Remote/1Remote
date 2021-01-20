using PRM.Core.Protocol;
using System.Diagnostics;
using System.Windows;
using PRM.Model;

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
            };
            Closed += (sender, args) =>
            {
                if (ProtocolHostBase != null)
                {
                    RemoteWindowPool.Instance.DelProtocolHostInSyncContext(ProtocolHostBase.ConnectionId);
                }
            };
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
