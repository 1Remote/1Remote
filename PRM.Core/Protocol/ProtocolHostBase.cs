using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolHostBase : UserControl
    {
        public readonly ProtocolServerBase ProtocolServer;
        public readonly bool CanFullScreen;

        protected ProtocolHostBase(ProtocolServerBase protocolServer, bool canFullScreen = false) 
        {
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;
        }

        public abstract void Conn();
        public abstract void DisConn();
        public abstract void GoFullScreen();

    }
}
