using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PRM.Core.Protocol;

namespace PRM.View
{
    public class FullScreenWindow : Window
    {
        public readonly ProtocolHostBase ProtocolHostBase;
        public FullScreenWindow(ProtocolHostBase content)
        {
            ProtocolHostBase = content;
            ProtocolHostBase.Parent = this;
            ProtocolHostBase.OnFullScreen2Window += OnFullScreen2Window;
            Loaded += (sender, args) =>
            {
                this.Background = Brushes.Black;
                this.Content = ProtocolHostBase;
            };
        }

        private void OnFullScreen2Window()
        {
            ProtocolHostBase.OnFullScreen2Window -= OnFullScreen2Window;
            // TODO MOVE To TAB
            this.Close();
        }
    }
}
