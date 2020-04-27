using PRM.Core.Model;
using PRM.Core.Protocol;
using System;
using System.Collections.Generic;
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

namespace PRM.View
{
    /// <summary>
    /// FullScreenWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FullScreenWindow : Window
    {

        public ProtocolHostBase ProtocolHostBase;
        public FullScreenWindow(ProtocolHostBase content)
        {
            this.Title = content.ProtocolServer.DispName + " - " + content.ProtocolServer.SubTitle;
            this.Icon = content.ProtocolServer.IconImg;
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
