using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using PRM.Core.Model;
using PRM.Core.Protocol;

namespace PRM.View
{
    public class FullScreenWindow : Window
    {
        public ProtocolHostBase ProtocolHostBase;
        public FullScreenWindow(ProtocolHostBase content)
        {
            this.WindowStyle = WindowStyle.None;
            this.Background = Brushes.Black;
            ProtocolHostBase = content;
            ProtocolHostBase.Parent = this;
            ProtocolHostBase.OnFullScreen2Window += OnFullScreen2Window;
            Loaded += (sender, args) =>
            {
                this.Background = Brushes.Black;
                this.Content = ProtocolHostBase;
            };
            Closed += (sender, args) =>
            {
                if (ProtocolHostBase != null)
                {
                    Global.GetInstance().DelFullScreenWindow(ProtocolHostBase.ProtocolServer.Id);
                }
            };
        }
        public void OnFullScreen2Window()
        {
            Global.GetInstance().MoveProtocolToTab(ProtocolHostBase.ProtocolServer.Id);
        }
    }
}
