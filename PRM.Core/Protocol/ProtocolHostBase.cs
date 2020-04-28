using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolHostBase : UserControl
    {
        public uint Id => ProtocolServer.Id;
        public readonly ProtocolServerBase ProtocolServer;
        public bool CanFullScreen { get; protected set; }
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
            Debug.Assert(protocolServer != null);
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;
        }

        public abstract void Conn();
        public abstract void DisConn();
        public abstract void GoFullScreen();
        public abstract bool IsConnected();
        public abstract bool IsConnecting();

        //public Action OnDisconnected = null;
        public Action<uint> OnClose = null;
        public Action<uint> OnFullScreen2Window = null;
        //public Action OnWindow2FullScreen = null;
    }
}
