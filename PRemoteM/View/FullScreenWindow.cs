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
    public class FullScreenWindow:  Window
    {
        public FullScreenWindow(ProtocolHostBase content)
        {
            Loaded += (sender, args) =>
            {
                this.Background = Brushes.Black;
                this.Content = content;
            };
            content.Parent = this;
        }
    }
}
