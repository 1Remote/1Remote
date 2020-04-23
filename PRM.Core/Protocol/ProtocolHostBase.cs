using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolHostBase : UserControl
    {
        public readonly ProtocolServerBase ProtocolServer;
        public readonly bool CanFullScreen;
        private Window _parent = null;
        public Window Parent
        {
            get => _parent;
            set
            {
                _parent = value;
            }
        }


        protected ProtocolHostBase(ProtocolServerBase protocolServer, bool canFullScreen = false)
        {
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;
        }

        public abstract void Conn();
        public abstract void DisConn();
        public abstract void GoFullScreen();
        public abstract bool IsConnected();

        public Action OnFullScreen2Window = null;
        public Action OnWindow2FullScreen = null;
    }
}
