using PRM.Core.Model;
using PRM.Core.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PRM.Model;

namespace PRM.View
{
    /// <summary>
    /// FullScreenWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FullScreenWindow : Window
    {
        public ProtocolHostBase ProtocolHostBase { get; private set; }= null;
        public FullScreenWindow(ProtocolHostBase content)
        {
            Debug.Assert(content != null);
            InitializeComponent();
            SetProtocolHost(content);
            Loaded += (sender, args) =>
            {
                this.Content = ProtocolHostBase;
            };
            Closed += (sender, args) =>
            {
                if (ProtocolHostBase != null)
                {
                    WindowPool.DelProtocolHost(ProtocolHostBase.ProtocolServer.ConnectionId);
                }
            };
        }

        public void SetProtocolHost(ProtocolHostBase content)
        {
            this.Content = null;
            ProtocolHostBase = content;
            this.Title = ProtocolHostBase.ProtocolServer.DispName + " - " + ProtocolHostBase.ProtocolServer.SubTitle;
            this.Icon = ProtocolHostBase.ProtocolServer.IconImg;
            ProtocolHostBase.ParentWindow = this;
            if (IsLoaded)
                this.Content = content;
        }
    }
}
